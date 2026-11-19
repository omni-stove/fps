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
            return SpecJsonReader.Read(text, sourceName, _kind, ItemNames, Read);
        }

        // 別の調整値のファイルの中の節 (battle-royale.json の redeploy) からも同じ項目で読めるよう、節を受け取る口を分ける
        internal static RedeploySpec Read(ServerDataJson.Fields fields)
        {
            var charges = fields.ReadInt(ChargesItem, RedeploySpec.DefaultCharges);
            var lastPhaseIndex = fields.ReadInt(LastPhaseIndexItem, RedeploySpec.DefaultLastPhaseIndex);
            var delaySeconds = fields.ReadFloat(DelaySecondsItem, RedeploySpec.DefaultDelaySeconds);
            return SpecJsonReader.Build(fields, ItemNames, () => new RedeploySpec(charges, lastPhaseIndex, delaySeconds));
        }
    }
}
