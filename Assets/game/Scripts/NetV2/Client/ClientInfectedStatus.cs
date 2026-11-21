using System;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    // Detail に専用の型を作らず Core の InfectedStatus を置くのは、オフラインの InfectedRunner と同じ型にして、UI が InfectedStatus に登録した書式役をそのまま使えるようにするため。
    // サーバー (InfectedRules) は生存者と感染者の人数を AlphaPoints・BravoPoints に、感染が始まるまでの秒を ObjectiveRemainingSeconds に載せ、
    // 始まった後は 0 を送る。各行のチームは陣営 (生存者 Alpha・感染者 Bravo) に書き直して届く
    public sealed class ClientInfectedStatus
    {
        private readonly int _localPlayerId;
        private Shown _shown;

        public ClientInfectedStatus(int localPlayerId)
        {
            _localPlayerId = localPlayerId;
        }

        // Infected の試合のスナップショットが届いていなければ null
        public ModeStatus Status { get; private set; }

        public InfectedStatus Infected => Status?.Detail as InfectedStatus;

        // Status を作り直したら true。届いた値が前と同じなら、UI が持っている Status を差し替えない
        public bool Apply(SnapshotMessage snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (!TryRead(snapshot, out var shown))
            {
                return Clear();
            }

            if (Status != null && shown.Equals(_shown))
            {
                return false;
            }

            _shown = shown;
            var hasBrokenOut = shown.SecondsUntilOutbreak == 0;
            bool? localInfected = null;
            if (shown.OwnTeam.HasValue)
            {
                localInfected = Side.FromTeam(shown.OwnTeam.Value) == Core.Infected.InfectedSide;
            }

            var detail = new InfectedStatus(hasBrokenOut, shown.SecondsUntilOutbreak, shown.Survivors, shown.InfectedCount, localInfected);

            // ModeStatus は出した後も UI が持つので、得点の並びは使い回さずに毎回作る
            var sideScores = new[]
            {
                new SideScore(Core.Infected.SurvivorSide, shown.SurvivorScore),
                new SideScore(Core.Infected.InfectedSide, shown.InfectedScore),
            };
            Status = new ModeStatus(
                shown.RemainingSeconds,
                sideScores,
                shown.OwnTeam.HasValue ? Side.FromTeam(shown.OwnTeam.Value) : (Side?)null,
                null,
                shown.IsEnded,
                Side.FromTeam(shown.Winner),
                detail);
            return true;
        }

        // 試合を離れたときや、Infected でない試合に移ったときに呼ぶ。消したら true
        public bool Clear()
        {
            if (Status == null)
            {
                return false;
            }

            Status = null;
            _shown = default;
            return true;
        }

        private bool TryRead(SnapshotMessage snapshot, out Shown shown)
        {
            shown = default;
            var match = snapshot.Match;
            if (match.Objective != MatchObjective.Infected)
            {
                return false;
            }

            Team? ownTeam = null;
            var players = snapshot.Players;
            for (var i = 0; i < players.Count; i++)
            {
                ref var row = ref players[i];
                if (row.PlayerId == _localPlayerId)
                {
                    ownTeam = row.Team;
                    break;
                }
            }

            var ended = match.State == GameModeState.Ended;
            shown = new Shown(
                match.AlphaPoints,
                match.BravoPoints,
                match.ObjectiveRemainingSeconds,
                match.AlphaScore,
                match.BravoScore,
                match.RemainingSeconds,
                ownTeam,
                ended,
                ended && match.HasWinner ? match.Winner : (Team?)null);
            return true;
        }

        private readonly struct Shown : IEquatable<Shown>
        {
            public Shown(
                int survivors,
                int infectedCount,
                int secondsUntilOutbreak,
                int survivorScore,
                int infectedScore,
                int remainingSeconds,
                Team? ownTeam,
                bool isEnded,
                Team? winner)
            {
                Survivors = survivors;
                InfectedCount = infectedCount;
                SecondsUntilOutbreak = secondsUntilOutbreak;
                SurvivorScore = survivorScore;
                InfectedScore = infectedScore;
                RemainingSeconds = remainingSeconds;
                OwnTeam = ownTeam;
                IsEnded = isEnded;
                Winner = winner;
            }

            public int Survivors { get; }

            public int InfectedCount { get; }

            public int SecondsUntilOutbreak { get; }

            public int SurvivorScore { get; }

            public int InfectedScore { get; }

            public int RemainingSeconds { get; }

            public Team? OwnTeam { get; }

            public bool IsEnded { get; }

            public Team? Winner { get; }

            public bool Equals(Shown other)
            {
                return Survivors == other.Survivors
                    && InfectedCount == other.InfectedCount
                    && SecondsUntilOutbreak == other.SecondsUntilOutbreak
                    && SurvivorScore == other.SurvivorScore
                    && InfectedScore == other.InfectedScore
                    && RemainingSeconds == other.RemainingSeconds
                    && OwnTeam == other.OwnTeam
                    && IsEnded == other.IsEnded
                    && Winner == other.Winner;
            }

            public override bool Equals(object obj) => obj is Shown other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(Survivors, InfectedCount, SecondsUntilOutbreak, SurvivorScore, InfectedScore, RemainingSeconds);
        }
    }
}
