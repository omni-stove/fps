using System;
using System.Globalization;

namespace Fps.Core
{
    /// <summary>
    /// ゲーム進行がメニューへ戻した理由、またはオンラインを始めずにメニューに留めた理由。どう言うかはメニューの UI が決める。
    /// <see cref="Disconnected"/> はサーバーとの接続を失ったとき、<see cref="OnlineFailed"/> はオンラインの試合を始められなかったとき。
    /// どちらも、batchmode のプレイヤーならそのまま終わるときの終了コード <see cref="ExitCode"/> を持つ。
    /// </summary>
    public sealed class MenuReturnReason
    {
        private MenuReturnReason(MenuReturnKind kind, int exitCode, DisconnectNotice disconnect, string detail)
        {
            Kind = kind;
            ExitCode = exitCode;
            Disconnect = disconnect;
            Detail = detail;
        }

        public MenuReturnKind Kind { get; }

        /// <summary>batchmode のプレイヤーで終わるときの終了コード。</summary>
        public int ExitCode { get; }

        /// <summary><see cref="MenuReturnKind.Disconnected"/> のときの、接続を失った理由。それ以外は null。</summary>
        public DisconnectNotice Disconnect { get; }

        /// <summary><see cref="MenuReturnKind.OnlineFailed"/> のときの、始められなかった訳 (ログと同じ開発者向けの文)。それ以外は空文字。</summary>
        public string Detail { get; }

        /// <summary>サーバーとの接続を失ってメニューへ戻る理由。</summary>
        /// <exception cref="ArgumentNullException">notice が null。</exception>
        public static MenuReturnReason Disconnected(DisconnectNotice notice, int exitCode)
        {
            if (notice == null)
            {
                throw new ArgumentNullException(nameof(notice));
            }

            return new MenuReturnReason(MenuReturnKind.Disconnected, exitCode, notice, string.Empty);
        }

        /// <summary>オンラインの試合を始められずにメニューへ戻る (留まる) 理由。detail が null なら空文字にする。</summary>
        public static MenuReturnReason OnlineFailed(int exitCode, string detail)
        {
            return new MenuReturnReason(MenuReturnKind.OnlineFailed, exitCode, null, detail ?? string.Empty);
        }

        /// <summary>ログに出す 1 行の中身。例: <c>return=Disconnected exit=0 kind=ServerStopped connected=True reason="fps:server-stopped"</c>。</summary>
        public string Describe()
        {
            var text = "return=" + Kind + " exit=" + ExitCode.ToString(CultureInfo.InvariantCulture);
            if (Disconnect != null)
            {
                text += " " + Disconnect.Describe();
            }

            if (Detail.Length > 0)
            {
                text += " detail=\"" + Detail + "\"";
            }

            return text;
        }

        public override string ToString()
        {
            return Describe();
        }
    }
}
