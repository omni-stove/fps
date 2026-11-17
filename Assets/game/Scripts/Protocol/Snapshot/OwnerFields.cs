using System;

namespace Fps.Protocol
{
    /// <summary><see cref="OwnerSnapshot"/> の部分。</summary>
    [Flags]
    public enum OwnerFields
    {
        None = 0,
        Weapon = 1 << 0,
        /// <summary>持たせている武器の Id の並び。</summary>
        Loadout = 1 << 1,
        Grenades = 1 << 2,
        /// <summary>持ち物による移動の速さの倍率。</summary>
        CarrySpeed = 1 << 3,
        /// <summary>吹き飛ばされている最中の速さと秒数。</summary>
        Knockback = 1 << 4,
        All = Weapon | Loadout | Grenades | CarrySpeed | Knockback,
    }
}
