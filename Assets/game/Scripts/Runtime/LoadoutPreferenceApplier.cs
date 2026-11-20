using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Runtime
{
    // 武器と能力を別々に検証するのは、オンラインのサーバーと同じく、能力だけが合わなくても武器の選択は当てるため。
    // 既定を呼ぶ側から受け取るのは、シーンに直接つないだ並びがロードアウトより優先されるかどうかを呼ぶ側 (GameBootstrap) が決めるため。
    public static class LoadoutPreferenceApplier
    {
        public static AppliedLoadout Apply(
            LoadoutChoice saved,
            LoadoutDefinition loadout,
            WeaponCatalogAsset weaponCatalog,
            AbilityCatalogAsset abilityCatalog,
            IReadOnlyList<WeaponDefinition> defaultWeapons,
            IReadOnlyList<AbilityDefinition> defaultAbilities,
            Object context = null,
            Func<string, bool> isWeaponAvailable = null,
            Func<string, bool> isAbilityAvailable = null)
        {
            if (defaultWeapons == null)
            {
                throw new ArgumentNullException(nameof(defaultWeapons));
            }

            if (defaultAbilities == null)
            {
                throw new ArgumentNullException(nameof(defaultAbilities));
            }

            // 保存が無いのは選んだことがないだけなので、警告にしない
            if (saved == null)
            {
                return new AppliedLoadout(defaultWeapons, defaultAbilities, false, false);
            }

            // 参照切れ (破棄されたアセット) も Unity の == null で拾う
            if (loadout == null)
            {
                Debug.LogWarning(LoadoutPreferenceLog.NotApplied(LoadoutPreferenceLog.NoLoadout()), context);
                return new AppliedLoadout(defaultWeapons, defaultAbilities, false, false);
            }

            var weapons = TryWeapons(saved.WeaponIds, loadout, weaponCatalog, isWeaponAvailable, out var reason);
            if (weapons == null)
            {
                Debug.LogWarning(LoadoutPreferenceLog.WeaponsNotApplied(reason), context);
            }

            // 能力を保存していない (前の版の) 選択は既定に戻すだけで、合わなかったわけではない
            IReadOnlyList<AbilityDefinition> abilities = null;
            if (saved.AbilityIds != null)
            {
                abilities = TryAbilities(saved.AbilityIds, loadout, abilityCatalog, isAbilityAvailable, out reason);
                if (abilities == null)
                {
                    Debug.LogWarning(LoadoutPreferenceLog.AbilitiesNotApplied(reason), context);
                }
            }

            if (weapons != null || abilities != null)
            {
                Debug.Log(
                    LoadoutPreferenceLog.Applied(weapons != null ? saved.WeaponIds : null, abilities != null ? saved.AbilityIds : null),
                    context);
            }

            return new AppliedLoadout(weapons ?? defaultWeapons, abilities ?? defaultAbilities, weapons != null, abilities != null);
        }

        private static IReadOnlyList<WeaponDefinition> TryWeapons(
            IReadOnlyList<string> saved,
            LoadoutDefinition loadout,
            WeaponCatalogAsset catalog,
            Func<string, bool> isAvailable,
            out string reason)
        {
            if (!LoadoutSelectionAssets.TryCreate(loadout, catalog, saved, null, out var selection, out reason, isAvailable))
            {
                return null;
            }

            if (!selection.RestoredPreference)
            {
                reason = WeaponMismatch(saved, selection, catalog, isAvailable);
                return null;
            }

            reason = selection.Validate();
            if (reason != null)
            {
                return null;
            }

            var definitions = new WeaponDefinition[selection.SlotCount];
            for (var slot = 0; slot < definitions.Length; slot++)
            {
                var id = selection.SelectedAt(slot).Id;
                if (!catalog.TryFind(id, out definitions[slot]))
                {
                    reason = LoadoutPreferenceLog.WeaponNotInCatalog(slot, id);
                    return null;
                }
            }

            return definitions;
        }

        // LoadoutSelection は戻せなかったことしか返さないので、どの枠がなぜ合わないかをここで調べ直す
        private static string WeaponMismatch(
            IReadOnlyList<string> saved,
            LoadoutSelection selection,
            WeaponCatalogAsset catalog,
            Func<string, bool> isAvailable)
        {
            if (saved.Count != selection.SlotCount)
            {
                return LoadoutPreferenceLog.WeaponCount(saved.Count, selection.SlotCount);
            }

            var all = catalog.ToCatalog().All;
            for (var slot = 0; slot < saved.Count; slot++)
            {
                var id = saved[slot];
                if (string.IsNullOrEmpty(id))
                {
                    return LoadoutPreferenceLog.NoWeaponId(slot);
                }

                if (!ContainsWeapon(all, id))
                {
                    return LoadoutPreferenceLog.WeaponNotInCatalog(slot, id);
                }

                if (!ContainsWeapon(selection.OptionsAt(slot), id))
                {
                    return LoadoutPreferenceLog.WeaponNotAccepted(slot, id);
                }

                if (isAvailable != null && !isAvailable(id))
                {
                    return LoadoutPreferenceLog.WeaponLocked(slot, id);
                }
            }

            return LoadoutPreferenceLog.WeaponsDoNotFit();
        }

        private static IReadOnlyList<AbilityDefinition> TryAbilities(
            IReadOnlyList<string> saved,
            LoadoutDefinition loadout,
            AbilityCatalogAsset catalog,
            Func<string, bool> isAvailable,
            out string reason)
        {
            // TryCreateAbilities はカタログが無いゲームを失敗にしないが、能力を保存してあるのに選べる一覧が無いのは当てられない
            if (catalog == null)
            {
                reason = LoadoutPreferenceLog.NoAbilityCatalog();
                return null;
            }

            if (!LoadoutSelectionAssets.TryCreateAbilities(loadout, catalog, saved, out var selection, out reason, isAvailable))
            {
                return null;
            }

            if (!selection.RestoredPreference)
            {
                reason = AbilityMismatch(saved, selection, catalog, isAvailable);
                return null;
            }

            var definitions = new AbilityDefinition[selection.SlotCount];
            for (var slot = 0; slot < definitions.Length; slot++)
            {
                var id = selection.SelectedAt(slot);
                if (id == AbilitySelection.None)
                {
                    continue;
                }

                if (!catalog.TryFind(id, out definitions[slot]))
                {
                    reason = LoadoutPreferenceLog.AbilityNotInCatalog(slot, id);
                    return null;
                }
            }

            reason = null;
            return definitions;
        }

        private static string AbilityMismatch(
            IReadOnlyList<string> saved,
            AbilitySelection selection,
            AbilityCatalogAsset catalog,
            Func<string, bool> isAvailable)
        {
            if (saved.Count != selection.SlotCount)
            {
                return LoadoutPreferenceLog.AbilityCount(saved.Count, selection.SlotCount);
            }

            var ids = catalog.ToIds();
            var chosen = new HashSet<string>(StringComparer.Ordinal);
            for (var slot = 0; slot < saved.Count; slot++)
            {
                var id = saved[slot];
                if (id == AbilitySelection.None)
                {
                    continue;
                }

                if (!ContainsId(ids, id))
                {
                    return LoadoutPreferenceLog.AbilityNotInCatalog(slot, id);
                }

                if (isAvailable != null && !isAvailable(id))
                {
                    return LoadoutPreferenceLog.AbilityLocked(slot, id);
                }

                if (!chosen.Add(id))
                {
                    return LoadoutPreferenceLog.AbilityRepeated(slot, id);
                }
            }

            return LoadoutPreferenceLog.AbilitiesDoNotFit();
        }

        private static bool ContainsWeapon(IReadOnlyList<WeaponSpec> weapons, string id)
        {
            for (var i = 0; i < weapons.Count; i++)
            {
                if (string.Equals(weapons[i].Id, id, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsId(IReadOnlyList<string> ids, string id)
        {
            for (var i = 0; i < ids.Count; i++)
            {
                if (string.Equals(ids[i], id, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
