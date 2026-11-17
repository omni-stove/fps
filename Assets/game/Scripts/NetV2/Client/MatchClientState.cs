namespace Shooter.NetV2
{
    /// <summary><see cref="MatchClient"/> の接続の段階。</summary>
    public enum MatchClientState
    {
        /// <summary>まだつなぎ始めていない。</summary>
        Idle,

        /// <summary>通信路がサーバーへつなごうとしている。</summary>
        Connecting,

        /// <summary>通信路はつながり、承認の返事を待っている。</summary>
        Approving,

        /// <summary>承認されて試合にいる。</summary>
        Connected,

        /// <summary>切れた (つながらなかった・断られた・自分で抜けたを含む)。もう使えない。</summary>
        Disconnected,
    }
}
