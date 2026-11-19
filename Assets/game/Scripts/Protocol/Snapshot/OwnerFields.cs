using System;

namespace Shooter.Protocol
{
    [Flags]
    public enum OwnerFields
    {
        None = 0,
        Weapon = 1 << 0,
        Loadout = 1 << 1,
        Grenades = 1 << 2,
        CarrySpeed = 1 << 3,
        Knockback = 1 << 4,
        Respawn = 1 << 5,
        Money = 1 << 6,
        All = Weapon | Loadout | Grenades | CarrySpeed | Knockback | Respawn | Money,
    }
}
