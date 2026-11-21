using System;

namespace Shooter.Protocol
{
    // 1 バイトに収まらなくなったので、SnapshotMessage は部分のビットを 2 バイトで書く。
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
        Redeploy = 1 << 7,
        Extraction = 1 << 8,

        // 走っている間と戻っている間は毎 tick 変わるので、変わりにくいよろけと制圧とは別の部分にして、変わった物だけを送る。
        Stamina = 1 << 9,
        Stagger = 1 << 10,
        Suppression = 1 << 11,
        All = Weapon | Loadout | Grenades | CarrySpeed | Knockback | Respawn | Money | Redeploy | Extraction | Stamina | Stagger | Suppression,
    }
}
