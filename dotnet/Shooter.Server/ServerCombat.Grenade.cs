using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // グレネード (Unity の NetTeamDeathmatch.Explosion・NetGrenade・ServerGrenadeSupply)。種類ごとの数と間隔は 1 人ごとの ServerGrenadeSupply で持ち、
    // 出現のたびにルールの数に戻す。信管が切れたら、投げた種類の ImpactSpec の爆発・範囲効果・目くらましを起こす。投げる位置と初速はクライアントの値を使わず、サーバーの知る目の位置と送られた向きから決める。
    // 爆発のダメージは参加中で生きている全員に、体のカプセルの最も近い点までの距離で決める。投げた本人も巻き込む
    internal sealed partial class ServerCombat
    {
        // Unity の GrenadeThrower.WeaponId と同じ Id にする。
        public const string GrenadeWeaponId = "grenade";

        public const string GrenadeLogTag = "[Grenade]";

        // 要求の届く時刻は揺れるので、持ち主の時計どおりに送られた次の要求が間隔より少し早く届いても断らない。
        public const float GrenadeThrowJitterSeconds = 0.15f;

        // 頭の高さへの線を、カプセルの頂からどれだけ下へ引くか (m)。身をかがめて頭だけ出した相手にも当てる
        private const float _explosionTopProbeInset = 0.1f;

        // これより短い向きは投げる向きとして扱わない
        private const float _minThrowDirectionSqrMagnitude = 1e-8f;

        private readonly List<LiveGrenade> _grenades = new List<LiveGrenade>();
        private readonly List<int> _explosionTargets = new List<int>();
        private readonly ExplosionMessage _explosion = new ExplosionMessage();
        private bool _grenadesEnabled;
        private int _nextGrenadeId;

        internal int LiveGrenadeCount => _grenades.Count;

        internal static float DistanceToCapsule(Vector3 point, Vector3 bottom, Vector3 top, float radius)
        {
            var axis = top - bottom;
            var length = axis.Length();
            Vector3 a;
            Vector3 b;
            if (length <= radius * 2f)
            {
                a = b = Vector3.Lerp(bottom, top, 0.5f);
            }
            else
            {
                var unit = axis / length;
                a = bottom + (unit * radius);
                b = top - (unit * radius);
            }

            var segment = b - a;
            var segmentSqr = segment.LengthSquared();
            var t = segmentSqr > 0f ? Math.Clamp(Vector3.Dot(point - a, segment) / segmentSqr, 0f, 1f) : 0f;
            return Math.Max(0f, Vector3.Distance(point, a + (segment * t)) - radius);
        }

        private void PrepareGrenades(GameRules rules)
        {
            _grenadesEnabled = rules.Actions.Grenade == Grenade.On;
            if (!_grenadesEnabled)
            {
                _log.WriteLine("[Match] grenades=" + rules.Actions.Grenade);
                return;
            }

            foreach (var spec in _data.Grenades.Carried)
            {
                _log.WriteLine("[Match] grenades=On type=" + spec.LogName + " count=" + Text(rules.Actions.GrenadeCount) + " fuse="
                    + Text(spec.FuseSeconds) + "s radius=" + Text(spec.DamageRadius) + " maxDamage=" + Text(spec.MaxDamage));
            }
        }

        private void RefillGrenades(Fighter fighter)
        {
            fighter.Grenades.Refill(_data.Rules.Actions);
        }

        private void ThrowGrenade(Fighter thrower, Vector3 direction, string typeId)
        {
            // ダウン中・倒れている人の要求は、射撃と同じく理由を出さずに捨てる
            var playerId = thrower.PlayerId;
            if (!_rules.CanFire(playerId) || !_bodies.TryGet(playerId, out var body))
            {
                return;
            }

            if (!float.IsFinite(direction.X) || !float.IsFinite(direction.Y) || !float.IsFinite(direction.Z)
                || direction.LengthSquared() < _minThrowDirectionSqrMagnitude)
            {
                WarnGrenadeRejected(playerId, GrenadeThrowRejection.InvalidDirection);
                return;
            }

            var rejection = _grenadesEnabled ? thrower.Grenades.Check(_now, typeId, out _) : GrenadeThrowRejection.Disabled;
            if (rejection != GrenadeThrowRejection.None)
            {
                WarnGrenadeRejected(playerId, rejection);
                return;
            }

            if (_grenades.Count >= SnapshotMessage.MaxGrenades)
            {
                WarnGrenadeRejected(playerId, GrenadeThrowRejection.TooMany);
                return;
            }

            direction = Vector3.Normalize(direction);
            thrower.Grenades.Take(_now, typeId, out var spec);
            var position = ReleasePoint(body.EyePosition, direction, spec);
            var velocity = (direction * spec.ThrowSpeed) + new Vector3(0f, spec.ThrowLiftSpeed, 0f);
            var flight = new ServerGrenadeFlight(_context.World, position, velocity, spec);
            flight.CollideWithCharacters(_bodies, playerId, IsHittable);
            _grenades.Add(new LiveGrenade(_nextGrenadeId++, playerId, spec, flight));

            // 撃ったときと同じく、投げた時点で出現直後の無敵を解く
            _rules.NotifyFired(playerId);
            _log.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "{0} net thrown player={1} remaining={2} at=({3:0.0}, {4:0.0}, {5:0.0}) velocity=({6:0.0}, {7:0.0}, {8:0.0}) type={9}",
                GrenadeLogTag,
                playerId,
                thrower.Grenades.RemainingOf(spec.Id),
                position.X,
                position.Y,
                position.Z,
                velocity.X,
                velocity.Y,
                velocity.Z,
                spec.LogName));
        }

        // 目から spec の ReleaseDistance 先 (オフラインの GrenadeThrower と同じ)。間に壁があれば球が埋まらない壁の手前
        private Vector3 ReleasePoint(Vector3 eye, Vector3 direction, GrenadeSpec spec)
        {
            var distance = spec.ReleaseDistance;
            if (_context.World.Raycast(
                    eye, direction, spec.ReleaseDistance + spec.BodyRadius, ServerHitboxes.RaycastLayers, out var hit))
            {
                distance = Math.Min(distance, Math.Max(0f, hit.Distance - spec.BodyRadius));
            }

            return eye + (direction * distance);
        }

        private void TickGrenades(float deltaSeconds)
        {
            for (var i = 0; i < _grenades.Count; i++)
            {
                var grenade = _grenades[i];
                grenade.Flight.Step(deltaSeconds);
                grenade.Remaining -= deltaSeconds;
                if (grenade.Remaining > 0f)
                {
                    continue;
                }

                _grenades.RemoveAt(i);
                i--;
                GoOff(grenade);
            }
        }

        // 投げ物は当たって効くのではないので、着弾の指示 (ImpactResolver) を通さず ImpactSpec の効果をそのまま起こす
        private void GoOff(LiveGrenade grenade)
        {
            var impact = grenade.Spec.Impact;
            var center = grenade.Flight.Position;
            if (impact.Explosion != null)
            {
                Explode(center, impact.Explosion, grenade.ThrowerId, GrenadeWeaponId);
            }

            var owner = _match.TryGetPlayer(grenade.ThrowerId, out _) ? grenade.ThrowerId : (int?)null;
            if (impact.AreaEffect != null)
            {
                _areaEffects.Spread(impact.AreaEffect, center, owner, GrenadeWeaponId);
            }

            if (impact.Blind != null)
            {
                _flash.Detonate(center, impact.Blind, owner, GrenadeWeaponId);
            }
        }

        private void ClearGrenades()
        {
            _grenades.Clear();
        }

        // 爆発はサーバーの時刻で起きるので巻き戻さない。
        internal (int Damaged, int Killed) Explode(Vector3 center, ExplosionSpec explosion, int throwerId, string weaponId)
        {
            ArgumentNullException.ThrowIfNull(explosion);
            ArgumentNullException.ThrowIfNull(weaponId);
            var thrower = _match.TryGetPlayer(throwerId, out _) ? throwerId : (int?)null;
            var tag = ExplosionLogTag(weaponId);
            var damaged = 0;
            var killed = 0;

            // ダメージで倒れた人の処理が一覧に触れても回し続けられるよう、写してから回す
            _explosionTargets.Clear();
            foreach (var body in _bodies.All)
            {
                _explosionTargets.Add(body.PlayerId);
            }

            foreach (var victimId in _explosionTargets)
            {
                var outcome = ExplodeOn(thrower, victimId, center, explosion.Damage, weaponId, tag);
                if (outcome == DamageOutcome.Ignored)
                {
                    continue;
                }

                damaged++;
                if (outcome == DamageOutcome.Killed)
                {
                    killed++;
                }
            }

            var structures = _structures.ApplyAround(center, explosion.StructureDamageAtCenter, explosion.Damage.Radius, _changedStructures, _log);
            SendStructureChanges();
            var vehicles = _vehicles?.ApplyExplosion(center, explosion.Damage, thrower) ?? 0;
            var deployables = _deployables?.ApplyExplosion(center, explosion.Damage, thrower) ?? 0;

            _explosion.Center = center;
            _explosion.HasThrower = thrower.HasValue;
            _explosion.ThrowerId = thrower ?? 0;
            _explosion.WeaponId = weaponId;
            SendToAll(_explosion);
            _log.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "{0} net exploded thrower={1} at=({2:0.0}, {3:0.0}, {4:0.0}) damaged={5} killed={6} structures={7} vehicles={8} deployables={9}",
                tag,
                throwerId,
                center.X,
                center.Y,
                center.Z,
                damaged,
                killed,
                structures,
                vehicles,
                deployables));
            return (damaged, killed);
        }

        private DamageOutcome ExplodeOn(int? thrower, int victimId, Vector3 center, ExplosionDamage explosion, string weaponId, string tag)
        {
            if (!IsHittable(victimId) || !_bodies.TryGet(victimId, out var body))
            {
                return DamageOutcome.Ignored;
            }

            var shape = body.Shape;
            var bottom = body.Position + new Vector3(0f, shape.BottomY, 0f);
            var top = body.Position + new Vector3(0f, shape.TopY, 0f);
            var distance = DistanceToCapsule(center, bottom, top, body.Radius);
            if (!explosion.Reaches(distance))
            {
                return DamageOutcome.Ignored;
            }

            var bodyCenter = Vector3.Lerp(bottom, top, 0.5f);
            var head = top - new Vector3(0f, _explosionTopProbeInset, 0f);
            if (head.Y < bodyCenter.Y)
            {
                head = bodyCenter;
            }

            // 人とグレネードは爆風を遮らない。遮るのは世界の物と、壁として置かれる車体・置いた物 (オフラインの Explosion.Blocks と同じ)
            var obstructed = IsLineBlocked(center, bodyCenter) && IsLineBlocked(center, head);
            var damage = explosion.At(distance, obstructed);
            if (damage <= 0)
            {
                if (obstructed)
                {
                    _log.WriteLine(tag + " net blocked victim=" + Text(victimId));
                }

                return DamageOutcome.Ignored;
            }

            // 投げた人が抜けていれば、ApplyDamage は本人のダメージ (倒れたら自滅) として数えるので、キルフィードの撃った人も本人にそろえる
            var attackerId = thrower ?? victimId;
            var outcome = _rules.ApplyDamage(thrower, victimId, damage, new KillShot(attackerId, victimId, weaponId, HitZone.Body));
            if (outcome == DamageOutcome.Ignored)
            {
                return outcome;
            }

            var hurt = thrower.HasValue && IsReflected(thrower.Value, victimId) ? thrower.Value : victimId;
            if (_fighters.TryGetValue(hurt, out var hurtFighter) && _bodies.TryGet(hurt, out var hurtBody))
            {
                hurtFighter.LastHitDirection = hurtBody.Position - center;
            }

            if (hurt == victimId && outcome != DamageOutcome.Killed)
            {
                PushAway(victimId, center, bodyCenter, explosion.StrengthAt(distance));
            }

            SendExplosionHit(thrower, victimId, outcome, damage, center);
            _log.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "{0} net hit attacker={1} victim={2} dmg={3} distance={4:0.0} outcome={5}",
                tag,
                attackerId,
                victimId,
                damage,
                distance,
                outcome));
            return outcome;
        }

        // オフラインの Explosion と同じく体の中ほどへ向けた向きと中心からの強さで、Core の KnockbackSpec に速さを決めさせる。
        // 倒した相手はサーバーでは動かさない (抜け殻を押すのはクライアントの見た目のため)。
        // Bot はオフラインの敵の代わりなので、重さは敵の重さにする。
        private void PushAway(int victimId, Vector3 center, Vector3 bodyCenter, float strength)
        {
            var spec = _movement.Knockback;
            var weight = BotRoster.IsBotId(victimId) ? spec.EnemyWeight : spec.PlayerWeight;
            if (spec.TryGetVelocity(DamageKind.Explosion, bodyCenter - center, strength, weight, out var velocity))
            {
                _movement.Launch(victimId, velocity);
            }
        }

        private void SendExplosionHit(int? thrower, int victimId, DamageOutcome outcome, int damage, Vector3 center)
        {
            if (!thrower.HasValue || thrower.Value == victimId)
            {
                SendDamageTaken(victimId, center, damage);
                return;
            }

            if (IsReflected(thrower.Value, victimId))
            {
                SendDamageTaken(thrower.Value, null, damage);
                return;
            }

            SendHitConfirm(thrower.Value, damage, HitZone.Body, outcome);
            SendDamageTaken(victimId, center, damage);
        }

        // グレネードのログは今までの [Grenade] のまま残し、武器の爆発は戦闘のログに出す
        private static string ExplosionLogTag(string weaponId)
        {
            return string.Equals(weaponId, GrenadeWeaponId, StringComparison.Ordinal) ? GrenadeLogTag : LogTag;
        }

        private bool IsLineBlocked(Vector3 from, Vector3 to)
        {
            var offset = to - from;
            var length = offset.Length();
            return length >= 1e-4f
                && (_context.World.Raycast(from, offset / length, length, ServerHitboxes.RaycastLayers, out _)
                    || (_vehicles != null && _vehicles.BlocksLine(from, to, float.MaxValue, PlayerIds.None))
                    || (_deployables != null && _deployables.BlocksLine(from, to, null)));
        }

        private void ContributeGrenades(SnapshotList<GrenadeSnapshot> list)
        {
            list.Clear();
            foreach (var grenade in _grenades)
            {
                if (list.Count == list.MaxCount)
                {
                    break;
                }

                ref var row = ref list.Add();
                row.GrenadeId = grenade.Id;
                row.Present = GrenadeFields.All;
                row.HasThrower = _match.TryGetPlayer(grenade.ThrowerId, out _);
                row.ThrowerId = row.HasThrower ? grenade.ThrowerId : 0;
                row.WeaponId = GrenadeWeaponId;
                row.TypeId = grenade.Spec.Id;
                row.IsStuck = grenade.Flight.IsStuck;
                var stuckTo = grenade.Flight.StuckToPlayerId;
                row.HasStuckTo = stuckTo.HasValue && _match.TryGetPlayer(stuckTo.Value, out _);
                row.StuckToId = row.HasStuckTo ? stuckTo.Value : 0;
                row.Position = grenade.Flight.Position;
                row.Rotation = grenade.Flight.Rotation;
            }
        }

        private void WarnGrenadeRejected(int playerId, GrenadeThrowRejection reason)
        {
            if (CanWarn())
            {
                _log.WriteLine(GrenadeLogTag + " net rejected player=" + Text(playerId) + " reason=" + reason);
            }
        }

        private sealed class LiveGrenade
        {
            public LiveGrenade(int id, int throwerId, GrenadeSpec spec, ServerGrenadeFlight flight)
            {
                Id = id;
                ThrowerId = throwerId;
                Spec = spec;
                Flight = flight;
                Remaining = spec.FuseSeconds;
            }

            public GrenadeSpec Spec { get; }

            public int Id { get; }

            public int ThrowerId { get; }

            public ServerGrenadeFlight Flight { get; }

            public float Remaining { get; set; }
        }
    }
}
