namespace Fps.Core
{
    /// <summary>拾えなかった理由。拾えたときは <see cref="None"/>。</summary>
    public enum PickupRejection
    {
        None = 0,

        /// <summary>拾おうとした人が倒れている。</summary>
        CollectorDown = 1,

        /// <summary>拾える陣営の条件 (<see cref="PickupSpec.Eligibility"/>) に合わない。</summary>
        SideNotAllowed = 2,

        /// <summary>武器を持てない人が武器か予備弾を拾おうとした。</summary>
        CannotHoldWeapons = 3,

        /// <summary>弾を足す武器を持っていない。</summary>
        NoWeapon = 4,

        /// <summary>予備弾がもう上限 (か無限) で足せない。</summary>
        AmmoFull = 5,

        /// <summary>体力が満タン。</summary>
        HealthFull = 6,

        /// <summary>防具を持てない人が防具を拾おうとした。</summary>
        NoArmor = 7,

        /// <summary>防具が満タン。</summary>
        ArmorFull = 8,

        /// <summary>もう拾われていて (か再出現を待っていて) 無い。<see cref="PickupJudge"/> は返さず、物を置く側が使う。</summary>
        Gone = 9,

        /// <summary>拾えると判定したが、拾う人に反映できなかった (武器の定義が無いなど)。<see cref="PickupJudge"/> は返さず、物を置く側が使う。</summary>
        ApplyFailed = 10,
    }
}
