using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public static class MinimapJson
    {
        public const string ResourcePath = "Tuning/minimap";

        public const string RadiusItem = "radius";
        public const string ScaleItem = "scale";
        public const string UpdateIntervalSecondsItem = "updateIntervalSeconds";
        public const string SpottedSecondsItem = "spottedSeconds";
        public const string RotateWithPlayerItem = "rotateWithPlayer";

        private const string _kind = "minimap JSON";

        public static IReadOnlyList<string> ItemNames { get; } = new[]
        {
            RadiusItem,
            ScaleItem,
            UpdateIntervalSecondsItem,
            SpottedSecondsItem,
            RotateWithPlayerItem,
        };

        public static MinimapSpec Parse(string text, string sourceName, Action<string> log)
        {
            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            var source = ServerDataJson.SourceOf(sourceName, _kind);
            try
            {
                var fields = new ServerDataJson.Fields(source, _kind, ServerDataJson.ParseDocument(text, source, _kind), string.Empty, ItemNames);
                return new MinimapSpec(
                    ReadInRange(fields, RadiusItem, MinimapSpec.DefaultRadius, log),
                    ReadInRange(fields, ScaleItem, MinimapSpec.DefaultScale, log),
                    ReadInRange(fields, UpdateIntervalSecondsItem, MinimapSpec.DefaultUpdateIntervalSeconds, log),
                    ReadInRange(fields, SpottedSecondsItem, MinimapSpec.DefaultSpottedSeconds, log),
                    fields.ReadBool(RotateWithPlayerItem, MinimapSpec.DefaultRotateWithPlayer));
            }
            catch (ServerDataJsonException e)
            {
                // KitTuning は TuningJsonException だけを「読めないので既定値で続ける」と扱う。
                throw new TuningJsonException(e.SourceName, e.Item, e.Message, e);
            }
        }

        // 型の違いは書き間違いなのでファイルごと断るが、範囲外は調整の行き過ぎなので、その項目だけ既定値に戻してほかの項目を生かす。
        private static float ReadInRange(ServerDataJson.Fields fields, string item, float fallback, Action<string> log)
        {
            var value = fields.ReadFloat(item, fallback);
            if (MinimapSpec.IsInRange(value))
            {
                return value;
            }

            log(MinimapLog.ValueReplaced(fields.Source, item, value, fallback));
            return fallback;
        }
    }
}
