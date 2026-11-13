using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using UnityEditor;
using UnityEngine;

namespace Fps.Editor
{
    /// <summary>
    /// 武器のアセット (<see cref="RiflePath"/>・<see cref="HandgunPath"/> の <see cref="WeaponDefinition"/> と、
    /// それを rifle・handgun の順に並べた <see cref="CatalogPath"/> の <see cref="WeaponCatalogAsset"/>) を作るか上書きする。
    /// 既存のアセットは GUID を保って中身だけ更新するので、参照しているシーンやプレハブは壊れない。
    /// 何度実行しても同じ結果になる。値が不正なら保存せずに例外を投げる (batchmode では Unity が終了コード 1 で終わる)。
    /// Editor を閉じた状態で次のように呼べる:
    /// <code>unity -batchmode -quit -projectPath . -executeMethod Fps.Editor.WeaponAssetBuilder.Build</code>
    /// </summary>
    public static class WeaponAssetBuilder
    {
        public const string RiflePath = "Assets/game/Weapons/Rifle.asset";
        public const string HandgunPath = "Assets/game/Weapons/Handgun.asset";
        public const string CatalogPath = "Assets/game/Weapons/WeaponCatalog.asset";

        public const string MenuPath = "Fps/Build Weapon Assets";
        public const string LogTag = "[WeaponAssetBuilder]";

        public const string RifleId = "rifle";
        public const string HandgunId = "handgun";

        /// <summary>
        /// rifle の値 (Auto、30/90 発、0.1 秒、リロード 2 秒、25 ダメージ、射程 100m、20〜60m で 0.5 倍まで減衰)。
        /// オフラインのプレイヤー (<see cref="WeaponController"/>) と敵 (<see cref="EnemyAgent"/>) が使う。
        /// </summary>
        public static WeaponSpec RifleSpec()
        {
            return new WeaponSpec(
                RifleId,
                "Rifle",
                FireMode.Auto,
                30,
                90,
                0.1f,
                2f,
                25,
                100f,
                new DamageFalloff(20f, 60f, 0.5f));
        }

        /// <summary>
        /// <see cref="RiflePath"/> の rifle の定義を読む。シーン生成スクリプトが <see cref="WeaponController"/> や
        /// <see cref="EnemyAgent"/> に配線するのに使う。無ければ (または別の型なら) 先に <see cref="Build"/> を走らせるよう例外を投げる。
        /// 値が不正でも例外を投げ、壊れた定義をシーンに配線しない。
        /// </summary>
        public static WeaponDefinition LoadRifle()
        {
            return LoadDefinition(RiflePath);
        }

        /// <summary>path の <see cref="WeaponDefinition"/> を読み、<see cref="WeaponDefinition.ToSpec"/> に通ることを確かめて返す。</summary>
        public static WeaponDefinition LoadDefinition(string path)
        {
            path = SceneBuildHelpers.ValidateAssetPath(path);

            var definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
            if (definition == null)
            {
                throw new InvalidOperationException(
                    $"weapon definition '{path}' was not found; run {MenuPath} (Fps.Editor.WeaponAssetBuilder.Build) first");
            }

            definition.ToSpec();
            return definition;
        }

        /// <summary>handgun の値 (Semi、12/48 発、0.2 秒、リロード 1.5 秒、34 ダメージ、射程 60m、15〜40m で 0.6 倍まで減衰)。</summary>
        public static WeaponSpec HandgunSpec()
        {
            return new WeaponSpec(
                HandgunId,
                "Handgun",
                FireMode.Semi,
                12,
                48,
                0.2f,
                1.5f,
                34,
                60f,
                new DamageFalloff(15f, 40f, 0.6f));
        }

        [MenuItem(MenuPath)]
        public static void Build()
        {
            BuildAt(RiflePath, HandgunPath, CatalogPath);
        }

        /// <summary>
        /// riflePath・handgunPath に武器の定義を、catalogPath にそれを並べたカタログを作るか上書きして保存し、カタログを返す。
        /// 3 つのパスは互いに異なる Assets/ 以下の .asset であること。
        /// </summary>
        public static WeaponCatalogAsset BuildAt(string riflePath, string handgunPath, string catalogPath)
        {
            riflePath = SceneBuildHelpers.ValidateAssetPath(riflePath);
            handgunPath = SceneBuildHelpers.ValidateAssetPath(handgunPath);
            catalogPath = SceneBuildHelpers.ValidateAssetPath(catalogPath);
            RequireDistinct(riflePath, handgunPath, catalogPath);

            var rifleSpec = RifleSpec();
            var handgunSpec = HandgunSpec();
            var rifle = SceneBuildHelpers.CreateOrUpdateAsset<WeaponDefinition>(riflePath, d => Configure(d, rifleSpec));
            var handgun = SceneBuildHelpers.CreateOrUpdateAsset<WeaponDefinition>(handgunPath, d => Configure(d, handgunSpec));
            var catalog = SceneBuildHelpers.CreateOrUpdateAsset<WeaponCatalogAsset>(
                catalogPath, c => Configure(c, new[] { rifle, handgun }));

            Debug.Log($"{LogTag} saved '{riflePath}', '{handgunPath}' and '{catalogPath}'");
            return catalog;
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
            definition.DisplayName = spec.DisplayName;
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

        private static void RequireDistinct(params string[] paths)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in paths)
            {
                if (!seen.Add(path))
                {
                    throw new ArgumentException($"weapon asset path '{path}' is used more than once");
                }
            }
        }
    }
}
