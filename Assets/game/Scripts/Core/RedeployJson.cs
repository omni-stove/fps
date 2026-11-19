using System.Collections.Generic;

namespace Shooter.Core
{
    public static class RedeployJson
    {
        public const string ResourcePath = "Tuning/redeploy";

        public const string ChargesItem = "charges";
        public const string LastPhaseIndexItem = "lastPhaseIndex";
        public const string DelaySecondsItem = "delaySeconds";

        private const string _kind = "redeploy JSON";

        public static IReadOnlyList<string> ItemNames { get; } = new[] { ChargesItem, LastPhaseIndexItem, DelaySecondsItem };

        public static RedeploySpec Parse(string text, string sourceName)
        {
            return SpecJsonReader.Read(text, sourceName, _kind, ItemNames, fields =>
            {
                var charges = fields.ReadInt(ChargesItem, RedeploySpec.DefaultCharges);
                var lastPhaseIndex = fields.ReadInt(LastPhaseIndexItem, RedeploySpec.DefaultLastPhaseIndex);
                var delaySeconds = fields.ReadFloat(DelaySecondsItem, RedeploySpec.DefaultDelaySeconds);
                return SpecJsonReader.Build(fields, ItemNames, () => new RedeploySpec(charges, lastPhaseIndex, delaySeconds));
            });
        }
    }
}
