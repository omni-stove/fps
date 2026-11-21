using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Shooter.Runtime
{
    // メインメニューの保存の行と突き合わせられるよう、LoadoutPreferenceStore と同じ頭で出す。
    public static class LoadoutPreferenceLog
    {
        public const string LogTag = LoadoutPreferenceStore.LogTag;

        public static string NotApplied(string reason)
        {
            return $"{LogTag} the saved loadout is not used: {Reason(reason)}; using the scene's loadout";
        }

        public static string WeaponsNotApplied(string reason)
        {
            return $"{LogTag} the saved weapons are not used: {Reason(reason)}; using the scene's weapons";
        }

        public static string AbilitiesNotApplied(string reason)
        {
            return $"{LogTag} the saved abilities are not used: {Reason(reason)}; using the scene's abilities";
        }

        // 片方だけを既定に戻したときに、どちらを保存から当てたかを見分けられるよう、当てなかった側は default と書く
        public static string Applied(IReadOnlyList<string> weaponIds, IReadOnlyList<string> abilityIds)
        {
            return $"{LogTag} using the saved loadout weapons={JoinOrDefault(weaponIds)} abilities={JoinOrDefault(abilityIds)}";
        }

        public static string Unreadable(string key, string problem)
        {
            return $"{LogTag} saved loadout '{key}': {Reason(problem)}; choosing from the match's loadout instead";
        }

        public static string PresetsDropped(string key, int dropped, int maxPresets)
        {
            return $"{LogTag} saved loadout '{key}': {Number(dropped)} presets beyond the limit of {Number(maxPresets)} are not kept";
        }

        public static string PresetSwitched(int index, int count)
        {
            return $"{LogTag} switched to preset {Number(index)} of {Number(count)}";
        }

        public static string PresetAdded(int index, int count, int maxPresets)
        {
            return $"{LogTag} saved a new preset {Number(index)} ({Number(count)} of at most {Number(maxPresets)})";
        }

        public static string PresetRenamed(int index)
        {
            return $"{LogTag} renamed preset {Number(index)}";
        }

        public static string PresetsNotSaved(string reason)
        {
            return $"{LogTag} could not save the presets: {Reason(reason)}";
        }

        public static string NoLoadout()
        {
            return "no loadout is assigned";
        }

        public static string NoAbilityCatalog()
        {
            return "no ability catalog is assigned, so abilities cannot be chosen";
        }

        public static string WeaponCount(int saved, int slots)
        {
            return $"it has {Number(saved)} weapons but the loadout has {Number(slots)} slots";
        }

        public static string NoWeaponId(int slot)
        {
            return $"slot {Number(slot)} has no weapon id";
        }

        public static string WeaponNotInCatalog(int slot, string weaponId)
        {
            return $"weapon '{weaponId}' in slot {Number(slot)} is not in the weapon catalog";
        }

        public static string WeaponNotAccepted(int slot, string weaponId)
        {
            return $"slot {Number(slot)} does not accept weapon '{weaponId}'";
        }

        public static string WeaponLocked(int slot, string weaponId)
        {
            return $"weapon '{weaponId}' in slot {Number(slot)} is not available";
        }

        public static string WeaponsDoNotFit()
        {
            return "the saved weapons do not fit the loadout";
        }

        public static string AbilityCount(int saved, int slots)
        {
            return $"it has {Number(saved)} abilities but there are {Number(slots)} ability slots";
        }

        public static string AbilityNotInCatalog(int slot, string abilityId)
        {
            return $"ability '{abilityId}' in slot {Number(slot)} is not in the ability catalog";
        }

        public static string AbilityLocked(int slot, string abilityId)
        {
            return $"ability '{abilityId}' in slot {Number(slot)} is not available";
        }

        public static string AbilityRepeated(int slot, string abilityId)
        {
            return $"ability '{abilityId}' in slot {Number(slot)} is already chosen in another slot";
        }

        public static string AbilitiesDoNotFit()
        {
            return "the saved abilities do not fit the ability slots";
        }

        private static string Reason(string reason)
        {
            return string.IsNullOrEmpty(reason) ? "-" : reason;
        }

        private static string Number(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string JoinOrDefault(IReadOnlyList<string> ids)
        {
            if (ids == null)
            {
                return "default";
            }

            if (ids.Count == 0)
            {
                return "-";
            }

            var builder = new StringBuilder();
            for (var i = 0; i < ids.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                builder.Append(string.IsNullOrEmpty(ids[i]) ? "-" : ids[i]);
            }

            return builder.ToString();
        }
    }
}
