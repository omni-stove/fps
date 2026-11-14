using System.Numerics;
using Fps.Core;
using Fps.Protocol;

namespace Fps.Server
{
    // サーバー権威のヒットスキャン (Unity の NetTeamDeathmatch.Fire と NetPlayer.Fire のサーバー側)
    internal sealed partial class ServerCombat
    {
        /// <summary>着弾の音と破片の既定の材質 (Unity の SurfaceMaterial.DefaultId)。.fpsmap は材質を持たないので、どの面もこれで送る。</summary>
        public const string DefaultSurfaceMaterialId = "default";

        private readonly ShotMessage _shot = new ShotMessage();
        private readonly HitConfirmMessage _hitConfirm = new HitConfirmMessage();
        private readonly DamageTakenMessage _damageTaken = new DamageTakenMessage();
        private readonly ShotIndexCorrectedMessage _shotIndexCorrected = new ShotIndexCorrectedMessage();

        /// <summary>
        /// 射撃 1 発。撃てる状態・武器と連射の間隔・始点を確かめ、弾倉から 1 発消費してから、撃った人が見ていた時点まで他の人を巻き戻して当てる。
        /// 判定の向きはサーバーが数えた発射番号でばらつきを掛けた向き。検査で捨てた射撃では弾も連射の枠も減らさない。
        /// </summary>
        private void Fire(Fighter shooter, FireRequestMessage request)
        {
            var shooterId = shooter.PlayerId;

            // ダウン中・倒れている人の射撃は理由を出さずに捨てる
            if (!_rules.CanFire(shooterId) || !_bodies.TryGet(shooterId, out var body))
            {
                return;
            }

            // 回避・乗り越えの最中は撃たせない。連射の枠も弾も使わないよう、武器の検査より先に捨てる
            _movement.TryGetState(shooterId, out var motion);
            if (motion.IsDodging || motion.IsVaulting)
            {
                WarnRejected(shooterId, motion.IsDodging ? ShotRejection.Dodging : ShotRejection.Vaulting);
                return;
            }

            var spec = shooter.Weapons.CurrentSpec;
            var weaponCheck = _rules.Loadout.TryFire(shooterId, spec, request.WeaponId, _now);
            if (weaponCheck != ShotRejection.None)
            {
                WarnRejected(shooterId, weaponCheck);
                return;
            }

            var time = new ShotTime(request.ClientTick, request.InterpolationAlpha);
            var rejection = ShotValidator.Check(request.Origin, request.Direction, time, body.Position, _data.Settings.MaxShotOriginOffset);
            if (rejection != ShotRejection.None)
            {
                WarnRejected(shooterId, rejection);
                return;
            }

            if (IsOriginBlocked(body.EyePosition, request.Origin))
            {
                WarnRejected(shooterId, ShotRejection.OriginBlocked);
                return;
            }

            // ブラインドファイアの始点はクライアントの始点ではなく、サーバーの知るカバーの状態と目の位置から決める
            var blind = ResolveBlindFire(motion.Cover, body, out var blindOrigin);
            if (blind == BlindFireAim.Blocked)
            {
                WarnRejected(shooterId, ShotRejection.BlindFireBlocked);
                return;
            }

            var ammo = shooter.Weapons.TryFire();
            if (ammo != ShotRejection.None)
            {
                WarnRejected(shooterId, ammo);
                return;
            }

            // 弾を撃った時点で出現直後の無敵を解く (外れても)。跳ね返しの味方撃ちが撃った本人に届くよう、ダメージより先に
            _rules.NotifyFired(shooterId);

            var origin = blind == BlindFireAim.Blind ? blindOrigin : request.Origin;
            var direction = ApplySpread(shooter, spec, request, motion, blind == BlindFireAim.Blind, origin);
            var any = _hitboxes.Raycast(origin, direction, spec.Range, time.ToTick(), shooterId, IsHittable, out var hit);
            SendShot(shooterId, spec.Id, origin, direction, any, hit);
            if (!any)
            {
                return;
            }

            if (!hit.HitTarget)
            {
                _structures.ApplyHit(hit.ColliderId, hit.Point, spec.Damage, spec.ExplosionRadius, _changedStructures, _log);
                SendStructureChanges();
                return;
            }

            var victimId = hit.TargetId;
            var damage = DamageCalculator.Calculate(spec.Damage, hit.Zone, hit.Distance, spec.Falloff);
            var outcome = _rules.ApplyDamage(shooterId, victimId, damage, new KillShot(shooterId, victimId, spec.Id, hit.Zone));
            if (outcome != DamageOutcome.Ignored)
            {
                var hurt = IsReflected(shooterId, victimId) ? shooterId : victimId;
                if (_fighters.TryGetValue(hurt, out var hurtFighter))
                {
                    hurtFighter.LastHitDirection = direction;
                }

                SendHitResult(shooterId, victimId, outcome, damage, hit.Zone, body.Position);
            }

            var session = _rules.Session;
            var health = session != null && session.HasPlayer(victimId) ? session.GetHealth(victimId) : 0;
            _log.WriteLine(NetHitLog.Hit(shooterId, victimId, hit.RewindTicks, hit.Zone, damage, health, outcome, hit.TargetCrouched));
        }

        /// <summary>
        /// 受け入れた弾にサーバーの発射番号を付け、ずれていれば持ち主へ訂正を送り、ばらつきを掛けた判定の向きを返す。
        /// 散らばりは武器の値と、サーバーの知る覗き込みと移動の速さで決める (持ち主の予測と同じ計算)。
        /// </summary>
        private Vector3 ApplySpread(Fighter shooter, WeaponSpec spec, FireRequestMessage request, CharacterMotorState motion, bool blindFire, Vector3 origin)
        {
            var shooterId = shooter.PlayerId;
            var numbering = shooter.Shots.Assign(request.ShotIndex, request.ShotGeneration);
            if (numbering.NeedsCorrection)
            {
                _log.WriteLine(NetHitLog.ShotIndexCorrected(shooterId, request.ShotIndex, numbering.Index, numbering.Generation));
                _shotIndexCorrected.Generation = numbering.Generation;
                _shotIndexCorrected.ClientIndex = request.ShotIndex;
                _shotIndexCorrected.ServerIndex = numbering.Index;
                Send(shooterId, _shotIndexCorrected);
            }

            var velocity = motion.Velocity;
            var speed = new Vector2(velocity.X, velocity.Z).Length();
            var walk = _movement.Settings.Motor.WalkSpeed;
            var moveFraction = float.IsFinite(speed) && float.IsFinite(walk) ? SpreadPattern.MoveFraction(speed, walk) : 0f;
            var cone = SpreadPattern.ConeDegrees(spec, _movement.IsAiming(shooterId) ? 1f : 0f, moveFraction);
            var direction = cone > 0f ? SpreadPattern.Direction(request.Direction, cone, shooterId, numbering.Index) : request.Direction;
            if (blindFire)
            {
                direction = BlindFireShot.Spread(direction, spec.BlindFireSpreadDegrees, shooterId, numbering.Index);
                _log.WriteLine("[Net] blind-fire attacker=" + Text(shooterId) + " index=" + Text(numbering.Index)
                    + " spread=" + spec.BlindFireSpreadDegrees.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                    + " origin=(" + F2(origin.X) + "," + F2(origin.Y) + "," + F2(origin.Z) + ")");
            }

            _log.WriteLine(NetHitLog.Shot(shooterId, numbering.Index, cone, direction.X, direction.Y, direction.Z));
            return direction;
        }

        /// <summary>ルールの Cover と BlindFire がどちらも On で、カバー中で覗いていないならブラインドファイア (Unity の NetBlindFire.Resolve と同じ)。</summary>
        private BlindFireAim ResolveBlindFire(CharacterCoverState cover, ServerBody body, out Vector3 origin)
        {
            origin = body.EyePosition;
            var actions = _data.Rules.Actions;
            if (actions.Cover != Cover.On || actions.BlindFire != BlindFire.On || !cover.IsInCover || cover.IsPeeking)
            {
                return BlindFireAim.Normal;
            }

            // 検査を通った状態しか来ないはずだが、壊れた法線で例外にするより撃たせない
            if (!cover.IsValid)
            {
                return BlindFireAim.Blocked;
            }

            return BlindFireShot.TryResolveOrigin(
                    cover.Height, cover.Edges, cover.Snapshot.PreferredSide, cover.Normal, cover.TopY, body.Radius, body.EyePosition, out origin)
                ? BlindFireAim.Blind
                : BlindFireAim.Blocked;
        }

        // サーバーの知る目の位置から始点までの間に壁があれば、壁の向こうから撃たせない
        private bool IsOriginBlocked(Vector3 eye, Vector3 origin)
        {
            var toOrigin = origin - eye;
            var distance = toOrigin.Length();
            return distance > 0f && _context.World.Raycast(eye, toOrigin / distance, distance, ServerHitboxes.RaycastLayers, out _);
        }

        // 撃った本人は返事を待たずに自分で演出を出すので送らない
        private void SendShot(int shooterId, string weaponId, Vector3 origin, Vector3 direction, bool any, ShotHit hit)
        {
            _shot.HasShooter = true;
            _shot.ShooterId = shooterId;
            _shot.WeaponId = weaponId;
            _shot.Origin = origin;
            _shot.Direction = direction;
            _shot.HasImpact = any;
            _shot.ImpactPoint = any ? hit.Point : default;
            _shot.ImpactNormal = any ? hit.Normal : default;
            _shot.ImpactMaterialId = any ? DefaultSurfaceMaterialId : "";
            _shot.ImpactIsCharacter = any && hit.HitTarget;
            _shot.HasImpactZone = any && hit.HitTarget;
            _shot.ImpactZone = any && hit.HitTarget ? hit.Zone : default;
            SendToAll(_shot, shooterId);
        }

        /// <summary>
        /// 1 発の結果を本人たちに知らせる。跳ね返した味方撃ちなら撃った本人に被弾 (位置なし) だけを、それ以外は撃った人にヒット、
        /// 受けた人に被弾 (撃った人の位置から) を送る。
        /// </summary>
        private void SendHitResult(int shooterId, int victimId, DamageOutcome outcome, int damage, HitZone zone, Vector3 shooterPosition)
        {
            if (IsReflected(shooterId, victimId))
            {
                SendDamageTaken(shooterId, null, damage);
                return;
            }

            SendHitConfirm(shooterId, damage, zone, outcome);
            SendDamageTaken(victimId, shooterPosition, damage);
        }

        private void SendHitConfirm(int attackerId, int damage, HitZone zone, DamageOutcome outcome)
        {
            _hitConfirm.Damage = damage;
            _hitConfirm.Zone = zone;
            _hitConfirm.Killed = outcome == DamageOutcome.Killed;
            _hitConfirm.Downed = outcome == DamageOutcome.Downed;
            Send(attackerId, _hitConfirm);
        }

        private void SendDamageTaken(int victimId, Vector3? source, int amount)
        {
            _damageTaken.HasSource = source.HasValue;
            _damageTaken.SourcePosition = source ?? default;
            _damageTaken.Amount = amount;
            Send(victimId, _damageTaken);
        }

        private void WarnRejected(int shooterId, ShotRejection reason)
        {
            if (CanWarn())
            {
                _log.WriteLine(NetHitLog.Rejected(shooterId, reason));
            }
        }

        private static string F2(float value) => value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

        private enum BlindFireAim
        {
            Normal,
            Blind,
            Blocked,
        }
    }
}
