namespace Fps.Core
{
    /// <summary>チームデスマッチの進行状態。</summary>
    public enum TeamDeathmatchState
    {
        /// <summary>開始前。キルは数えない。</summary>
        Waiting,

        /// <summary>試合中。</summary>
        Playing,

        /// <summary>終了。以後、状態は変わらない。</summary>
        Ended,
    }
}
