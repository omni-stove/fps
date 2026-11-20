using System;
using System.Collections.Generic;
using System.Globalization;
using Shooter.Core;

namespace Shooter.Server
{
    // 行ごとに集めて人と指標で絞り込めるよう、疑いは 1 行の JSON で出す。
    internal static class ServerCheatStatsLog
    {
        public const string Tag = "[CheatStats]";
        public const string RecordType = "cheat-stats";

        public static string Suspicion(
            int playerId,
            string productUserId,
            ServerCheatMetric metric,
            float value,
            float threshold,
            float windowSeconds,
            int? samples = null)
        {
            var values = new List<KeyValuePair<string, string>>
            {
                Pair("type", RecordType),
                Pair("playerId", playerId.ToString(CultureInfo.InvariantCulture)),
            };
            if (productUserId != null)
            {
                values.Add(Pair("productUserId", productUserId));
            }

            values.Add(Pair("metric", Key(metric)));
            values.Add(Pair("value", Number(value)));
            values.Add(Pair("threshold", Number(threshold)));
            values.Add(Pair("windowSeconds", Number(windowSeconds)));
            if (samples.HasValue)
            {
                values.Add(Pair("samples", samples.Value.ToString(CultureInfo.InvariantCulture)));
            }

            return FlatJsonObject.Format(values);
        }

        public static string Started(AntiCheatTuning.StatsSettings stats)
        {
            ArgumentNullException.ThrowIfNull(stats);
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} collecting window={1}s headshotRatio>{2} reactionMs<{3} snapDegreesPerTick>{4} wallTrackSeconds>{5} minSamples={6}",
                Tag,
                Number(stats.WindowSeconds),
                Number(stats.HeadshotRatio),
                Number(stats.MinReactionMs),
                Number(stats.SnapDegreesPerTick),
                Number(stats.WallTrackSeconds),
                stats.MinSamples);
        }

        public static string Key(ServerCheatMetric metric)
        {
            switch (metric)
            {
                case ServerCheatMetric.HeadshotRatio:
                    return "headshotRatio";
                case ServerCheatMetric.ReactionMs:
                    return "reactionMs";
                case ServerCheatMetric.SnapDegreesPerTick:
                    return "snapDegreesPerTick";
                case ServerCheatMetric.WallTrackSeconds:
                    return "wallTrackSeconds";
                default:
                    throw new ArgumentOutOfRangeException(nameof(metric), metric, "metric must be a defined ServerCheatMetric");
            }
        }

        private static string Number(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static KeyValuePair<string, string> Pair(string key, string value)
        {
            return new KeyValuePair<string, string>(key, value);
        }
    }
}
