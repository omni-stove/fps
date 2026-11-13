namespace Fps.Core
{
    /// <summary>
    /// <see cref="TeamDeathmatchSession.ApplyDamage"/> の結果。<see cref="FriendlyFire.Reflect"/> で跳ね返したときは、
    /// 撃たれた味方ではなく撃った本人に起きたことを表す。
    /// </summary>
    public enum DamageOutcome
    {
        /// <summary>ダメージを与えなかった (試合中でない・相手がすでに倒れている・味方撃ちが Off の味方・ダメージ 0)。</summary>
        Ignored,

        /// <summary>体力 (ダウン中ならダウン中の体力) を減らしたが倒してはいない。</summary>
        Damaged,

        /// <summary>このダメージで倒した。</summary>
        Killed,

        /// <summary>
        /// このダメージで体力が 0 になり、倒さずにダウンさせた (<see cref="ActionRules.Downed"/> が <see cref="Core.Downed.Revive"/> のときだけ)。
        /// 体力が変わったことの扱いは <see cref="Damaged"/> と同じ。
        /// </summary>
        Downed,
    }
}
