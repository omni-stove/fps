using System;
using System.Collections.Generic;
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
        private readonly FireRequestMessage _botFire = new FireRequestMessage();
        private readonly List<ShotTraceSegment> _segments = new List<ShotTraceSegment>();

        /// <summary>
        /// 射撃 1 発。撃てる状態・武器と連射の間隔・始点を確かめ、弾倉から 1 発消費してから、<see cref="ShotPlanner"/> で弾を並べ、
        /// 弾ごとに届け方 (<see cref="RoundSpec.Delivery"/>) へ渡す。ヒットスキャンの弾は撃った人が見ていた時点まで他の人を巻き戻して当て、
        /// 当たりを <see cref="ServerImpact"/> で処理する。判定の向きはサーバーが数えた発射番号でばらつきを掛けた向き。
        /// 検査で捨てた射撃では弾も連射の枠も減らさない。引き金を離した知らせは検査せずに、押している間出続ける届け方へ渡す。
        /// </summary>
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

            var spec = shooter.Weapons.CurrentSpec;
            var weaponCheck = _rules.Loadout.TryFire(shooterId, spec, request.WeaponId, _now);
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

            var ammo = shooter.Weapons.TryFire();
            if (ammo != ShotRejection.None)
            {
                WarnRejected(shooterId, ammo);
                return false;
            }

            // 弾を撃った時点で出現直後の無敵を解く (外れても)。跳ね返しの味方撃ちが撃った本人に届くよう、ダメージより先に
            _rules.NotifyFired(shooterId);

            var origin = blind == BlindFireAim.Blind ? blindOrigin : request.Origin;
            var round = shooter.Weapons.CurrentRound;
            var plan = PlanShot(shooter, spec, round, request, motion, blind == BlindFireAim.Blind, origin);
            var shot = new ServerShot(
                shooterId,
                spec,
                round,
                shooter.Weapons.CurrentImpact,
                shooter.Weapons.AcceptCharge(request.ChargeFraction),
                request.HasLockTarget ? _lockOn.Resolve(shooterId, round, request) : null,
                time.ToTick(),
                body.Position,
                request.Trigger);
            Deliver(shot, plan);
            return true;
        }

        /// <summary>
        /// 弾を届け方ごとに振り分ける。ヒットスキャンの弾はすべて通してから 1 つの <see cref="ShotMessage"/> で送り、そのあと当たりを順に処理する。
        /// 同じ射撃の弾が、先の弾のダメージで倒れた相手を素通りしないよう、判定はダメージより先にまとめて行う。
        /// </summary>
        private void Deliver(in ServerShot shot, ShotPlan plan)
        {
            _segments.Clear();
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

            SendShot(shot.ShooterId, shot.Weapon.Id, _segments);
            foreach (var segment in _segments)
            {
                if (segment.HasHit)
                {
                    _impact.Apply(shot, segment.Direction, segment.Hit, segment.DamageMultiplier);
                }
            }
        }

        /// <summary>
        /// shot の弾が direction へ進んで hit のプレイヤーに amount のダメージを与える。ルールに通し、受けた人の倒れる向きを残し、
        /// 本人たちにヒット・被弾を送って NetHitLog に残す。
        /// </summary>
        internal void DamagePlayer(in ServerShot shot, Vector3 direction, in ShotHit hit, int amount)
        {
            var shooterId = shot.ShooterId;
            var victimId = hit.TargetId;
            var outcome = _rules.ApplyDamage(shooterId, victimId, amount, new KillShot(shooterId, victimId, shot.Weapon.Id, hit.Zone));
            if (outcome != DamageOutcome.Ignored)
            {
                var hurt = IsReflected(shooterId, victimId) ? shooterId : victimId;
                if (_fighters.TryGetValue(hurt, out var hurtFighter))
                {
                    hurtFighter.LastHitDirection = direction;
                }

                SendHitResult(shooterId, victimId, outcome, amount, hit.Zone, shot.ShooterPosition);
            }

            var session = _rules.Session;
            var health = session != null && session.HasPlayer(victimId) ? session.GetHealth(victimId) : 0;
            _log.WriteLine(NetHitLog.Hit(shooterId, victimId, hit.RewindTicks, hit.Zone, amount, health, outcome, hit.TargetCrouched));
        }

        /// <summary>世界の colliderId の point に amount の被害を与え、壊れた建物の状態を送る。</summary>
        internal void DamageStructure(int colliderId, Vector3 point, int amount, float radius)
        {
            _structures.ApplyHit(colliderId, point, amount, radius, _changedStructures, _log);
            SendStructureChanges();
        }

        /// <summary>
        /// Bot の射撃を人の射撃と同じ検査 (撃てる状態・武器・連射・始点・弾数) に通す。Bot はサーバーの上で今の位置を見て撃つので、
        /// 見ていた tick はこの tick (巻き戻しは無い)。発射番号はサーバーが次に受け入れる番号を添え、訂正を起こさない。
        /// </summary>
        public bool BotFire(int playerId, Vector3 origin, Vector3 direction, int tick)
        {
            if (!BotRoster.IsBotId(playerId) || !_fighters.TryGetValue(playerId, out var fighter) || fighter.Weapons.CurrentSpec == null)
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
            _botFire.ChargeFraction = 0f;
            _botFire.AmmoTypeIndex = fighter.Weapons.AmmoTypeIndex;
            _botFire.HasLockTarget = false;
            _botFire.LockTargetId = 0;
            _botFire.Trigger = TriggerEdge.None;
            return Fire(fighter, _botFire);
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
            return BotRoster.IsBotId(playerId) && _fighters.TryGetValue(playerId, out var fighter) && fighter.Weapons.TryReload();
        }

        /// <summary>
        /// 受け入れた弾にサーバーの発射番号を付け、ずれていれば持ち主へ訂正を送り、ばらつきを掛けた弾の並びを返す。
        /// 散らばりは武器の値と、サーバーの知る覗き込みと移動の速さで決める (持ち主の予測と同じ計算)。
        /// </summary>
        private ShotPlan PlanShot(
            Fighter shooter, WeaponSpec spec, RoundSpec round, FireRequestMessage request, CharacterMotorState motion, bool blindFire, Vector3 origin)
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
