using System;
using Shooter.Core;

namespace Shooter.Runtime
{
    public static class DisconnectNoticeText
    {
        public static LocalizedText ServerStoppedMessage => new LocalizedText("net.disconnect.serverStopped");
        public static LocalizedText TimedOutMessage => new LocalizedText("net.disconnect.timedOut");
        public static LocalizedText ConnectTimedOutMessage => new LocalizedText("net.disconnect.connectTimedOut");
        public static LocalizedText RejectedMessage => new LocalizedText("net.disconnect.rejected");
        public static LocalizedText VersionMismatchMessage => new LocalizedText("net.disconnect.versionMismatch");
        public static LocalizedText ServerDrainingMessage => new LocalizedText("net.disconnect.serverDraining");
        public static LocalizedText IdleKickedMessage => new LocalizedText("net.disconnect.idleKicked");
        public static LocalizedText BannedMessage => new LocalizedText("net.disconnect.banned");
        public static LocalizedText KickedMessage => new LocalizedText("net.disconnect.kicked");
        public static LocalizedText VoteKickedMessage => new LocalizedText("net.disconnect.voteKicked");

        public static LocalizedText For(MenuReturnReason reason)
        {
            if (reason == null)
            {
                return default;
            }

            switch (reason.Kind)
            {
                case MenuReturnKind.Disconnected:
                    return For(reason.Disconnect);
                case MenuReturnKind.OnlineFailed:
                    return string.IsNullOrWhiteSpace(reason.Detail) ? default : LocalizedText.Literal(reason.Detail);
                default:
                    throw new ArgumentOutOfRangeException(nameof(reason), reason.Kind, "unknown menu return kind");
            }
        }

        public static LocalizedText For(DisconnectNotice notice)
        {
            if (notice == null)
            {
                throw new ArgumentNullException(nameof(notice));
            }

            switch (notice.Kind)
            {
                case DisconnectKind.ServerStopped:
                    return ServerStoppedMessage;
                case DisconnectKind.IdleKicked:
                    return IdleKickedMessage;
                case DisconnectKind.VoteKicked:
                    return VoteKickedMessage;
                case DisconnectKind.VersionMismatch:
                    return VersionMismatchText(notice.ServerVersion, notice.LocalVersion);
                case DisconnectKind.Rejected:
                    return RejectedText(notice.Detail);
                case DisconnectKind.TimedOut:
                    return notice.WasConnected ? TimedOutMessage : ConnectTimedOutMessage;
                default:
                    throw new ArgumentOutOfRangeException(nameof(notice), notice.Kind, "unknown disconnect kind");
            }
        }

        private static LocalizedText RejectedText(string detail)
        {
            switch (detail)
            {
                case DisconnectNotice.ServerDrainingDetail:
                    return ServerDrainingMessage;
                case DisconnectNotice.BannedDetail:
                    return BannedMessage;
                case DisconnectNotice.KickedDetail:
                    return KickedMessage;
                default:
                    return RejectedMessage;
            }
        }

        public static LocalizedText VersionMismatchText(string serverVersion, string localVersion)
        {
            if (string.IsNullOrEmpty(serverVersion) || string.IsNullOrEmpty(localVersion))
            {
                return new LocalizedText("net.disconnect.versionMismatchAdvice", VersionMismatchMessage);
            }

            return new LocalizedText("net.disconnect.versionMismatchDetail", serverVersion, localVersion);
        }
    }
}
