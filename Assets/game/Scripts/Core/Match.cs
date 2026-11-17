using System;

namespace Shooter.Core
{
    public sealed class Match : IGameMode
    {
        public event Action<GameModeState> StateChanged;

        public event Action<float> RemainingSecondsChanged;

        public float TimeLimitSeconds { get; }

        public bool HasTimeLimit => !float.IsPositiveInfinity(TimeLimitSeconds);

        public int KillsToWin { get; }

        public int PlayerId { get; }

        public GameModeState State { get; private set; } = GameModeState.Waiting;

        public MatchOutcome? Result { get; private set; }

        public float RemainingSeconds { get; private set; }

        public int Kills { get; private set; }

        public bool IsOver => State == GameModeState.Ended;

        public Match(float timeLimitSeconds, int killsToWin, int playerId)
        {
            if (!(timeLimitSeconds > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(timeLimitSeconds), timeLimitSeconds, "timeLimitSeconds must be positive");
            }

            if (killsToWin <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(killsToWin), killsToWin, "killsToWin must be positive");
            }

            TimeLimitSeconds = timeLimitSeconds;
            KillsToWin = killsToWin;
            PlayerId = playerId;
            RemainingSeconds = timeLimitSeconds;
        }

        public void Start()
        {
            if (State != GameModeState.Waiting)
            {
                return;
            }

            State = GameModeState.Playing;
            StateChanged?.Invoke(State);
        }

        public void Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must not be negative");
            }

            if (State != GameModeState.Playing || !HasTimeLimit)
            {
                return;
            }

            var previous = RemainingSeconds;
            RemainingSeconds = Math.Max(0f, RemainingSeconds - deltaSeconds);
            if (RemainingSeconds != previous)
            {
                RemainingSecondsChanged?.Invoke(RemainingSeconds);
            }

            if (RemainingSeconds <= 0f)
            {
                End(MatchOutcome.Lost);
            }
        }

        public void ReportKill(int killerId, int victimId)
        {
            if (State != GameModeState.Playing)
            {
                return;
            }

            if (victimId == PlayerId)
            {
                End(MatchOutcome.Lost);
                return;
            }

            if (killerId != PlayerId)
            {
                return;
            }

            Kills++;
            if (Kills >= KillsToWin)
            {
                End(MatchOutcome.Won);
            }
        }

        public void ReportDeath(int playerId)
        {
            if (State != GameModeState.Playing || playerId != PlayerId)
            {
                return;
            }

            End(MatchOutcome.Lost);
        }

        private void End(MatchOutcome result)
        {
            Result = result;
            State = GameModeState.Ended;
            StateChanged?.Invoke(State);
        }
    }
}
