using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.Runtime
{
    public interface IWeaponHolder
    {
        int MaxWeapons { get; }

        IReadOnlyList<PickupWeaponState> DescribeWeapons(out string currentWeaponId);

        bool AddWeapon(WeaponDefinition definition, WeaponAmmo ammo = null, IReadOnlyList<string> attachments = null);

        bool SwapWeapon(string droppedWeaponId, WeaponDefinition definition, WeaponAmmo ammo, IReadOnlyList<string> attachments, out HeldWeapon dropped);

        int AddReserveAmmo(string weaponId, int amount);
    }
}
