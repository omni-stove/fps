namespace Fps.Core
{
    /// <summary><see cref="WaveSchedule"/> の段階。</summary>
    public enum WavePhase
    {
        /// <summary>まだ始めていない。</summary>
        Waiting,

        /// <summary>波の敵を出して戦っている。</summary>
        Fighting,

        /// <summary>波を全滅させ、次の波までの休憩中。</summary>
        Resting,

        /// <summary>最後の波を全滅させて終わった。</summary>
        Completed,
    }
}
