namespace Fps.Protocol
{
    /// <summary>対戦のメッセージ全体の決まり。</summary>
    public static class MatchProtocol
    {
        /// <summary>
        /// メッセージの組み合わせの版。クライアントが <see cref="ConnectRequestMessage.ProtocolVersion"/> で送り、サーバーは違えば断る。
        /// 個々のメッセージの版 (<see cref="IMessage.Version"/>) だけだと、つないだ後に初めて読めない種類が届くまで食い違いに気付けないため。
        /// </summary>
        public const ushort Version = 2;

        /// <summary>切断・拒否の理由 (<c>Fps.Core.DisconnectNotice</c> の符号) の最長のバイト数。</summary>
        public const int MaxReasonBytes = 128;

        /// <summary>武器や表面の素材の Id の最長のバイト数 (UTF-8)。</summary>
        public const int MaxContentIdBytes = 64;
    }
}
