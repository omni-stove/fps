namespace Fps.Core
{
    /// <summary><see cref="DownedTracker.AddReviveProgress"/> の結果。</summary>
    public enum ReviveStep
    {
        /// <summary>進めなかった (ダウンしていない・別の人がこの Tick ですでに起こしている)。</summary>
        Rejected,

        /// <summary>累計を進めたが、まだ起き上がっていない。</summary>
        Progressed,

        /// <summary>累計が蘇生の秒数に達した。ダウンの状態は呼んだ側が消す。</summary>
        Completed,
    }
}
