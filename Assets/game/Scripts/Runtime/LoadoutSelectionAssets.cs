using System;
using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.Runtime
{
    // メインメニューとオンラインの HUD が同じアセットから同じ並びを作れるよう、作り方をここにまとめる。
    public static class LoadoutSelectionAssets
    {
        public static bool TryCreate(
            LoadoutDefinition loadout,
            WeaponCatalogAsset catalog,
            IReadOnlyList<string> preferred,
            IReadOnlyList<string> fallback,
            out LoadoutSelection selection,
            out string error,
            Func<string, bool> isAvailable = null)
        {
            selection = null;

            // 参照切れ (破棄されたアセット) も Unity の == null で拾う
            if (loadout == null)
            {
                error = "no loadout is assigned";
                return false;
            }

            if (catalog == null)
            {
                error = "no weapon catalog is assigned, so the loadout cannot be changed";
                return false;
            }

            LoadoutRule rule;
            WeaponCatalog weapons;
            IReadOnlyList<WeaponSpec> defaults;
            try
            {
                rule = loadout.ToRule();
                defaults = loadout.ToSpecs();
                weapons = catalog.ToCatalog();
            }
            catch (WeaponDefinitionException e)
            {
                error = e.Message;
                return false;
            }

            var fallbackIds = fallback;
            if (fallbackIds == null || fallbackIds.Count != rule.SlotCount)
            {
                var ids = new string[defaults.Count];
                for (var i = 0; i < ids.Length; i++)
                {
                    ids[i] = defaults[i].Id;
                }

                fallbackIds = ids;
            }

            selection = new LoadoutSelection(rule, weapons, preferred, fallbackIds, isAvailable);
            error = null;
            return true;
        }

        // カタログが無いのは能力を選ばせないゲームなので、失敗にせず selection を null にして true を返す。武器の選択まで止めないため。
        public static bool TryCreateAbilities(
            LoadoutDefinition loadout,
            AbilityCatalogAsset catalog,
            IReadOnlyList<string> preferred,
            out AbilitySelection selection,
            out string error,
            Func<string, bool> isAvailable = null)
        {
            selection = null;

            // 参照切れ (破棄されたアセット) も Unity の == null で拾う
            if (loadout == null)
            {
                error = "no loadout is assigned";
                return false;
            }

            if (catalog == null)
            {
                error = null;
                return true;
            }

            IReadOnlyList<string> abilityIds;
            try
            {
                abilityIds = catalog.ToIds();
            }
            catch (AbilityDefinitionException e)
            {
                error = e.Message;
                return false;
            }

            selection = new AbilitySelection(abilityIds, preferred, DefaultAbilityIds(loadout), isAvailable, AbilityHost.SlotCount);
            error = null;
            return true;
        }

        // ロードアウトの空の要素はその枠を空にする決まりなので、「なし」にそろえる
        public static IReadOnlyList<string> DefaultAbilityIds(LoadoutDefinition loadout)
        {
            if (loadout == null)
            {
                throw new ArgumentNullException(nameof(loadout));
            }

            var abilities = loadout.Abilities;
            var ids = new string[abilities.Count];
            for (var i = 0; i < ids.Length; i++)
            {
                ids[i] = abilities[i] != null ? abilities[i].Id : AbilitySelection.None;
            }

            return ids;
        }
    }
}
