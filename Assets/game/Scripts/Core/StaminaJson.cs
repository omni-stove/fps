using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public static class StaminaJson
    {
        public const string MaxItem = "max";
        public const string SprintCostPerSecondItem = "sprintCostPerSecond";
        public const string SlideCostItem = "slideCost";
        public const string DodgeCostItem = "dodgeCost";
        public const string RegenPerSecondItem = "regenPerSecond";
        public const string RegenDelaySecondsItem = "regenDelaySeconds";
        public const string RecoverAmountItem = "recoverAmount";

        private const string _kind = "stamina JSON";

        public static IReadOnlyList<string> ItemNames { get; } = new[]
        {
            MaxItem,
            SprintCostPerSecondItem,
            SlideCostItem,
            DodgeCostItem,
            RegenPerSecondItem,
            RegenDelaySecondsItem,
            RecoverAmountItem,
        };

        public static StaminaSpec Parse(string text, string sourceName)
        {
            var source = ServerDataJson.SourceOf(sourceName, _kind);
            try
            {
                var fields = new ServerDataJson.Fields(source, _kind, ServerDataJson.ParseDocument(text, source, _kind), string.Empty, ItemNames);
                try
                {
                    return new StaminaSpec(
                        fields.ReadFloat(MaxItem, StaminaSpec.DefaultMax),
                        fields.ReadFloat(SprintCostPerSecondItem, StaminaSpec.DefaultSprintCostPerSecond),
                        fields.ReadFloat(SlideCostItem, StaminaSpec.DefaultSlideCost),
                        fields.ReadFloat(DodgeCostItem, StaminaSpec.DefaultDodgeCost),
                        fields.ReadFloat(RegenPerSecondItem, StaminaSpec.DefaultRegenPerSecond),
                        fields.ReadFloat(RegenDelaySecondsItem, StaminaSpec.DefaultRegenDelaySeconds),
                        fields.ReadFloat(RecoverAmountItem, StaminaSpec.DefaultRecoverAmount));
                }
                catch (ArgumentException e)
                {
                    throw fields.Rejected(Known(e.ParamName) ? e.ParamName : null, e.Message, e);
                }
            }
            catch (ServerDataJsonException e)
            {
                // KitTuning とサーバーは TuningJsonException だけを「調整値のファイルが読めない」と扱う。
                var inner = e.InnerException is ArgumentException rejected ? (Exception)rejected : e;
                throw new TuningJsonException(e.SourceName, e.Item, e.Message, inner);
            }
        }

        // StaminaSpec は引数名を ParamName に入れて投げ、引数名は項目名と同じなので、そのまま項目名として返せる。
        private static bool Known(string name)
        {
            foreach (var item in ItemNames)
            {
                if (string.Equals(item, name, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
