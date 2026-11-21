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

        public static string NoAbility => GameText.Get("loadout.abilityNone");

        public static string PreviousAbility => GameText.Get("loadout.abilityPrevious");

        public static string NextAbility => GameText.Get("loadout.abilityNext");

        public static string PreviousPreset => GameText.Get("loadout.presetPrevious");

        public static string NextPreset => GameText.Get("loadout.presetNext");

        public static string PresetNameLabel => GameText.Get("loadout.presetNameLabel");

        public static string RenamePreset => GameText.Get("loadout.presetRename");

        public static string SaveAsNewPreset => GameText.Get("loadout.presetSaveAsNew");

        public static string PresetSaveFailed => GameText.Get("loadout.presetSaveFailed");

        // index は 0 から。画面には 1 から出す
        public static string PresetTitle(int index, int count)
        {
            if (index < 0 || index >= count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "index must be in [0, " + count + ")");
            }

            return GameText.Format("loadout.presetTitle", index + 1, count);
        }

        // プレイヤーが名前を付けていない組は、並びの番号で呼ぶ
        public static string PresetName(LoadoutPreset preset, int index)
        {
            if (preset == null)
            {
                throw new ArgumentNullException(nameof(preset));
            }

            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "index must not be negative");
            }

            return preset.HasName ? preset.Name : GameText.Format("loadout.presetUnnamed", index + 1);
        }

        public static string PresetAdded(string name)
        {
            return GameText.Format("loadout.presetAdded", name);
        }

        public static string PresetRenamed(string name)
        {
            return GameText.Format("loadout.presetRenamed", name);
        }

        public static string PresetSwitched(string name)
        {
            return GameText.Format("loadout.presetSwitched", name);
        }

        public static string PresetFull(int maxPresets)
        {
            return GameText.Format("loadout.presetFull", maxPresets);
        }

        public static string PresetNameInvalid(int maxNameLength)
        {
            return GameText.Format("loadout.presetNameInvalid", maxNameLength);
        }

        public static string SlotTitle(int slot)
        {
            if (slot < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(slot), slot, "slot must not be negative");
            }

            return GameText.Format("loadout.slot", slot + 1);
        }

        public static string AbilitySlotTitle(int slot)
        {
            if (slot < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(slot), slot, "slot must not be negative");
            }

            return GameText.Format("loadout.abilitySlot", slot + 1);
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
                case WeaponCategory.Melee:
                    return GameText.Get("weapon.category." + category);
                default:
                    throw new ArgumentOutOfRangeException(nameof(category), category, "category is not a valid WeaponCategory");
            }
        }
    }
}
