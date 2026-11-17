using System;

namespace Shooter.Core
{
    public static class PickupJudge
    {
        public static PickupOutcome Judge(PickupSpec spec, PickupCollector collector)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (collector == null)
            {
                throw new ArgumentNullException(nameof(collector));
            }

            if (!collector.IsAlive)
            {
                return PickupOutcome.Rejected(spec.Kind, PickupRejection.CollectorDown);
            }

            if (!spec.Eligibility.Allows(collector.Side))
            {
                return PickupOutcome.Rejected(spec.Kind, PickupRejection.SideNotAllowed);
            }

            switch (spec.Kind)
            {
                case PickupKind.Weapon:
                    return JudgeWeapon(spec, collector);
                case PickupKind.ReserveAmmo:
                    return JudgeAmmo(spec, collector);
                case PickupKind.Health:
                    var missingHealth = collector.MaxHealth - collector.Health;
                    return missingHealth > 0
                        ? PickupOutcome.Healed(Math.Min(spec.Amount, missingHealth))
                        : PickupOutcome.Rejected(spec.Kind, PickupRejection.HealthFull);
                case PickupKind.Armor:
                    if (collector.MaxArmor <= 0)
                    {
                        return PickupOutcome.Rejected(spec.Kind, PickupRejection.NoArmor);
                    }

                    var missingArmor = collector.MaxArmor - collector.Armor;
                    return missingArmor > 0
                        ? PickupOutcome.ArmorAdded(Math.Min(spec.Amount, missingArmor))
                        : PickupOutcome.Rejected(spec.Kind, PickupRejection.ArmorFull);
                case PickupKind.Tag:
                    var ownSide = spec.TagOwnerSide.HasValue && collector.Side.HasValue && spec.TagOwnerSide.Value == collector.Side.Value;
                    return PickupOutcome.TagTaken(spec.TagOwnerId, spec.TagOwnerSide, ownSide);
                case PickupKind.Item:
                    if (collector.Inventory == null)
                    {
                        return PickupOutcome.Rejected(spec.Kind, PickupRejection.NoInventory);
                    }

                    var items = Math.Min(spec.Amount, collector.Inventory.RoomFor(spec.ItemId));
                    return items > 0
                        ? PickupOutcome.ItemsAdded(spec.ItemId, items)
                        : PickupOutcome.Rejected(spec.Kind, PickupRejection.InventoryFull);
                default:
                    throw new ArgumentOutOfRangeException(nameof(spec), spec.Kind, "spec has an unknown PickupKind");
            }
        }

        private static PickupOutcome JudgeWeapon(PickupSpec spec, PickupCollector collector)
        {
            if (collector.Weapons == null)
            {
                return PickupOutcome.Rejected(spec.Kind, PickupRejection.CannotHoldWeapons);
            }

            var index = collector.IndexOfWeapon(spec.WeaponId);
            if (index < 0)
            {
                var full = collector.MaxWeapons > 0 && collector.Weapons.Count >= collector.MaxWeapons && collector.CurrentWeaponId != null;
                var replaced = full ? collector.CurrentWeaponId : null;
                if (collector.Inventory != null && !collector.Inventory.CanHoldWeapon(spec.WeaponId, spec.WeaponWeight, replaced))
                {
                    return PickupOutcome.Rejected(spec.Kind, PickupRejection.InventoryFull);
                }

                return full ? PickupOutcome.WeaponSwapped(spec.WeaponId, collector.CurrentWeaponId) : PickupOutcome.WeaponAdded(spec.WeaponId);
            }

            var added = Math.Min(spec.Amount, collector.Weapons[index].Room);
            return added > 0
                ? PickupOutcome.AmmoAdded(spec.Kind, spec.WeaponId, added)
                : PickupOutcome.Rejected(spec.Kind, PickupRejection.AmmoFull);
        }

        private static PickupOutcome JudgeAmmo(PickupSpec spec, PickupCollector collector)
        {
            var weapons = collector.Weapons;
            if (weapons == null)
            {
                return PickupOutcome.Rejected(spec.Kind, PickupRejection.CannotHoldWeapons);
            }

            if (spec.WeaponId != null)
            {
                var index = collector.IndexOfWeapon(spec.WeaponId);
                return index < 0 ? PickupOutcome.Rejected(spec.Kind, PickupRejection.NoWeapon) : AddAmmo(spec, weapons[index]);
            }

            if (weapons.Count == 0)
            {
                return PickupOutcome.Rejected(spec.Kind, PickupRejection.NoWeapon);
            }

            var current = collector.IndexOfWeapon(collector.CurrentWeaponId);
            if (current >= 0 && weapons[current].Room > 0)
            {
                return AddAmmo(spec, weapons[current]);
            }

            for (var i = 0; i < weapons.Count; i++)
            {
                if (weapons[i].Room > 0)
                {
                    return AddAmmo(spec, weapons[i]);
                }
            }

            return PickupOutcome.Rejected(spec.Kind, PickupRejection.AmmoFull);
        }

        private static PickupOutcome AddAmmo(PickupSpec spec, PickupWeaponState weapon)
        {
            var added = Math.Min(spec.Amount, weapon.Room);
            return added > 0
                ? PickupOutcome.AmmoAdded(spec.Kind, weapon.WeaponId, added)
                : PickupOutcome.Rejected(spec.Kind, PickupRejection.AmmoFull);
        }
    }
}
