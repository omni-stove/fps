namespace Fps.Protocol
{
    /// <summary><see cref="EosP2PNotice"/> の種類。</summary>
    public enum EosP2PNoticeKind
    {
        /// <summary>ログインできた。<see cref="EosP2PNotice.Text"/> は自分の ProductUserId。</summary>
        SignedIn,

        /// <summary>ログインできなかった。<see cref="EosP2PNotice.Text"/> は理由。</summary>
        SignInFailed,

        /// <summary>相手から接続の要求が届いた。サーバーとして始めたときだけ出る。</summary>
        ConnectionRequested,

        /// <summary>相手との接続が開いた。</summary>
        ConnectionEstablished,

        /// <summary>相手との接続が閉じた。<see cref="EosP2PNotice.Text"/> は SDK が返した閉じた理由 (ログ向け)。</summary>
        ConnectionClosed,
    }
}
