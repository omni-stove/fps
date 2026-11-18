using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class TeamDeathmatch : IGameMode
    {
        public event Action<GameModeState> StateChanged;

        public event Action<int> RespawnReady;

        public event Action<Team?> Ended;

        private const int _twoTeamSideCount = 2;

        private readonly ModeSession _session;

        public TeamDeathmatchSpec Spec { get; }
        public GameModeState State => _session.State;

        public float RemainingSeconds => _session.RemainingSeconds;

        public Team? Winner => Side.ToTeam(_session.Winner);

        public int PlayerCount => _session.PlayerCount;

        public TeamDeathmatch(TeamDeathmatchSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));

            // Team (Alpha・Bravo) で受け答えする API なので、3 つ以上の陣営は黙って 2 つにせず断る。
            if (spec.SideCount != _twoTeamSideCount)
            {
                throw new ArgumentException(
                    $"team deathmatch supports exactly {_twoTeamSideCount} sides, but the spec has {spec.SideCount}; use TeamDeathmatchRunner for more sides",
                    nameof(spec));
            }

            var scoreLimit = new ScoreLimitWin(spec.ScoreLimit);
            var timeLimit = new TimeLimitWin();
            var winConditions = spec.Rules.RespawnMode == RespawnMode.None && !spec.RespawnsByRound
                ? new IWinCondition[] { new LastSideStandingWin(), scoreLimit, timeLimit }
                : new IWinCondition[] { scoreLimit, timeLimit };

            // NoRespawn にすると倒れた人が ModeSession の中で倒れたままになり、次のラウンドのキルを数えなくなるので、出直しの決まりを持たせない
            var respawn = spec.RespawnsByRound ? null : RespawnPolicy.FromRules(spec.Rules, spec.RespawnSeconds);

            _session = new ModeSession(
                new TwoTeams(),
                spec.TimeLimitSeconds,
                new IScoreRule[] { new KillScore(spec.PointsPerKill, spec.PointsPerAssist) },
                respawn,
                winConditions);
            _session.StateChanged += state => StateChanged?.Invoke(state);
            _session.RespawnReady += playerId => RespawnReady?.Invoke(playerId);
            _session.Ended += winner => Ended?.Invoke(Side.ToTeam(winner));
        }

        public void Start() => _session.Start();

        public void Reset() => _session.Reset();

        public void AddPlayer(int playerId, Team team)
        {
            _session.AddPlayer(playerId, Side.FromTeam(team));
        }

        // 戻ってきた人のキルは抜ける前にもうチームの点数に入っているので、成績を引き継いでもチームの点数は動かさない。
        public void AddPlayer(int playerId, Team team, int kills, int deaths)
        {
            _session.AddPlayer(playerId, Side.FromTeam(team), kills, deaths);
        }

        // 戻ってきた人のキルとアシストは抜ける前にもうチームの点数に入っているので、成績を引き継いでもチームの点数は動かさない。
        public void AddPlayer(int playerId, Team team, int kills, int deaths, int assists)
        {
            _session.AddPlayer(playerId, Side.FromTeam(team), kills, deaths, assists);
        }

        public void RemovePlayer(int playerId) => _session.RemovePlayer(playerId);

        public bool HasPlayer(int playerId) => _session.HasPlayer(playerId);

        public Team GetTeam(int playerId) => _session.GetSide(playerId).ToTeam();

        public int GetKills(int playerId) => _session.GetKills(playerId);

        public int GetDeaths(int playerId) => _session.GetDeaths(playerId);

        public int GetAssists(int playerId) => _session.GetAssists(playerId);

        public bool IsAwaitingRespawn(int playerId) => _session.IsAwaitingRespawn(playerId);

        public bool IsEliminated(int playerId) => _session.IsEliminated(playerId);

        public float? GetRespawnRemaining(int playerId) => _session.GetRespawnRemaining(playerId);

        public float? SecondsUntilNextWave => _session.SecondsUntilNextWave;

        // 点はキル 1 回・アシスト 1 回につき整数の PointsPerKill・PointsPerAssist しか入らないので、double で持っていても整数のまま。
        public int GetScore(Team team) => (int)_session.GetScore(Side.FromTeam(team));

        public void ReportKill(int killerId, int victimId) => _session.ReportKill(killerId, victimId);

        public void ReportKill(int killerId, int victimId, IReadOnlyList<int> assisterIds) => _session.ReportKill(killerId, victimId, assisterIds);

        public void ReportDeath(int playerId) => _session.ReportDeath(playerId);

        public bool Finish(Team? winner) => _session.Finish(Side.FromTeam(winner));

        public void Tick(float deltaSeconds) => _session.Tick(deltaSeconds);
    }
}
