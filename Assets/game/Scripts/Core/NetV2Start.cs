namespace Fps.Core
{
    /// <summary>起動したプロセスで <c>-netstack v2</c> のクライアントをどう始めるか (<see cref="NetStackLaunch.ResolveStart"/>)。</summary>
    public enum NetV2Start
    {
        /// <summary>v2 を使わない (専用サーバー・<c>-netstack</c> が無いか v1)。</summary>
        NotRequested,

        /// <summary><c>-mode client -netstack v2</c>: 起動してすぐサーバーへつなぐ。</summary>
        Connect,

        /// <summary><c>-netstack v2</c> だけ: メニューのオンラインのボタンでサーバーへつなぐ。</summary>
        FromMenu,

        /// <summary>引数が誤っている。</summary>
        Invalid,
    }
}
