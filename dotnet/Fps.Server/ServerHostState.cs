namespace Fps.Server
{
    /// <summary><see cref="ServerHost"/> の段階。</summary>
    internal enum ServerHostState
    {
        /// <summary>モジュールを登録できる。</summary>
        NotStarted,

        /// <summary>すべてのモジュールが起動し、tick と接続のフックを受け取る。</summary>
        Running,

        /// <summary>止まった (起動に失敗したときも含む)。もう動かせない。</summary>
        Stopped,
    }
}
