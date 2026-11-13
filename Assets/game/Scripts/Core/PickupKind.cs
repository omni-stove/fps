namespace Fps.Core
{
    /// <summary>拾える物の種類。拾ったときに何が起きるかは <see cref="PickupJudge"/> が決める。</summary>
    public enum PickupKind
    {
        /// <summary>武器。持っていなければ持ち武器に加え、持っていればその武器の予備弾になる。</summary>
        Weapon = 0,

        /// <summary>予備弾。</summary>
        ReserveAmmo = 1,

        /// <summary>体力の回復。</summary>
        Health = 2,

        /// <summary>防具 (アーマー) の補充。</summary>
        Armor = 3,

        /// <summary>倒れた人のタグ。拾ったときの得点などはモードが決める。</summary>
        Tag = 4,
    }
}
