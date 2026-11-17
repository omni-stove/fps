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
    }
}
