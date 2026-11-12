namespace Fps.Core
{
    /// <summary><see cref="TeamDeathmatchSession.ApplyDamage"/> の結果。</summary>
    public enum DamageOutcome
    {
        /// <summary>ダメージを与えなかった (試合中でない・相手がすでに倒れている・味方・ダメージ 0)。</summary>
        Ignored,

        /// <summary>体力を減らしたが倒してはいない。</summary>
        Damaged,

        /// <summary>このダメージで倒した。</summary>
        Killed,
    }
}
