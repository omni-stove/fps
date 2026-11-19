using System;
using System.Collections.Generic;
using System.Globalization;
using Shooter.Core;
using Shooter.Runtime;
using UnityEditor;
using UnityEngine;

namespace Shooter.Editor
{
    // サンプルの武器を足すときは JsonFolder に JSON を 1 つ置いて走らせ直すだけでよいよう、JSON から武器のアセットと射撃場の一覧を作る。
    // 既存のアセットは GUID を保って中身だけ更新するので、参照しているシーンやプレハブは壊れない。
    public static class WeaponAssetBuilder
    {
        public static string Folder => WeaponAssets.Folder;

        public static string RiflePath => Folder + "/Rifle.asset";
        public static string HandgunPath => Folder + "/Handgun.asset";
        public static string CatalogPath => WeaponAssets.CatalogPath;
        public static string LoadoutPath => Folder + "/DefaultLoadout.asset";
        public static string LauncherPath => Folder + "/Launcher.asset";
        public static string KnifePath => Folder + "/Knife.asset";

        public static string JsonFolder => Folder + "/Json";

        public static string RangeWeaponsPath => Folder + "/RangeWeapons.asset";

        public static string ReferenceJsonFolder => Folder + "/ReferenceJson";

        public static string ReferenceFolder => Folder + "/Reference";

        public static string ReferenceWeaponsPath => Folder + "/ReferenceWeapons.asset";

        public static string RealisticJsonFolder => Folder + "/RealisticJson";

        public static string RealisticFolder => Folder + "/Realistic";

        public static string RealisticWeaponsPath => Folder + "/RealisticWeapons.asset";

        public const string MenuPath = "Shooter/Build Weapon Assets";
        public const string LogTag = "[WeaponAssetBuilder]";

        // 再ビルドしても Id が変わらないよう固定する。サーバーとクライアントはこの Id で武器を突き合わせる
        public const string RifleId = "6c1f3b2a-8d4e-4f7a-9b0c-2e5d7a1f4c83";
        public const string HandgunId = "b4e2a9d7-3c16-4a58-8f2e-7d0b9c6a1e45";
        public const string LauncherId = "e7a3c5d1-2f84-4b69-a0d3-5c8e1b7f2a96";
        public const string KnifeId = "f4df3778-7da1-49c7-88a6-3f4769c23f9e";

        public const string RifleNameKey = "weapon.rifle";
        public const string HandgunNameKey = "weapon.handgun";
        public const string LauncherNameKey = "weapon.launcher";
        public const string KnifeNameKey = "weapon.knife";

        public const float LauncherExplosionRadius = 4f;

        // 弾倉が 1 発なので、実際の間隔はリロード時間で決まる。
        public const float LauncherFireInterval = 0.5f;

        // 共通の初期値 (AdsZoom.DefaultFieldOfView) より狭くして、遠くを狙いやすくする。
        public const float RifleAdsFieldOfView = 45f;

        // 近距離で使うので rifle より広くする。
        public const float HandgunAdsFieldOfView = 55f;

        public const float RifleBlindFireSpreadDegrees = 8f;

        public const float RifleActiveReloadWindowStart = 0.5f;

        public const float RifleActiveReloadWindowLength = 0.15f;

        public const float RifleActiveReloadFailSeconds = 0.6f;

        // 連射間隔 0.1 秒が反動の戻り待ち (WeaponSpec.DefaultRecoilRecoveryDelaySeconds) より短いので、
        // 撃ち続ける間は戻らずに上へ溜まり、30 発撃ち切ると 15° 上がる。
        public const float RifleRecoilPitchDegrees = 0.5f;

        // 縦より小さくし、溜まる向きを上に寄せる。
        public const float RifleRecoilYawDegrees = 0.2f;

        // 撃ち切った 15° が、離してから約 1.5 秒で戻る速さにする。
        public const float RifleRecoilRecoveryPerSecond = 10f;

        public const float RifleHipSpreadDegrees = 3f;

        public const float RifleAdsSpreadDegrees = 0.5f;

        public const float RifleMoveSpreadDegrees = 3f;

        // 覗き込んで動いたときに照準の開きが腰だめで動いたときより大きく見えないよう、移動の 3 割に絞る。
        public const float RifleAdsMoveSpreadDegrees = 0.9f;

        public const float RifleWeight = 3.5f;

        // rifle の 1 発より大きく跳ねるようにする。
        public const float HandgunRecoilPitchDegrees = 2.5f;

        public const float HandgunRecoilYawDegrees = 0.6f;

        // 連射間隔 0.2 秒のうち戻り待ち 0.15 秒を除いた 0.05 秒で 1 発分 (縦横で最大約 2.57°) が戻るので、
        // 最短間隔で撃ち続けても上へは溜まらない。
        public const float HandgunRecoilRecoveryPerSecond = 60f;

        // 近距離用なので rifle より狭くする。
        public const float HandgunHipSpreadDegrees = 2f;

        // 片手寄りの構えなので rifle ほどは絞れなくする。
        public const float HandgunAdsSpreadDegrees = 0.8f;

        // 軽いので rifle より小さくする。
        public const float HandgunMoveSpreadDegrees = 1.5f;

        // rifle と同じく移動の 3 割に絞る。
        public const float HandgunAdsMoveSpreadDegrees = 0.45f;

        public const float HandgunWeight = 1f;

        public const float LauncherWeight = 7f;

        // 片手で構えるので rifle より広くする。
        public const float HandgunBlindFireSpreadDegrees = 12f;

        public const float HandgunActiveReloadWindowStart = 0.45f;

        public const float HandgunActiveReloadWindowLength = 0.2f;

        public const float HandgunActiveReloadFailSeconds = 0.4f;

        public static WeaponSpec RifleSpec()
        {
            return new WeaponSpec(
                RifleId,
                RifleNameKey,
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
                activeReloadFailSeconds: RifleActiveReloadFailSeconds,
                recoilPitchDegrees: RifleRecoilPitchDegrees,
                recoilYawDegrees: RifleRecoilYawDegrees,
                recoilRecoveryPerSecond: RifleRecoilRecoveryPerSecond,
                hipSpreadDegrees: RifleHipSpreadDegrees,
                adsSpreadDegrees: RifleAdsSpreadDegrees,
                moveSpreadDegrees: RifleMoveSpreadDegrees,
                weight: RifleWeight,
                adsMoveSpreadDegrees: RifleAdsMoveSpreadDegrees);
        }

        public static WeaponDefinition LoadRifle()
        {
            return LoadDefinition(RiflePath);
        }

        public static WeaponDefinition LoadDefinition(string path) => WeaponAssets.LoadDefinition(path);

        public static WeaponCatalogAsset LoadCatalog() => WeaponAssets.LoadCatalog(CatalogPath);

        public static WeaponDefinition FindInCatalog(WeaponCatalogAsset catalog, string placementId, string weaponId) =>
            WeaponAssets.FindInCatalog(catalog, placementId, weaponId);

        public static WeaponSpec HandgunSpec()
        {
            return new WeaponSpec(
                HandgunId,
                HandgunNameKey,
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
                activeReloadFailSeconds: HandgunActiveReloadFailSeconds,
                recoilPitchDegrees: HandgunRecoilPitchDegrees,
                recoilYawDegrees: HandgunRecoilYawDegrees,
                recoilRecoveryPerSecond: HandgunRecoilRecoveryPerSecond,
                hipSpreadDegrees: HandgunHipSpreadDegrees,
                adsSpreadDegrees: HandgunAdsSpreadDegrees,
                moveSpreadDegrees: HandgunMoveSpreadDegrees,
                weight: HandgunWeight,
                adsMoveSpreadDegrees: HandgunAdsMoveSpreadDegrees);
        }

        // 建造物を壊す確認用なので、既定のロードアウトとカタログには入れない。
        public static WeaponSpec LauncherSpec()
        {
            var flat = new WeaponSpec(
                LauncherId,
                LauncherNameKey,
                WeaponCategory.Launcher,
                FireMode.Semi,
                1,
                4,
                LauncherFireInterval,
                3f,
                120,
                150f,
                DamageFalloff.None,
                explosionRadius: LauncherExplosionRadius,
                weight: LauncherWeight);
            var impact = new ImpactSpec(flat.Impact.Damage, flat.Impact.Falloff, flat.Impact.StructureRadius, LauncherExplosion());
            return new WeaponSpec(flat.Id, flat.NameKey, flat.Category, flat.Trigger, flat.Ammo, flat.Round, impact, flat.Aim, flat.AmmoTypes, flat.ShotNoiseRadius, flat.Weight, flat.MoveSpeedMultiplier);
        }

        public static ExplosionSpec LauncherExplosion()
        {
            return new ExplosionSpec(new ExplosionDamage(100, 5f, 1f), 100);
        }

        // ナイフは近接 (MeleeController.MeleeWeapon) で倒したときの武器の Id と名前に使うだけで銃としては持たせず、
        // 当たりとダメージは MeleeController の値 (MeleeSpec) で決まる。
        public static WeaponSpec KnifeSpec()
        {
            // WeaponCategory に近接が無く、銃としては撃たないので、射撃の値は近接の既定の値に寄せるだけにする。
            return new WeaponSpec(
                KnifeId,
                KnifeNameKey,
                WeaponCategory.Handgun,
                FireMode.Semi,
                1,
                0,
                MeleeSpec.DefaultSwingSeconds,
                MeleeSpec.DefaultSwingSeconds,
                MeleeSpec.DefaultDamage,
                MeleeSpec.DefaultReach,
                DamageFalloff.None);
        }

        public static WeaponDefinition LoadKnife()
        {
            return LoadDefinition(KnifePath);
        }

        public static WeaponDefinition LoadLauncher()
        {
            return LoadDefinition(LauncherPath);
        }

        public static LoadoutDefinition LoadDefaultLoadout()
        {
            return LoadLoadout(LoadoutPath);
        }

        // ネット対戦と同じアセットから引くので、並びを変えるときは DefaultLoadoutSlots だけを直せばよい。
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

        public static LoadoutDefinition LoadLoadout(string path) => WeaponAssets.LoadLoadout(path);

        // 種別が増えたら自動で入るよう、列挙から Handgun 以外のすべてを集める。
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

        public static WeaponCategory[] DefaultSecondaryCategories()
        {
            return new[] { WeaponCategory.Handgun };
        }

        public static LoadoutSlotDefinition[] DefaultLoadoutSlots(WeaponDefinition rifle, WeaponDefinition handgun)
        {
            return new[]
            {
                new LoadoutSlotDefinition(DefaultPrimaryCategories(), rifle),
                new LoadoutSlotDefinition(DefaultSecondaryCategories(), handgun),
            };
        }

        public static WeaponCatalogAsset LoadRangeWeapons() => WeaponAssets.LoadCatalog(RangeWeaponsPath);

        public static WeaponCatalogAsset LoadReferenceWeapons() => WeaponAssets.LoadCatalog(ReferenceWeaponsPath);

        public static WeaponCatalogAsset LoadRealisticWeapons() => WeaponAssets.LoadCatalog(RealisticWeaponsPath);

        // 演出の仮素材は武器のアセットより先に作られることがあるので、アセットでなく JSON から読む。
        public static IReadOnlyList<(string Name, WeaponSpec Spec)> RangeWeaponSources()
        {
            var sources = new List<(string Name, WeaponSpec Spec)>();
            AddSources(sources, JsonFolder);
            return sources;
        }

        // プレイヤーの初期武器と射撃場の台の武器は Reference と Realistic から取るので、見た目と音はこの全部に作る。
        public static IReadOnlyList<(string Name, WeaponSpec Spec)> AllWeaponSources()
        {
            var sources = new List<(string Name, WeaponSpec Spec)>();
            AddSources(sources, JsonFolder);
            AddSources(sources, ReferenceJsonFolder);
            AddSources(sources, RealisticJsonFolder);
            return sources;
        }

        private static void AddSources(List<(string Name, WeaponSpec Spec)> sources, string jsonFolder)
        {
            foreach (var file in WeaponJsonImporter.FindJsonFiles(jsonFolder))
            {
                var name = System.IO.Path.GetFileNameWithoutExtension(file);
                sources.Add((name, WeaponJsonImporter.ParseSpec(System.IO.File.ReadAllText(file), name + ".json")));
            }
        }

        [MenuItem(MenuPath)]
        public static void Build()
        {
            BuildFromJsonAt(JsonFolder, Folder, RangeWeaponsPath);
            BuildFromJsonAt(ReferenceJsonFolder, ReferenceFolder, ReferenceWeaponsPath);
            BuildFromJsonAt(RealisticJsonFolder, RealisticFolder, RealisticWeaponsPath);
            WindUpSoundAssetBuilder.Build();

            // rifle と handgun はコードの値でなく JSON から作ったアセットを並べ、JSON を直せば既定の武器も変わるようにする。
            var rifle = LoadDefinition(RiflePath);
            var handgun = LoadDefinition(HandgunPath);
            BuildCatalogAndLoadoutAt(rifle, handgun, CatalogPath, LoadoutPath);
            BuildKnifeAt(KnifePath);
        }

        public static IReadOnlyList<WeaponDefinition> BuildFromJsonAt(string jsonFolder, string folder, string rangeWeaponsPath)
        {
            rangeWeaponsPath = SceneBuildHelpers.ValidateAssetPath(rangeWeaponsPath);

            var weapons = WeaponJsonImporter.ImportFrom(jsonFolder, folder, null);
            SceneBuildHelpers.CreateOrUpdateAsset<WeaponCatalogAsset>(rangeWeaponsPath, c => Configure(c, weapons));

            Debug.Log($"{LogTag} saved {weapons.Count.ToString(CultureInfo.InvariantCulture)} weapon(s) from '{jsonFolder}' and '{rangeWeaponsPath}'");
            return weapons;
        }

        public static WeaponDefinition BuildLauncherAt(string path)
        {
            path = SceneBuildHelpers.ValidateAssetPath(path);

            var spec = LauncherSpec();
            var launcher = SceneBuildHelpers.CreateOrUpdateAsset<WeaponDefinition>(path, d => Configure(d, spec));

            Debug.Log($"{LogTag} saved '{path}'");
            return launcher;
        }

        public static WeaponDefinition BuildKnifeAt(string path)
        {
            path = SceneBuildHelpers.ValidateAssetPath(path);

            var spec = KnifeSpec();
            var knife = SceneBuildHelpers.CreateOrUpdateAsset<WeaponDefinition>(path, d => Configure(d, spec));

            Debug.Log($"{LogTag} saved '{path}'");
            return knife;
        }

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
            var catalog = BuildCatalogAndLoadoutAt(rifle, handgun, catalogPath, loadoutPath);

            Debug.Log($"{LogTag} saved '{riflePath}' and '{handgunPath}'");
            return catalog;
        }

        private static WeaponCatalogAsset BuildCatalogAndLoadoutAt(
            WeaponDefinition rifle, WeaponDefinition handgun, string catalogPath, string loadoutPath)
        {
            var catalog = SceneBuildHelpers.CreateOrUpdateAsset<WeaponCatalogAsset>(
                catalogPath, c => Configure(c, new[] { rifle, handgun }));
            SceneBuildHelpers.CreateOrUpdateAsset<LoadoutDefinition>(
                loadoutPath, l => Configure(l, DefaultLoadoutSlots(rifle, handgun), LoadoutDefinition.DefaultSwitchSeconds));

            Debug.Log($"{LogTag} saved '{catalogPath}' and '{loadoutPath}'");
            return catalog;
        }

        public static void Configure(WeaponDefinition definition, WeaponSpec spec) => WeaponAssets.Configure(definition, spec);

        public static void Configure(WeaponCatalogAsset catalog, IEnumerable<WeaponDefinition> weapons) =>
            WeaponAssets.Configure(catalog, weapons);

        public static void Configure(LoadoutDefinition loadout, IEnumerable<LoadoutSlotDefinition> slots, float switchSeconds) =>
            WeaponAssets.Configure(loadout, slots, switchSeconds);

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
