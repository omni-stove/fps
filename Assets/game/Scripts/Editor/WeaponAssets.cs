using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using UnityEditor;

namespace Fps.Editor
{
    /// <summary>
    /// 武器のアセット (<see cref="WeaponDefinition"/>・<see cref="WeaponCatalogAsset"/>・<see cref="LoadoutDefinition"/>) に値を入れ、読み直して確かめる。
    /// 値の中身 (どの武器を作るか) は持たず、JSON の取り込みやゲームのビルダーがここを通して保存する。
    /// </summary>
    public static class WeaponAssets
    {
        public const string CatalogFileName = "WeaponCatalog.asset";

        /// <summary>武器のアセットを置く、出力先ルート (<see cref="FpsKitRoot.Output"/>) の下のフォルダ。</summary>
        public static string Folder => FpsKitRoot.OutputPath("Weapons");

        /// <summary><see cref="Folder"/> の下の武器の一覧 (<see cref="CatalogFileName"/>)。</summary>
        public static string CatalogPath => Folder + "/" + CatalogFileName;

        /// <summary>path の <see cref="WeaponDefinition"/> を読み、<see cref="WeaponDefinition.ToSpec"/> に通ることを確かめて返す。</summary>
        /// <exception cref="InvalidOperationException">path に武器の定義が無い。</exception>
        public static WeaponDefinition LoadDefinition(string path)
        {
            path = SceneBuildHelpers.ValidateAssetPath(path);

            var definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
            if (definition == null)
            {
                throw new InvalidOperationException($"weapon definition '{path}' was not found; build or import the weapons first");
            }

            definition.ToSpec();
            return definition;
        }

        /// <summary>path の <see cref="WeaponCatalogAsset"/> を読み、<see cref="WeaponCatalogAsset.ToCatalog"/> に通ることを確かめて返す。</summary>
        /// <exception cref="InvalidOperationException">path に武器の一覧が無い。</exception>
        public static WeaponCatalogAsset LoadCatalog(string path)
        {
            path = SceneBuildHelpers.ValidateAssetPath(path);

            var catalog = AssetDatabase.LoadAssetAtPath<WeaponCatalogAsset>(path);
            if (catalog == null)
            {
                throw new InvalidOperationException($"weapon catalog '{path}' was not found; build or import the weapons first");
            }

            catalog.ToCatalog();
            return catalog;
        }

        /// <summary>path の <see cref="LoadoutDefinition"/> を読み、<see cref="LoadoutDefinition.ToSpecs"/> に通ることを確かめて返す。</summary>
        /// <exception cref="InvalidOperationException">path にロードアウトが無い。</exception>
        public static LoadoutDefinition LoadLoadout(string path)
        {
            path = SceneBuildHelpers.ValidateAssetPath(path);

            var loadout = AssetDatabase.LoadAssetAtPath<LoadoutDefinition>(path);
            if (loadout == null)
            {
                throw new InvalidOperationException($"loadout '{path}' was not found; build or import the weapons first");
            }

            loadout.ToSpecs();
            return loadout;
        }

        /// <summary>
        /// catalog から、Id が weaponId の武器の定義を引く。placementId は失敗したときにどの配置かを示すためだけに使う。
        /// </summary>
        /// <exception cref="KeyNotFoundException">weaponId の武器が catalog に無いとき。メッセージに配置の Id と武器の Id を含む。</exception>
        /// <exception cref="WeaponDefinitionException">catalog が不正なとき。</exception>
        public static WeaponDefinition FindInCatalog(WeaponCatalogAsset catalog, string placementId, string weaponId)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (!catalog.TryFind(weaponId, out var definition))
            {
                throw new KeyNotFoundException(
                    $"placement '{placementId}' uses weapon id '{weaponId}', which is not in weapon catalog '{catalog.name}'");
            }

            return definition;
        }

        /// <summary>definition の中身を spec の値にし、<see cref="WeaponDefinition.ToSpec"/> に通ることを確かめる。</summary>
        public static void Configure(WeaponDefinition definition, WeaponSpec spec)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            definition.Id = spec.Id;
            definition.NameKey = spec.NameKey;
            definition.Category = spec.Category;
            definition.FireMode = spec.FireMode;
            definition.MagazineSize = spec.MagazineSize;
            definition.ReserveAmmo = spec.ReserveAmmo;
            definition.FireInterval = spec.FireInterval;
            definition.ReloadSeconds = spec.ReloadSeconds;
            definition.Damage = spec.Damage;
            definition.Range = spec.Range;
            definition.FalloffStart = spec.Falloff.StartDistance;
            definition.FalloffEnd = spec.Falloff.EndDistance;
            definition.FalloffMinMultiplier = spec.Falloff.MinMultiplier;
            definition.ExplosionRadius = spec.ExplosionRadius;
            definition.AdsFieldOfView = spec.AdsFieldOfView;
            definition.BlindFireSpreadDegrees = spec.BlindFireSpreadDegrees;
            definition.ActiveReloadWindowStart = spec.ActiveReloadWindowStart;
            definition.ActiveReloadWindowLength = spec.ActiveReloadWindowLength;
            definition.ActiveReloadFailSeconds = spec.ActiveReloadFailSeconds;
            definition.RecoilPitchDegrees = spec.RecoilPitchDegrees;
            definition.RecoilYawDegrees = spec.RecoilYawDegrees;
            definition.RecoilRecoveryPerSecond = spec.RecoilRecoveryPerSecond;
            definition.HipSpreadDegrees = spec.HipSpreadDegrees;
            definition.AdsSpreadDegrees = spec.AdsSpreadDegrees;
            definition.MoveSpreadDegrees = spec.MoveSpreadDegrees;
            definition.RecoilRecoveryDelaySeconds = spec.RecoilRecoveryDelaySeconds;

            // spec は検査済みだが、アセットの形で読み直せることも確かめる。不正なら保存しない。
            definition.ToSpec();
        }

        /// <summary>catalog の一覧を weapons にし、<see cref="WeaponCatalogAsset.ToCatalog"/> に通ることを確かめる。</summary>
        public static void Configure(WeaponCatalogAsset catalog, IEnumerable<WeaponDefinition> weapons)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (weapons == null)
            {
                throw new ArgumentNullException(nameof(weapons));
            }

            catalog.SetWeapons(weapons);

            // 不正ならここで WeaponDefinitionException になり、壊れたアセットを保存しない。
            catalog.ToCatalog();
        }

        /// <summary>loadout のスロットを slots に、持ち替え秒数を switchSeconds にし、<see cref="LoadoutDefinition.ToSpecs"/> に通ることを確かめる。</summary>
        public static void Configure(LoadoutDefinition loadout, IEnumerable<LoadoutSlotDefinition> slots, float switchSeconds)
        {
            if (loadout == null)
            {
                throw new ArgumentNullException(nameof(loadout));
            }

            if (slots == null)
            {
                throw new ArgumentNullException(nameof(slots));
            }

            loadout.SetSlots(slots);
            loadout.SwitchSeconds = switchSeconds;

            // 不正ならここで WeaponDefinitionException になり、壊れたアセットを保存しない。
            loadout.ToSpecs();
        }
    }
}
