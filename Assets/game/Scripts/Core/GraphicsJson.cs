using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public static class GraphicsJson
    {
        public const string ResourcePath = "Tuning/graphics";

        public const string VSyncItem = "vSync";

        public const string FrameRateLimitItem = "frameRateLimit";

        public const string QualityLevelItem = "qualityLevel";

        public const string FrameRateChoicesItem = "frameRateChoices";

        public const string HeadlessFrameRateItem = "headlessFrameRate";

        private const string _kind = "graphics JSON";

        public static IReadOnlyList<string> ItemNames { get; } = new[]
        {
            VSyncItem,
            FrameRateLimitItem,
            QualityLevelItem,
            FrameRateChoicesItem,
            HeadlessFrameRateItem,
        };

        public static GraphicsSpec Parse(string text, string sourceName)
        {
            var source = ServerDataJson.SourceOf(sourceName, _kind);
            try
            {
                var fields = new ServerDataJson.Fields(source, _kind, ServerDataJson.ParseDocument(text, source, _kind), string.Empty, ItemNames);
                var choices = ReadChoices(fields);
                try
                {
                    var defaults = new GraphicsSettings(
                        ScreenResolution.Native,
                        fields.ReadInt(QualityLevelItem, GraphicsSettings.DefaultQualityLevel),
                        fields.ReadBool(VSyncItem, GraphicsSettings.DefaultVSync),
                        fields.ReadInt(FrameRateLimitItem, GraphicsSettings.DefaultFrameRateLimit));
                    return new GraphicsSpec(
                        defaults,
                        choices,
                        fields.ReadInt(HeadlessFrameRateItem, GraphicsSpec.DefaultHeadlessFrameRate));
                }
                catch (ArgumentException e)
                {
                    throw fields.Rejected(ItemOf(e.ParamName), e.Message, e);
                }
            }
            catch (ServerDataJsonException e)
            {
                // KitTuning は TuningJsonException だけを「読めないので既定値で続ける」と扱う。
                var inner = e.InnerException is ArgumentException rejected ? (Exception)rejected : e;
                throw new TuningJsonException(e.SourceName, e.Item, e.Message, inner);
            }
        }

        private static int[] ReadChoices(ServerDataJson.Fields fields)
        {
            if (!fields.Has(FrameRateChoicesItem))
            {
                return null;
            }

            var nodes = fields.Array(FrameRateChoicesItem);
            var choices = new int[nodes.Count];
            for (var i = 0; i < choices.Length; i++)
            {
                choices[i] = fields.ToInt(fields.ElementPath(FrameRateChoicesItem, i), nodes[i]);
            }

            return choices;
        }

        private static string ItemOf(string paramName)
        {
            foreach (var item in ItemNames)
            {
                if (string.Equals(item, paramName, StringComparison.Ordinal))
                {
                    return item;
                }
            }

            return null;
        }
    }
}
