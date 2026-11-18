using System.Collections.Generic;

namespace Shooter.Core
{
    public static class ExtractionJson
    {
        public const string ResourcePath = "Tuning/extraction";

        public const string RaidSecondsItem = "raidSeconds";
        public const string ExtractHoldSecondsItem = "extractHoldSeconds";
        public const string OpenPointsItem = "openPoints";
        public const string OpenAfterSecondsItem = "openAfterSeconds";
        public const string KeepCarriedOnDeathItem = "keepCarriedOnDeath";

        private const string _kind = "extraction JSON";

        public static IReadOnlyList<string> ItemNames { get; } = new[]
        {
            RaidSecondsItem,
            ExtractHoldSecondsItem,
            OpenPointsItem,
            OpenAfterSecondsItem,
            KeepCarriedOnDeathItem,
        };

        public static ExtractionSpec Parse(string text, string sourceName)
        {
            return SpecJsonReader.Read(text, sourceName, _kind, ItemNames, fields =>
            {
                var raidSeconds = fields.ReadFloat(RaidSecondsItem, ExtractionSpec.DefaultRaidSeconds);
                var extractHoldSeconds = fields.ReadFloat(ExtractHoldSecondsItem, ExtractionSpec.DefaultExtractHoldSeconds);
                var openPoints = fields.ReadInt(OpenPointsItem, ExtractionSpec.DefaultOpenPoints);
                var openAfterSeconds = fields.ReadFloat(OpenAfterSecondsItem, ExtractionSpec.DefaultOpenAfterSeconds);
                var keepCarriedOnDeath = fields.ReadBool(KeepCarriedOnDeathItem, ExtractionSpec.DefaultKeepCarriedOnDeath);
                return SpecJsonReader.Build(
                    fields,
                    ItemNames,
                    () => new ExtractionSpec(raidSeconds, extractHoldSeconds, openPoints, openAfterSeconds, keepCarriedOnDeath));
            });
        }
    }
}
