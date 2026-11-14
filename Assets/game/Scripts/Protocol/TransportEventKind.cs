namespace Fps.Protocol
{
    /// <summary><see cref="TransportEvent"/> の種類。</summary>
    public enum TransportEventKind
    {
        /// <summary>接続が確立した。サーバーでは相手が入ってきた、クライアントではサーバーにつながった。</summary>
        Connected,

        /// <summary>接続が切れた (どちらから切ったか、つながらなかったかを問わない)。以後その Id は使えない。</summary>
        Disconnected,

        /// <summary>データが届いた。</summary>
        Data,
    }
}
