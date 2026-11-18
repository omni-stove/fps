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

        public static string Record(
            DateTime utcNow,
            string matchId,
            int reporterId,
            string reporterProductUserId,
            int targetId,
            string targetProductUserId,
            ReportReason reason,
            string comment)
        {
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
