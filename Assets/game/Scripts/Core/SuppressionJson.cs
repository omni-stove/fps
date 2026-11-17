using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public static class SuppressionJson
    {
        public const string NearMissRadiusItem = "nearMissRadius";
        public const string BuildPerNearMissItem = "buildPerNearMiss";
        public const string DecayDelaySecondsItem = "decayDelaySeconds";
        public const string DecayPerSecondItem = "decayPerSecond";
        public const string SuppressedLevelItem = "suppressedLevel";
        public const string ReleasedLevelItem = "releasedLevel";
        public const string MaxSpreadDegreesItem = "maxSpreadDegrees";
        public const string MaxCrosshairSpreadItem = "maxCrosshairSpread";
        public const string MaxVignetteAlphaItem = "maxVignetteAlpha";

        private const string _kind = "suppression JSON";

        public static IReadOnlyList<string> ItemNames { get; } = new[]
        {
            NearMissRadiusItem,
            BuildPerNearMissItem,
            DecayDelaySecondsItem,
            DecayPerSecondItem,
            SuppressedLevelItem,
            ReleasedLevelItem,
            MaxSpreadDegreesItem,
            MaxCrosshairSpreadItem,
            MaxVignetteAlphaItem,
        };

        public static SuppressionSpec Parse(string text, string sourceName)
        {
            var source = ServerDataJson.SourceOf(sourceName, _kind);
            try
            {
                var fields = new ServerDataJson.Fields(source, _kind, ServerDataJson.ParseDocument(text, source, _kind), string.Empty, ItemNames);
                try
                {
                    return new SuppressionSpec(
                        fields.ReadFloat(NearMissRadiusItem, SuppressionSpec.DefaultNearMissRadius),
                        fields.ReadFloat(BuildPerNearMissItem, SuppressionSpec.DefaultBuildPerNearMiss),
                        fields.ReadFloat(DecayDelaySecondsItem, SuppressionSpec.DefaultDecayDelaySeconds),
                        fields.ReadFloat(DecayPerSecondItem, SuppressionSpec.DefaultDecayPerSecond),
                        fields.ReadFloat(SuppressedLevelItem, SuppressionSpec.DefaultSuppressedLevel),
                        fields.ReadFloat(ReleasedLevelItem, SuppressionSpec.DefaultReleasedLevel),
                        fields.ReadFloat(MaxSpreadDegreesItem, SuppressionSpec.DefaultMaxSpreadDegrees),
                        fields.ReadFloat(MaxCrosshairSpreadItem, SuppressionSpec.DefaultMaxCrosshairSpread),
                        fields.ReadFloat(MaxVignetteAlphaItem, SuppressionSpec.DefaultMaxVignetteAlpha));
                }
                catch (ArgumentException e)
                {
                    throw fields.Rejected(Known(e.ParamName) ? e.ParamName : null, e.Message, e);
                }
            }
            catch (ServerDataJsonException e)
            {
                // KitTuning は TuningJsonException だけを「読めないので既定値で続ける」と扱う。
                var inner = e.InnerException is ArgumentException rejected ? (Exception)rejected : e;
                throw new TuningJsonException(e.SourceName, e.Item, e.Message, inner);
            }
        }

        // SuppressionSpec は引数名を ParamName に入れて投げ、引数名は項目名と同じなので、そのまま項目名として返せる。
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
