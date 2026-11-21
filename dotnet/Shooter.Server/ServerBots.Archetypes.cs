using System;
using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // 種類 (archetype) の判断が決めた段階を、サーバーの体・戦闘・近接で行う。オフラインの EnemyCharger・EnemyBomber・EnemyShield・EnemySniper と同じ決まりにする
    internal sealed partial class ServerBots
    {
        // 突進が段や角で切り詰められたとみなす割合。オフラインの EnemyCharger.BlockedProgressRatio と同じ値で、浮動小数の誤差で少し短くなっただけでは止めない
        // (EnemyCharger は Runtime にあり、サーバーからは読めない)
        private const float _blockedProgressRatio = 0.5f;

        private const string _cancelKnockback = "knockback";
        private const string _cancelLostControl = "downed";

        private readonly List<FallenBomber> _fallen = new List<FallenBomber>();

        public void Contribute(SnapshotMessage snapshot, ServerPlayer receiver)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            ArgumentNullException.ThrowIfNull(receiver);
            var players = snapshot.Players;
            for (var i = 0; i < players.Count; i++)
            {
                ref var row = ref players[i];
                if (_agents.TryGetValue(row.PlayerId, out var agent))
                {
                    row.Archetype = agent.Archetype;
                    row.ArchetypeCue = row.Concealed ? BotArchetypeCue.None : agent.Cue;
                }
            }
        }

        private void StartArchetypes()
        {
            _combat.BotShotDamageMultiplier = playerId => _agents.TryGetValue(playerId, out var agent) ? agent.ShotDamageMultiplier : 1f;
            _combat.BotShield = playerId => _agents.TryGetValue(playerId, out var agent) ? agent.RaisedShield : null;
            _combat.Wounded += HandleWounded;
            _combat.PlayerKilled += HandleKilled;
        }

        private void StopArchetypes()
        {
            _combat.BotShotDamageMultiplier = null;
            _combat.BotShield = null;
            _combat.Wounded -= HandleWounded;
            _combat.PlayerKilled -= HandleKilled;
            _fallen.Clear();
        }

        // 銃弾では下ろさない (ShieldSpec.LowerSecondsFor)。近接と爆発で崩す
        private void HandleWounded(int playerId, DamageKind kind)
        {
            if (!_agents.TryGetValue(playerId, out var agent))
            {
                return;
            }

            var seconds = agent.LowerShield(kind);
            if (seconds > 0f)
            {
                _log.WriteLine(ServerBotLog.ShieldLowered(playerId, kind, seconds));
            }
        }

        // 倒された自爆兵の爆発は、キルを数えている最中に別のダメージを起こさないよう、次の tick の始めに起こす。
        // 爆ぜる場所と大小は倒された時点で決める (オフラインの EnemyBomber.OnDied と同じ)
        private void HandleKilled(KillFeedEntry entry)
        {
            var victimId = entry.VictimId;
            if (!_agents.TryGetValue(victimId, out var agent) || !_bodies.TryGet(victimId, out var body))
            {
                return;
            }

            var blast = agent.DeathBlast();
            if (blast == null)
            {
                return;
            }

            int? killer = entry.KillerId != victimId && _match.TryGetPlayer(entry.KillerId, out _) ? entry.KillerId : (int?)null;
            _fallen.Add(new FallenBomber(victimId, killer, body.Center, blast));
        }

        private void DetonateFallen()
        {
            if (_fallen.Count == 0)
            {
                return;
            }

            var fallen = _fallen.ToArray();
            _fallen.Clear();
            foreach (var bomber in fallen)
            {
                var (damaged, killed) = _combat.BotDetonate(bomber.PlayerId, bomber.Center, bomber.Blast, bomber.KillerId);
                _log.WriteLine(ServerBotLog.Exploded(bomber.PlayerId, bomber.KillerId.HasValue ? "killed" : "died", bomber.Blast.Radius, damaged, killed));
            }
        }

        // 突進している・爆ぜたなら、この tick の歩く入力と射撃を出さないよう true を返す
        private bool StepArchetype(ServerBotAgent agent, ServerBody body, TeamDeathmatchSession session, CharacterMotorState motion, bool thinking, float dt)
        {
            var playerId = agent.PlayerId;
            foreach (var line in agent.TakePhaseLog())
            {
                _log.WriteLine(line);
            }

            if (agent.TickGuard(dt))
            {
                _log.WriteLine(ServerBotLog.ShieldRaised(playerId));
            }

            if (thinking && agent.IsFuseBurnt)
            {
                Detonate(agent, body, session);
                return true;
            }

            if (!agent.IsCharging)
            {
                return false;
            }

            // 吹き飛ばされた・ダウンした・試合が終わった間は突進をやめる。続けると、飛ばされた先から元の向きへ走り出す
            if (!thinking || motion.Knockback.IsFlying)
            {
                agent.CancelCharge();
                _log.WriteLine(ServerBotLog.ChargeCancelled(playerId, thinking ? _cancelKnockback : _cancelLostControl));
                return false;
            }

            Charge(agent, body, session, dt);
            return true;
        }

        private void Detonate(ServerBotAgent agent, ServerBody body, TeamDeathmatchSession session)
        {
            var playerId = agent.PlayerId;
            if (agent.Detonate() == BomberPhase.Exploded)
            {
                return;
            }

            var blast = agent.Brain.Spec.Bomber.Blast;
            var (damaged, killed) = _combat.BotDetonate(playerId, body.Center, blast, null);
            _log.WriteLine(ServerBotLog.Exploded(playerId, "fuse", blast.Radius, damaged, killed));

            // 自分の爆発で倒れきらなかったら (味方撃ちが切ってあるなど) 残りの体力を落とす。自爆兵は爆ぜたら残らない
            if (session.IsAlive(playerId))
            {
                _combat.DealDamage(playerId, playerId, Math.Max(1, session.GetHealth(playerId)), ServerCombat.BotBodyWeaponId, HitZone.Body, null);
            }
        }

        private void Charge(ServerBotAgent agent, ServerBody body, TeamDeathmatchSession session, float dt)
        {
            var playerId = agent.PlayerId;
            var heading = agent.ChargeDirection;
            var spec = agent.Brain.Spec.Charger;
            if (!heading.HasValue)
            {
                return;
            }

            var direction = Vector3.Normalize(new Vector3(heading.Value.X, 0f, heading.Value.Z));
            if (TryStrike(agent, body, session, spec, direction))
            {
                return;
            }

            var step = spec.ChargeSpeed * dt;
            if (!(step > 0f))
            {
                return;
            }

            var yaw = MathF.Atan2(direction.X, direction.Z);
            var moved = _movement.DashBot(playerId, direction * step, yaw, out var blocked);
            agent.AddChargeTravel(moved);
            if (blocked || moved < step * _blockedProgressRatio)
            {
                var end = blocked ? ChargeEnd.Obstacle : ChargeEnd.Edge;
                agent.EndCharge(end);
                _log.WriteLine(ServerBotLog.ChargeEnded(playerId, end, agent.ChargeTravelled));
                return;
            }

            TryStrike(agent, body, session, spec, direction);
        }

        // 当てるのは前にいるいちばん近い敵 1 人だけ。途中の味方を巻き込むと、群れで出したときに突進のたびに同士討ちになる (オフラインと同じ)
        private bool TryStrike(ServerBotAgent agent, ServerBody self, TeamDeathmatchSession session, ChargerSpec spec, Vector3 direction)
        {
            var selfId = agent.PlayerId;
            var center = self.Center;
            ServerBody best = null;
            var bestDistance = float.PositiveInfinity;
            foreach (var other in _bodies.All)
            {
                var id = other.PlayerId;
                if (id == selfId || !session.HasPlayer(id) || !session.IsAlive(id) || session.IsDowned(id) || !_rules.AreHostile(selfId, id))
                {
                    continue;
                }

                var closest = ServerMelee.ClosestPointOnBody(other, center);
                var offset = closest - center;
                var distance = offset.Length();
                if (distance > spec.HitReach || distance >= bestDistance)
                {
                    continue;
                }

                // 近さだけで当てると、すれ違った後ろの相手まで突進の向きへ飛ばしてしまう
                var flat = new Vector3(offset.X, 0f, offset.Z);
                if (flat.LengthSquared() > 1e-6f && Vector3.Dot(Vector3.Normalize(flat), direction) < 0f)
                {
                    continue;
                }

                // 低い壁越しでも距離は届くので、間に壁があれば当てない
                if (IsWorldBlocked(center, closest))
                {
                    continue;
                }

                best = other;
                bestDistance = distance;
            }

            if (best == null)
            {
                return false;
            }

            var outcome = _combat.BotCharge(selfId, best.PlayerId, spec.ImpactDamage, center, direction);
            agent.EndCharge(ChargeEnd.Hit);
            _log.WriteLine(ServerBotLog.ChargeHit(selfId, best.PlayerId, spec.ImpactDamage, agent.ChargeTravelled, outcome));
            return true;
        }

        // 盾持ちの殴り。振りの時間は盾の振り (MeleeSwing) で進め、当たりの瞬間に近接のモジュールで相手を選んで与える
        private void Bash(ServerBotAgent agent, ServerBody body, Vector3? lookAt, bool canAct, float dt)
        {
            if (agent.ShieldBehavior == null)
            {
                return;
            }

            var facing = canAct && lookAt.HasValue && IsWithinArc(body, agent.Brain.Spec.Shield.Bash.ArcDegrees, lookAt.Value);
            var struck = agent.TickBash(dt, facing, out var started, out var spec);
            if (started)
            {
                _log.WriteLine(ServerBotLog.ShieldBash(agent.PlayerId));
            }

            if (struck && canAct && _melee != null)
            {
                _melee.BotBash(agent.PlayerId, spec);
            }
        }

        private static bool IsWithinArc(ServerBody body, float arcDegrees, Vector3 target)
        {
            var self = body.Position;
            var forward = new Vector3(MathF.Sin(body.Yaw), 0f, MathF.Cos(body.Yaw));
            return MeleeJudge.IsWithinArc(self, forward, arcDegrees, new Vector3(target.X, self.Y, target.Z));
        }

        private readonly struct FallenBomber
        {
            public FallenBomber(int playerId, int? killerId, Vector3 center, ExplosionDamage blast)
            {
                PlayerId = playerId;
                KillerId = killerId;
                Center = center;
                Blast = blast;
            }

            public int PlayerId { get; }

            public int? KillerId { get; }

            public Vector3 Center { get; }

            public ExplosionDamage Blast { get; }
        }
    }
}
