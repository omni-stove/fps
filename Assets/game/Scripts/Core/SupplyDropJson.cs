using System.Collections.Generic;

namespace Shooter.Core
{
    public static class SupplyDropJson
    {
        public const string ResourcePath = "Tuning/supply-drops";

        public const string StagesItem = "stages";
        public const string TableIdItem = "tableId";
        public const string DropHeightItem = "dropHeight";
        public const string FallSpeedItem = "fallSpeed";
        public const string PlaceFractionItem = "placeFraction";
        public const string ReachItem = "reach";
        public const string BotSearchRadiusItem = "botSearchRadius";

        public const string CountItem = "count";
        public const string DelaySecondsItem = "delaySeconds";
        public const string IntervalSecondsItem = "intervalSeconds";

        private const string _kind = "supply drop JSON";

        public static IReadOnlyList<string> ItemNames { get; } = new[]
        {
            StagesItem,
            TableIdItem,
            DropHeightItem,
            FallSpeedItem,
            PlaceFractionItem,
            ReachItem,
            BotSearchRadiusItem,
        };

        public static IReadOnlyList<string> StageItemNames { get; } = new[] { CountItem, DelaySecondsItem, IntervalSecondsItem };

        public static SupplyDropSpec Parse(string text, string sourceName)
        {
            return SpecJsonReader.Read(text, sourceName, _kind, ItemNames, Read);
        }

        // 別の調整値のファイルの中の節 (battle-royale.json の supplyDrops) からも同じ項目で読めるよう、節を受け取る口を分ける
        internal static SupplyDropSpec Read(ServerDataJson.Fields fields)
        {
            var stages = SpecJsonReader.ReadList(fields, StagesItem, StageItemNames, ReadStage);
            var tableId = fields.ReadString(TableIdItem, SupplyDropSpec.DefaultTableId);
            var dropHeight = fields.ReadFloat(DropHeightItem, SupplyDropSpec.DefaultDropHeight);
            var fallSpeed = fields.ReadFloat(FallSpeedItem, SupplyDropSpec.DefaultFallSpeed);
            var placeFraction = fields.ReadFloat(PlaceFractionItem, SupplyDropSpec.DefaultPlaceFraction);
            var reach = fields.ReadFloat(ReachItem, SupplyDropSpec.DefaultReach);
            var botSearchRadius = fields.ReadFloat(BotSearchRadiusItem, SupplyDropSpec.DefaultBotSearchRadius);
            return SpecJsonReader.Build(
                fields,
                ItemNames,
                () => new SupplyDropSpec(stages, tableId, dropHeight, fallSpeed, placeFraction, reach, botSearchRadius));
        }

        // 数と間隔は書き忘れると落ちる数が変わってしまうので必須にし、最初の待ちだけ省けるようにする。
        private static SupplyDropStage ReadStage(ServerDataJson.Fields stage)
        {
            var count = stage.ToInt(stage.ItemPath(CountItem), stage.Require(CountItem));
            var delaySeconds = stage.ReadFloat(DelaySecondsItem, 0f);
            var intervalSeconds = stage.ToFloat(stage.ItemPath(IntervalSecondsItem), stage.Require(IntervalSecondsItem));
            return new SupplyDropStage(count, delaySeconds, intervalSeconds);
        }
    }
}
