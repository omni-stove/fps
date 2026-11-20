using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Shooter.Core;

namespace Shooter.Server
{
    // 値を文字列で載せるのは、通報の行やイベントの JSON と同じ FlatJsonObject で書き、読む側が 1 つの読み方で済むようにするため。
    // 時刻を通報からさかのぼる秒 (ago) で書くのは、tick の秒数の和はサーバーの起動からの値で、読む人には意味が無いため。
    internal static class ServerReportEvidenceLog
    {
        public const string Tag = "[Evidence]";
        public const string LookType = "look";
        public const string ShotType = "shot";
        public const string HitType = "hit";
        public const string FileSuffix = ".evidence.jsonl";

        public static string Look(double ago, double tick, Vector3? position, float yaw, float? pitch)
        {
            var values = Begin(LookType, ago);
            values.Add(Pair("tick", Number(tick)));
            if (position.HasValue)
            {
                AddVector(values, "x", "y", "z", position.Value);
            }

            values.Add(Pair("yaw", Number(yaw)));
            if (pitch.HasValue)
            {
                values.Add(Pair("pitch", Number(pitch.Value)));
            }

            return FlatJsonObject.Format(values);
        }

        public static string Shot(double ago, double shotTick, Vector3 origin, Vector3 direction)
        {
            var values = Begin(ShotType, ago);
            values.Add(Pair("tick", Number(shotTick)));
            AddVector(values, "x", "y", "z", origin);
            AddVector(values, "dirX", "dirY", "dirZ", direction);
            return FlatJsonObject.Format(values);
        }

        public static string Hit(double ago, int victimId, HitZone zone, double rewindTicks)
        {
            var values = Begin(HitType, ago);
            values.Add(Pair("victimId", victimId.ToString(CultureInfo.InvariantCulture)));
            values.Add(Pair("zone", zone.ToString()));
            values.Add(Pair("rewindTicks", Number(rewindTicks)));
            return FlatJsonObject.Format(values);
        }

        public static string Holding(float seconds)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} holding the last {1:0.##}s of each player for reports", Tag, seconds);
        }

        public static string Off()
        {
            return Tag + " evidence is off (evidence.enabled is false in the anticheat tuning)";
        }

        public static string Saved(string reportId, int targetId, string path)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} saved report={1} target={2} to {3}", Tag, reportId, targetId, path);
        }

        public static string Limited(int targetId, int maxFilesPerMinute)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} skipped the evidence for target={1}: already {2} files in the last minute (evidence.maxFilesPerMinute); the report is kept without it",
                Tag,
                targetId,
                maxFilesPerMinute);
        }

        public static string WriteFailed(string path, string error)
        {
            return Tag + " could not write the evidence to " + path + " (" + error + "); the report is kept without it";
        }

        private static List<KeyValuePair<string, string>> Begin(string type, double ago)
        {
            if (!(ago >= 0d) || double.IsInfinity(ago))
            {
                throw new ArgumentOutOfRangeException(nameof(ago), ago, "ago must be a non-negative finite number");
            }

            return new List<KeyValuePair<string, string>>
            {
                Pair("type", type),
                Pair("ago", ago.ToString("0.000", CultureInfo.InvariantCulture)),
            };
        }

        private static void AddVector(List<KeyValuePair<string, string>> values, string x, string y, string z, Vector3 value)
        {
            values.Add(Pair(x, Number(value.X)));
            values.Add(Pair(y, Number(value.Y)));
            values.Add(Pair(z, Number(value.Z)));
        }

        private static string Number(double value)
        {
            return value.ToString("0.####", CultureInfo.InvariantCulture);
        }

        private static KeyValuePair<string, string> Pair(string key, string value)
        {
            return new KeyValuePair<string, string>(key, value);
        }
    }
}
