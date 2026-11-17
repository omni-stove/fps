using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public static class SkillJson
    {
        public const string InitialRatingItem = "initialRating";
        public const string KFactorItem = "kFactor";
        public const string MinRatingItem = "minRating";
        public const string MaxRatingItem = "maxRating";

        private const string _kind = "skill JSON";

        public static IReadOnlyList<string> ItemNames { get; } = new[]
        {
            InitialRatingItem,
            KFactorItem,
            MinRatingItem,
            MaxRatingItem,
        };

        public static SkillSpec Parse(string text, string sourceName)
        {
            var source = ServerDataJson.SourceOf(sourceName, _kind);
            try
            {
                var fields = new ServerDataJson.Fields(source, _kind, ServerDataJson.ParseDocument(text, source, _kind), string.Empty, ItemNames);
                try
                {
                    return new SkillSpec(
                        fields.ReadInt(InitialRatingItem, SkillSpec.DefaultInitialRating),
                        fields.ReadFloat(KFactorItem, SkillSpec.DefaultKFactor),
                        fields.ReadInt(MinRatingItem, SkillSpec.DefaultMinRating),
                        fields.ReadInt(MaxRatingItem, SkillSpec.DefaultMaxRating));
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

        // SkillSpec は引数名を ParamName に入れて投げ、引数名は項目名と同じなので、そのまま項目名として返せる。
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
