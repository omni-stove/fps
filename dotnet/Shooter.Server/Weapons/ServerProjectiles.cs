using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    internal sealed class ServerProjectiles
    {
        private readonly List<Flying> _flying = new List<Flying>();
        private readonly ProjectileSpawnedMessage _spawned = new ProjectileSpawnedMessage();
        private readonly ProjectileEndedMessage _ended = new ProjectileEndedMessage();
        private int _nextId;

        public ServerProjectiles(ServerCombat combat)
        {
            Combat = combat ?? throw new ArgumentNullException(nameof(combat));
        }

        public ServerCombat Combat { get; }

        public int Count => _flying.Count;

        // スナップショットに載らない弾を作らないよう、載る数を超えるならいちばん古い弾を消してから作る。
        public void Launch(in ServerShot shot, PlannedRound planned)
        {
            var projectile = shot.Round.Projectile
                ?? throw new ArgumentException("the round needs RoundSpec.Projectile", nameof(shot));
            if (_flying.Count >= SnapshotMessage.MaxProjectiles)
            {
                var oldest = _flying[0];
                _flying.RemoveAt(0);
                SendEnded(oldest, ProjectileEndReason.Removed);
            }

            var collision = new ProjectileCollision(Combat, shot.ShooterId);
            var flight = new BallisticFlight(
                projectile,
                shot.Round.Homing,
                collision,
                ServerHitboxes.RaycastLayers,
                planned.Origin,
                planned.Direction,
                new Vector3(0f, ServerGrenadeFlight.Gravity, 0f),
                _ => collision.LastHit.HitTarget,
                ChargeScaling.SpeedMultiplier(shot.Weapon.Trigger.Charge, shot.ChargeFraction));
            var flying = new Flying(_nextId++, shot, collision, flight);
            _flying.Add(flying);

            _spawned.ProjectileId = flying.Id;
            _spawned.WeaponId = shot.Weapon.Id;
            _spawned.HasShooter = true;
            _spawned.ShooterId = shot.ShooterId;
            _spawned.Position = flight.Position;
            _spawned.Velocity = flight.Velocity;

            // 撃った本人はヒットスキャンの弾と同じく、返事を待たずに自分で弾を出している
            Combat.SendToAll(_spawned, shot.ShooterId);
        }

        public void Tick(float deltaSeconds)
        {
            for (var i = 0; i < _flying.Count; i++)
            {
                var flying = _flying[i];
                var lured = flying.Flight.LuredDecoyId;
                var step = flying.Flight.Step(deltaSeconds, HomingTarget(flying.Shot), Decoy(flying.Shot));
                var luring = flying.Flight.LuredDecoyId;
                if (luring.HasValue && luring != lured)
                {
                    Combat.Context.Log.WriteLine(ServerCountermeasureLog.Lured(flying.Id, flying.Shot.Weapon.Id, luring.Value));
                }

                if (step.State == BallisticState.Flying)
                {
                    continue;
                }

                // 着弾で試合が終わって次の試合が始まると Clear が並びを空にするので、外してから着弾を処理する
                _flying.RemoveAt(i);
                i--;
                Finish(flying, step.State);
            }
        }

        // 弾は巻き戻さずに今の位置で当たるので、追う先も相手の今の位置にする。倒れた・抜けた相手はもう追わない。
        private Vector3? HomingTarget(ServerShot shot)
        {
            if (shot.Round.Homing == null || !(shot.LockTarget is ServerLockTarget target))
            {
                return null;
            }

            if (target.IsVehicle)
            {
                return Combat.Vehicles != null && Combat.Vehicles.TryGetCenter(target.Id, out var center) ? center : (Vector3?)null;
            }

            return Combat.IsHittable(target.Id) && Combat.Context.Bodies.TryGet(target.Id, out var body) ? body.Center : (Vector3?)null;
        }

        // 撃った後に撒いたフレアにも逸れるよう、撃った時のフレアではなく相手の今のフレアを毎回問い合わせる (Unity の ILockOnTargets.Decoy と同じ)。
        private ILockOnDecoy Decoy(ServerShot shot)
        {
            return shot.Round.Homing != null && shot.LockTarget is ServerLockTarget target && Combat.Vehicles != null
                ? Combat.Vehicles.DecoyOf(target)
                : null;
        }

        // フレアに誘われず相手を追っている誘導弾を並べる。誰に警告を出すか (ServerLockOnWarnings) を決めるのに使う。
        public void CollectTracking(List<TrackingRound> into)
        {
            ArgumentNullException.ThrowIfNull(into);
            foreach (var flying in _flying)
            {
                if (flying.Flight.IsDone || flying.Flight.LuredDecoyId.HasValue || !(flying.Shot.LockTarget is ServerLockTarget target)
                    || !HomingTarget(flying.Shot).HasValue)
                {
                    continue;
                }

                into.Add(new TrackingRound(flying.Id, flying.Shot.ShooterId, target, flying.Flight.Position));
            }
        }

        public void Contribute(SnapshotList<ProjectileSnapshot> list)
        {
            ArgumentNullException.ThrowIfNull(list);
            list.Clear();
            var match = Combat.Context.Match;
            foreach (var flying in _flying)
            {
                ref var row = ref list.Add();
                row.ProjectileId = flying.Id;
                row.Present = ProjectileFields.All;
                row.HasShooter = match.TryGetPlayer(flying.Shot.ShooterId, out _);
                row.ShooterId = row.HasShooter ? flying.Shot.ShooterId : 0;
                row.WeaponId = flying.Shot.Weapon.Id;
                row.Position = flying.Flight.Position;
                row.Velocity = flying.Flight.Velocity;
            }
        }

        public void Clear()
        {
            _flying.Clear();
        }

        private void Finish(Flying flying, BallisticState state)
        {
            var flight = flying.Flight;
            if (state == BallisticState.Expired)
            {
                SendEnded(flying, ProjectileEndReason.Expired);
                Log(flying, null);
                Combat.Impact.Detonate(flying.Shot, DetonationTrigger.OnLifetimeEnd, flight.Position, Vector3.Zero);
                return;
            }

            var last = flying.Collision.LastHit;

            // 距離減衰は撃った所から飛んだ道のりで決める (オフラインの ProjectileBody と同じ)
            var hit = last.HitTarget
                ? ShotHit.Target(last.Point, last.Normal, flight.Distance, last.TargetId, last.TargetPosition, last.TargetCrouched, last.Zone)
                : last.HitVehicle
                    ? ShotHit.Vehicle(last.Point, last.Normal, flight.Distance, last.VehicleIndex, last.DamageMultiplier, last.VehiclePartId)
                    : last.HitDeployable
                        ? ShotHit.Deployable(last.Point, last.Normal, flight.Distance, last.DeployableId)
                        : ShotHit.World(new WorldHit(last.Point, last.Normal, flight.Distance, last.ColliderId, last.Layer));
            SendEnded(flying, hit.HitTarget ? ProjectileEndReason.HitCharacter : ProjectileEndReason.HitWorld);
            Log(flying, hit);
            var velocity = flight.Velocity;
            var direction = velocity.LengthSquared() > 1e-12f ? Vector3.Normalize(velocity) : -hit.Normal;
            Combat.Impact.Apply(flying.Shot, direction, hit);
            Combat.Impact.Detonate(flying.Shot, DetonationTrigger.OnStop, hit.Point, hit.Normal);
        }

        private void SendEnded(Flying flying, ProjectileEndReason reason)
        {
            _ended.ProjectileId = flying.Id;
            _ended.Reason = reason;
            _ended.Position = flying.Flight.Position;
            Combat.SendToAll(_ended);
        }

        private void Log(Flying flying, ShotHit? hit)
        {
            var flight = flying.Flight;
            var position = flight.Position;
            Combat.Context.Log.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "{0} net projectile id={1} shooter={2} weapon={3} end={4} pos=({5:F2},{6:F2},{7:F2}) dist={8:F1} age={9:F2} hit={10}",
                ServerCombat.LogTag,
                flying.Id,
                flying.Shot.ShooterId,
                flying.Shot.Weapon.Id,
                flight.State,
                position.X,
                position.Y,
                position.Z,
                flight.Distance,
                flight.Age,
                hit is { HitTarget: true } target ? target.TargetId.ToString(CultureInfo.InvariantCulture) : "none"));
        }

        private sealed class Flying
        {
            public Flying(int id, ServerShot shot, ProjectileCollision collision, BallisticFlight flight)
            {
                Id = id;
                Shot = shot;
                Collision = collision;
                Flight = flight;
            }

            public int Id { get; }

            public ServerShot Shot { get; }

            public ProjectileCollision Collision { get; }

            public BallisticFlight Flight { get; }
        }

        // WorldHit は誰に当たったかを持てないので、最後の当たりを LastHit に残す。
        private sealed class ProjectileCollision : IWorldCollision
        {
            private readonly ServerCombat _combat;
            private readonly int _shooterId;
            private readonly Func<int, bool> _hittable;

            public ProjectileCollision(ServerCombat combat, int shooterId)
            {
                _combat = combat;
                _shooterId = shooterId;
                _hittable = combat.IsHittable;
            }

            public ShotHit LastHit { get; private set; }

            public bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, int layerMask, out WorldHit hit)
            {
                hit = default;
                if (!(maxDistance > 0f) || float.IsInfinity(maxDistance) || direction.LengthSquared() < 1e-12f)
                {
                    return false;
                }

                var hitboxes = _combat.Hitboxes;
                if (!hitboxes.Raycast(origin, direction, maxDistance, hitboxes.LastRecordedTick, _shooterId, _hittable, out var found))
                {
                    return false;
                }

                LastHit = found;
                hit = new WorldHit(found.Point, found.Normal, found.Distance, found.ColliderId, found.Layer);
                return true;
            }

            public bool RaycastCollider(int colliderId, Vector3 origin, Vector3 direction, float maxDistance, out WorldHit hit)
            {
                return _combat.Context.World.RaycastCollider(colliderId, origin, direction, maxDistance, out hit);
            }

            public int OverlapCapsule(Vector3 point0, Vector3 point1, float radius, int layerMask, int[] results)
            {
                return _combat.Context.World.OverlapCapsule(point0, point1, radius, layerMask, results);
            }

            public bool CapsuleCast(
                Vector3 point0, Vector3 point1, float radius, Vector3 direction, float maxDistance, int layerMask, out WorldHit hit)
            {
                return _combat.Context.World.CapsuleCast(point0, point1, radius, direction, maxDistance, layerMask, out hit);
            }
        }
    }
}
