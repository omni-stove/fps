using System.Collections.Generic;

namespace Shooter.Core
{
    // battle-royale.json。エリア・物資投下・復帰の節は、それぞれ単独のファイル (ZoneJson・SupplyDropJson・RedeployJson) と同じ項目で読む
    public static class BattleRoyaleJson
    {
        public const string SquadCountItem = "squadCount";
        public const string SquadSizeItem = "squadSize";
        public const string TimeLimitSecondsItem = "timeLimitSeconds";
        public const string BotSearchRadiusItem = "botSearchRadius";
        public const string BotPlanIntervalSecondsItem = "botPlanIntervalSeconds";
        public const string ZoneItem = "zone";
        public const string SupplyDropsItem = "supplyDrops";
        public const string RedeployItem = "redeploy";

        private const string _kind = "battle royale JSON";

        public static IReadOnlyList<string> ItemNames { get; } = new[]
        {
            SquadCountItem,
            SquadSizeItem,
            TimeLimitSecondsItem,
            BotSearchRadiusItem,
            BotPlanIntervalSecondsItem,
            ZoneItem,
            SupplyDropsItem,
            RedeployItem,
        };

        // 節を書かなければ、その節の Spec の既定を使う
        public static BattleRoyaleSpec Parse(string text, string sourceName)
        {
            return SpecJsonReader.Read(text, sourceName, _kind, ItemNames, fields =>
            {
                var squadCount = fields.ReadInt(SquadCountItem, BattleRoyaleSpec.DefaultSquadCount);
                var squadSize = fields.ReadInt(SquadSizeItem, BattleRoyaleSpec.DefaultSquadSize);
                var timeLimitSeconds = fields.ReadFloat(TimeLimitSecondsItem, BattleRoyaleSpec.DefaultTimeLimitSeconds);
                var botSearchRadius = fields.ReadFloat(BotSearchRadiusItem, BattleRoyaleSpec.DefaultBotSearchRadius);
                var botPlanIntervalSeconds = fields.ReadFloat(BotPlanIntervalSecondsItem, BattleRoyaleSpec.DefaultBotPlanIntervalSeconds);
                var zoneFields = fields.Object(ZoneItem, ZoneJson.ItemNames);
                var zone = zoneFields != null ? ZoneJson.Read(zoneFields) : null;
                var supplyFields = fields.Object(SupplyDropsItem, SupplyDropJson.ItemNames);
                var supplyDrops = supplyFields != null ? SupplyDropJson.Read(supplyFields) : null;
                var redeployFields = fields.Object(RedeployItem, RedeployJson.ItemNames);
                var redeploy = redeployFields != null ? RedeployJson.Read(redeployFields) : null;
                return SpecJsonReader.Build(
                    fields,
                    ItemNames,
                    () => new BattleRoyaleSpec(squadCount, squadSize, timeLimitSeconds, botSearchRadius, botPlanIntervalSeconds, zone, supplyDrops, redeploy));
            });
        }
    }
}
