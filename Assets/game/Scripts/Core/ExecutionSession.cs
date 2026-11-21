using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class ExecutionSession : IExecutionView
    {
        public const int DefaultWinsToWin = 3;
        public const float DefaultPreparationSeconds = 3f;
        public const float DefaultRoundTimeLimitSeconds = 120f;

        // 蘇生 (ActionRules.DefaultReviveSeconds) より短くし、とどめを競り勝ちやすくする。
        public const float DefaultExecuteSeconds = 1.5f;

        public event Action<int, int?> PlayerDowned;

        public event Action<int, int> PlayerRevived;

        public event Action<int, int> PlayerExecuted;

        public event Action<int, int?> PlayerBledOut;

        private readonly RoundSession _round;

        private readonly DownedTracker _downed;

        // 倒れる人の id の昇順に回して、同じ入力なら同じ結果にする。
        private readonly SortedDictionary<int, ExecuteHold> _executeHolds = new SortedDictionary<int, ExecuteHold>();

        public ExecutionSession(int winsToWin, float preparationSeconds, float roundTimeLimitSeconds, ActionRules actions, float executeSeconds)
        {
            Actions = actions ?? throw new ArgumentNullException(nameof(actions));

            if (!(executeSeconds > 0f) || float.IsInfinity(executeSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(executeSeconds), executeSeconds, "executeSeconds must be positive and finite");
            }

            ExecuteSeconds = executeSeconds;
            if (actions.Downed == Downed.Revive)
            {
                // ダウン中は撃っても倒れないので、DownedTracker のダウン中の体力は使わない。1 以上でないと作れないので 1 を入れる。
                _downed = new DownedTracker(1, actions.DownedBleedoutSeconds, actions.ReviveSeconds);
            }

            var mode = new ModeSession(
                new TwoTeams(),
                roundTimeLimitSeconds,
                Array.Empty<IScoreRule>(),
                new NoRespawn(),
                new IWinCondition[] { new LastSideStandingWin(), new MoreStandingAtTimeUp(this) });
            _round = new RoundSession(mode, winsToWin, preparationSeconds, 0);
            _round.PreparationStarted += OnPreparationStarted;
            _round.RoundEnded += OnRoundEnded;
        }

        public RoundSession Round => _round;

        public ModeSession Mode => _round.Mode;

        public ActionRules Actions { get; }

        public float ExecuteSeconds { get; }

        public bool UsesDowned => _downed != null;

        public Side? LastRoundWinner { get; private set; }

        public bool HasFinishedRound { get; private set; }

        public bool IsLive => _round.Phase == RoundPhase.Live && Mode.State == GameModeState.Playing;

        // 同じ値は Round と Mode から読めるので、公開の名前を増やさないよう IExecutionView の口としてだけ出す
        RoundPhase IExecutionView.Phase => _round.Phase;

        int IExecutionView.RoundNumber => _round.RoundNumber;

        int IExecutionView.WinsToWin => _round.WinsToWin;

        float IExecutionView.PreparationRemaining => _round.PreparationRemaining;

        float IExecutionView.RoundRemaining => Mode.RemainingSeconds;

        Side? IExecutionView.Winner => _round.Phase == RoundPhase.Ended ? _round.Winner : null;

        int IExecutionView.GetRoundWins(Side side) => _round.GetRoundWins(side);

        bool IExecutionView.HasPlayer(int playerId) => Mode.HasPlayer(playerId);

        Side IExecutionView.GetSide(int playerId) => Mode.GetSide(playerId);

        public void Start()
        {
            _round.Start();
        }

        public void Reset()
        {
            ClearDowned();
            LastRoundWinner = null;
            HasFinishedRound = false;
            _round.Reset();
        }

        public bool IsDowned(int playerId) => _downed != null && _downed.IsDowned(playerId);

        public IReadOnlyList<int> GetDownedIds() => _downed != null ? _downed.GetDownedIds() : (IReadOnlyList<int>)Array.Empty<int>();

        public float? GetBleedoutRemaining(int playerId) => _downed?.GetBleedoutRemaining(playerId);

        public float GetReviveProgress(int playerId) => _downed != null ? _downed.GetReviveProgress(playerId) : 0f;

        public float GetExecuteProgress(int playerId) =>
            _executeHolds.TryGetValue(playerId, out var hold) ? Math.Min(1f, hold.Seconds / ExecuteSeconds) : 0f;

        public int CountRemaining(Side side)
        {
            var count = 0;
            foreach (var id in Mode.GetPlayerIds())
            {
                if (Mode.GetSide(id) == side && !Mode.IsDown(id))
                {
                    count++;
                }
            }

            return count;
        }

        public int CountDowned(Side side)
        {
            var count = 0;
            foreach (var id in GetDownedIds())
            {
                if (Mode.HasPlayer(id) && Mode.GetSide(id) == side)
                {
                    count++;
                }
            }

            return count;
        }

        public int CountStanding(Side side) => CountRemaining(side) - CountDowned(side);

        public int RevivedHealth(int maxHealth) => DownedTracker.HealthFor(maxHealth, Actions.DownedHealthFraction);

        public ExecutionHit ReportLethal(int victimId, int? attackerId)
        {
            RequirePlayer(victimId, nameof(victimId));
            if (attackerId.HasValue)
            {
                RequirePlayer(attackerId.Value, nameof(attackerId));
            }

            if (!IsLive || Mode.IsDown(victimId) || IsDowned(victimId))
            {
                return ExecutionHit.Ignored;
            }

            if (_downed == null)
            {
                ReportFall(attackerId, victimId);
                return ExecutionHit.Killed;
            }

            var byEnemy = attackerId.HasValue && attackerId.Value != victimId && Mode.GetSide(attackerId.Value) != Mode.GetSide(victimId);
            _downed.Down(victimId, attackerId ?? victimId, byEnemy);
            PlayerDowned?.Invoke(victimId, attackerId.HasValue && attackerId.Value != victimId ? attackerId : null);
            return ExecutionHit.Downed;
        }

        public ExecutionStep Interact(int actorId, int targetId, float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must not be negative and must be finite");
            }

            RequirePlayer(actorId, nameof(actorId));
            RequirePlayer(targetId, nameof(targetId));

            if (_downed == null || !IsLive || actorId == targetId || Mode.IsDown(actorId) || IsDowned(actorId) || !IsDowned(targetId))
            {
                return ExecutionStep.Rejected;
            }

            if (Mode.GetSide(actorId) == Mode.GetSide(targetId))
            {
                switch (_downed.AddReviveProgress(targetId, actorId, deltaSeconds))
                {
                    case ReviveStep.Progressed:
                        return ExecutionStep.Reviving;
                    case ReviveStep.Completed:
                        _downed.Remove(targetId);
                        _executeHolds.Remove(targetId);
                        PlayerRevived?.Invoke(actorId, targetId);
                        return ExecutionStep.Revived;
                    default:
                        return ExecutionStep.Rejected;
                }
            }

            if (_executeHolds.TryGetValue(targetId, out var hold))
            {
                if (hold.ExecutorId != actorId)
                {
                    if (hold.Touched)
                    {
                        return ExecutionStep.Rejected;
                    }

                    hold.ExecutorId = actorId;
                    hold.Seconds = 0f;
                }
            }
            else
            {
                hold = new ExecuteHold { ExecutorId = actorId };
                _executeHolds.Add(targetId, hold);
            }

            hold.Touched = true;
            hold.Seconds += deltaSeconds;
            if (hold.Seconds < ExecuteSeconds)
            {
                return ExecutionStep.Executing;
            }

            _downed.Remove(targetId);
            _executeHolds.Remove(targetId);
            _round.ReportKill(actorId, targetId);
            PlayerExecuted?.Invoke(actorId, targetId);
            return ExecutionStep.Executed;
        }

        public void Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsPositiveInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be finite and not negative");
            }

            if (IsLive)
            {
                ForgetUntouchedExecutes();
                if (_downed != null)
                {
                    BleedOut(_downed.Tick(deltaSeconds));
                }
            }

            _round.Tick(deltaSeconds);
        }

        public void RemovePlayer(int playerId)
        {
            Mode.RemovePlayer(playerId);
            _downed?.RemovePlayer(playerId);

            List<int> removed = null;
            foreach (var pair in _executeHolds)
            {
                if (pair.Key == playerId || pair.Value.ExecutorId == playerId)
                {
                    (removed ??= new List<int>()).Add(pair.Key);
                }
            }

            if (removed != null)
            {
                foreach (var targetId in removed)
                {
                    _executeHolds.Remove(targetId);
                }
            }
        }

        private void BleedOut(IReadOnlyList<int> bledOut)
        {
            foreach (var victimId in bledOut)
            {
                // 前の人のハンドラでラウンドが終わったり始め直されたりしたら、残りはもう数えない。
                if (!IsLive)
                {
                    return;
                }

                if (!_downed.IsDowned(victimId))
                {
                    continue;
                }

                var creditId = _downed.GetBleedoutCreditId(victimId);
                _downed.Remove(victimId);
                _executeHolds.Remove(victimId);
                ReportFall(creditId, victimId);
                PlayerBledOut?.Invoke(victimId, creditId);
            }
        }

        private void ReportFall(int? killerId, int victimId)
        {
            if (killerId.HasValue && killerId.Value != victimId)
            {
                _round.ReportKill(killerId.Value, victimId);
            }
            else
            {
                _round.ReportDeath(victimId);
            }
        }

        private void ForgetUntouchedExecutes()
        {
            List<int> untouched = null;
            foreach (var pair in _executeHolds)
            {
                if (pair.Value.Touched)
                {
                    pair.Value.Touched = false;
                }
                else
                {
                    (untouched ??= new List<int>()).Add(pair.Key);
                }
            }

            if (untouched != null)
            {
                foreach (var targetId in untouched)
                {
                    _executeHolds.Remove(targetId);
                }
            }
        }

        private void ClearDowned()
        {
            _downed?.Clear();
            _executeHolds.Clear();
        }

        // 体力と位置は Core の外にあるので、ここではダウンととどめの累計だけを消し、体力と位置は PreparationStarted を受けた側が戻す。
        private void OnPreparationStarted(int roundNumber)
        {
            ClearDowned();
        }

        private void OnRoundEnded(Side? winner)
        {
            LastRoundWinner = winner;
            HasFinishedRound = true;
        }

        private void RequirePlayer(int playerId, string paramName)
        {
            if (!Mode.HasPlayer(playerId))
            {
                throw new ArgumentOutOfRangeException(paramName, playerId, "no player with this id");
            }
        }

        private sealed class ExecuteHold
        {
            public int ExecutorId { get; set; }

            public float Seconds { get; set; }

            public bool Touched { get; set; }
        }

        private sealed class MoreStandingAtTimeUp : IWinCondition
        {
            private readonly ExecutionSession _owner;

            public MoreStandingAtTimeUp(ExecutionSession owner)
            {
                _owner = owner;
            }

            public double? ScoreLimit => null;

            public ModeOutcome CheckStanding(ModeSession session) => ModeOutcome.Undecided;

            public ModeOutcome CheckTimeUp(ModeSession session)
            {
                var alpha = _owner.CountStanding(Side.Alpha);
                var bravo = _owner.CountStanding(Side.Bravo);
                if (alpha == bravo)
                {
                    return ModeOutcome.Draw;
                }

                return ModeOutcome.Win(alpha > bravo ? Side.Alpha : Side.Bravo);
            }
        }
    }
}
