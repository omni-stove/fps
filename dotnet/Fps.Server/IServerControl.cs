namespace Fps.Server
{
    /// <summary>モジュールがサーバーの状態を見て、続けられないときに止めるための口。</summary>
    internal interface IServerControl
    {
        /// <summary>止まる準備中 (新しい参加を断り、試合の終わりを待っている) か。</summary>
        bool IsDraining { get; }

        /// <summary>
        /// サーバーを止め、プロセスを <paramref name="exitCode"/> で終える。drain を待たないのは、続けられない失敗だから。
        /// 最初の呼び出しの終了コードを使う。
        /// </summary>
        /// <param name="exitCode">終了コード (<see cref="ServerProgram"/> の Exit*)。</param>
        /// <param name="message">ログに出す理由。</param>
        void Fail(int exitCode, string message);
    }
}
