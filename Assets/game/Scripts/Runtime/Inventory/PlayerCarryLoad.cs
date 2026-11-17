using System;
using Shooter.Core;

namespace Shooter.Runtime
{
    public sealed class PlayerCarryLoad : IPlayerCarryLoad
    {
        private readonly WeaponController _weapons;
        private readonly PlayerInventory _inventory;

        public PlayerCarryLoad(WeaponController weapons, PlayerInventory inventory)
        {
            _weapons = weapons;
            _inventory = inventory;
        }

        public float CarrySpeedMultiplier(CarryLoadSpec carry)
        {
            if (carry == null)
            {
                throw new ArgumentNullException(nameof(carry));
            }

            var itemWeight = _inventory != null ? _inventory.CarriedWeight : 0f;
            return _weapons != null ? _weapons.CarrySpeedMultiplier(carry, itemWeight) : carry.SpeedMultiplier(null, null, itemWeight);
        }
    }
}
