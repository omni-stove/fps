using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using UnityEditor;
using UnityEngine;

namespace Fps.Editor
{
    /// <summary>
    /// 武器のアセット (<see cref="RiflePath"/>・<see cref="HandgunPath"/> の <see cref="WeaponDefinition"/>、
    /// それを rifle・handgun の順に並べた <see cref="CatalogPath"/> の <see cref="WeaponCatalogAsset"/>、
    /// それをスロットに入れた <see cref="LoadoutPath"/> の <see cref="LoadoutDefinition"/>、カタログに入れない <see cref="LauncherPath"/> の
    /// <see cref="WeaponDefinition"/>) を作るか上書きする。
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
        public const string LoadoutPath = "Assets/game/Weapons/DefaultLoadout.asset";
        public const string LauncherPath = "Assets/game/Weapons/Launcher.asset";

        public const string MenuPath = "Fps/Build Weapon Assets";
        public const string LogTag = "[WeaponAssetBuilder]";

        // 再ビルドしても Id が変わらないよう固定する。サーバーとクライアントはこの Id で武器を突き合わせる
        public const string RifleId = "6c1f3b2a-8d4e-4f7a-9b0c-2e5d7a1f4c83";
        public const string HandgunId = "b4e2a9d7-3c16-4a58-8f2e-7d0b9c6a1e45";
        public const string LauncherId = "e7a3c5d1-2f84-4b69-a0d3-5c8e1b7f2a96";

        /// <summary>launcher の 1 発が建造物に被害を広げる半径 (m)。</summary>
        public const float LauncherExplosionRadius = 4f;

        /// <summary>launcher の連射間隔 (秒)。弾倉が 1 発なので、実際の間隔はリロード時間で決まる。</summary>
        public const float LauncherFireInterval = 0.5f;

        /// <summary>rifle を覗き込み切ったときの縦の視野角 (度)。共通の初期値 (<see cref="AdsZoom.DefaultFieldOfView"/>) より狭く、遠くを狙いやすくする。</summary>
        public const float RifleAdsFieldOfView = 45f;

        /// <summary>handgun を覗き込み切ったときの縦の視野角 (度)。近距離で使うので rifle より広い。</summary>
        public const float HandgunAdsFieldOfView = 55f;

        /// <summary>rifle を隠れたまま撃ったときの散らばりの最大角度 (度)。</summary>
        public const float RifleBlindFireSpreadDegrees = 8f;

        /// <summary>rifle のアクティブリロードの判定の幅の始まり (リロード時間に対する割合)。</summary>
        public const float RifleActiveReloadWindowStart = 0.5f;

        /// <summary>rifle のアクティブリロードの判定の幅の長さ (リロード時間に対する割合)。0.5〜0.65 になる。</summary>
        public const float RifleActiveReloadWindowLength = 0.15f;

        /// <summary>rifle のアクティブリロードに失敗したときに足される秒数。</summary>
        public const float RifleActiveReloadFailSeconds = 0.6f;

        /// <summary>handgun を隠れたまま撃ったときの散らばりの最大角度 (度)。片手で構えるので rifle より広い。</summary>
        public const float HandgunBlindFireSpreadDegrees = 12f;

        /// <summary>handgun のアクティブリロードの判定の幅の始まり (リロード時間に対する割合)。</summary>
        public const float HandgunActiveReloadWindowStart = 0.45f;

        /// <summary>handgun のアクティブリロードの判定の幅の長さ (リロード時間に対する割合)。0.45〜0.65 になる。</summary>
        public const float HandgunActiveReloadWindowLength = 0.2f;

        /// <summary>handgun のアクティブリロードに失敗したときに足される秒数。</summary>
        public const float HandgunActiveReloadFailSeconds = 0.4f;

        /// <summary>
        /// rifle の値 (AssaultRifle、Auto、30/90 発、0.1 秒、リロード 2 秒、25 ダメージ、射程 100m、20〜60m で 0.5 倍まで減衰、
        /// ADS の視野角 <see cref="RifleAdsFieldOfView"/>、ブラインドファイアの散らばり 8°、アクティブリロードの判定の幅 0.5〜0.65・失敗 0.6 秒)。
        /// オフラインのプレイヤー (<see cref="WeaponController"/>) と、配置でこの Id を指定した敵 (<see cref="EnemyAgent"/>) が使う。
        /// </summary>
        public static WeaponSpec RifleSpec()
        {
            return new WeaponSpec(
                RifleId,
                "Rifle",
                WeaponCategory.AssaultRifle,
                FireMode.Auto,
                30,
                90,
                0.1f,
                2f,
                25,
                100f,
                new DamageFalloff(20f, 60f, 0.5f),
                adsFieldOfView: RifleAdsFieldOfView,
                blindFireSpreadDegrees: RifleBlindFireSpreadDegrees,
                activeReloadWindowStart: RifleActiveReloadWindowStart,
                activeReloadWindowLength: RifleActiveReloadWindowLength,
                activeReloadFailSeconds: RifleActiveReloadFailSeconds);
        }

        /// <summary>
        /// <see cref="RiflePath"/> の rifle の定義を読む (プレイヤーの武器は <see cref="LoadPlayerLoadout"/>、敵の武器は
        /// <see cref="LoadCatalog"/> の一覧から配置ごとに引く)。無ければ (または別の型なら) 先に <see cref="Build"/> を走らせるよう例外を投げる。
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

        /// <summary>
        /// <see cref="CatalogPath"/> の武器の一覧を読む。シーン生成スクリプトが敵の配置ごとの武器を引くのに使う。
        /// 無ければ (または別の型なら) 先に <see cref="Build"/> を走らせるよう例外を投げる。
        /// 一覧が不正なら <see cref="WeaponDefinitionException"/> を投げ、壊れた定義をシーンに配線しない。
        /// </summary>
        public static WeaponCatalogAsset LoadCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<WeaponCatalogAsset>(CatalogPath);
            if (catalog == null)
            {
                throw new InvalidOperationException(
                    $"weapon catalog '{CatalogPath}' was not found; run {MenuPath} (Fps.Editor.WeaponAssetBuilder.Build) first");
            }

            catalog.ToCatalog();
            return catalog;
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

        /// <summary>
        /// handgun の値 (Handgun、Semi、12/48 発、0.2 秒、リロード 1.5 秒、34 ダメージ、射程 60m、15〜40m で 0.6 倍まで減衰、
        /// ADS の視野角 <see cref="HandgunAdsFieldOfView"/>、ブラインドファイアの散らばり 12°、アクティブリロードの判定の幅 0.45〜0.65・失敗 0.4 秒)。
        /// </summary>
        public static WeaponSpec HandgunSpec()
        {
            return new WeaponSpec(
                HandgunId,
                "Handgun",
                WeaponCategory.Handgun,
                FireMode.Semi,
                12,
                48,
                0.2f,
                1.5f,
                34,
                60f,
                new DamageFalloff(15f, 40f, 0.6f),
                adsFieldOfView: HandgunAdsFieldOfView,
                blindFireSpreadDegrees: HandgunBlindFireSpreadDegrees,
                activeReloadWindowStart: HandgunActiveReloadWindowStart,
                activeReloadWindowLength: HandgunActiveReloadWindowLength,
                activeReloadFailSeconds: HandgunActiveReloadFailSeconds);
        }

        /// <summary>
        /// launcher の値 (Launcher、Semi、1/4 発、<see cref="LauncherFireInterval"/> 秒、リロード 3 秒、120 ダメージ、射程 150m、減衰なし、
        /// 建造物への被害の半径 <see cref="LauncherExplosionRadius"/>m)。建造物を壊す確認用で、既定のロードアウトとカタログには入れない。
        /// </summary>
        public static WeaponSpec LauncherSpec()
        {
            return new WeaponSpec(
                LauncherId,
                "Launcher",
                WeaponCategory.Launcher,
                FireMode.Semi,
                1,
                4,
                LauncherFireInterval,
                3f,
                120,
                150f,
                DamageFalloff.None,
                explosionRadius: LauncherExplosionRadius);
        }

        /// <summary>
        /// <see cref="LauncherPath"/> の launcher の定義を読む。無ければ (または別の型なら) 先に <see cref="Build"/> を走らせるよう例外を投げる。
        /// 値が不正でも例外を投げ、壊れた定義をシーンに配線しない。
        /// </summary>
        public static WeaponDefinition LoadLauncher()
        {
            return LoadDefinition(LauncherPath);
        }

        /// <summary>
        /// <see cref="LoadoutPath"/> のデフォルトロードアウトを読む。無ければ (または別の型なら) 先に <see cref="Build"/> を走らせるよう
        /// 例外を投げる。値が不正でも例外を投げ、壊れたロードアウトを配線しない。
        /// </summary>
        public static LoadoutDefinition LoadDefaultLoadout()
        {
            return LoadLoadout(LoadoutPath);
        }

        /// <summary>
        /// オフラインのプレイヤーの既定ロードアウト。<see cref="LoadDefaultLoadout"/> のスロット順の武器の定義 (rifle, handgun) で、
        /// シーン生成スクリプトが <see cref="GameBootstrap.WeaponDefinitions"/> に配線する。
        /// ネット対戦と同じアセットから引くので、並びを変えるときは <see cref="DefaultLoadoutSlots"/> だけを直せばよい。
        /// 持ち替え秒数 (<see cref="LoadoutDefinition.SwitchSeconds"/>) はオフラインでは使わない。
        /// </summary>
        public static WeaponDefinition[] LoadPlayerLoadout()
        {
            var loadout = LoadDefaultLoadout();
            var weapons = new WeaponDefinition[loadout.Slots.Count];
            for (var i = 0; i < weapons.Length; i++)
            {
                // LoadDefaultLoadout が ToSpecs を通しているので、どのスロットにも武器がある。
                weapons[i] = loadout.Slots[i].Weapon;
            }

            return weapons;
        }

        /// <summary>path の <see cref="LoadoutDefinition"/> を読み、<see cref="LoadoutDefinition.ToSpecs"/> に通ることを確かめて返す。</summary>
        public static LoadoutDefinition LoadLoadout(string path)
        {
            path = SceneBuildHelpers.ValidateAssetPath(path);

            var loadout = AssetDatabase.LoadAssetAtPath<LoadoutDefinition>(path);
            if (loadout == null)
            {
                throw new InvalidOperationException(
                    $"loadout '{path}' was not found; run {MenuPath} (Fps.Editor.WeaponAssetBuilder.Build) first");
            }

            loadout.ToSpecs();
            return loadout;
        }

        /// <summary>デフォルトロードアウトのスロット 0 が許す種別。Handgun 以外のすべて (種別が増えたら自動で入る)。</summary>
        public static WeaponCategory[] DefaultPrimaryCategories()
        {
            var categories = new List<WeaponCategory>();
            foreach (WeaponCategory category in Enum.GetValues(typeof(WeaponCategory)))
            {
                if (category != WeaponCategory.Handgun)
                {
                    categories.Add(category);
                }
            }

            return categories.ToArray();
        }

        /// <summary>デフォルトロードアウトのスロット 1 が許す種別。Handgun だけ。</summary>
        public static WeaponCategory[] DefaultSecondaryCategories()
        {
            return new[] { WeaponCategory.Handgun };
        }

        /// <summary>デフォルトロードアウトのスロット。0 番は <see cref="DefaultPrimaryCategories"/> で rifle、1 番は <see cref="DefaultSecondaryCategories"/> で handgun。</summary>
        public static LoadoutSlotDefinition[] DefaultLoadoutSlots(WeaponDefinition rifle, WeaponDefinition handgun)
        {
            return new[]
            {
                new LoadoutSlotDefinition(DefaultPrimaryCategories(), rifle),
                new LoadoutSlotDefinition(DefaultSecondaryCategories(), handgun),
            };
        }

        [MenuItem(MenuPath)]
        public static void Build()
        {
            BuildAt(RiflePath, HandgunPath, CatalogPath, LoadoutPath);
            BuildLauncherAt(LauncherPath);
        }

        /// <summary>
        /// path に launcher の定義を作るか上書きして保存し、返す。カタログとロードアウトには入れないので、
        /// <see cref="BuildAt"/> のパスと重ならない Assets/ 以下の .asset であること。
        /// </summary>
        public static WeaponDefinition BuildLauncherAt(string path)
        {
            path = SceneBuildHelpers.ValidateAssetPath(path);

            var spec = LauncherSpec();
            var launcher = SceneBuildHelpers.CreateOrUpdateAsset<WeaponDefinition>(path, d => Configure(d, spec));

            Debug.Log($"{LogTag} saved '{path}'");
            return launcher;
        }

        /// <summary>
        /// riflePath・handgunPath に武器の定義を、catalogPath にそれを並べたカタログを、loadoutPath にそれをスロットに入れた
        /// デフォルトロードアウトを作るか上書きして保存し、カタログを返す。
        /// 4 つのパスは互いに異なる Assets/ 以下の .asset であること。
        /// </summary>
        public static WeaponCatalogAsset BuildAt(string riflePath, string handgunPath, string catalogPath, string loadoutPath)
        {
            riflePath = SceneBuildHelpers.ValidateAssetPath(riflePath);
            handgunPath = SceneBuildHelpers.ValidateAssetPath(handgunPath);
            catalogPath = SceneBuildHelpers.ValidateAssetPath(catalogPath);
            loadoutPath = SceneBuildHelpers.ValidateAssetPath(loadoutPath);
            RequireDistinct(riflePath, handgunPath, catalogPath, loadoutPath);

            var rifleSpec = RifleSpec();
            var handgunSpec = HandgunSpec();
            var rifle = SceneBuildHelpers.CreateOrUpdateAsset<WeaponDefinition>(riflePath, d => Configure(d, rifleSpec));
            var handgun = SceneBuildHelpers.CreateOrUpdateAsset<WeaponDefinition>(handgunPath, d => Configure(d, handgunSpec));
            var catalog = SceneBuildHelpers.CreateOrUpdateAsset<WeaponCatalogAsset>(
                catalogPath, c => Configure(c, new[] { rifle, handgun }));
            SceneBuildHelpers.CreateOrUpdateAsset<LoadoutDefinition>(
                loadoutPath, l => Configure(l, DefaultLoadoutSlots(rifle, handgun), LoadoutDefinition.DefaultSwitchSeconds));

            Debug.Log($"{LogTag} saved '{riflePath}', '{handgunPath}', '{catalogPath}' and '{loadoutPath}'");
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
