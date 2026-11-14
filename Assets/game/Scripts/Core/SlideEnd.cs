namespace Fps.Core
{
    /// <summary><see cref="SlideMotion"/> のスライディングが終わった理由。</summary>
    public enum SlideEnd
    {
        /// <summary>まだ終わっていない (一度も滑っていないか、滑っている最中)。</summary>
        None,

        /// <summary>速さが <see cref="SlideMotion.EndSpeed"/> まで落ちた。</summary>
        Stopped,

        /// <summary><see cref="SlideMotion.MaxSeconds"/> 滑った。</summary>
        TimedOut,

        /// <summary>ジャンプした。</summary>
        Jumped,

        /// <summary>足場から離れて宙に出た。</summary>
        Airborne,

        /// <summary>外の理由 (ルールを Off にした・死亡・ほかの動きに替わった) でやめた。</summary>
        Cancelled,
    }
}
