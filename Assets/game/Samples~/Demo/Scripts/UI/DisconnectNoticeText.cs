using System;
using Fps.Core;

namespace Fps.Runtime
{
    /// <summary>
    /// メニューに戻った理由 (<see cref="MenuReturnReason"/>)・サーバーとの接続を失った理由 (<see cref="DisconnectNotice"/>) を、メニューに出す文にする
    /// (表のキー <c>net.disconnect.*</c>。メニューが今の言語で引く)。
    /// </summary>
    public static class DisconnectNoticeText
    {
        public static LocalizedText ServerStoppedMessage => new LocalizedText("net.disconnect.serverStopped");
        public static LocalizedText TimedOutMessage => new LocalizedText("net.disconnect.timedOut");
        public static LocalizedText ConnectTimedOutMessage => new LocalizedText("net.disconnect.connectTimedOut");
        public static LocalizedText RejectedMessage => new LocalizedText("net.disconnect.rejected");
        public static LocalizedText VersionMismatchMessage => new LocalizedText("net.disconnect.versionMismatch");
        public static LocalizedText ServerDrainingMessage => new LocalizedText("net.disconnect.serverDraining");
        public static LocalizedText IdleKickedMessage => new LocalizedText("net.disconnect.idleKicked");

        /// <summary>
        /// メニューに戻った理由の文。接続を失ったなら <see cref="For(DisconnectNotice)"/>、オンラインを始められなかったなら
        /// その訳 (<see cref="MenuReturnReason.Detail"/>) をそのまま出す。reason が null なら <c>default</c> (何も出さない)。
        /// </summary>
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

        /// <summary>
        /// 接続を失った理由の文。
        /// <list type="bullet">
        /// <item><see cref="DisconnectKind.ServerStopped"/> は <see cref="ServerStoppedMessage"/>、<see cref="DisconnectKind.IdleKicked"/> は <see cref="IdleKickedMessage"/>。</item>
        /// <item><see cref="DisconnectKind.VersionMismatch"/> は <see cref="VersionMismatchText"/> (両方の版が分かれば添える)。</item>
        /// <item><see cref="DisconnectKind.Rejected"/> は、訳が <see cref="DisconnectNotice.ServerDrainingDetail"/> なら <see cref="ServerDrainingMessage"/>、
        /// それ以外は <see cref="RejectedMessage"/>。</item>
        /// <item><see cref="DisconnectKind.TimedOut"/> は、つながっていたなら <see cref="TimedOutMessage"/>、つながる前なら <see cref="ConnectTimedOutMessage"/>。</item>
        /// </list>
        /// </summary>
        /// <exception cref="ArgumentNullException">notice が null。</exception>
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

        /// <summary>
        /// 版の違いを伝える文。両方の版が分かれば添える。
        /// 例: <c>版が違います (サーバー 0.2.0 / このゲーム 0.1.0)。サーバーと同じ版のゲームでつないでください。</c>
        /// </summary>
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
