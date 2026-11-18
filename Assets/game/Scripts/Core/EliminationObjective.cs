using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    // RoundSession の ModeSession に体力やキルを持たせず、試合の側 (TeamDeathmatchSession) で倒れたことを写すだけにするのは、
    // ダメージ・ダウン・成績を 2 か所で数えないため
    public sealed class EliminationObjective
    {
        public event Action<int> PreparationStarted;

        public event Action<int> RoundStarted;

        public event Action<int, Team?> RoundEnded;

        private readonly Dictionary<int, Team> _teams = new Dictionary<int, Team>();
        private readonly Func<Team, int> _standingAtTimeUp;

        public RoundSession Rounds { get; }

        public ModeSession Mode => Rounds.Mode;

        public RoundPhase Phase => Rounds.Phase;

        public int PlayerCount => _teams.Count;

        public EliminationObjective(int winsToWin, float preparationSeconds, float roundTimeLimitSeconds, Func<Team, int> standingAtTimeUp = null)
        {
            if (!(roundTimeLimitSeconds > 0f) || float.IsInfinity(roundTimeLimitSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(roundTimeLimitSeconds), roundTimeLimitSeconds, "roundTimeLimitSeconds must be positive and finite");
            }

            _standingAtTimeUp = standingAtTimeUp;
            var mode = new ModeSession(
                new TwoTeams(),
                roundTimeLimitSeconds,
                Array.Empty<IScoreRule>(),
                new NoRespawn(),
                new IWinCondition[] { new LastSideStandingWin(), new MoreStandingAtTimeUp(this) });
            Rounds = new RoundSession(mode, winsToWin, preparationSeconds, 0);
            Rounds.PreparationStarted += round => PreparationStarted?.Invoke(round);
            Rounds.RoundStarted += round => RoundStarted?.Invoke(round);
            Rounds.RoundEnded += winner => RoundEnded?.Invoke(Rounds.RoundNumber, Side.ToTeam(winner));
        }

        public bool HasPlayer(int playerId) => _teams.ContainsKey(playerId);

        public bool IsPreparing => Rounds.Phase == RoundPhase.Preparing;

        public float RemainingSeconds => Rounds.Phase == RoundPhase.Preparing ? Rounds.PreparationRemaining : Mode.RemainingSeconds;

        public int GetRoundWins(Team team) => Rounds.GetRoundWins(Side.FromTeam(team));

        // ラウンドの途中で入った人を倒れた扱いにしないのは、試合の側でも生きたまま出現して撃ち合えるため
        public void SetPlayer(int playerId, Team team)
        {
            Side.FromTeam(team);
            if (_teams.TryGetValue(playerId, out var current))
            {
                if (current == team)
                {
                    return;
                }

                Mode.RemovePlayer(playerId);
                _teams.Remove(playerId);
            }

            Mode.AddPlayer(playerId, Side.FromTeam(team));
            _teams[playerId] = team;
        }

        public bool RemovePlayer(int playerId)
        {
            if (!_teams.Remove(playerId))
            {
                return false;
            }

            Mode.RemovePlayer(playerId);
            return true;
        }

        public void ReportFall(int? killerId, int victimId)
        {
            if (!_teams.TryGetValue(victimId, out var victimTeam) || Rounds.Phase != RoundPhase.Live)
            {
                return;
            }

            if (killerId.HasValue && killerId.Value != victimId
                && _teams.TryGetValue(killerId.Value, out var killerTeam) && killerTeam != victimTeam)
            {
                Rounds.ReportKill(killerId.Value, victimId);
            }
            else
            {
                Rounds.ReportDeath(victimId);
            }
        }

        public bool Tick(bool matchPlaying, float deltaSeconds)
        {
            if (!matchPlaying)
            {
                return false;
            }

            if (Rounds.Phase == RoundPhase.Waiting)
            {
                Rounds.Start();
            }

            if (Rounds.Phase != RoundPhase.Ended)
            {
                Rounds.Tick(deltaSeconds);
            }

            return Rounds.Phase == RoundPhase.Ended;
        }

        public Team? DecideWinner()
        {
            if (Rounds.Phase == RoundPhase.Ended)
            {
                return Side.ToTeam(Rounds.Winner);
            }

            return DominationObjective.Leader(GetRoundWins(Team.Alpha), GetRoundWins(Team.Bravo));
        }

        public void Reset()
        {
            Rounds.Reset();
        }

        private int CountStanding(Team team)
        {
            if (_standingAtTimeUp != null)
            {
                return _standingAtTimeUp(team);
            }

            var side = Side.FromTeam(team);
            var count = 0;
            foreach (var playerId in Mode.GetPlayerIds())
            {
                if (Mode.GetSide(playerId) == side && !Mode.IsDown(playerId))
                {
                    count++;
                }
            }

            return count;
        }

        private sealed class MoreStandingAtTimeUp : IWinCondition
        {
            private readonly EliminationObjective _owner;

            public MoreStandingAtTimeUp(EliminationObjective owner)
            {
                _owner = owner;
            }

            public double? ScoreLimit => null;

            public ModeOutcome CheckStanding(ModeSession session) => ModeOutcome.Undecided;

            public ModeOutcome CheckTimeUp(ModeSession session)
            {
                var alpha = _owner.CountStanding(Team.Alpha);
                var bravo = _owner.CountStanding(Team.Bravo);
                if (alpha == bravo)
                {
                    return ModeOutcome.Draw;
                }

                return ModeOutcome.Win(alpha > bravo ? Side.Alpha : Side.Bravo);
            }
        }
    }
}
