using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Shooter.Core
{
    // 値をすべて文字列で書くのは、通報やイベントの行と同じ FlatJsonObject で書き、同じ読み手で読めるようにするため。
    public static class TelemetryLog
    {
        public const string Tag = "[Telemetry]";

        public const string CrashType = "crash";

        public const string MatchSummaryType = "matchSummary";

        // 1 行が TelemetrySpec.MinMaxFileBytes を超えると、書くたびにファイルが切り替わって前の行を押し出すので、長い文を切る。
        // JSON の書き出しで 1 文字が最大 6 バイト (\uXXXX) になっても 64 KiB に収まる長さにしてある。
        public const int MaxMessageLength = 1024;

        public const int MaxStackTraceLength = 8 * 1024;

        public const int MaxExceptionTypeLength = 256;

        public const string TruncatedMark = "...";

        public static string Crash(DateTime utcNow, TelemetryOrigin origin, string exceptionType, string message, string stackTrace)
        {
            var values = Begin(CrashType, utcNow, origin);
            values.Add(Pair("exception", string.IsNullOrWhiteSpace(exceptionType) ? "unknown" : Truncate(exceptionType.Trim(), MaxExceptionTypeLength)));
            values.Add(Pair("message", Truncate(message, MaxMessageLength)));
            values.Add(Pair("stackTrace", Truncate(stackTrace, MaxStackTraceLength)));
            return FlatJsonObject.Format(values);
        }

        public static string MatchSummary(DateTime utcNow, TelemetryOrigin origin, TelemetryMatchSummary summary)
        {
            if (summary == null)
            {
                throw new ArgumentNullException(nameof(summary));
            }

            var values = Begin(MatchSummaryType, utcNow, origin);
            AddText(values, "matchId", summary.MatchId);
            AddText(values, "mode", summary.Mode);
            AddText(values, "map", summary.Map);
            values.Add(Pair("durationSeconds", summary.DurationSeconds.ToString("0.###", CultureInfo.InvariantCulture)));
            values.Add(Pair("winner", summary.Winner.HasValue ? SideText(summary.Winner.Value) : "none"));
            if (summary.OwnSide.HasValue)
            {
                values.Add(Pair("ownSide", SideText(summary.OwnSide.Value)));
                values.Add(Pair("result", ResultOf(summary.OwnSide.Value, summary.Winner)));
            }

            foreach (var score in summary.SideScores)
            {
                var key = "score" + SideText(score.Side);
                // 同じ陣営が 2 度並ぶのは出した側の誤りだが、要約 1 件のために試合の終わりを止めないので後のものを捨てる
                if (!ContainsKey(values, key))
                {
                    values.Add(Pair(key, score.Score.ToString("0.###", CultureInfo.InvariantCulture)));
                }
            }

            AddCount(values, "humans", summary.Humans);
            AddCount(values, "bots", summary.Bots);
            return FlatJsonObject.Format(values);
        }

        public static string ResultOf(Side ownSide, Side? winner)
        {
            if (!winner.HasValue)
            {
                return "draw";
            }

            return winner.Value == ownSide ? "won" : "lost";
        }

        public static string Recording(TelemetrySink sink, string target)
        {
            return Tag + " recording to " + target + " (sink=" + sink + ")";
        }

        public static string NotRecording(bool consent, TelemetrySpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            return Tag + " not recording: " + (spec.IsOff ? "the sink is None" : !consent ? "no consent" : "crash reports and match summaries are both off");
        }

        public static string ConsentChanged(bool consent)
        {
            return Tag + (consent ? " consent given; recording from now on" : " consent withdrawn; nothing more is recorded or sent");
        }

        public static string SampledOut(float sampleRate)
        {
            return string.Format(
                CultureInfo.InvariantCulture, "{0} this session is not sampled (sampleRate={1:0.###}); match summaries are skipped", Tag, sampleRate);
        }

        public static string CrashLimitReached(int limit)
        {
            return string.Format(
                CultureInfo.InvariantCulture, "{0} recorded {1} different crashes this session; further new crashes are not recorded", Tag, limit);
        }

        public static string CannotCreateSink(TelemetrySink sink, string error)
        {
            return Tag + " cannot create the " + sink + " sink (" + error + "); the record is lost";
        }

        public static string CannotOpen(string path, string error)
        {
            return Tag + " cannot open " + path + " (" + error + "); the record is lost";
        }

        public static string WriteFailed(string path, string error)
        {
            return Tag + " could not write to " + path + " (" + error + "); the record is lost";
        }

        public static string RolledOver(string path, string previousPath)
        {
            return Tag + " " + path + " reached its size limit; moved it to " + previousPath;
        }

        public static string PostFailed(string endpoint, string reason, int lines, bool kept)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} could not send {1} record(s) to {2} ({3}); {4}",
                Tag,
                lines,
                endpoint,
                reason,
                kept ? "keeping them for the next send" : "dropping them");
        }

        public static string Dropped(string target, int lines)
        {
            return string.Format(
                CultureInfo.InvariantCulture, "{0} dropped {1} unsent record(s) for {2} to stay under the size limit", Tag, lines, target);
        }

        private static List<KeyValuePair<string, string>> Begin(string type, DateTime utcNow, TelemetryOrigin origin)
        {
            if (origin == null)
            {
                throw new ArgumentNullException(nameof(origin));
            }

            var values = new List<KeyValuePair<string, string>>
            {
                Pair("type", type),
                Pair("time", ServerEventLog.FormatTime(utcNow)),
                Pair("source", origin.Source),
                Pair("session", origin.Session),
            };
            AddText(values, "version", origin.Version);
            AddText(values, "platform", origin.Platform);
            return values;
        }

        private static string Truncate(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            if (text.Length <= maxLength)
            {
                return text;
            }

            var length = maxLength - TruncatedMark.Length;
            // サロゲートの組を半分で切ると、読み手によっては行ごと読めなくなるので、組の前で切る
            if (char.IsHighSurrogate(text[length - 1]))
            {
                length--;
            }

            return new StringBuilder(maxLength).Append(text, 0, length).Append(TruncatedMark).ToString();
        }

        private static string SideText(Side side)
        {
            return side.Id.ToString(CultureInfo.InvariantCulture);
        }

        private static void AddText(List<KeyValuePair<string, string>> values, string key, string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                values.Add(Pair(key, text));
            }
        }

        private static void AddCount(List<KeyValuePair<string, string>> values, string key, int? count)
        {
            if (count.HasValue)
            {
                values.Add(Pair(key, count.Value.ToString(CultureInfo.InvariantCulture)));
            }
        }

        private static bool ContainsKey(List<KeyValuePair<string, string>> values, string key)
        {
            foreach (var pair in values)
            {
                if (string.Equals(pair.Key, key, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static KeyValuePair<string, string> Pair(string key, string value)
        {
            return new KeyValuePair<string, string>(key, value);
        }
    }
}
