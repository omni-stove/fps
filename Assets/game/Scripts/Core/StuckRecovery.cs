namespace Fps.Core
{
    /// <summary>
    /// 動こうとしているのに進めないときに試す立て直し。<see cref="StuckMonitor"/> が軽いものから順に返す。
    /// </summary>
    public enum StuckRecovery
    {
        /// <summary>立て直しは要らない。</summary>
        None,

        /// <summary>経路を引き直す。</summary>
        Repath,

        /// <summary>近くの別の地点へ少し寄り道してから、元の目的地へ向かい直す。</summary>
        Sidestep,

        /// <summary>今の目的地をあきらめる (巡回なら次の地点へ、Search なら打ち切る)。</summary>
        AbandonGoal,

        /// <summary>経路の先か出現地点へ瞬間移動させる。最後の手段。</summary>
        Warp,
    }
}
