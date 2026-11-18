using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    // EOS の Stats の集計の種類 (SUM・MAX・MIN・LATEST) と同じものだけを持つ。
    public enum LeaderboardAggregation
    {
        Sum,
        Max,
        Min,
        Latest,
    }

    public sealed class LeaderboardSpec
    {
        public const int DefaultPageSize = 50;

        // EOS の QueryLeaderboardRanks が 1 回で返せる行の上限。
        public const int MaxPageSize = 1000;

        public const float DefaultRefreshSeconds = 60f;

        public static IReadOnlyList<LeaderboardDefinition> DefaultBoards { get; } = new[]
        {
            new LeaderboardDefinition("kills", "leaderboard.kills", PlayerStat.Kills, LeaderboardAggregation.Sum),
            new LeaderboardDefinition("wins", "leaderboard.wins", PlayerStat.MatchesWon, LeaderboardAggregation.Sum),
        };

        public static LeaderboardSpec Default { get; } = new LeaderboardSpec();

        public LeaderboardSpec(IReadOnlyList<LeaderboardDefinition> boards = null, int pageSize = DefaultPageSize, float refreshSeconds = DefaultRefreshSeconds)
        {
            Boards = boards == null ? DefaultBoards : SpecLists.UniqueById(boards, b => b.Id, nameof(boards));
            if (pageSize < 1 || pageSize > MaxPageSize)
            {
                throw new ArgumentOutOfRangeException(nameof(pageSize), pageSize, nameof(pageSize) + " must be between 1 and " + MaxPageSize);
            }

            PageSize = pageSize;
            RefreshSeconds = SpecGuard.PositiveFinite(refreshSeconds, nameof(refreshSeconds));
        }

        public IReadOnlyList<LeaderboardDefinition> Boards { get; }

        public int PageSize { get; }

        public float RefreshSeconds { get; }

        public LeaderboardDefinition Find(string id)
        {
            return SpecLists.FindById(Boards, b => b.Id, id);
        }
    }

    public sealed class LeaderboardDefinition
    {
        public const LeaderboardAggregation DefaultAggregation = LeaderboardAggregation.Sum;

        public LeaderboardDefinition(string id, string nameKey, PlayerStat stat, LeaderboardAggregation aggregation = DefaultAggregation, string statName = null)
        {
            Id = SpecLists.Id(id, nameof(id));
            DisplayNameKey = SpecGuard.NameKey(nameKey);
            Stat = SpecGuard.Defined(stat, nameof(stat));
            Aggregation = SpecGuard.Defined(aggregation, nameof(aggregation));

            // EOS の統計の名前はポータルで決めるので書けるようにし、書かなければ数える値の名前を使う。
            StatName = string.IsNullOrWhiteSpace(statName) ? stat.ToString() : statName.Trim();
        }

        // EOS のポータルで作るリーダーボードの Id と同じにする。
        public string Id { get; }

        public string DisplayNameKey { get; }

        public PlayerStat Stat { get; }

        public LeaderboardAggregation Aggregation { get; }

        public string StatName { get; }
    }
}
