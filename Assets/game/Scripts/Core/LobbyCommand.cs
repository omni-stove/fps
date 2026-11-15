namespace Fps.Core
{
    /// <summary>コマンドライン引数 <c>-lobby quickmatch</c> で選ぶ、起動時のロビーの操作。</summary>
    public enum LobbyCommand
    {
        /// <summary>ロビーを使わない (<c>-mode client</c> などで直接つなぐ)。</summary>
        None,

        /// <summary>
        /// 専用サーバーが <c>-register-lobby</c> で載せたロビーを探し、選んだサーバーへクライアントとしてつなぐ。ロビーには入らない。
        /// </summary>
        QuickMatch,
    }
}
