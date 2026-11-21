using System;
using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // サーバー権威のヒットスキャン (Unity の NetTeamDeathmatch.Fire と NetPlayer.Fire のサーバー側)
    internal sealed partial class ServerCombat
    {
        // .fpsmap は材質を持たないので、どの面も Unity の SurfaceMaterial.DefaultId と同じこの材質で送る。
        public const string DefaultSurfaceMaterialId = "default";

        private readonly ShotMessage _shot = new ShotMessage();
        private readonly HitConfirmMessage _hitConfirm = new HitConfirmMessage();
        private readonly DamageTakenMessage _damageTaken = new DamageTakenMessage();
        private readonly ShotIndexCorrectedMessage _shotIndexCorrected = new ShotIndexCorrectedMessage();
        private readonly FireRequestMessage _botFire = new FireRequestMessage();
        private readonly List<ShotTraceSegment> _segments = new List<ShotTraceSegment>();
        private readonly PelletHits<int> _pelletHits = new PelletHits<int>();
        private bool _collectingPelletHits;
        // ペレットごとに乗り物のヒットマーカーを出さないよう、1 回の射撃で 1 度だけ送る
        private bool _vehicleHitConfirmed;

        private bool Fire(Fighter shooter, FireRequestMessage request)
        {
            var shooterId = shooter.PlayerId;
            if (request.Trigger == TriggerEdge.Released)
            {
                _beams.Release(shooterId);
                _spray.Release(shooterId);
                return false;
            }

            // ダウン中・倒れている人の射撃は理由を出さずに捨てる
            if (!_rules.CanFire(shooterId) || !_bodies.TryGet(shooterId, out var body))
            {
                return false;
            }

            // 回避・乗り越えの最中は撃たせない。連射の枠も弾も使わないよう、武器の検査より先に捨てる
            _movement.TryGetState(shooterId, out var motion);
            if (motion.IsDodging || motion.IsVaulting)
            {
                WarnRejected(shooterId, motion.IsDodging ? ShotRejection.Dodging : ShotRejection.Vaulting);
                return false;
            }

            var seatCheck = CheckSeat(shooterId, request, out var weapons, out var spreadMultiplier);
            if (seatCheck != ShotRejection.None)
            {
                WarnRejected(shooterId, seatCheck);
                return false;
            }

            var spec = weapons.CurrentSpec;

            // 連射の枠を使わないよう、ServerLoadout.TryFire より先に断る
            if (spec != null && spec.IsShield)
            {
                WarnRejected(shooterId, ShotRejection.ShieldWeapon);
                return false;
            }

            var weaponCheck = weapons == shooter.Weapons
                ? _rules.Loadout.TryFire(shooterId, spec, request.WeaponId, _now)
                : CheckMountFire(weapons, spec, request.WeaponId);
            if (weaponCheck != ShotRejection.None)
            {
                WarnRejected(shooterId, weaponCheck);
                return false;
            }

            var time = new ShotTime(request.ClientTick, request.InterpolationAlpha);
            var rejection = ShotValidator.Check(request.Origin, request.Direction, time, body.Position, _data.Settings.MaxShotOriginOffset);
            if (rejection != ShotRejection.None)
            {
                WarnRejected(shooterId, rejection);
                return false;
            }

            if (IsOriginBlocked(body.EyePosition, request.Origin))
            {
                WarnRejected(shooterId, ShotRejection.OriginBlocked);
                return false;
            }

            // ブラインドファイアの始点はクライアントの始点ではなく、サーバーの知るカバーの状態と目の位置から決める
            var blind = ResolveBlindFire(motion.Cover, body, out var blindOrigin);
            if (blind == BlindFireAim.Blocked)
            {
                WarnRejected(shooterId, ShotRejection.BlindFireBlocked);
                return false;
            }

            var chargeCheck = weapons.AcceptCharge(request.ChargeFraction, out var chargeFraction);
            if (chargeCheck != ShotRejection.None)
            {
                WarnRejected(shooterId, chargeCheck);
                return false;
            }

            var ammo = weapons.TryFire();
            if (ammo != ShotRejection.None)
            {
                WarnRejected(shooterId, ammo);
                return false;
            }

            // 弾を撃った時点で出現直後の無敵を解く (外れても)。跳ね返しの味方撃ちが撃った本人に届くよう、ダメージより先に
            _rules.NotifyFired(shooterId);

            var origin = blind == BlindFireAim.Blind ? blindOrigin : request.Origin;
            _signals?.RaiseShotAccepted(shooterId, origin, request.Direction, time.ToTick());
            var round = weapons.CurrentRound;
            var plan = PlanShot(shooter, spec, round, request, motion, blind == BlindFireAim.Blind, origin, spreadMultiplier);
            var shot = new ServerShot(
                shooterId,
                spec,
                round,
                weapons.CurrentImpact,
                chargeFraction,
                request.HasLockTarget ? _lockOn.Resolve(shooterId, round, request) : null,
                time.ToTick(),
                body.Position,
                request.Trigger);
            Deliver(shot, plan);
            return true;
        }

        // 同じ射撃の弾が、先の弾のダメージで倒れた相手を素通りしないよう、判定はダメージより先にまとめて行う。
        private void Deliver(in ServerShot shot, ShotPlan plan)
        {
            _segments.Clear();
            _vehicleHitConfirmed = false;
            foreach (var planned in plan.Rounds)
            {
                switch (shot.Round.Delivery)
                {
                    case RoundDelivery.Hitscan:
                        _shotTrace.Trace(shot.Round, planned, shot.Tick, shot.ShooterId, IsHittable, _segments);
                        break;
                    case RoundDelivery.Projectile:
                        _projectiles.Launch(shot, planned);
                        break;
                    case RoundDelivery.Beam:
                        _beams.Fire(shot, planned);
                        break;
                    case RoundDelivery.Spray:
                        _spray.Fire(shot, planned);
                        break;
                    default:
                        throw new InvalidOperationException("unknown round delivery " + shot.Round.Delivery);
                }
            }

            if (_segments.Count == 0)
            {
                return;
            }

            ReportNearMisses(shot, _segments);
            SendShot(shot.ShooterId, shot.Weapon.Id, _segments);

            // ペレットごとに知らせると 1 回の射撃でヒットマーカーと被弾の表示が何度も出るので、相手ごとにまとめてから送る。
            _pelletHits.Clear();
            _collectingPelletHits = true;
            try
            {
                foreach (var segment in _segments)
                {
                    if (segment.HasHit && !segment.Bounced)
                    {
                        _impact.Apply(shot.WithDamageMultiplier(segment.DamageMultiplier), segment.Direction, segment.Hit);
                    }
                }
            }
            finally
            {
                _collectingPelletHits = false;
            }

            for (var i = 0; i < _pelletHits.Count; i++)
            {
                var hit = _pelletHits[i];
                SendHitResult(shot.ShooterId, hit.Target, hit.Outcome, hit.Damage, hit.Zone, shot.ShooterPosition);
            }

            _pelletHits.Clear();
        }

        internal void DamagePlayer(in ServerShot shot, Vector3 direction, in ShotHit hit, int amount)
        {
            var shooterId = shot.ShooterId;
            var victimId = hit.TargetId;
            // 飛ぶ弾は撃った人が抜けた後にも当たる。グレネードと同じく攻撃者のいないダメージ (倒れたら自滅) にし、キルフィードも本人にそろえる
            var attacker = _match.TryGetPlayer(shooterId, out _) ? shooterId : (int?)null;
            var outcome = _rules.ApplyDamage(attacker, victimId, amount, new KillShot(attacker ?? victimId, victimId, shot.Weapon.Id, hit.Zone));
            if (outcome != DamageOutcome.Ignored)
            {
                _signals?.RaiseHit(shooterId, victimId, hit.Zone, hit.RewindTicks);
                var hurt = IsReflected(shooterId, victimId) ? shooterId : victimId;
                ReportStatusDamage(hurt, hit.Zone, DamageKind.Bullet, amount, outcome);
                if (_fighters.TryGetValue(hurt, out var hurtFighter))
                {
                    hurtFighter.LastHitDirection = direction;
                }

                if (_collectingPelletHits)
                {
                    _pelletHits.Add(victimId, amount, hit.Zone, outcome);
                }
                else
                {
                    SendHitResult(shooterId, victimId, outcome, amount, hit.Zone, shot.ShooterPosition);
                }
            }

            var session = _rules.Session;
            var health = session != null && session.HasPlayer(victimId) ? session.GetHealth(victimId) : 0;
            _log.WriteLine(NetHitLog.Hit(shooterId, victimId, hit.RewindTicks, hit.Zone, amount, health, outcome, hit.TargetCrouched));
        }

        internal DamageOutcome DamageOverTime(int? attackerId, int victimId, int amount, string weaponId)
        {
            var session = _rules.Session;
            var attacker = attackerId.HasValue && session != null && session.HasPlayer(attackerId.Value) ? attackerId : null;
            var outcome = attacker.HasValue
                ? _rules.ApplyDamage(attacker, victimId, amount, new KillShot(attacker.Value, victimId, weaponId, HitZone.Body))
                : _rules.ApplyDamage(null, victimId, amount);
            if (outcome != DamageOutcome.Ignored)
            {
                SendDamageTaken(attacker.HasValue && IsReflected(attacker.Value, victimId) ? attacker.Value : victimId, null, amount);
            }

            return outcome;
        }

        internal bool CanAfflict(int attackerId, int victimId)
        {
            var session = _rules.Session;
            if (session == null || !session.HasPlayer(victimId))
            {
                return false;
            }

            return attackerId == victimId
                || !session.HasPlayer(attackerId)
                || session.Spec.Rules.FriendlyFire == FriendlyFire.On
                || _rules.AreHostile(attackerId, victimId);
        }

        internal void DamageStructure(int colliderId, Vector3 point, int amount, float radius)
        {
            _structures.ApplyHit(colliderId, point, amount, radius, _changedStructures, _log);
            SendStructureChanges();
        }

        // Bot はサーバーの上で今の位置を見て撃つので、見ていた tick はこの tick にして巻き戻さない。
        // 発射番号はサーバーが次に受け入れる番号を添え、訂正を起こさない。
        public bool BotFire(int playerId, Vector3 origin, Vector3 direction, int tick)
        {
            // 盾の武器を持った Bot は撃てないので、断られる要求を作って違反の数と警告を増やさない
            if (!BotRoster.IsBotId(playerId) || !_fighters.TryGetValue(playerId, out var fighter) || fighter.Weapons.CurrentSpec == null
                || fighter.Weapons.CurrentSpec.IsShield || !fighter.Weapons.IsChargedForBot())
            {
                return false;
            }

            _botFire.WeaponId = fighter.Weapons.CurrentSpec.Id;
            _botFire.Origin = origin;
            _botFire.Direction = direction;
            _botFire.ClientTick = tick;
            _botFire.InterpolationAlpha = 0f;
            _botFire.ShotIndex = fighter.Shots.NextIndex;
            _botFire.ShotGeneration = fighter.Shots.Generation;
            _botFire.ChargeFraction = 1f;
            _botFire.AmmoTypeIndex = fighter.Weapons.AmmoTypeIndex;
            _botFire.HasLockTarget = false;
            _botFire.LockTargetId = 0;
            _botFire.Trigger = TriggerEdge.None;
            return Fire(fighter, _botFire);
        }

        private ShotRejection CheckMountFire(ServerPlayerWeapons mount, WeaponSpec spec, string weaponId)
        {
            if (!string.Equals(spec.Id, weaponId, StringComparison.Ordinal))
            {
                return ShotRejection.WeaponMismatch;
            }

            if (!_mountBudgets.TryGetValue(mount, out var budget))
            {
                // ビームと火炎の撃ち方は最短の間隔が 0 なので、ServerLoadout と同じく照射の間隔で制限する
                var round = spec.Round;
                budget = round.Delivery == RoundDelivery.Beam ? new ShotBudget(round.Beam.TickSeconds)
                    : round.Delivery == RoundDelivery.Spray ? new ShotBudget(round.Spray.TickSeconds)
                    : new ShotBudget(TriggerModels.For(spec));
                _mountBudgets.Add(mount, budget);
            }

            return budget.TryConsume(_now) ? ShotRejection.None : ShotRejection.FireRateExceeded;
        }

        private ShotRejection CheckSeat(int shooterId, FireRequestMessage request, out ServerPlayerWeapons weapons, out float spreadMultiplier)
        {
            var fighter = _fighters[shooterId];
            weapons = fighter.Weapons;
            spreadMultiplier = 1f;
            if (_vehicles == null || !_vehicles.TryGetSeat(shooterId, out var vehicle, out var seat))
            {
                return ShotRejection.None;
            }

            switch (_vehicles.FiringOf(shooterId, out _, out _, out var weapon))
            {
                case VehicleSeatFiring.VehicleWeapon:
                    weapons = MountWeapons(vehicle, weapon);
                    return _vehicles.AllowsVehicleWeapon(vehicle, weapon, request.Direction) ? ShotRejection.None : ShotRejection.OutsideSeatArc;
                case VehicleSeatFiring.OwnWeapon:
                    spreadMultiplier = _vehicles.MapOf(vehicle).Spec.Seats[seat].OwnWeaponSpreadMultiplier;
                    return _vehicles.AllowsOwnWeapon(vehicle, seat, request.Direction) ? ShotRejection.None : ShotRejection.OutsideSeatArc;
                default:
                    return ShotRejection.SeatCannotFire;
            }
        }

        // Fire の検査を照射のたびに通さないのは、始点・向き・発射番号・連射の枠は押したときの要求にしか無いため。
        // 倒れた・試合が止まったなど撃てない状態は、警告にしないよう理由を ShotRejection.None にして false を返す。
        internal bool TryContinueFire(int shooterId, string weaponId, bool consume, out ShotRejection rejection)
        {
            rejection = ShotRejection.None;
            if (!_rules.CanFire(shooterId) || !_fighters.TryGetValue(shooterId, out var fighter))
            {
                return false;
            }

            var weapons = ActiveWeapons(fighter);
            var spec = weapons.CurrentSpec;
            if (spec == null)
            {
                rejection = ShotRejection.NoWeapon;
                return false;
            }

            if (!string.Equals(spec.Id, weaponId, StringComparison.Ordinal))
            {
                rejection = ShotRejection.WeaponMismatch;
                return false;
            }

            _movement.TryGetState(shooterId, out var motion);
            if (motion.IsDodging || motion.IsVaulting)
            {
                rejection = motion.IsDodging ? ShotRejection.Dodging : ShotRejection.Vaulting;
                return false;
            }

            if (consume)
            {
                rejection = weapons.TryFire();
            }

            return rejection == ShotRejection.None;
        }

        public bool TryGetWeapon(int playerId, out WeaponSpec spec, out WeaponSync state)
        {
            if (_fighters.TryGetValue(playerId, out var fighter) && fighter.Weapons.CurrentSpec != null)
            {
                spec = fighter.Weapons.CurrentSpec;
                state = fighter.Weapons.Sync();
                return true;
            }

            spec = null;
            state = default;
            return false;
        }

        public bool BotReload(int playerId)
        {
            return BotRoster.IsBotId(playerId) && _fighters.TryGetValue(playerId, out var fighter) && !IsReloadHeldOff(fighter) && fighter.Weapons.TryReload();
        }

        // 散らばりは持ち主の予測と同じ計算になるよう、武器の値と、サーバーの知る覗き込みと移動の速さで決める。
        private ShotPlan PlanShot(
            Fighter shooter,
            WeaponSpec spec,
            RoundSpec round,
            FireRequestMessage request,
            CharacterMotorState motion,
            bool blindFire,
            Vector3 origin,
            float spreadMultiplier = 1f)
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
            var cone = SpreadPattern.ConeDegrees(spec, _movement.IsAiming(shooterId) ? 1f : 0f, moveFraction) * spreadMultiplier;
            var plan = ShotPlanner.Plan(round, origin, request.Direction, cone, shooterId, numbering.Index);
            if (blindFire)
            {
                plan = BlindFireSpread(plan, spec.BlindFireSpreadDegrees, shooterId);
                _log.WriteLine("[Net] blind-fire attacker=" + Text(shooterId) + " index=" + Text(numbering.Index)
                    + " spread=" + spec.BlindFireSpreadDegrees.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                    + " origin=(" + F2(origin.X) + "," + F2(origin.Y) + "," + F2(origin.Z) + ")");
            }

            var direction = plan.Rounds[0].Direction;
            _log.WriteLine(NetHitLog.Shot(shooterId, numbering.Index, cone, direction.X, direction.Y, direction.Z));
            return plan;
        }

        private static ShotPlan BlindFireSpread(ShotPlan plan, float spreadDegrees, int shooterId)
        {
            var rounds = new PlannedRound[plan.Rounds.Count];
            for (var i = 0; i < rounds.Length; i++)
            {
                var planned = plan.Rounds[i];
                rounds[i] = new PlannedRound(
                    planned.Origin, BlindFireShot.Spread(planned.Direction, spreadDegrees, shooterId, plan.ShotIndex), planned.Index);
            }

            return new ShotPlan(plan.Round, plan.ShotIndex, rounds);
        }

        // Unity の NetBlindFire.Resolve と同じ決め方にする。
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
        internal bool IsOriginBlocked(Vector3 eye, Vector3 origin)
        {
            var toOrigin = origin - eye;
            var distance = toOrigin.Length();
            return distance > 0f && _context.World.Raycast(eye, toOrigin / distance, distance, ServerHitboxes.RaycastLayers, out _);
        }

        // 撃った本人は返事を待たずに自分で演出を出すので送らない。弾の区間が並びに入り切らなければ、後ろの区間は演出を出さない
        private void SendShot(int shooterId, string weaponId, List<ShotTraceSegment> segments)
        {
            _shot.HasShooter = true;
            _shot.ShooterId = shooterId;
            _shot.WeaponId = weaponId;
            _shot.ResetSegments();
            var count = Math.Min(segments.Count, ShotMessage.MaxSegments);
            for (var i = 0; i < count; i++)
            {
                ref var row = ref (i == 0 ? ref _shot.Segment(0) : ref _shot.AddSegment());
                var segment = segments[i];
                var hit = segment.Hit;
                var any = segment.HasHit;
                row.Origin = segment.Origin;
                row.Direction = segment.Direction;
                row.HasImpact = any;
                row.ImpactPoint = any ? hit.Point : default;
                row.ImpactNormal = any ? hit.Normal : default;
                row.ImpactMaterialId = any ? DefaultSurfaceMaterialId : "";
                row.ImpactIsCharacter = any && hit.HitTarget;
                row.HasImpactZone = any && hit.HitTarget;
                row.ImpactZone = any && hit.HitTarget ? hit.Zone : default;
            }

            SendToAll(_shot, shooterId);
        }

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
            // 警告の行は連打で間引くが、違反の累積は間引かずに数えるので、間引く前に出す
            _signals?.RaiseShotRejected(shooterId, reason);
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
