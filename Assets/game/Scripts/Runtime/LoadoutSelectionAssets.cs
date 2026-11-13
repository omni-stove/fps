using System.Collections.Generic;
using Fps.Core;

namespace Fps.Runtime
{
    /// <summary>
    /// ロードアウトのアセット (スロットのルールと既定の武器) と武器の一覧のアセットから、選ぶ画面の中身 (<see cref="LoadoutSelection"/>) を作る。
    /// メインメニューとオンラインの HUD が同じアセットから同じ並びを作るためのもの。
    /// </summary>
    public static class LoadoutSelectionAssets
    {
        /// <summary>
        /// loadout のルール (<see cref="LoadoutDefinition.ToRule"/>) と catalog の武器 (<see cref="WeaponCatalogAsset.ToCatalog"/>) から
        /// <see cref="LoadoutSelection"/> を作る。最初に選んでおく並びは preferred、次に fallback、どちらも合わなければ
        /// loadout の既定の武器で決める。どちらかのアセットが未設定・不正なら false と理由を返す。
        /// </summary>
        public static bool TryCreate(
            LoadoutDefinition loadout,
            WeaponCatalogAsset catalog,
            IReadOnlyList<string> preferred,
            IReadOnlyList<string> fallback,
            out LoadoutSelection selection,
            out string error)
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

            selection = new LoadoutSelection(rule, weapons, preferred, fallbackIds);
            error = null;
            return true;
        }
    }
}
