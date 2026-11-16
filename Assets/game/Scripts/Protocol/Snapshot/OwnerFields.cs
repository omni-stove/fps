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
        All = Weapon | Loadout | Grenades | CarrySpeed,
    }
}
