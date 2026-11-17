namespace Shooter.Core
{
    /// <summary>起動したプロセスでクライアントをどう始めるか (<see cref="NetClientLaunch.ResolveStart"/>)。</summary>
    public enum NetClientStart
    {
        /// <summary><c>-mode</c> も <c>-lobby</c> も無い: メニューのクイックマッチでサーバーへつなぐ。</summary>
        FromMenu,

        /// <summary><c>-mode client</c>: 起動してすぐ <c>-address</c> か <c>-eos-server</c> のサーバーへつなぐ。</summary>
        Connect,

        /// <summary><c>-lobby quickmatch</c>: 起動してすぐ専用サーバーのロビーを探してつなぐ。</summary>
        QuickMatch,

        /// <summary>引数が誤っている。</summary>
        Invalid,
    }
}
