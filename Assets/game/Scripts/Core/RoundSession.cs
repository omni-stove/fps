using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    // ModeSession の Start と Reset は RoundSession が呼ぶので、外からは呼ばない。
    // ModeSession の ReportKill などを直接呼ぶとラウンドの終わりを次の Tick まで扱えないので、すぐに扱う
    // ReportKill・ReportDeath・AddScore を別に持つ。
    public sealed class RoundSession
    {
        // 体力と位置は Core の外にあるので、受けた側がここで戻す。
        public event Action<int> PreparationStarted;

        public event Action<int> RoundStarted;

        public event Action<Side?> RoundEnded;

        public event Action SidesSwapped;

        public event Action<Side> Ended;

        private readonly ModeSession _mode;
        private readonly Side _firstAttacker;
        private readonly Side _firstDefender;
        private readonly Dictionary<Side, int> _roundWins = new Dictionary<Side, int>();

        // イベントのハンドラで Reset されたら、その後の処理を続けないために数える。
        private int _generation;
        private bool _finishingRound;

        public ModeSession Mode => _mode;

        public RoundPhase Phase { get; private set; }

        public int WinsToWin { get; }

        public float PreparationSeconds { get; }

        public int SwapEveryRounds { get; }

        public int RoundNumber { get; private set; }

        public float PreparationRemaining { get; private set; }

        public Side Attacker { get; private set; }

        public Side Defender { get; private set; }

        public Side? Winner { get; private set; }

        public RoundSession(ModeSession mode, int winsToWin, float preparationSeconds, int swapEveryRounds)
        {
            _mode = mode ?? throw new ArgumentNullException(nameof(mode));

            if (mode.State != GameModeState.Waiting)
            {
                throw new ArgumentException("mode must not have started", nameof(mode));
            }

            var fixedSides = mode.Layout.FixedSides;
            if (fixedSides.Count != 2)
            {
                throw new ArgumentException("mode must have exactly two fixed sides", nameof(mode));
            }

            if (winsToWin < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(winsToWin), winsToWin, "winsToWin must be at least 1");
            }

            if (!(preparationSeconds >= 0f) || float.IsInfinity(preparationSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(preparationSeconds), preparationSeconds, "preparationSeconds must not be negative and must be finite");
            }

            if (swapEveryRounds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(swapEveryRounds), swapEveryRounds, "swapEveryRounds must not be negative");
            }

            WinsToWin = winsToWin;
            PreparationSeconds = preparationSeconds;
            SwapEveryRounds = swapEveryRounds;
            _firstAttacker = fixedSides[0];
            _firstDefender = fixedSides[1];
            Attacker = _firstAttacker;
            Defender = _firstDefender;
            _roundWins.Add(_firstAttacker, 0);
            _roundWins.Add(_firstDefender, 0);
            Phase = RoundPhase.Waiting;
        }

        public int GetRoundWins(Side side)
        {
            if (!_roundWins.TryGetValue(side, out var wins))
            {
                throw new ArgumentOutOfRangeException(nameof(side), side, "side is not one of the two sides of this match");
            }

            return wins;
        }

        public void Start()
        {
            if (Phase != RoundPhase.Waiting)
            {
                throw new InvalidOperationException("match has already started");
            }

            if (_mode.State != GameModeState.Waiting)
            {
                throw new InvalidOperationException("the round's ModeSession was started from outside the RoundSession");
            }

            BeginPreparation();
        }

        public void Reset()
        {
            _generation++;
            _finishingRound = false;
            _roundWins[_firstAttacker] = 0;
            _roundWins[_firstDefender] = 0;
            Attacker = _firstAttacker;
            Defender = _firstDefender;
            RoundNumber = 0;
            PreparationRemaining = 0f;
            Winner = null;
            Phase = RoundPhase.Waiting;
            _mode.Reset();
        }

        public void Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsPositiveInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be finite and not negative");
            }

            if (Phase == RoundPhase.Preparing)
            {
                if (deltaSeconds < PreparationRemaining)
                {
                    PreparationRemaining -= deltaSeconds;
                    return;
                }

                deltaSeconds -= PreparationRemaining;
                var generation = _generation;
                BeginLive();
                if (generation != _generation || Phase != RoundPhase.Live)
                {
                    return;
                }
            }

            if (Phase != RoundPhase.Live)
            {
                return;
            }

            _mode.Tick(deltaSeconds);
            FinishRoundIfEnded();
        }

        public void ReportKill(int killerId, int victimId)
        {
            _mode.ReportKill(killerId, victimId);
            FinishRoundIfEnded();
        }

        public void ReportDeath(int playerId)
        {
            _mode.ReportDeath(playerId);
            FinishRoundIfEnded();
        }

        public void AddScore(Side side, double points)
        {
            _mode.AddScore(side, points);
            FinishRoundIfEnded();
        }

        private void BeginPreparation()
        {
            var generation = _generation;
            RoundNumber++;
            Phase = RoundPhase.Preparing;
            PreparationRemaining = PreparationSeconds;
            _mode.Reset();
            PreparationStarted?.Invoke(RoundNumber);
            if (generation != _generation || Phase != RoundPhase.Preparing)
            {
                return;
            }

            if (PreparationSeconds == 0f)
            {
                BeginLive();
            }
        }

        private void BeginLive()
        {
            if (_mode.State != GameModeState.Waiting)
            {
                throw new InvalidOperationException("the round's ModeSession was started from outside the RoundSession");
            }

            PreparationRemaining = 0f;
            Phase = RoundPhase.Live;
            _mode.Start();
            RoundStarted?.Invoke(RoundNumber);
        }

        private void FinishRoundIfEnded()
        {
            // RoundEnded などのハンドラから報告されても、同じラウンドの終わりを 2 度扱わない。
            if (Phase != RoundPhase.Live || _finishingRound || _mode.State != GameModeState.Ended)
            {
                return;
            }

            var winner = _mode.Winner;
            if (winner.HasValue)
            {
                if (!_roundWins.TryGetValue(winner.Value, out var wins))
                {
                    throw new InvalidOperationException($"the round was won by {winner.Value}, which is not one of the two sides of this match");
                }

                wins++;
                _roundWins[winner.Value] = wins;
                if (wins >= WinsToWin)
                {
                    Winner = winner;
                    Phase = RoundPhase.Ended;
                }
            }

            var generation = _generation;
            _finishingRound = true;
            RoundEnded?.Invoke(winner);
            if (generation != _generation)
            {
                return;
            }

            if (Phase == RoundPhase.Ended)
            {
                Ended?.Invoke(Winner.Value);
                return;
            }

            if (SwapEveryRounds > 0 && RoundNumber % SwapEveryRounds == 0)
            {
                var attacker = Attacker;
                Attacker = Defender;
                Defender = attacker;
                SidesSwapped?.Invoke();
                if (generation != _generation)
                {
                    return;
                }
            }

            _finishingRound = false;
            BeginPreparation();
        }
    }
}
