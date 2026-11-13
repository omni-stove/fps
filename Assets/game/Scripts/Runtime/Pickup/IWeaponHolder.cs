using System.Collections.Generic;
using Fps.Core;

namespace Fps.Runtime
{
    /// <summary>
    /// 拾える物 (<see cref="Pickup"/>) から武器と予備弾を受け取れる持ち主。プレイヤーの <see cref="WeaponController"/> と bot の <see cref="EnemyAgent"/> が実装する。
    /// 武器を持っていない (<see cref="DescribeWeapons"/> が空) 持ち主も、武器を拾えば持てる。
    /// </summary>
    public interface IWeaponHolder
    {
        /// <summary>持っている武器の今の予備弾。武器を持っていなければ空。currentWeaponId は今持っている武器の Id で、持っていなければ null。</summary>
        IReadOnlyList<PickupWeaponState> DescribeWeapons(out string currentWeaponId);

        /// <summary>
        /// definition の武器を満タンで持つ。同じ Id の武器を既に持っていれば何もせず false。
        /// 定義が不正なら <see cref="WeaponDefinitionException"/> を投げ、今の武器はそのまま残す。
        /// </summary>
        bool AddWeapon(WeaponDefinition definition);

        /// <summary>Id が weaponId の持ち武器に予備弾を amount 足し、実際に足した数を返す。持っていない武器なら 0。</summary>
        int AddReserveAmmo(string weaponId, int amount);
    }
}
