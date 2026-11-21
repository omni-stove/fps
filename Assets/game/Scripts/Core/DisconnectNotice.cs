using System;

namespace Shooter.Core
{
    public sealed class DisconnectNotice
    {
        public const string ReasonPrefix = "shooter:";

        public const string ServerStoppedReason = ReasonPrefix + "server-stopped";

        public const string IdleKickedReason = ReasonPrefix + "idle-kicked";

        public const string VoteKickedReason = ReasonPrefix + "vote-kicked";

        public const string VersionMismatchPrefix = ReasonPrefix + "version-mismatch:";

        public const string RejectedPrefix = ReasonPrefix + "rejected:";

        public const string ServerDrainingDetail = "server-draining";

        public const string ServerFullDetail = "server-full";

        public const string JoinCodeMismatchDetail = "join-code-mismatch";

        public const string BannedDetail = "banned";

        public const string KickedDetail = "kicked";

        // 切断理由はサーバー (または途中の誰か) が自由に送れるので、文に混ぜる符号は長さと文字を絞ってから使う。
        public const int MaxDetailLength = 32;

        private DisconnectNotice(
            DisconnectKind kind, bool wasConnected, string serverVersion, string localVersion, string detail, string rawReason)
        {
            Kind = kind;
            WasConnected = wasConnected;
            ServerVersion = serverVersion;
            LocalVersion = localVersion;
            Detail = detail;
            RawReason = rawReason;
        }

        public DisconnectKind Kind { get; }

        public bool WasConnected { get; }

        public string ServerVersion { get; }

        public string LocalVersion { get; }

        public string Detail { get; }

        public string RawReason { get; }

        public static string VersionMismatchReason(string serverVersion)
        {
            if (!ConnectionPayload.IsNormalizedVersion(serverVersion))
            {
                throw new ArgumentException("server version '" + serverVersion + "' is not a normalized build version", nameof(serverVersion));
            }

            return VersionMismatchPrefix + serverVersion;
        }

        public static string RejectedReason(string detail)
        {
            if (!IsValidDetail(detail))
            {
                throw new ArgumentException(
                    "rejection detail must be 1-" + MaxDetailLength + " characters of [a-z0-9-]", nameof(detail));
            }

            return RejectedPrefix + detail;
        }

        public static bool IsValidDetail(string detail)
        {
            if (string.IsNullOrEmpty(detail) || detail.Length > MaxDetailLength)
            {
                return false;
            }

            foreach (var c in detail)
            {
                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-'))
                {
                    return false;
                }
            }

            return true;
        }

        // transport が自分で入れる切断理由の文言は版で変わるので、分け方には使わずログにだけ残す。
        // 符号の無い切断は、サーバーが知らせずに消えた (止まった・落ちた・回線が切れた) ので、相手の応答が途絶えたものとして扱う。
        public static DisconnectNotice Classify(string reason, bool wasConnected, string localVersion)
        {
            var raw = reason ?? "";
            var code = raw.Trim();

            if (string.Equals(code, ServerStoppedReason, StringComparison.Ordinal))
            {
                return new DisconnectNotice(DisconnectKind.ServerStopped, wasConnected, null, null, null, raw);
            }

            if (string.Equals(code, IdleKickedReason, StringComparison.Ordinal))
            {
                return new DisconnectNotice(DisconnectKind.IdleKicked, wasConnected, null, null, null, raw);
            }

            if (string.Equals(code, VoteKickedReason, StringComparison.Ordinal))
            {
                return new DisconnectNotice(DisconnectKind.VoteKicked, wasConnected, null, null, null, raw);
            }

            if (code.StartsWith(VersionMismatchPrefix, StringComparison.Ordinal))
            {
                var serverVersion = code.Substring(VersionMismatchPrefix.Length);
                if (!ConnectionPayload.IsNormalizedVersion(serverVersion))
                {
                    serverVersion = null;
                }

                var local = ConnectionPayload.IsNormalizedVersion(localVersion) ? localVersion : null;
                return new DisconnectNotice(DisconnectKind.VersionMismatch, wasConnected, serverVersion, local, null, raw);
            }

            if (code.StartsWith(RejectedPrefix, StringComparison.Ordinal))
            {
                var detail = code.Substring(RejectedPrefix.Length);
                if (!IsValidDetail(detail))
                {
                    detail = null;
                }

                return new DisconnectNotice(DisconnectKind.Rejected, wasConnected, null, null, detail, raw);
            }

            return new DisconnectNotice(DisconnectKind.TimedOut, wasConnected, null, null, null, raw);
        }

        public string Describe()
        {
            var text = "kind=" + Kind + " connected=" + WasConnected;
            if (ServerVersion != null)
            {
                text += " server-version=" + ServerVersion;
            }

            if (Detail != null)
            {
                text += " detail=" + Detail;
            }

            if (RawReason.Length > 0)
            {
                text += " reason=\"" + RawReason + "\"";
            }

            return text;
        }

        public override string ToString()
        {
            return Describe();
        }
    }
}
