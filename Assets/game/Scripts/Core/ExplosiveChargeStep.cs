namespace Fps.Core
{
    /// <summary><see cref="ExplosiveCharge.ApplyDamage"/> の 1 回で起きたこと。</summary>
    public enum ExplosiveChargeStep
    {
        /// <summary>何も変わらない (被害が 0・爆発を待っている・爆発した後)。</summary>
        None,

        /// <summary>耐久が減ったが、まだ残っている。</summary>
        Damaged,

        /// <summary>耐久が 0 になり、燃えずに爆発を待ち始めた。</summary>
        Triggered,

        /// <summary>耐久が 0 になり、燃え始めた (<see cref="ExplosivePropSpec.BurnSeconds"/> の後に爆発する)。</summary>
        Ignited,
    }
}
