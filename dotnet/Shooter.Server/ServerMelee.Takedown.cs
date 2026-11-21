using System;
using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;

namespace Shooter.Server
{
    // 背後から気づいていない相手を倒す技を、オフラインの ContextMeleeAction と同じ ContextMeleeSelector・ContextMeleeRun で決める。
    // 位置合わせ (ContextMeleeMove.StandDistance) で振る者を動かさないのは、持ち主が予測している移動をサーバーだけで動かすと、
    // 持ち主の画面と食い違って引き戻しになるため。代わりに、とどめの瞬間にも届くかを確かめ直す
    internal sealed partial class ServerMelee
    {
        private readonly ContextMeleeSelector _selector = new ContextMeleeSelector();
        private readonly Dictionary<int, Takedown> _takedowns = new Dictionary<int, Takedown>();
        private readonly List<int> _takedownIds = new List<int>();
        private MeleeSpec _takedownSource;
        private MeleeSpec _takedownSearch;
        private ContextMeleeMove _objectiveMove;
        private double _nextNoMoveLogTime = double.NegativeInfinity;

        // 相手が気づいていないかを Id で答える口。答えの無い相手 (人) は、オフラインと同じく常にこちらを見ている前提にする。
        // Bot の判断は ServerBots が持ち、ServerMelee は ServerBots に頼らず作るので、作った後に差し込む (UnawareBots)
        internal Func<int, bool> IsTargetUnaware { get; set; }

        // 投げて持ち物から武器を外した後に呼ぶ。持ち物の武器の写しは ServerInventories が持ち、ServerMelee はそれを知らないので、
        // 作った後に差し込む (ServerInventories.HoldWeapons)
        internal Action<int> WeaponsDropped { get; set; }

        internal bool IsPerformingTakedown(int playerId)
        {
            return _takedowns.ContainsKey(playerId);
        }

        internal bool TryGetTakedownTarget(int attackerId, out int targetId)
        {
            if (_takedowns.TryGetValue(attackerId, out var takedown))
            {
                targetId = takedown.TargetId;
                return true;
            }

            targetId = -1;
            return false;
        }

        // Bot の動きを止める側 (ServerBots) が、技を受けている相手を引けるようにする
        internal bool IsTakedownTarget(int playerId)
        {
            foreach (var takedown in _takedowns.Values)
            {
                if (takedown.TargetId == playerId)
                {
                    return true;
                }
            }

            return false;
        }

        // サーバーには発見ゲージ (IMeleeWitness) が無いので、オフラインの ContextMeleeAction が部品の無い敵を扱うのと同じく、判断だけで決める
        internal static Func<int, bool> UnawareBots(ServerBots bots)
        {
            ArgumentNullException.ThrowIfNull(bots);
            return playerId => bots.TryGetAgent(playerId, out var agent) && ContextMeleeSelector.IsUnaware(agent.Brain.State, true);
        }

        // 振っている最中や技の最中に近接の値を替えても、始めた技はそのときの値のまま終える (ContextMeleeRun が技を持つ)
        private void SyncTakedown()
        {
            if (ReferenceEquals(_spec, _takedownSource))
            {
                return;
            }

            _selector.Register(ContextMeleeMove.Takedown(_spec));
            _takedownSource = _spec;
            UpdateTakedownSearch();
        }

        // 目的のとどめの技 (Execution) は、目的がルールの替わり目で作り直されるので、技を試すたびに今の目的のものに合わせる
        private void SyncObjectiveMove()
        {
            var move = _combat.Rules.Objective?.DownedFinisher;
            if (ReferenceEquals(move, _objectiveMove))
            {
                return;
            }

            if (_objectiveMove != null)
            {
                _selector.Unregister(_objectiveMove.Id);
            }

            if (move != null)
            {
                _selector.Register(move);
            }

            _objectiveMove = move;
            UpdateTakedownSearch();
        }

        // 相手はふつうの近接と同じ扇で探し、届く距離だけを登録した技のいちばん遠いものにする (ContextMeleeAction.SearchSpec と同じ)
        private void UpdateTakedownSearch()
        {
            _takedownSearch = new MeleeSpec(
                _spec.Damage, _selector.MaxReach(), _spec.ArcDegrees, _spec.SwingSeconds, _spec.CooldownSeconds, _spec.ImpactSeconds, _spec.LungeDistance);
        }

        // 振れない理由は、続けて試すふつうの近接の側でログに出すので、ここでは黙って false を返す (ContextMeleeAction.TryBegin と同じ)
        private bool TryTakedown(int attackerId)
        {
            if (Check(attackerId) != MeleeRefusal.None || !_context.Bodies.TryGet(attackerId, out var attacker))
            {
                return false;
            }

            SyncObjectiveMove();

            var origin = attacker.Center;
            var targetId = FindTarget(attackerId, origin, Forward(attacker.Yaw), _takedownSearch, out var point);
            if (targetId < 0 || !_combat.CanAfflict(attackerId, targetId) || IsTakedownTarget(targetId)
                || !_context.Bodies.TryGet(targetId, out var target))
            {
                return false;
            }

            var conditions = Observe(attacker, target);
            var move = _selector.Select(new ContextMeleeSituation(conditions, Vector3.Distance(origin, point)));
            if (move == null)
            {
                // テイクダウンのボタンの連打でログを埋められないよう、拒んだ警告と同じく間引く
                if (_now >= _nextNoMoveLogTime)
                {
                    _nextNoMoveLogTime = _now + _refusalLogInterval;
                    _log.WriteLine(ContextMeleeLog.NoMove(Text(attackerId), Text(targetId), conditions));
                }

                return false;
            }

            var run = new ContextMeleeRun();
            run.Begin(move);
            _takedowns.Add(attackerId, new Takedown(targetId, run, Health(attackerId)));
            _log.WriteLine(ContextMeleeLog.Started(Text(attackerId), Text(targetId), move.Id));
            _combat.InterruptReload(attackerId, ReloadInterrupt.Melee);
            return true;
        }

        // 走っているかはサーバーが持たないので SelfSprinting は付けない。今の技 (テイクダウン) はそれを見ない
        internal ContextMeleeConditions Observe(ServerBody attacker, ServerBody target)
        {
            ArgumentNullException.ThrowIfNull(attacker);
            ArgumentNullException.ThrowIfNull(target);

            var conditions = ContextMeleeConditions.None;
            var session = _combat.Rules.Session;
            if (target.IsDowned || (session != null && session.HasPlayer(target.PlayerId) && session.IsDowned(target.PlayerId)))
            {
                conditions |= ContextMeleeConditions.TargetDowned;
            }

            if (ContextMeleeSelector.IsBehind(target.Position, Forward(target.Yaw), attacker.Position, _spec.BehindArcDegrees))
            {
                conditions |= ContextMeleeConditions.TargetBehind;
            }

            var unaware = IsTargetUnaware;
            if (unaware != null && unaware(target.PlayerId))
            {
                conditions |= ContextMeleeConditions.TargetUnaware;
            }

            if (_movement.TryGetState(attacker.PlayerId, out var motion))
            {
                if (motion.IsGrounded)
                {
                    conditions |= ContextMeleeConditions.SelfGrounded;
                }

                if (motion.Stance == PlayerStance.Crouching)
                {
                    conditions |= ContextMeleeConditions.SelfCrouching;
                }
            }

            return conditions;
        }

        private void TickTakedowns(float deltaSeconds)
        {
            if (_takedowns.Count == 0)
            {
                return;
            }

            // とどめのダメージで倒れた知らせから技が止まって一覧が変わっても回し続けられるよう、写しを Id の順に回す
            _takedownIds.Clear();
            _takedownIds.AddRange(_takedowns.Keys);
            _takedownIds.Sort();
            foreach (var attackerId in _takedownIds)
            {
                if (!_takedowns.TryGetValue(attackerId, out var takedown))
                {
                    continue;
                }

                if (!IsStanding(attackerId))
                {
                    StopTakedown(attackerId, ContextMeleeInterruption.AttackerDown);
                    continue;
                }

                // ダウン中の相手への技は、起こされた相手を倒さないよう、起き上がった時点で止める (ExecutionSession が蘇生でとどめの累計を捨てるのと同じ)
                if (!_combat.IsHittable(takedown.TargetId) || !_context.Bodies.TryGet(takedown.TargetId, out _)
                    || (NeedsDowned(takedown.Run.Move) && !IsDowned(takedown.TargetId)))
                {
                    StopTakedown(attackerId, ContextMeleeInterruption.TargetLost);
                    continue;
                }

                // サーバーは被弾の知らせを技ごとに受けないので、体力が減ったかで撃たれたとみなす (ContextMeleeAction.OnSelfDamaged)
                var health = Health(attackerId);
                if (health < takedown.AttackerHealth && takedown.Run.Move.Interruptible)
                {
                    StopTakedown(attackerId, ContextMeleeInterruption.Shot);
                    continue;
                }

                takedown.AttackerHealth = health;
                var move = takedown.Run.Move;
                if ((takedown.Run.Tick(deltaSeconds) & ContextMeleeEvents.Resolved) != 0)
                {
                    _takedowns.Remove(attackerId);
                    Resolve(attackerId, takedown.TargetId, move);
                }
            }
        }

        private void Resolve(int attackerId, int targetId, ContextMeleeMove move)
        {
            if (!_context.Bodies.TryGet(attackerId, out var attacker) || !_context.Bodies.TryGet(targetId, out var target)
                || !IsWithinReach(attacker, target, move.Reach))
            {
                _log.WriteLine(ContextMeleeLog.Interrupted(Text(attackerId), Text(targetId), move.Id, ContextMeleeInterruption.TargetLost));
                return;
            }

            var damage = move.Outcome == ContextMeleeOutcome.Kill ? Health(targetId) : move.Damage;
            var outcome = _combat.DealDamage(
                attackerId, targetId, damage, StrikeWeaponId(attacker), HitZone.Body, attacker.Position, finishesDowned: NeedsDowned(move));
            _log.WriteLine(ContextMeleeLog.Resolved(Text(attackerId), Text(targetId), move.Id, move.Outcome, damage, outcome == DamageOutcome.Killed));
        }

        // 相手が技の間に歩いて離れても倒せてしまわないよう、探したときと同じく中心から体の表面までと壁越しでないかを見る
        private bool IsWithinReach(ServerBody attacker, ServerBody target, float reach)
        {
            var origin = attacker.Center;
            var closest = ClosestPointOnBody(target, origin);
            return Vector3.Distance(origin, closest) <= reach && !IsBlocked(origin, closest);
        }

        private void StopTakedown(int attackerId, ContextMeleeInterruption reason)
        {
            if (!_takedowns.TryGetValue(attackerId, out var takedown))
            {
                return;
            }

            _takedowns.Remove(attackerId);
            _log.WriteLine(ContextMeleeLog.Interrupted(Text(attackerId), Text(takedown.TargetId), takedown.Run.Move.Id, reason));
        }

        // とどめのキルで試合が終わって始まり直すと TickTakedowns の途中で呼ばれるので、その写し (_takedownIds) とは別の写しを回す
        private void CancelAllTakedowns()
        {
            if (_takedowns.Count == 0)
            {
                return;
            }

            var attackerIds = new List<int>(_takedowns.Keys);
            attackerIds.Sort();
            foreach (var attackerId in attackerIds)
            {
                StopTakedown(attackerId, ContextMeleeInterruption.Cancelled);
            }
        }

        private static bool NeedsDowned(ContextMeleeMove move)
        {
            return (move.Required & ContextMeleeConditions.TargetDowned) != 0;
        }

        private bool IsDowned(int playerId)
        {
            var session = _combat.Rules.Session;
            return session != null && session.HasPlayer(playerId) && session.IsDowned(playerId);
        }

        private int Health(int playerId)
        {
            var session = _combat.Rules.Session;
            return session != null && session.HasPlayer(playerId) ? session.GetHealth(playerId) : 0;
        }

        private static Vector3 Forward(float yaw)
        {
            return new Vector3(MathF.Sin(yaw), 0f, MathF.Cos(yaw));
        }

        private sealed class Takedown
        {
            public Takedown(int targetId, ContextMeleeRun run, int attackerHealth)
            {
                TargetId = targetId;
                Run = run;
                AttackerHealth = attackerHealth;
            }

            public int TargetId { get; }

            public ContextMeleeRun Run { get; }

            public int AttackerHealth { get; set; }
        }

        private sealed partial class Swinger
        {
            public bool TakedownHeld { get; set; }
        }
    }
}
