using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Fps.Core;
using Fps.Protocol;

namespace Fps.Server
{
    // グレネード (Unity の NetTeamDeathmatch.Explosion・NetGrenade・ServerGrenadeSupply)。数と間隔は 1 人ごとの ServerGrenadeSupply で持ち、
    // 出現のたびにルールの数に戻す。投げる位置と初速はクライアントの値を使わず、サーバーの知る目の位置と送られた向きから決める。
    // 爆発のダメージは参加中で生きている全員に、体のカプセルの最も近い点までの距離で決める。投げた本人も巻き込む
    internal sealed partial class ServerCombat
    {
        /// <summary>グレネードのキルフィードと見た目を選ぶ Id (Unity の GrenadeThrower.WeaponId と同じ)。</summary>
        public const string GrenadeWeaponId = "grenade";

        public const string GrenadeLogTag = "[Grenade]";

        /// <summary>要求の届く時刻は揺れるので、持ち主の時計どおりに送られた次の要求が間隔より少し早く届いても断らない。</summary>
        public const float GrenadeThrowJitterSeconds = 0.15f;

        /// <summary>目からグレネードを出す位置までの距離 (Unity の GrenadeThrower.ReleaseDistance)。</summary>
        public const float ReleaseDistance = 0.7f;

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

        /// <summary>
        /// point から、bottom (足元) と top (頭のてっぺん) を両端に持つ半径 radius の縦のカプセルの表面までの距離。中にあれば 0。
        /// カプセルが半径の 2 倍より低ければ、両端の中点を中心にした球として測る。
        /// </summary>
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
            var spec = _data.Grenade;
            _log.WriteLine(_grenadesEnabled
                ? "[Match] grenades=On count=" + Text(rules.Actions.GrenadeCount) + " fuse=" + Text(spec.FuseSeconds) + "s radius="
                    + Text(spec.DamageRadius) + " maxDamage=" + Text(spec.MaxDamage)
                : "[Match] grenades=" + rules.Actions.Grenade);
        }

        private void RefillGrenades(Fighter fighter)
        {
            fighter.Grenades.Refill(_data.Rules.Actions);
        }

        private void ThrowGrenade(Fighter thrower, Vector3 direction)
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

            var rejection = _grenadesEnabled ? thrower.Grenades.Check(_now) : GrenadeThrowRejection.Disabled;
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
            var spec = _data.Grenade;
            var position = ReleasePoint(body.EyePosition, direction);
            var velocity = (direction * spec.ThrowSpeed) + new Vector3(0f, spec.ThrowLiftSpeed, 0f);
            thrower.Grenades.Take(_now);
            _grenades.Add(new LiveGrenade(_nextGrenadeId++, playerId, new ServerGrenadeFlight(_context.World, position, velocity), spec.FuseSeconds));

            // 撃ったときと同じく、投げた時点で出現直後の無敵を解く
            _rules.NotifyFired(playerId);
            _log.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "{0} net thrown player={1} remaining={2} at=({3:0.0}, {4:0.0}, {5:0.0}) velocity=({6:0.0}, {7:0.0}, {8:0.0})",
                GrenadeLogTag,
                playerId,
                thrower.Grenades.Remaining,
                position.X,
                position.Y,
                position.Z,
                velocity.X,
                velocity.Y,
                velocity.Z));
        }

        // 目から ReleaseDistance 先。間に壁があれば球が埋まらない壁の手前
        private Vector3 ReleasePoint(Vector3 eye, Vector3 direction)
        {
            var distance = ReleaseDistance;
            if (_context.World.Raycast(
                    eye, direction, ReleaseDistance + ServerGrenadeFlight.BodyRadius, ServerHitboxes.RaycastLayers, out var hit))
            {
                distance = Math.Min(distance, Math.Max(0f, hit.Distance - ServerGrenadeFlight.BodyRadius));
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
                Explode(grenade.Flight.Position, grenade.ThrowerId);
            }
        }

        private void ClearGrenades()
        {
            _grenades.Clear();
        }

        /// <summary>
        /// throwerId が投げたグレネードの center での爆発。参加中で生きている全員に範囲ダメージ、ルールで壊せるなら建物に被害を与え、ダメージを与えた相手ごとに本人たちへ
        /// ヒット・被弾を、全員に爆発を送る。投げた人が抜けていれば、攻撃者のいないダメージ (倒れたら自滅) として与える。
        /// 爆発はサーバーの時刻で起きるので巻き戻さない。
        /// </summary>
        internal void Explode(Vector3 center, int throwerId)
        {
            var thrower = _match.TryGetPlayer(throwerId, out _) ? throwerId : (int?)null;
            var spec = _data.Grenade;
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
                var outcome = ExplodeOn(thrower, victimId, center, spec.Explosion);
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

            var structures = _structures.ApplyAround(center, spec.StructureDamageAtCenter, spec.Explosion.Radius, _changedStructures, _log);
            SendStructureChanges();

            _explosion.Center = center;
            _explosion.HasThrower = thrower.HasValue;
            _explosion.ThrowerId = thrower ?? 0;
            _explosion.WeaponId = GrenadeWeaponId;
            SendToAll(_explosion);
            _log.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "{0} net exploded thrower={1} at=({2:0.0}, {3:0.0}, {4:0.0}) damaged={5} killed={6} structures={7}",
                GrenadeLogTag,
                throwerId,
                center.X,
                center.Y,
                center.Z,
                damaged,
                killed,
                structures));
        }

        private DamageOutcome ExplodeOn(int? thrower, int victimId, Vector3 center, ExplosionDamage explosion)
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

            // 人とグレネードは .fpsmap に無いので、爆風を遮るのは世界の物だけ
            var obstructed = IsLineBlocked(center, bodyCenter) && IsLineBlocked(center, head);
            var damage = explosion.At(distance, obstructed);
            if (damage <= 0)
            {
                if (obstructed)
                {
                    _log.WriteLine(GrenadeLogTag + " net blocked victim=" + Text(victimId));
                }

                return DamageOutcome.Ignored;
            }

            // 投げた人が抜けていれば、ApplyDamage は本人のダメージ (倒れたら自滅) として数えるので、キルフィードの撃った人も本人にそろえる
            var attackerId = thrower ?? victimId;
            var outcome = _rules.ApplyDamage(thrower, victimId, damage, new KillShot(attackerId, victimId, GrenadeWeaponId, HitZone.Body));
            if (outcome == DamageOutcome.Ignored)
            {
                return outcome;
            }

            var hurt = thrower.HasValue && IsReflected(thrower.Value, victimId) ? thrower.Value : victimId;
            if (_fighters.TryGetValue(hurt, out var hurtFighter) && _bodies.TryGet(hurt, out var hurtBody))
            {
                hurtFighter.LastHitDirection = hurtBody.Position - center;
            }

            SendExplosionHit(thrower, victimId, outcome, damage, center);
            _log.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "{0} net hit attacker={1} victim={2} dmg={3} distance={4:0.0} outcome={5}",
                GrenadeLogTag,
                attackerId,
                victimId,
                damage,
                distance,
                outcome));
            return outcome;
        }

        /// <summary>
        /// 爆発 1 人分の結果を本人たちに知らせる。自分の爆発・投げた人が抜けた爆発なら体力の減った本人に被弾 (爆発の中心から) だけを、
        /// 跳ね返した味方撃ちなら投げた本人に被弾 (位置なし) だけを、それ以外は投げた人にヒット、受けた人に被弾 (爆発の中心から) を送る。
        /// </summary>
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

        private bool IsLineBlocked(Vector3 from, Vector3 to)
        {
            var offset = to - from;
            var length = offset.Length();
            return length >= 1e-4f && _context.World.Raycast(from, offset / length, length, ServerHitboxes.RaycastLayers, out _);
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
            public LiveGrenade(int id, int throwerId, ServerGrenadeFlight flight, float fuseSeconds)
            {
                Id = id;
                ThrowerId = throwerId;
                Flight = flight;
                Remaining = fuseSeconds;
            }

            public int Id { get; }

            public int ThrowerId { get; }

            public ServerGrenadeFlight Flight { get; }

            public float Remaining { get; set; }
        }
    }
}
