using System;
using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    // ネット対戦の相手は、最新のスナップショットで生きている自分以外のプレイヤーと、壊れていない自分の乗っていない乗り物。
    // 狙う点は画面に出している (補間した) 姿の体・車体の中ほどにし、サーバーも撃った人が見ていた tick の同じ点で確かめる (ServerLockOn)。
    // 乗り物の Id は ClientLockTargetIds で人と重ならない数にする。
    public sealed class ClientLockOnTargets : ILockOnTargets
    {
        // サーバーの ServerHitboxes.RaycastLayers と同じく Ignore Raycast (2) だけを外す。
        public const int SightLayers = ~(1 << 2);

        private const float _minLength = 1e-6f;

        private readonly int _localPlayerId;
        private readonly Func<SnapshotMessage> _latest;
        private readonly TrySampleRemote _sample;
        private readonly IWorldCollision _world;
        private readonly StanceGeometry _geometry;
        private readonly IClientLockOnVehicles _vehicles;
        private readonly Dictionary<int, FlareDecoy> _decoys = new Dictionary<int, FlareDecoy>();

        // vehicles が null なら乗り物を相手にせず、フレアの囮も出さない。
        public ClientLockOnTargets(
            int localPlayerId,
            Func<SnapshotMessage> latest,
            TrySampleRemote sample,
            IWorldCollision world,
            StanceGeometry geometry,
            IClientLockOnVehicles vehicles = null)
        {
            _localPlayerId = localPlayerId;
            _latest = latest ?? throw new ArgumentNullException(nameof(latest));
            _sample = sample ?? throw new ArgumentNullException(nameof(sample));
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
            _vehicles = vehicles;
        }

        public delegate bool TrySampleRemote(int playerId, out RemotePose pose);

        public void Collect(Vector3 eye, Vector3 forward, float range, List<LockOnCandidate> into)
        {
            if (into == null)
            {
                throw new ArgumentNullException(nameof(into));
            }

            var latest = _latest();
            if (latest == null)
            {
                return;
            }

            for (var i = 0; i < latest.Players.Count; i++)
            {
                var row = latest.Players[i];
                if (row.PlayerId == _localPlayerId || row.Concealed || row.Health <= 0 || !TryLocate(row.PlayerId, out var point))
                {
                    continue;
                }

                if (InSight(eye, point, range))
                {
                    into.Add(new LockOnCandidate(row.PlayerId, point, Decoy(row.PlayerId)));
                }
            }

            if (_vehicles == null)
            {
                return;
            }

            var own = latest.Players.IndexOf(_localPlayerId);
            var aboard = own >= 0 && latest.Players[own].Vehicle.IsRiding ? latest.Players[own].Vehicle.Vehicle : VehicleSeatSync.NoVehicle;
            for (var i = 0; i < latest.Vehicles.Count; i++)
            {
                var row = latest.Vehicles[i];

                // 自分の乗り物と壊れた乗り物はサーバーも捉えさせない (ServerLockOn の self・not-hittable)
                if (row.Index == aboard || row.Stage == VehicleDamageStage.Destroyed || !_vehicles.TryGetShownCenter(row.Index, out var point))
                {
                    continue;
                }

                if (InSight(eye, point, range))
                {
                    var id = ClientLockTargetIds.OfVehicle(row.Index);
                    into.Add(new LockOnCandidate(id, point, Decoy(id)));
                }
            }
        }

        public Func<Vector3?> Follow(int id)
        {
            if (ClientLockTargetIds.IsVehicle(id))
            {
                var vehicle = ClientLockTargetIds.WireIdOf(id);
                return () => TryLocateVehicle(vehicle, out var center) ? center : (Vector3?)null;
            }

            return () => TryLocate(id, out var point) ? point : (Vector3?)null;
        }

        // 人を狙っても、その人が乗っている乗り物のフレアで外れる (サーバーの ServerVehicles.DecoyOf と同じ)。
        // 乗り降りで囮の持ち主が変わるので、乗り物を決めずに問い合わせのたびに引き直す。
        public ILockOnDecoy Decoy(int id)
        {
            if (_vehicles == null)
            {
                return null;
            }

            if (!_decoys.TryGetValue(id, out var decoy))
            {
                decoy = new FlareDecoy(this, id);
                _decoys.Add(id, decoy);
            }

            return decoy;
        }

        // CountermeasurePod.TryLure と同じく、狙う側から見て相手との角度が cone 以内でいちばん小さいフレアを選び、同じ角度なら後に撒いた方を選ぶ。
        public static bool TryLure(
            VehicleFlareSync flares, float coneDegrees, Vector3 seeker, Vector3 target, out int decoyId, out Vector3 decoyPoint)
        {
            decoyId = -1;
            decoyPoint = default;
            var toTarget = target - seeker;
            if (flares == null || toTarget.LengthSquared() < _minLength * _minLength)
            {
                return false;
            }

            var unit = Vector3.Normalize(toTarget);
            var best = float.MaxValue;
            for (var i = 0; i < flares.Count; i++)
            {
                var position = flares.PositionAt(i);
                var angle = LockOnTracker.AngleTo(seeker, unit, position);
                if (angle > coneDegrees || angle > best)
                {
                    continue;
                }

                best = angle;
                decoyId = flares.IdAt(i);
                decoyPoint = position;
            }

            return decoyId >= 0;
        }

        private bool InSight(Vector3 eye, Vector3 point, float range)
        {
            var offset = point - eye;
            var distance = offset.Length();
            return distance <= range && !(distance > 1e-4f && _world.Raycast(eye, offset / distance, distance, SightLayers, out _));
        }

        private bool TryLocate(int playerId, out Vector3 point)
        {
            point = default;
            if (!_sample(playerId, out var pose))
            {
                return false;
            }

            point = pose.Position + new Vector3(0f, _geometry.Shape(pose.Stance).CenterY, 0f);
            return true;
        }

        // 壊れた乗り物はサーバーの弾ももう追わない (ServerVehicles.TryGetCenter) ので、手元でも見失ったことにする。
        private bool TryLocateVehicle(int vehicle, out Vector3 center)
        {
            center = default;
            var latest = _latest();
            if (_vehicles == null || latest == null)
            {
                return false;
            }

            var at = latest.Vehicles.IndexOf(vehicle);
            return at >= 0 && latest.Vehicles[at].Stage != VehicleDamageStage.Destroyed && _vehicles.TryGetShownCenter(vehicle, out center);
        }

        private bool TryGetFlares(int id, out VehicleFlareSync flares, out float coneDegrees)
        {
            flares = VehicleFlareSync.None;
            coneDegrees = 0f;
            var latest = _latest();
            if (latest == null)
            {
                return false;
            }

            var vehicle = VehicleSeatSync.NoVehicle;
            if (ClientLockTargetIds.IsVehicle(id))
            {
                vehicle = ClientLockTargetIds.WireIdOf(id);
            }
            else
            {
                var player = latest.Players.IndexOf(id);
                if (player >= 0 && latest.Players[player].Vehicle.IsRiding)
                {
                    vehicle = latest.Players[player].Vehicle.Vehicle;
                }
            }

            var at = vehicle >= 0 ? latest.Vehicles.IndexOf(vehicle) : -1;
            var cone = at >= 0 ? _vehicles.DecoyConeDegrees(vehicle) : null;
            if (!cone.HasValue)
            {
                return false;
            }

            flares = latest.Vehicles[at].Flares;
            coneDegrees = cone.Value;
            return true;
        }

        private sealed class FlareDecoy : ILockOnDecoy
        {
            private readonly ClientLockOnTargets _targets;
            private readonly int _id;

            public FlareDecoy(ClientLockOnTargets targets, int id)
            {
                _targets = targets;
                _id = id;
            }

            public bool TryLure(Vector3 seeker, Vector3 target, out int decoyId, out Vector3 decoyPoint)
            {
                if (!_targets.TryGetFlares(_id, out var flares, out var cone))
                {
                    decoyId = -1;
                    decoyPoint = default;
                    return false;
                }

                return ClientLockOnTargets.TryLure(flares, cone, seeker, target, out decoyId, out decoyPoint);
            }

            public bool TryLocate(int decoyId, out Vector3 decoyPoint)
            {
                if (_targets.TryGetFlares(_id, out var flares, out _))
                {
                    return flares.TryLocate(decoyId, out decoyPoint);
                }

                decoyPoint = default;
                return false;
            }
        }
    }
}
