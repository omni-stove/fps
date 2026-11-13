namespace Fps.Core
{
    /// <summary>クライアントがサーバーとの接続を失った理由の分け方。<see cref="DisconnectNotice.Classify"/> が決める。</summary>
    public enum DisconnectKind
    {
        /// <summary>サーバーが止まると知らせてから切った。</summary>
        ServerStopped,

        /// <summary>理由の知らせが無いまま切れた。つながる前ならつながらなかった (接続の試行が尽きた)。</summary>
        TimedOut,

        /// <summary>サーバーが承認で断った (版の違い以外)。</summary>
        Rejected,

        /// <summary>サーバーが、版が違うとして承認で断った。</summary>
        VersionMismatch,

        /// <summary>入力が無い時間が続いたので、サーバーが切った (放置)。</summary>
        IdleKicked,
    }
}
