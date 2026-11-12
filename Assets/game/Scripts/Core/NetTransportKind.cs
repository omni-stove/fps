namespace Fps.Core
{
    /// <summary>ネットワークの transport。コマンドライン引数 <c>-transport udp|eos</c> に対応する。</summary>
    public enum NetTransportKind
    {
        /// <summary>IP アドレスとポートへ直接つなぐ (Unity Transport)。</summary>
        Udp,

        /// <summary>Epic Online Services の P2P でつなぐ。相手は ProductUserId で指定する。</summary>
        Eos,
    }
}
