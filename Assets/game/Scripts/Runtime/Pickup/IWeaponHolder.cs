using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.Runtime
{
    /// <summary>
    /// 拾える物 (<see cref="Pickup"/>) から武器と予備弾を受け取れる持ち主。プレイヤーの <see cref="WeaponController"/> と bot の <see cref="EnemyAgent"/> が実装する。
    /// 武器を持っていない (<see cref="DescribeWeapons"/> が空) 持ち主も、武器を拾えば持てる。
    /// </summary>
    public interface IWeaponHolder
    {
        /// <summary>同時に持てる武器の数。0 なら上限なし。上限に届いていて別の武器を拾うと、<see cref="SwapWeapon"/> で今の武器と持ち替える。</summary>
        int MaxWeapons { get; }

        /// <summary>持っている武器の今の予備弾。武器を持っていなければ空。currentWeaponId は今持っている武器の Id で、持っていなければ null。</summary>
        IReadOnlyList<PickupWeaponState> DescribeWeapons(out string currentWeaponId);

        /// <summary>
        /// definition の武器を持つ。ammo があればその弾数 (誰かが落とした武器)、null なら満タン。attachments はその武器に付いていた物 (持ち物のアイテムの Id) で、
        /// 付かない物は捨てる。同じ Id の武器を既に持っていれば何もせず false。
        /// 定義が不正なら <see cref="WeaponDefinitionException"/> を投げ、今の武器はそのまま残す。
        /// </summary>
        bool AddWeapon(WeaponDefinition definition, WeaponAmmo ammo = null, IReadOnlyList<string> attachments = null);

        /// <summary>
        /// Id が droppedWeaponId の持ち武器を手放し、同じ番号に definition の武器 (弾数と付いていた物は <see cref="AddWeapon"/> と同じ) を持って構える。
        /// 手放した武器の定義と残弾と付けていた物を dropped に入れて true。droppedWeaponId を持っていない・definition と同じ Id の武器を既に持っているなら何もせず false。
        /// 定義が不正なら <see cref="WeaponDefinitionException"/> を投げ、今の武器はそのまま残す。
        /// </summary>
        bool SwapWeapon(string droppedWeaponId, WeaponDefinition definition, WeaponAmmo ammo, IReadOnlyList<string> attachments, out HeldWeapon dropped);

        /// <summary>Id が weaponId の持ち武器に予備弾を amount 足し、実際に足した数を返す。持っていない武器なら 0。</summary>
        int AddReserveAmmo(string weaponId, int amount);
    }
}
