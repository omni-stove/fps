using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public static class AimAssistJson
    {
        public const string SlowdownDegreesItem = "slowdownDegrees";
        public const string SlowdownScaleItem = "slowdownScale";
        public const string MagnetismDegreesItem = "magnetismDegrees";
        public const string MagnetismDegreesPerSecondItem = "magnetismDegreesPerSecond";
        public const string MagnetismSecondsItem = "magnetismSeconds";
        public const string MaxDistanceItem = "maxDistance";

        private const string _kind = "aim assist JSON";

        public static IReadOnlyList<string> ItemNames { get; } = new[]
        {
            SlowdownDegreesItem,
            SlowdownScaleItem,
            MagnetismDegreesItem,
            MagnetismDegreesPerSecondItem,
            MagnetismSecondsItem,
            MaxDistanceItem,
        };

        public static AimAssistSpec Parse(string text, string sourceName)
        {
            var source = ServerDataJson.SourceOf(sourceName, _kind);
            try
            {
                var fields = new ServerDataJson.Fields(source, _kind, ServerDataJson.ParseDocument(text, source, _kind), string.Empty, ItemNames);
                try
                {
                    return new AimAssistSpec(
                        fields.ReadFloat(SlowdownDegreesItem, AimAssistSpec.DefaultSlowdownDegrees),
                        fields.ReadFloat(SlowdownScaleItem, AimAssistSpec.DefaultSlowdownScale),
                        fields.ReadFloat(MagnetismDegreesItem, AimAssistSpec.DefaultMagnetismDegrees),
                        fields.ReadFloat(MagnetismDegreesPerSecondItem, AimAssistSpec.DefaultMagnetismDegreesPerSecond),
                        fields.ReadFloat(MagnetismSecondsItem, AimAssistSpec.DefaultMagnetismSeconds),
                        fields.ReadFloat(MaxDistanceItem, AimAssistSpec.DefaultMaxDistance));
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

        // AimAssistSpec は引数名を ParamName に入れて投げ、引数名は項目名と同じなので、そのまま項目名として返せる。
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
