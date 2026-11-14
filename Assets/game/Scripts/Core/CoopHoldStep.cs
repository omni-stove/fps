namespace Fps.Core
{
    /// <summary><see cref="CoopHold.Tick"/> の 1 回で起きたこと。</summary>
    public enum CoopHoldStep
    {
        /// <summary>進み始めも中断も完了も無い (進み続けている・人数が足りないまま・済んだあと)。</summary>
        None,

        /// <summary>人数が揃い、進み始めた。</summary>
        Started,

        /// <summary>進んでいる途中で人数が欠け、止まった。</summary>
        Interrupted,

        /// <summary>押し続ける秒数に届き、済んだ。</summary>
        Completed,
    }
}
