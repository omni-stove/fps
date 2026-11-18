using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // GameRules.Objective が None のとき。勝ち負けはキル数の上限と制限時間で Session が決める
    internal sealed class TeamKillsRules : IMatchObjectiveRules
    {
        public MatchObjective Objective => MatchObjective.None;

        public bool PlaysInRounds => false;

        public bool KeepsMatchScore => false;

        public bool HoldsFire => false;

        public float? RemainingSeconds => null;

        public TeamDeathmatchSpec CreateSessionSpec(GameRules rules, NetMatchSettings settings)
        {
            return new TeamDeathmatchSpec(settings.ScoreLimit, settings.TimeLimitSeconds, settings.RespawnSeconds, rules);
        }

        public void CheckMap(ServerMapData map, NetMapPoints points)
        {
        }

        public void UseMap(ServerMapData map, NetMapPoints points)
        {
        }

        public void Start()
        {
        }

        public void Stop()
        {
        }

        public bool Tick(bool matchPlaying, float deltaSeconds) => false;

        public void PlayerJoined(int playerId)
        {
        }

        public void PlayerLeft(int playerId)
        {
        }

        public void PlayerKilled(int killerId, int victimId)
        {
        }

        public void MatchRestarted()
        {
        }

        public void MatchEnded()
        {
        }

        public RoundPhase? CurrentRoundPhase() => null;

        public Team? DecideWinner(Team? killWinner) => killWinner;

        public void FillMatch(ref MatchSnapshot snapshot)
        {
            snapshot.Objective = Objective;
            snapshot.AlphaPoints = 0;
            snapshot.BravoPoints = 0;
        }

        public void FillControlPoints(SnapshotList<ControlPointSnapshot> points)
        {
            points.Clear();
        }

        public void FillObjectiveItems(SnapshotList<ObjectiveItemSnapshot> items)
        {
            items.Clear();
        }
    }
}
