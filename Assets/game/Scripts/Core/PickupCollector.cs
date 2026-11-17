using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class PickupCollector
    {
        private static readonly PickupWeaponState[] _noWeapons = Array.Empty<PickupWeaponState>();

        private PickupCollector(
            Side? side,
            int health,
            int maxHealth,
            int armor,
            int maxArmor,
            IReadOnlyList<PickupWeaponState> weapons,
            string currentWeaponId,
            int maxWeapons,
            Inventory inventory)
        {
            Inventory = inventory;
            Side = side;
            Health = health;
            MaxHealth = maxHealth;
            Armor = armor;
            MaxArmor = maxArmor;
            Weapons = weapons;
            CurrentWeaponId = currentWeaponId;
            MaxWeapons = maxWeapons;
        }

        public PickupCollector(Side? side, int health, int maxHealth)
            : this(side, health, maxHealth, 0, 0, null, null, 0, null)
        {
            if (maxHealth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHealth), maxHealth, "maxHealth must be positive");
            }

            if (health < 0 || health > maxHealth)
            {
                throw new ArgumentOutOfRangeException(nameof(health), health, "health must be between 0 and maxHealth");
            }
        }

        public Side? Side { get; }

        public int Health { get; }

        public int MaxHealth { get; }

        public bool IsAlive => Health > 0;

        public int Armor { get; }

        public int MaxArmor { get; }

        public IReadOnlyList<PickupWeaponState> Weapons { get; }

        public string CurrentWeaponId { get; }

        public int MaxWeapons { get; }

        public Inventory Inventory { get; }

        public PickupCollector WithWeapons(IReadOnlyList<PickupWeaponState> weapons, string currentWeaponId, int maxWeapons = 0)
        {
            if (maxWeapons < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxWeapons), maxWeapons, "maxWeapons must not be negative");
            }

            var copy = new List<PickupWeaponState>(weapons != null ? weapons.Count : 0);
            if (weapons != null)
            {
                copy.AddRange(weapons);
            }

            var current = string.IsNullOrEmpty(currentWeaponId) ? null : currentWeaponId;
            if (current != null && IndexIn(copy, current) < 0)
            {
                throw new ArgumentException("currentWeaponId '" + current + "' is not one of weapons", nameof(currentWeaponId));
            }

            return new PickupCollector(Side, Health, MaxHealth, Armor, MaxArmor, copy.Count > 0 ? copy.AsReadOnly() : (IReadOnlyList<PickupWeaponState>)_noWeapons, current, maxWeapons, Inventory);
        }

        public PickupCollector WithArmor(int armor, int maxArmor)
        {
            if (maxArmor <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxArmor), maxArmor, "maxArmor must be positive");
            }

            if (armor < 0 || armor > maxArmor)
            {
                throw new ArgumentOutOfRangeException(nameof(armor), armor, "armor must be between 0 and maxArmor");
            }

            return new PickupCollector(Side, Health, MaxHealth, armor, maxArmor, Weapons, CurrentWeaponId, MaxWeapons, Inventory);
        }

        public PickupCollector WithInventory(Inventory inventory)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            return new PickupCollector(Side, Health, MaxHealth, Armor, MaxArmor, Weapons, CurrentWeaponId, MaxWeapons, inventory);
        }

        public int IndexOfWeapon(string weaponId)
        {
            return Weapons != null && !string.IsNullOrEmpty(weaponId) ? IndexIn(Weapons, weaponId) : -1;
        }

        private static int IndexIn(IReadOnlyList<PickupWeaponState> weapons, string weaponId)
        {
            for (var i = 0; i < weapons.Count; i++)
            {
                if (string.Equals(weapons[i].WeaponId, weaponId, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
