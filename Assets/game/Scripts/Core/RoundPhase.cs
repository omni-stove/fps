namespace Fps.Core
{
    /// <summary><see cref="RoundSession"/> の進み具合。</summary>
    public enum RoundPhase
    {
        /// <summary>試合の開始前。</summary>
        Waiting,

        /// <summary>ラウンドの開始前の待ち (買い・準備)。キルも得点も数えない。</summary>
        Preparing,

        /// <summary>ラウンド中。</summary>
        Live,

        /// <summary>どれかの陣営が先取数に届いて試合が終わった。</summary>
        Ended,
    }
}
