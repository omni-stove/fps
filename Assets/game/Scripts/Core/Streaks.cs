namespace Shooter.Core
{
    /// <summary>
    /// 倒れずに倒した数で報酬 (UAV・空爆) を得るストリークを、プレイヤーに付けるか。
    /// </summary>
    public enum Streaks
    {
        /// <summary>付けない。倒した数を数えず、報酬も得ない。</summary>
        Off,

        /// <summary>付ける。数え方と報酬は調整値 (<see cref="StreakSpec"/>) で決める。</summary>
        On,
    }
}
