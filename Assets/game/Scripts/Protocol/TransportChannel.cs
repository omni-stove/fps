namespace Fps.Protocol
{
    /// <summary><see cref="IMatchTransport"/> で送るときの届け方。</summary>
    public enum TransportChannel
    {
        /// <summary>必ず届き、送った順に届く。試合の状態の変化やイベントに使う。</summary>
        Reliable,

        /// <summary>落ちたり順が入れ替わったりしてよい。毎 tick 送り直す入力やスナップショットに使う。</summary>
        Unreliable,
    }
}
