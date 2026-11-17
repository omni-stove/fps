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
                case DisconnectKind.VersionMismatch:
                    return VersionMismatchText(notice.ServerVersion, notice.LocalVersion);
                case DisconnectKind.Rejected:
                    return string.Equals(notice.Detail, DisconnectNotice.ServerDrainingDetail, StringComparison.Ordinal)
                        ? ServerDrainingMessage
                        : RejectedMessage;
                case DisconnectKind.TimedOut:
                    return notice.WasConnected ? TimedOutMessage : ConnectTimedOutMessage;
                default:
                    throw new ArgumentOutOfRangeException(nameof(notice), notice.Kind, "unknown disconnect kind");
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
