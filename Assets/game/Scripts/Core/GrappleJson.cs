using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    // 置ける物の一覧 (DeployCatalogJson) に並べないのは、グラップルは置く物ではなく、形や体力の項目を持たないため。
    // 掴める面のタグ (GrappleAbilityDefinition.GrabbableTags) は読まない。サーバーの当たりにはタグが無いため
    public static class GrappleJson
    {
        public const string IdItem = "id";
        public const string MinRangeItem = "minRange";
        public const string MaxRangeItem = "maxRange";
        public const string PullSpeedItem = "pullSpeed";
        public const string PullAccelerationItem = "pullAcceleration";
        public const string ArriveDistanceItem = "arriveDistance";
        public const string MaxPullSecondsItem = "maxPullSeconds";
        public const string HangHeightItem = "hangHeight";
        public const string EdgeSnapDistanceItem = "edgeSnapDistance";
        public const string HangSecondsItem = "hangSeconds";
        public const string GrabbableLayersItem = "grabbableLayers";
        public const string MaxChargesItem = "maxCharges";
        public const string RechargeSecondsItem = "rechargeSeconds";
        public const string CooldownSecondsItem = "cooldownSeconds";

        private const string _kind = "grapple JSON";

        // 項目名は GrappleType・GrappleSpec・AbilitySpec のコンストラクターの引数名にそろえ、値の検査で断られた項目を名前で示せるようにする。
        public static IReadOnlyList<string> ItemNames { get; } = new[]
        {
            IdItem,
            MinRangeItem,
            MaxRangeItem,
            PullSpeedItem,
            PullAccelerationItem,
            ArriveDistanceItem,
            MaxPullSecondsItem,
            HangHeightItem,
            EdgeSnapDistanceItem,
            HangSecondsItem,
            GrabbableLayersItem,
            MaxChargesItem,
            RechargeSecondsItem,
            CooldownSecondsItem,
        };

        public static GrappleType Parse(string text, string sourceName)
        {
            return TuningJson.ParseFlat(text, sourceName, _kind, ItemNames, Read);
        }

        // 縄を撃つまでの溜め (windupSeconds) は読まない。溜めは持ち主の手元で待ち、サーバーはボタンが届いた tick に縄を掛けるため
        private static GrappleType Read(ServerDataJson.Fields fields)
        {
            var id = fields.ToText(fields.ItemPath(IdItem), fields.Require(IdItem));
            try
            {
                var spec = new GrappleSpec(
                    fields.ReadFloat(MinRangeItem, GrappleSpec.DefaultMinRange),
                    fields.ReadFloat(MaxRangeItem, GrappleSpec.DefaultMaxRange),
                    fields.ReadFloat(PullSpeedItem, GrappleSpec.DefaultPullSpeed),
                    fields.ReadFloat(PullAccelerationItem, GrappleSpec.DefaultPullAcceleration),
                    fields.ReadFloat(ArriveDistanceItem, GrappleSpec.DefaultArriveDistance),
                    fields.ReadFloat(MaxPullSecondsItem, GrappleSpec.DefaultMaxPullSeconds),
                    fields.ReadFloat(HangHeightItem, GrappleSpec.DefaultHangHeight),
                    fields.ReadFloat(EdgeSnapDistanceItem, GrappleSpec.DefaultEdgeSnapDistance),
                    fields.ReadFloat(HangSecondsItem, GrappleSpec.DefaultHangSeconds));
                var ability = new AbilitySpec(
                    fields.ReadInt(MaxChargesItem, AbilitySpec.DefaultMaxCharges),
                    fields.ReadFloat(RechargeSecondsItem, AbilitySpec.DefaultRechargeSeconds),
                    fields.ReadFloat(CooldownSecondsItem, AbilitySpec.DefaultCooldownSeconds),
                    AbilitySpec.DefaultWindupSeconds);
                return new GrappleType(id, spec, ability, fields.ReadInt(GrabbableLayersItem, GrappleType.DefaultGrabbableLayers));
            }
            catch (ArgumentException e)
            {
                throw TuningJson.Rejected(fields.Source, _kind, ItemNames, e);
            }
        }
    }
}
