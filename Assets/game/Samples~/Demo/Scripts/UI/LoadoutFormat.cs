using System;
using Shooter.Core;

namespace Shooter.Runtime
{
    public static class LoadoutFormat
    {
        public static string Title => GameText.Get("loadout.title");

        public static string Confirm => GameText.Get("loadout.confirm");

        public static string Sending => GameText.Get("loadout.sending");

        public static string Accepted => GameText.Get("loadout.accepted");

        public static string Saved => GameText.Get("loadout.saved");

        public static string NoOption => GameText.Get("loadout.noOption");

        public static string SlotTitle(int slot)
        {
            if (slot < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(slot), slot, "slot must not be negative");
            }

            return GameText.Format("loadout.slot", slot + 1);
        }

        public static string Rejected(string reason)
        {
            return string.IsNullOrWhiteSpace(reason) ? GameText.Get("loadout.rejected") : GameText.Format("loadout.rejectedWithReason", reason);
        }

        public static string Category(WeaponCategory category)
        {
            switch (category)
            {
                case WeaponCategory.AssaultRifle:
                case WeaponCategory.SubmachineGun:
                case WeaponCategory.Shotgun:
                case WeaponCategory.MarksmanRifle:
                case WeaponCategory.SniperRifle:
                case WeaponCategory.LightMachineGun:
                case WeaponCategory.Handgun:
                case WeaponCategory.Launcher:
                    return GameText.Get("weapon.category." + category);
                default:
                    throw new ArgumentOutOfRangeException(nameof(category), category, "category is not a valid WeaponCategory");
            }
        }
    }
}
