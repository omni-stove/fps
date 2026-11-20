using System;
using System.Collections.Generic;
using System.Globalization;
using Shooter.Core;
using ReportReason = Shooter.Protocol.ReportReason;

namespace Shooter.Server
{
    // 通報の本文はプレイヤーが書いたものなので、サーバーのログには出さず、通報のファイルにだけ残す。
    internal static class ServerReportLog
    {
        public const string Tag = "[Report]";
        public const string RecordType = "report";
        public const int MaxReportIdLength = 64;

        public static string Record(
            string reportId,
            DateTime utcNow,
            string matchId,
            int reporterId,
            string reporterProductUserId,
            int targetId,
            string targetProductUserId,
            ReportReason reason,
            string comment)
        {
            if (!IsReportId(reportId))
            {
                throw new ArgumentException("reportId must be 1 to " + MaxReportIdLength + " letters, digits, '-' or '_'", nameof(reportId));
            }

            if (string.IsNullOrEmpty(matchId))
            {
                throw new ArgumentException("matchId must not be null or empty", nameof(matchId));
            }

            if (!Enum.IsDefined(reason))
            {
                throw new ArgumentOutOfRangeException(nameof(reason), reason, "reason must be a defined ReportReason");
            }

            var values = new List<KeyValuePair<string, string>>
            {
                Pair("type", RecordType),
                Pair("id", reportId),
                Pair("time", ServerEventLog.FormatTime(utcNow)),
                Pair("matchId", matchId),
            };
            AddPlayer(values, "reporterId", "reporterProductUserId", reporterId, reporterProductUserId);
            AddPlayer(values, "targetId", "targetProductUserId", targetId, targetProductUserId);
            values.Add(Pair("reason", reason.ToString()));
            var trimmed = comment?.Trim();
            if (!string.IsNullOrEmpty(trimmed))
            {
                values.Add(Pair("comment", trimmed));
            }

            return FlatJsonObject.Format(values);
        }

        // id は証拠のファイルの名前にもなるので、どの OS でもそのまま名前に使え、置き場所の外を指せない文字だけを許す
        public static bool IsReportId(string reportId)
        {
            if (string.IsNullOrEmpty(reportId) || reportId.Length > MaxReportIdLength)
            {
                return false;
            }

            foreach (var c in reportId)
            {
                if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-' || c == '_'))
                {
                    return false;
                }
            }

            return true;
        }

        public static string NewReportId()
        {
            return Guid.NewGuid().ToString("N");
        }

        public static string Opened(string path, double repeatSeconds)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} writing reports to {1} repeat={2:0.##}s", Tag, path, repeatSeconds);
        }

        public static string CannotOpen(string path, string error)
        {
            return Tag + " cannot open the report file " + path + " (" + error + ")";
        }

        public static string Saved(int reporterId, int targetId, ReportReason reason)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} saved reporter={1} target={2} reason={3}", Tag, reporterId, targetId, reason);
        }

        public static string Repeated(int reporterId, int targetId)
        {
            return string.Format(
                CultureInfo.InvariantCulture, "{0} skipped a repeated report reporter={1} target={2}", Tag, reporterId, targetId);
        }

        public static string Refused(int reporterId, int targetId, ServerReportRefusal reason)
        {
            if (!Enum.IsDefined(reason))
            {
                throw new ArgumentOutOfRangeException(nameof(reason), reason, "reason must be a defined ServerReportRefusal");
            }

            return string.Format(
                CultureInfo.InvariantCulture, "{0} refused reporter={1} target={2} reason={3}", Tag, reporterId, targetId, reason);
        }

        public static string NotJoined(ulong connection)
        {
            return Tag + " ignored a report from connection " + connection.ToString(CultureInfo.InvariantCulture) + " that has not joined";
        }

        public static string WriteFailed(string path, string error)
        {
            return Tag + " could not write a report to " + path + " (" + error + "); the report is lost";
        }

        public static string Closed(string path)
        {
            return Tag + " closed " + path;
        }

        private static void AddPlayer(
            List<KeyValuePair<string, string>> values, string idKey, string productUserIdKey, int playerId, string productUserId)
        {
            values.Add(Pair(idKey, playerId.ToString(CultureInfo.InvariantCulture)));
            if (productUserId != null)
            {
                values.Add(Pair(productUserIdKey, productUserId));
            }
        }

        private static KeyValuePair<string, string> Pair(string key, string value)
        {
            return new KeyValuePair<string, string>(key, value);
        }
    }
}
