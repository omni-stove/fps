using System.Collections.Generic;

namespace Shooter.Core
{
    public static class LeaderboardJson
    {
        public const string ResourcePath = "Tuning/leaderboards";

        public const string BoardsItem = "boards";
        public const string PageSizeItem = "pageSize";
        public const string RefreshSecondsItem = "refreshSeconds";

        public const string IdItem = "id";
        public const string NameKeyItem = "nameKey";
        public const string StatItem = "stat";
        public const string AggregationItem = "aggregation";
        public const string StatNameItem = "statName";

        private const string _kind = "leaderboard JSON";

        public static IReadOnlyList<string> ItemNames { get; } = new[] { BoardsItem, PageSizeItem, RefreshSecondsItem };

        public static IReadOnlyList<string> BoardItemNames { get; } = new[] { IdItem, NameKeyItem, StatItem, AggregationItem, StatNameItem };

        public static LeaderboardSpec Parse(string text, string sourceName)
        {
            return SpecJsonReader.Read(text, sourceName, _kind, ItemNames, fields =>
            {
                var boards = SpecJsonReader.ReadList(fields, BoardsItem, BoardItemNames, ReadBoard);
                var pageSize = fields.ReadInt(PageSizeItem, LeaderboardSpec.DefaultPageSize);
                var refreshSeconds = fields.ReadFloat(RefreshSecondsItem, LeaderboardSpec.DefaultRefreshSeconds);
                return SpecJsonReader.Build(fields, ItemNames, () => new LeaderboardSpec(boards, pageSize, refreshSeconds));
            });
        }

        private static LeaderboardDefinition ReadBoard(ServerDataJson.Fields entry)
        {
            var id = SpecJsonReader.RequireText(entry, IdItem);
            var nameKey = entry.ReadString(NameKeyItem, null);
            var stat = entry.ToEnum<PlayerStat>(entry.ItemPath(StatItem), entry.Require(StatItem));
            var aggregation = entry.ReadEnum(AggregationItem, LeaderboardDefinition.DefaultAggregation);
            var statName = entry.ReadString(StatNameItem, null);
            return new LeaderboardDefinition(id, nameKey, stat, aggregation, statName);
        }
    }
}
