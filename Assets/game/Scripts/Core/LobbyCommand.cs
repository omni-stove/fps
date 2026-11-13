namespace Fps.Core
{
    /// <summary>コマンドライン引数 <c>-lobby create|join|quickmatch</c> で選ぶ、起動時のロビーの操作。</summary>
    public enum LobbyCommand
    {
        /// <summary>ロビーを使わない (<c>-mode</c> などで直接つなぐ)。</summary>
        None,

        /// <summary>ロビーを作ってオーナーになり、Host として起動する。</summary>
        Create,

        /// <summary>ロビーを探して (または <c>-lobby-id</c> のロビーに) 参加し、オーナーへクライアントとして接続する。</summary>
        Join,

        /// <summary>
        /// 専用サーバーが <c>-register-lobby</c> で載せたロビーを探し、選んだサーバーの公開アドレスへ UDP のクライアントとして接続する。
        /// ロビーには入らない。
        /// </summary>
        QuickMatch,
    }
}
