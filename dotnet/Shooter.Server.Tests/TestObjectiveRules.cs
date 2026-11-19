using System;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    // キル数で決める目的 (TeamKillsRules) と同じ動きに、敵味方・開ける・Bot の行き先だけを差し替えられるようにした目的。
    // 差し替えないものはインターフェイスの既定を通すので、既定の答えも確かめられる
    internal sealed class TestObjectiveRules : IMatchObjectiveRules
    {
        // インターフェイスの既定の実装を呼ぶための、何も差し替えない目的
        private static readonly IMatchObjectiveRules _default = new TeamKillsRules();

        public Func<int, Team, int, Team, bool> Hostile { get; set; }

        public Func<int, Team, int, Team, bool> Allies { get; set; }

        public Func<int, int, InteractVerdict> OnInteract { get; set; }

        public Func<int, Vector3?> Goal { get; set; }

        public ObjectiveRally? Rally { get; set; }

        public MatchObjective Objective => MatchObjective.None;

        public bool PlaysInRounds => false;

        public bool KeepsMatchScore => false;

        public bool HoldsFire => false;

        public float? RemainingSeconds => null;

        ObjectiveRally? IMatchObjectiveRules.BotRally => Rally;

        bool IMatchObjectiveRules.AreHostile(int a, Team aTeam, int b, Team bTeam)
        {
            return Hostile != null ? Hostile(a, aTeam, b, bTeam) : _default.AreHostile(a, aTeam, b, bTeam);
        }

        bool IMatchObjectiveRules.AreAllies(int a, Team aTeam, int b, Team bTeam)
        {
            return Allies != null ? Allies(a, aTeam, b, bTeam) : !((IMatchObjectiveRules)this).AreHostile(a, aTeam, b, bTeam);
        }

        InteractVerdict IMatchObjectiveRules.Interact(int playerId, int itemId)
        {
            return OnInteract != null ? OnInteract(playerId, itemId) : _default.Interact(playerId, itemId);
        }

        Vector3? IMatchObjectiveRules.BotGoal(int playerId)
        {
            return Goal != null ? Goal(playerId) : _default.BotGoal(playerId);
        }

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
        }

        public void FillControlPoints(SnapshotList<ControlPointSnapshot> points)
        {
            points.Clear();
        }

        public void FillObjectiveItems(SnapshotList<ObjectiveItemSnapshot> items)
        {
            items.Clear();
        }

        public void FillPlayer(int playerId, ref PlayerSnapshot snapshot)
        {
        }
    }
}
