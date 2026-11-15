namespace Fps.Core
{
    /// <summary>ネットワークの起動モード。コマンドライン引数 <c>-mode server|client</c> に対応する。</summary>
    public enum NetLaunchMode
    {
        /// <summary>クライアントを持たない専用サーバー。</summary>
        Server,

        /// <summary>サーバーへ接続するクライアント。</summary>
        Client,
    }
}
