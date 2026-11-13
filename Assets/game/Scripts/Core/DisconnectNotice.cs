using System;

namespace Fps.Core
{
    /// <summary>
    /// クライアントがサーバーとの接続を失った理由を分け (<see cref="DisconnectKind"/>)、メニューに出す文を決める。
    /// サーバーは切るとき・断るときに <see cref="ServerStoppedReason"/> などの理由の符号を Netcode の切断理由として送り、
    /// クライアントは受け取った切断理由とつながっていたかどうかから <see cref="Classify"/> で分ける。
    /// </summary>
    /// <remarks>
    /// Netcode や transport が自分で入れる切断理由の文言は版で変わるので、分け方には使わずログにだけ残す。
    /// 符号の無い切断は、サーバーが知らせずに消えた (止まった・落ちた・回線が切れた) ので、相手の応答が途絶えたものとして扱う。
    /// 切断理由はサーバー (または途中の誰か) が自由に送れるので、文に混ぜる部分は文字を絞ってから使う。
    /// </remarks>
    public sealed class DisconnectNotice
    {
        /// <summary>このゲームのサーバーが送る切断理由の符号の接頭辞。</summary>
        public const string ReasonPrefix = "fps:";

        /// <summary>サーバーが止まるときに、つないでいるクライアントへ送る切断理由。</summary>
        public const string ServerStoppedReason = ReasonPrefix + "server-stopped";

        /// <summary>版が違うクライアントを断るときの切断理由の接頭辞。続けてサーバーの版を付ける。</summary>
        public const string VersionMismatchPrefix = ReasonPrefix + "version-mismatch:";

        /// <summary>版の違い以外で断るときの切断理由の接頭辞。続けて断った訳の符号を付ける。</summary>
        public const string RejectedPrefix = ReasonPrefix + "rejected:";

        /// <summary>サーバーが止まる準備中 (<see cref="ServerDrain"/>) で新しい参加を断るときの、断った訳の符号。</summary>
        public const string ServerDrainingDetail = "server-draining";

        /// <summary>断った訳の符号の最長の文字数。これより長い・使えない文字を含む符号は捨てる。</summary>
        public const int MaxDetailLength = 32;

        public const string ServerStoppedMessage = "サーバーが止まりました。";
        public const string TimedOutMessage = "サーバーからの応答が途絶えました (タイムアウト)。";
        public const string ConnectTimedOutMessage = "サーバーに接続できませんでした (タイムアウト)。";
        public const string RejectedMessage = "サーバーに接続を断られました。";
        public const string VersionMismatchMessage = "版が違います。";
        public const string ServerDrainingMessage = "サーバーが止まる準備中のため参加できません。別のサーバーにつないでください。";

        private DisconnectNotice(
            DisconnectKind kind, bool wasConnected, string message, string serverVersion, string detail, string rawReason)
        {
            Kind = kind;
            WasConnected = wasConnected;
            Message = message;
            ServerVersion = serverVersion;
            Detail = detail;
            RawReason = rawReason;
        }

        public DisconnectKind Kind { get; }

        /// <summary>切れる前にサーバーとつながっていた (承認された) なら true。</summary>
        public bool WasConnected { get; }

        /// <summary>メニューに出す文。</summary>
        public string Message { get; }

        /// <summary><see cref="DisconnectKind.VersionMismatch"/> でサーバーが知らせてきた版。読めなければ null。</summary>
        public string ServerVersion { get; }

        /// <summary><see cref="DisconnectKind.Rejected"/> でサーバーが知らせてきた断った訳の符号。読めなければ null。</summary>
        public string Detail { get; }

        /// <summary>受け取った切断理由そのもの。無ければ空文字。ログ用で、画面には出さない。</summary>
        public string RawReason { get; }

        /// <summary>
        /// 版が違うクライアントを断るときの切断理由。
        /// </summary>
        /// <exception cref="ArgumentException">serverVersion が正規化済みの版 (<see cref="ConnectionPayload.IsNormalizedVersion"/>) でない。</exception>
        public static string VersionMismatchReason(string serverVersion)
        {
            if (!ConnectionPayload.IsNormalizedVersion(serverVersion))
            {
                throw new ArgumentException("server version '" + serverVersion + "' is not a normalized build version", nameof(serverVersion));
            }

            return VersionMismatchPrefix + serverVersion;
        }

        /// <summary>版の違い以外で断るときの切断理由。</summary>
        /// <exception cref="ArgumentException">detail が <see cref="IsValidDetail"/> でない。</exception>
        public static string RejectedReason(string detail)
        {
            if (!IsValidDetail(detail))
            {
                throw new ArgumentException(
                    "rejection detail must be 1-" + MaxDetailLength + " characters of [a-z0-9-]", nameof(detail));
            }

            return RejectedPrefix + detail;
        }

        /// <summary>断った訳の符号として使えるなら true。小文字の英数字と <c>-</c> だけの 1〜<see cref="MaxDetailLength"/> 文字。</summary>
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

        /// <summary>
        /// 受け取った切断理由から、接続を失った理由を分ける。
        /// <list type="bullet">
        /// <item><see cref="ServerStoppedReason"/> なら <see cref="DisconnectKind.ServerStopped"/>。</item>
        /// <item><see cref="VersionMismatchPrefix"/> で始まれば <see cref="DisconnectKind.VersionMismatch"/>。</item>
        /// <item><see cref="RejectedPrefix"/> で始まれば <see cref="DisconnectKind.Rejected"/>。訳が <see cref="ServerDrainingDetail"/> なら
        /// 文は <see cref="ServerDrainingMessage"/>、それ以外は <see cref="RejectedMessage"/>。</item>
        /// <item>それ以外 (空、知らない符号、Netcode や transport の文言) は <see cref="DisconnectKind.TimedOut"/>。</item>
        /// </list>
        /// </summary>
        /// <param name="reason">Netcode の切断理由 (NetworkManager.DisconnectReason)。null は空として扱う。</param>
        /// <param name="wasConnected">切れる前に承認されてつながっていたか。タイムアウトの文を分けるのに使う。</param>
        /// <param name="localVersion">このゲームの版。版の違いの文に添える。読めなければ添えない。</param>
        public static DisconnectNotice Classify(string reason, bool wasConnected, string localVersion)
        {
            var raw = reason ?? "";
            var code = raw.Trim();

            if (string.Equals(code, ServerStoppedReason, StringComparison.Ordinal))
            {
                return new DisconnectNotice(DisconnectKind.ServerStopped, wasConnected, ServerStoppedMessage, null, null, raw);
            }

            if (code.StartsWith(VersionMismatchPrefix, StringComparison.Ordinal))
            {
                var serverVersion = code.Substring(VersionMismatchPrefix.Length);
                if (!ConnectionPayload.IsNormalizedVersion(serverVersion))
                {
                    serverVersion = null;
                }

                var local = ConnectionPayload.IsNormalizedVersion(localVersion) ? localVersion : null;
                return new DisconnectNotice(
                    DisconnectKind.VersionMismatch, wasConnected, VersionMismatchText(serverVersion, local), serverVersion, null, raw);
            }

            if (code.StartsWith(RejectedPrefix, StringComparison.Ordinal))
            {
                var detail = code.Substring(RejectedPrefix.Length);
                if (!IsValidDetail(detail))
                {
                    detail = null;
                }

                var message = string.Equals(detail, ServerDrainingDetail, StringComparison.Ordinal) ? ServerDrainingMessage : RejectedMessage;
                return new DisconnectNotice(DisconnectKind.Rejected, wasConnected, message, null, detail, raw);
            }

            return new DisconnectNotice(
                DisconnectKind.TimedOut, wasConnected, wasConnected ? TimedOutMessage : ConnectTimedOutMessage, null, null, raw);
        }

        /// <summary>
        /// 版の違いを伝える文。両方の版が分かれば添える。
        /// 例: <c>版が違います (サーバー 0.2.0 / このゲーム 0.1.0)。サーバーと同じ版のゲームでつないでください。</c>
        /// </summary>
        public static string VersionMismatchText(string serverVersion, string localVersion)
        {
            if (string.IsNullOrEmpty(serverVersion) || string.IsNullOrEmpty(localVersion))
            {
                return VersionMismatchMessage + "サーバーと同じ版のゲームでつないでください。";
            }

            return "版が違います (サーバー " + serverVersion + " / このゲーム " + localVersion + ")。サーバーと同じ版のゲームでつないでください。";
        }

        /// <summary>ログに出す 1 行の中身。例: <c>kind=VersionMismatch connected=False server-version=0.2.0 reason="fps:version-mismatch:0.2.0"</c>。</summary>
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
