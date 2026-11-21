using System;
using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // Unity の Turret と同じ流れ (選び直す → 頭を回す → 向き終えたら射線を見て撃つ) で動かす。散らばりとペレットは持たせず、狙った点へ 1 発ずつ撃つ
    internal sealed partial class ServerDeployables
    {
        // Unity の Turret.SearchIntervalSeconds と同じ間隔で選び直す。
        public const float SearchIntervalSeconds = 0.25f;

        // 撃つ位置は TurretDeployDefinition の仮の形の頭の中心 (台の高さ 0.6 と頭の半分 0.2) にそろえる。
        public const float TurretEyeHeightFraction = 0.8f;

        private readonly List<int> _candidates = new List<int>();
        private readonly List<TargetOption> _options = new List<TargetOption>();

        private void TickTurret(Placed placed, float deltaSeconds)
        {
            var state = placed.Turret;
            state.Tick(deltaSeconds);
            var over = state.Over;
            if (over.HasValue)
            {
                Remove(placed, over.Value);
                return;
            }

            placed.SearchIn -= deltaSeconds;
            if (placed.SearchIn <= 0f || (placed.TargetId.HasValue && !_combat.IsHittable(placed.TargetId.Value)))
            {
                placed.SearchIn = SearchIntervalSeconds;
                Retarget(placed);
            }

            if (!placed.TargetId.HasValue || !_bodies.TryGet(placed.TargetId.Value, out var target))
            {
                return;
            }

            var eye = EyeOf(placed);
            var toTarget = target.Center - eye;
            if (!TurnTowards(placed, toTarget, deltaSeconds) || !state.IsReady)
            {
                return;
            }

            Shoot(placed, eye, toTarget, placed.TargetId.Value);
        }

        private void Retarget(Placed placed)
        {
            var session = _rules.Session;
            var eye = EyeOf(placed);
            var reach = ReachOf(placed);
            _candidates.Clear();
            _options.Clear();
            if (session != null && session.State == GameModeState.Playing)
            {
                foreach (var body in _bodies.All)
                {
                    var id = body.PlayerId;
                    if (!body.HasSpawned || !_combat.IsHittable(id) || !IsHostile(placed, id))
                    {
                        continue;
                    }

                    var toBody = body.Center - eye;
                    var distance = toBody.Length();
                    if (distance > reach || (distance > 1e-4f && _context.World.Raycast(eye, toBody / distance, distance, ServerHitboxes.RaycastLayers, out _)))
                    {
                        continue;
                    }

                    _candidates.Add(id);
                    _options.Add(new TargetOption(true, body.Position, true));
                }
            }

            var index = TurretTargeting.Nearest(placed.Position, _options);
            int? next = index >= 0 ? _candidates[index] : (int?)null;
            if (next == placed.TargetId)
            {
                return;
            }

            placed.TargetId = next;
            _log.WriteLine(TurretLog.Target(
                ServerDeployableLog.Owner(placed.OwnerId), placed.Type.Id, placed.Id, next.HasValue ? ServerDeployableLog.Owner(next.Value) : null));
        }

        // 向き終えるまでは撃たない。上下は回さず、撃つ向きは目から相手の体の中心へそのまま引く (Unity の Turret.TurnTowards と同じ)
        private static bool TurnTowards(Placed placed, Vector3 toTarget, float deltaSeconds)
        {
            if ((toTarget.X * toTarget.X) + (toTarget.Z * toTarget.Z) < 1e-8f)
            {
                return true;
            }

            var spec = placed.Type.Turret;
            var desired = MathF.Atan2(toTarget.X, toTarget.Z) * (180f / MathF.PI);
            placed.HeadYaw = TurretTargeting.StepYaw(placed.HeadYaw, desired, spec.TurnDegreesPerSecond * Math.Max(0f, deltaSeconds));
            return Math.Abs(TurretTargeting.DeltaDegrees(placed.HeadYaw, desired)) <= spec.AimToleranceDegrees;
        }

        // 弾を減らす前に射線の最初に当たる物を見る。味方なら味方撃ちのルールで控え、別の敵ならそのまま撃ち、
        // 壁など撃っても仕方の無い物なら撃たずに選び直す (Unity の Turret.TryShoot と同じ)
        private void Shoot(Placed placed, Vector3 eye, Vector3 toTarget, int targetId)
        {
            var reach = ReachOf(placed);
            var distance = toTarget.Length();
            if (distance > reach)
            {
                placed.SearchIn = 0f;
                return;
            }

            var unit = distance > 1e-4f ? toTarget / distance : Forward(placed.HeadYaw);
            var hitboxes = _combat.Hitboxes;
            var tick = hitboxes.HasHistory ? hitboxes.LastRecordedTick : 0;
            var hasHit = hitboxes.RaycastIgnoringDeployables(eye, unit, reach, tick, PlayerIds.None, _combat.IsHittable, out var hit);
            if (Raycast(eye, unit, hasHit ? hit.Distance : reach, placed.OwnerId, placed.Id, out var blockerId, out var blockerDistance, out _))
            {
                // 敵の置いた盾などが前にあれば、それを撃って壊しにかかる。撃たずに待つと盾の後ろの相手を狙ったまま動けなくなる
                placed.HeldFor = null;
                placed.Turret.Fire();
                var blockerDamage = DamageAt(placed, HitZone.Body, blockerDistance);
                LogFired(placed, targetId, "deployable:" + ServerDeployableLog.Owner(blockerId), blockerDamage);
                Damage(blockerId, placed.OwnerId, blockerDamage);
                return;
            }

            if (!hasHit || !hit.HitTarget)
            {
                placed.SearchIn = 0f;
                return;
            }

            var hitId = hit.TargetId;
            if (hitId != targetId)
            {
                if (IsAlly(placed, hitId))
                {
                    if (TurretTargeting.HoldsFire(_data.Rules.FriendlyFire, true))
                    {
                        LogHeld(placed, hitId);
                        return;
                    }
                }
                else if (!IsHostile(placed, hitId))
                {
                    placed.SearchIn = 0f;
                    return;
                }
            }

            placed.HeldFor = null;
            placed.Turret.Fire();
            var damage = DamageAt(placed, hit.Zone, hit.Distance);
            var outcome = _combat.DealDamage(placed.OwnerId, hitId, damage, placed.Weapon.Id, hit.Zone, eye, DamageKind.Bullet);
            LogFired(placed, targetId, ServerDeployableLog.Owner(hitId), outcome == DamageOutcome.Ignored ? 0 : damage);
            if (outcome == DamageOutcome.Killed)
            {
                _log.WriteLine(TurretLog.Killed(ServerDeployableLog.Owner(placed.OwnerId), placed.Type.Id, placed.Id, ServerDeployableLog.Owner(hitId)));
            }
        }

        private int DamageAt(Placed placed, HitZone zone, float distance)
        {
            var impact = placed.Weapon.Impact;
            return DamageCalculator.Calculate(impact.Damage, zone, distance, impact.Falloff, _data.Rules.ZoneDamage);
        }

        private void LogFired(Placed placed, int targetId, string hitId, int damage)
        {
            _log.WriteLine(TurretLog.Fired(
                ServerDeployableLog.Owner(placed.OwnerId),
                placed.Type.Id,
                placed.Id,
                ServerDeployableLog.Owner(targetId),
                hitId,
                damage,
                placed.Turret.RoundsLeft));
        }

        // 毎 tick 出すとログが埋まるので、控える相手が変わったときだけ出す
        private void LogHeld(Placed placed, int allyId)
        {
            if (placed.HeldFor == allyId)
            {
                return;
            }

            placed.HeldFor = allyId;
            _log.WriteLine(TurretLog.Held(
                ServerDeployableLog.Owner(placed.OwnerId), placed.Type.Id, placed.Id, ServerDeployableLog.Owner(allyId), _data.Rules.FriendlyFire));
        }

        private bool IsAlly(Placed placed, int playerId)
        {
            return TurretTargeting.IsAlly(playerId == placed.OwnerId, Side.FromTeam(placed.Team), SideOf(playerId));
        }

        private bool IsHostile(Placed placed, int playerId)
        {
            return TurretTargeting.IsHostile(playerId == placed.OwnerId, Side.FromTeam(placed.Team), SideOf(playerId), BotRoster.IsBotId(playerId));
        }

        private static Vector3 EyeOf(Placed placed)
        {
            return placed.Position + new Vector3(0f, placed.Type.Deploy.Height * TurretEyeHeightFraction, 0f);
        }

        // 武器の射程より長く見えても、撃つのは射程の内だけ (Unity の Turret.Reach と同じ)
        private static float ReachOf(Placed placed)
        {
            return Math.Min(placed.Type.Turret.SightRange, placed.Weapon.Range);
        }
    }
}
