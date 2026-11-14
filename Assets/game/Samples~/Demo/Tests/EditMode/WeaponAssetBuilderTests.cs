using System;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// <see cref="WeaponAssetBuilder"/> を一時フォルダに走らせて、出力と GUID の保持を確かめる。
    /// リポジトリの Assets/game/Weapons は触らない。
    /// </summary>
    public sealed class WeaponAssetBuilderTests
    {
        private string _folder;
        private string _riflePath;
        private string _handgunPath;
        private string _catalogPath;
        private string _loadoutPath;

        [SetUp]
        public void SetUp()
        {
            _folder = "Assets/__WeaponAssetBuilderTests_" + Guid.NewGuid().ToString("N");
            _riflePath = _folder + "/Rifle.asset";
            _handgunPath = _folder + "/Handgun.asset";
            _catalogPath = _folder + "/WeaponCatalog.asset";
            _loadoutPath = _folder + "/DefaultLoadout.asset";
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(_folder))
            {
                AssetDatabase.DeleteAsset(_folder);
            }
        }

        [Test]
        public void DefaultPathsAreUnderGameWeapons()
        {
            Assert.That(WeaponAssetBuilder.RiflePath, Is.EqualTo(DemoSample.Root + "/Weapons/Rifle.asset"));
            Assert.That(WeaponAssetBuilder.HandgunPath, Is.EqualTo(DemoSample.Root + "/Weapons/Handgun.asset"));
            Assert.That(WeaponAssetBuilder.CatalogPath, Is.EqualTo(DemoSample.Root + "/Weapons/WeaponCatalog.asset"));
            Assert.That(WeaponAssetBuilder.LoadoutPath, Is.EqualTo(DemoSample.Root + "/Weapons/DefaultLoadout.asset"));
        }

        [Test]
        public void IdsAreFixedDistinctUuids()
        {
            Assert.That(WeaponAssetBuilder.RifleId, Is.Not.EqualTo(WeaponAssetBuilder.HandgunId));
            Assert.That(WeaponAssetBuilder.RifleSpec().Id, Is.EqualTo(WeaponAssetBuilder.RifleId));
            Assert.That(WeaponAssetBuilder.HandgunSpec().Id, Is.EqualTo(WeaponAssetBuilder.HandgunId));
            Assert.That(WeaponAssetBuilder.RifleSpec().Id, Is.EqualTo(WeaponAssetBuilder.RifleSpec().Id), "rebuilding keeps the id");
        }

        [Test]
        public void BuildsTheLauncherOutsideTheCatalogAndLoadout()
        {
            Build();
            var launcherPath = _folder + "/Launcher.asset";

            var built = WeaponAssetBuilder.BuildLauncherAt(launcherPath);

            Assert.That(built, Is.SameAs(AssetDatabase.LoadAssetAtPath<WeaponDefinition>(launcherPath)));
            Assert.That(WeaponAssetBuilder.LauncherPath, Is.EqualTo(DemoSample.Root + "/Weapons/Launcher.asset"));
            Assert.That(WeaponAssetBuilder.LauncherId, Is.Not.EqualTo(WeaponAssetBuilder.RifleId));
            Assert.That(WeaponAssetBuilder.LauncherId, Is.Not.EqualTo(WeaponAssetBuilder.HandgunId));

            var launcher = built.ToSpec();
            Assert.That(launcher.Id, Is.EqualTo(WeaponAssetBuilder.LauncherId));
            Assert.That(launcher.Category, Is.EqualTo(WeaponCategory.Launcher));
            Assert.That(launcher.FireMode, Is.EqualTo(FireMode.Semi));
            Assert.That(launcher.MagazineSize, Is.EqualTo(1));
            Assert.That(launcher.ReserveAmmo, Is.EqualTo(4));
            Assert.That(launcher.Damage, Is.EqualTo(120));
            Assert.That(launcher.Range, Is.EqualTo(150f));
            Assert.That(launcher.ExplosionRadius, Is.EqualTo(4f));
            Assert.That(launcher.ReloadSeconds, Is.EqualTo(3f));
            Assert.That(built.ExplosionRadius, Is.EqualTo(4f), "the asset stores the value");

            var catalog = AssetDatabase.LoadAssetAtPath<WeaponCatalogAsset>(_catalogPath);
            Assert.That(catalog.TryFind(WeaponAssetBuilder.LauncherId, out _), Is.False, "the catalog stays rifle, handgun");

            var guid = AssetDatabase.AssetPathToGUID(launcherPath);
            built.Damage = 1;
            Assert.That(WeaponAssetBuilder.BuildLauncherAt(launcherPath), Is.SameAs(built));
            Assert.That(AssetDatabase.AssetPathToGUID(launcherPath), Is.EqualTo(guid));
            Assert.That(built.Damage, Is.EqualTo(120), "a second run restores the values");
        }

        [Test]
        public void LegacySavesAreReadAsTheRiflesAmmo()
        {
            // 武器 1 本の頃のセーブには Id が無く、CampaignSave はその残弾をこの Id の武器に持たせる。
            Assert.That(CampaignSave.LegacyWeaponId, Is.EqualTo(WeaponAssetBuilder.RifleId));
        }

        [Test]
        public void ThePlayerLoadoutIsTheCommittedRifleThenHandgun()
        {
            Assume.That(
                AssetDatabase.LoadAssetAtPath<LoadoutDefinition>(WeaponAssetBuilder.LoadoutPath),
                Is.Not.Null,
                $"run {nameof(WeaponAssetBuilder)}.Build first");

            var weapons = WeaponAssetBuilder.LoadPlayerLoadout();

            Assert.That(weapons, Is.EqualTo(TestScenes.ExpectedPlayerWeapons()));
            Assert.That(weapons, Has.None.Null);
            Assert.That(weapons[0].ToSpec().Id, Is.EqualTo(WeaponAssetBuilder.RifleId));
            Assert.That(weapons[1].ToSpec().Id, Is.EqualTo(WeaponAssetBuilder.HandgunId));
            Assert.That(
                weapons[0].ToSpec().AdsFieldOfView,
                Is.EqualTo(WeaponAssetBuilder.RifleAdsFieldOfView),
                $"the committed assets predate the ADS field of view; run {nameof(WeaponAssetBuilder)}.Build");
            Assert.That(weapons[1].ToSpec().AdsFieldOfView, Is.EqualTo(WeaponAssetBuilder.HandgunAdsFieldOfView));
        }

        [Test]
        public void LoadDefinitionReturnsTheBuiltAsset()
        {
            Build();

            var rifle = WeaponAssetBuilder.LoadDefinition(_riflePath);

            Assert.That(rifle, Is.SameAs(AssetDatabase.LoadAssetAtPath<WeaponDefinition>(_riflePath)));
            Assert.That(rifle.ToSpec().Id, Is.EqualTo(WeaponAssetBuilder.RifleId));
        }

        [Test]
        public void LoadDefinitionRejectsAMissingOrWrongAsset()
        {
            Assert.Throws<InvalidOperationException>(() => WeaponAssetBuilder.LoadDefinition(_riflePath));
            Assert.Throws<ArgumentException>(() => WeaponAssetBuilder.LoadDefinition("Rifle.asset"));

            Build();
            Assert.Throws<InvalidOperationException>(
                () => WeaponAssetBuilder.LoadDefinition(_catalogPath), "a catalog is not a weapon definition");
        }

        [Test]
        public void LoadDefinitionRejectsAnInvalidAsset()
        {
            Build();
            var rifle = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(_riflePath);
            rifle.MagazineSize = 0;

            Assert.Throws<WeaponDefinitionException>(() => WeaponAssetBuilder.LoadDefinition(_riflePath));
        }

        [Test]
        public void BuildsRifleHandgunAndCatalogInThatOrder()
        {
            Build();

            var rifleAsset = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(_riflePath);
            var handgunAsset = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(_handgunPath);
            var catalogAsset = AssetDatabase.LoadAssetAtPath<WeaponCatalogAsset>(_catalogPath);
            Assert.That(rifleAsset, Is.Not.Null);
            Assert.That(handgunAsset, Is.Not.Null);
            Assert.That(catalogAsset, Is.Not.Null);
            Assert.That(catalogAsset.Weapons, Is.EqualTo(new[] { rifleAsset, handgunAsset }));

            var catalog = catalogAsset.ToCatalog();
            Assert.That(catalog.Count, Is.EqualTo(2));
            Assert.That(catalog.All[0].Id, Is.EqualTo(WeaponAssetBuilder.RifleId));
            Assert.That(catalog.All[1].Id, Is.EqualTo(WeaponAssetBuilder.HandgunId));

            var rifle = catalog.Get(WeaponAssetBuilder.RifleId);
            Assert.That(rifle.Category, Is.EqualTo(WeaponCategory.AssaultRifle));
            Assert.That(rifle.FireMode, Is.EqualTo(FireMode.Auto));
            Assert.That(rifle.MagazineSize, Is.EqualTo(30));
            Assert.That(rifle.ReserveAmmo, Is.EqualTo(90));
            Assert.That(rifle.FireInterval, Is.EqualTo(0.1f));
            Assert.That(rifle.ReloadSeconds, Is.EqualTo(2f));
            Assert.That(rifle.Damage, Is.EqualTo(25));
            Assert.That(rifle.Range, Is.EqualTo(100f));
            Assert.That(rifle.Falloff.StartDistance, Is.EqualTo(20f));
            Assert.That(rifle.Falloff.EndDistance, Is.EqualTo(60f));
            Assert.That(rifle.Falloff.MinMultiplier, Is.EqualTo(0.5f));
            Assert.That(rifle.AdsFieldOfView, Is.EqualTo(WeaponAssetBuilder.RifleAdsFieldOfView));
            Assert.That(rifleAsset.AdsFieldOfView, Is.EqualTo(WeaponAssetBuilder.RifleAdsFieldOfView), "the asset stores the value instead of leaving it unset");

            var handgun = catalog.Get(WeaponAssetBuilder.HandgunId);
            Assert.That(handgun.Category, Is.EqualTo(WeaponCategory.Handgun));
            Assert.That(handgun.FireMode, Is.EqualTo(FireMode.Semi));
            Assert.That(handgun.MagazineSize, Is.EqualTo(12));
            Assert.That(handgun.ReserveAmmo, Is.EqualTo(48));
            Assert.That(handgun.FireInterval, Is.EqualTo(0.2f));
            Assert.That(handgun.ReloadSeconds, Is.EqualTo(1.5f));
            Assert.That(handgun.Damage, Is.EqualTo(34));
            Assert.That(handgun.Range, Is.EqualTo(60f));
            Assert.That(handgun.Falloff.StartDistance, Is.EqualTo(15f));
            Assert.That(handgun.Falloff.EndDistance, Is.EqualTo(40f));
            Assert.That(handgun.Falloff.MinMultiplier, Is.EqualTo(0.6f));
            Assert.That(handgun.AdsFieldOfView, Is.EqualTo(WeaponAssetBuilder.HandgunAdsFieldOfView));

            Assert.That(rifle.BlindFireSpreadDegrees, Is.EqualTo(8f));
            Assert.That(rifle.ActiveReloadWindowStart, Is.EqualTo(0.5f));
            Assert.That(rifle.ActiveReloadWindowStart + rifle.ActiveReloadWindowLength, Is.EqualTo(0.65f).Within(1e-5f));
            Assert.That(rifle.ActiveReloadFailSeconds, Is.EqualTo(0.6f));
            Assert.That(rifleAsset.BlindFireSpreadDegrees, Is.EqualTo(8f), "the asset stores the value");
            Assert.That(rifleAsset.ActiveReloadFailSeconds, Is.EqualTo(0.6f), "the asset stores the value");

            Assert.That(handgun.BlindFireSpreadDegrees, Is.EqualTo(12f));
            Assert.That(handgun.ActiveReloadWindowStart, Is.EqualTo(0.45f));
            Assert.That(handgun.ActiveReloadWindowStart + handgun.ActiveReloadWindowLength, Is.EqualTo(0.65f).Within(1e-5f));
            Assert.That(handgun.ActiveReloadFailSeconds, Is.EqualTo(0.4f));
            Assert.That(handgunAsset.ActiveReloadWindowStart, Is.EqualTo(0.45f), "the asset stores the value");

            Assert.That(
                WeaponAssetBuilder.RifleAdsFieldOfView,
                Is.LessThan(WeaponAssetBuilder.HandgunAdsFieldOfView),
                "the rifle zooms in further than the handgun");

            Assert.That(rifleAsset.NameKey, Is.EqualTo(WeaponAssetBuilder.RifleNameKey), "the asset stores the key, not the text");
            Assert.That(handgunAsset.NameKey, Is.EqualTo(WeaponAssetBuilder.HandgunNameKey), "the asset stores the key, not the text");
            Assert.That(rifle.Name, Is.EqualTo(new LocalizedText(WeaponAssetBuilder.RifleNameKey)));
            Assert.That(handgun.Name, Is.EqualTo(new LocalizedText(WeaponAssetBuilder.HandgunNameKey)));
        }

        [Test]
        public void EveryBuiltWeaponNameIsInEveryLanguageTable()
        {
            foreach (var spec in new[] { WeaponAssetBuilder.RifleSpec(), WeaponAssetBuilder.HandgunSpec(), WeaponAssetBuilder.LauncherSpec() })
            {
                foreach (var language in GameLanguages.All)
                {
                    Assert.That(Localizer.TableOf(language).Contains(spec.NameKey), Is.True, $"{GameLanguages.Code(language)}: {spec.NameKey}");
                }
            }

            Assert.That(Localizer.TableOf(GameLanguage.Japanese).Resolve(WeaponAssetBuilder.RifleSpec().Name), Is.EqualTo("Rifle"));
            Assert.That(Localizer.TableOf(GameLanguage.Japanese).Resolve(WeaponAssetBuilder.HandgunSpec().Name), Is.EqualTo("Handgun"));
            Assert.That(Localizer.TableOf(GameLanguage.Japanese).Resolve(WeaponAssetBuilder.LauncherSpec().Name), Is.EqualTo("Launcher"));
        }

        [Test]
        public void BuildsTheDefaultLoadoutWithRifleThenHandgun()
        {
            Build();

            var rifleAsset = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(_riflePath);
            var handgunAsset = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(_handgunPath);
            var loadout = AssetDatabase.LoadAssetAtPath<LoadoutDefinition>(_loadoutPath);
            Assert.That(loadout, Is.Not.Null);
            Assert.That(loadout.SwitchSeconds, Is.EqualTo(0.4f));
            Assert.That(loadout.Slots, Has.Count.EqualTo(2));
            Assert.That(loadout.Slots[0].Weapon, Is.SameAs(rifleAsset));
            Assert.That(loadout.Slots[1].Weapon, Is.SameAs(handgunAsset));

            var rule = loadout.ToRule();
            Assert.That(rule.SlotCount, Is.EqualTo(2));
            foreach (WeaponCategory category in Enum.GetValues(typeof(WeaponCategory)))
            {
                var isHandgun = category == WeaponCategory.Handgun;
                Assert.That(rule.Accepts(0, category), Is.EqualTo(!isHandgun), "slot 0 accepts " + category);
                Assert.That(rule.Accepts(1, category), Is.EqualTo(isHandgun), "slot 1 accepts " + category);
            }

            var specs = loadout.ToSpecs();
            Assert.That(specs[0].Id, Is.EqualTo(WeaponAssetBuilder.RifleId));
            Assert.That(specs[1].Id, Is.EqualTo(WeaponAssetBuilder.HandgunId));
        }

        [Test]
        public void LoadLoadoutReturnsTheBuiltAsset()
        {
            Build();

            var loadout = WeaponAssetBuilder.LoadLoadout(_loadoutPath);

            Assert.That(loadout, Is.SameAs(AssetDatabase.LoadAssetAtPath<LoadoutDefinition>(_loadoutPath)));
        }

        [Test]
        public void LoadLoadoutRejectsAMissingWrongOrInvalidAsset()
        {
            Assert.Throws<InvalidOperationException>(() => WeaponAssetBuilder.LoadLoadout(_loadoutPath));
            Assert.Throws<ArgumentException>(() => WeaponAssetBuilder.LoadLoadout("DefaultLoadout.asset"));

            Build();
            Assert.Throws<InvalidOperationException>(
                () => WeaponAssetBuilder.LoadLoadout(_catalogPath), "a catalog is not a loadout");

            var loadout = AssetDatabase.LoadAssetAtPath<LoadoutDefinition>(_loadoutPath);
            var handgun = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(_handgunPath);
            var rifle = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(_riflePath);
            loadout.SetSlots(WeaponAssetBuilder.DefaultLoadoutSlots(handgun, rifle));
            Assert.Throws<WeaponDefinitionException>(() => WeaponAssetBuilder.LoadLoadout(_loadoutPath));
        }

        [Test]
        public void SecondRunKeepsTheLoadoutGuidAndRestoresItsValues()
        {
            Build();
            var loadoutGuid = AssetDatabase.AssetPathToGUID(_loadoutPath);
            Assert.That(loadoutGuid, Is.Not.Empty);

            var loadout = AssetDatabase.LoadAssetAtPath<LoadoutDefinition>(_loadoutPath);
            var handgun = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(_handgunPath);
            loadout.SetSlots(new[] { new LoadoutSlotDefinition(new[] { WeaponCategory.Handgun }, handgun) });
            loadout.SwitchSeconds = 3f;
            EditorUtility.SetDirty(loadout);
            AssetDatabase.SaveAssets();

            Build();

            Assert.That(AssetDatabase.AssetPathToGUID(_loadoutPath), Is.EqualTo(loadoutGuid));
            var rebuilt = AssetDatabase.LoadAssetAtPath<LoadoutDefinition>(_loadoutPath);
            Assert.That(rebuilt, Is.SameAs(loadout));
            Assert.That(rebuilt.SwitchSeconds, Is.EqualTo(0.4f));
            Assert.That(rebuilt.Slots, Has.Count.EqualTo(2));
            Assert.That(rebuilt.ToSpecs()[0].Id, Is.EqualTo(WeaponAssetBuilder.RifleId));
        }

        [Test]
        public void ConfigureDoesNotSaveAnInvalidLoadout()
        {
            Build();
            var otherPath = _folder + "/BrokenLoadout.asset";
            var handgun = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(_handgunPath);
            var rifle = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(_riflePath);

            Assert.Throws<WeaponDefinitionException>(
                () => SceneBuildHelpers.CreateOrUpdateAsset<LoadoutDefinition>(
                    otherPath,
                    l => WeaponAssetBuilder.Configure(l, WeaponAssetBuilder.DefaultLoadoutSlots(handgun, rifle), 0.4f)));
            Assert.Throws<WeaponDefinitionException>(
                () => SceneBuildHelpers.CreateOrUpdateAsset<LoadoutDefinition>(
                    otherPath,
                    l => WeaponAssetBuilder.Configure(l, WeaponAssetBuilder.DefaultLoadoutSlots(rifle, handgun), -1f)));
            Assert.That(AssetDatabase.LoadMainAssetAtPath(otherPath), Is.Null);
        }

        [Test]
        public void SecondRunKeepsTheGuidsAndRestoresTheValues()
        {
            Build();
            var rifleGuid = AssetDatabase.AssetPathToGUID(_riflePath);
            var handgunGuid = AssetDatabase.AssetPathToGUID(_handgunPath);
            var catalogGuid = AssetDatabase.AssetPathToGUID(_catalogPath);
            Assert.That(rifleGuid, Is.Not.Empty);
            Assert.That(handgunGuid, Is.Not.Empty);
            Assert.That(catalogGuid, Is.Not.Empty);

            // 手で書き換えられたアセットも、2 回目で GUID を保ったまま既定の値に戻る。
            var rifle = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(_riflePath);
            rifle.Damage = 1;
            rifle.Id = "00000000-0000-4000-8000-000000000099";
            rifle.Category = WeaponCategory.SniperRifle;
            rifle.AdsFieldOfView = WeaponDefinition.UnsetAdsFieldOfView;
            rifle.BlindFireSpreadDegrees = 0f;
            rifle.ActiveReloadFailSeconds = 3f;
            rifle.RecoilPitchDegrees = 5f;
            rifle.HipSpreadDegrees = 7f;
            EditorUtility.SetDirty(rifle);
            var catalogAsset = AssetDatabase.LoadAssetAtPath<WeaponCatalogAsset>(_catalogPath);
            catalogAsset.SetWeapons(new[] { rifle });
            EditorUtility.SetDirty(catalogAsset);
            AssetDatabase.SaveAssets();

            var rebuilt = Build();

            Assert.That(AssetDatabase.AssetPathToGUID(_riflePath), Is.EqualTo(rifleGuid));
            Assert.That(AssetDatabase.AssetPathToGUID(_handgunPath), Is.EqualTo(handgunGuid));
            Assert.That(AssetDatabase.AssetPathToGUID(_catalogPath), Is.EqualTo(catalogGuid));
            Assert.That(rebuilt, Is.SameAs(catalogAsset));
            var restored = rebuilt.ToCatalog().Get(WeaponAssetBuilder.RifleId);
            Assert.That(restored.Damage, Is.EqualTo(25));
            Assert.That(restored.Category, Is.EqualTo(WeaponCategory.AssaultRifle));
            Assert.That(restored.AdsFieldOfView, Is.EqualTo(WeaponAssetBuilder.RifleAdsFieldOfView));
            Assert.That(restored.BlindFireSpreadDegrees, Is.EqualTo(WeaponAssetBuilder.RifleBlindFireSpreadDegrees));
            Assert.That(restored.ActiveReloadFailSeconds, Is.EqualTo(WeaponAssetBuilder.RifleActiveReloadFailSeconds));
            Assert.That(restored.RecoilPitchDegrees, Is.EqualTo(WeaponAssetBuilder.RifleRecoilPitchDegrees));
            Assert.That(restored.HipSpreadDegrees, Is.EqualTo(WeaponAssetBuilder.RifleHipSpreadDegrees));
            Assert.That(rebuilt.Weapons, Has.Count.EqualTo(2));
        }

        [Test]
        public void BuiltWeaponsStoreTheirRecoilAndSpread()
        {
            Build();

            AssertRecoilAndSpread(
                AssetDatabase.LoadAssetAtPath<WeaponDefinition>(_riflePath), WeaponAssetBuilder.RifleSpec(), _riflePath);
            AssertRecoilAndSpread(
                AssetDatabase.LoadAssetAtPath<WeaponDefinition>(_handgunPath), WeaponAssetBuilder.HandgunSpec(), _handgunPath);

            var rifle = WeaponAssetBuilder.RifleSpec();
            Assert.That(rifle.RecoilPitchDegrees, Is.EqualTo(0.5f));
            Assert.That(rifle.RecoilYawDegrees, Is.EqualTo(0.2f));
            Assert.That(rifle.RecoilRecoveryPerSecond, Is.EqualTo(10f));
            Assert.That(rifle.HipSpreadDegrees, Is.EqualTo(3f));
            Assert.That(rifle.AdsSpreadDegrees, Is.EqualTo(0.5f));
            Assert.That(rifle.MoveSpreadDegrees, Is.EqualTo(3f));

            var handgun = WeaponAssetBuilder.HandgunSpec();
            Assert.That(handgun.RecoilPitchDegrees, Is.EqualTo(2.5f));
            Assert.That(handgun.RecoilYawDegrees, Is.EqualTo(0.6f));
            Assert.That(handgun.RecoilRecoveryPerSecond, Is.EqualTo(60f));
            Assert.That(handgun.HipSpreadDegrees, Is.EqualTo(2f));
            Assert.That(handgun.AdsSpreadDegrees, Is.EqualTo(0.8f));
            Assert.That(handgun.MoveSpreadDegrees, Is.EqualTo(1.5f));
        }

        [Test]
        public void TheRifleClimbsWhileFiringAndRecoversAfterLettingGo()
        {
            var rifle = WeaponAssetBuilder.RifleSpec();
            var recoil = new RecoilState();

            for (var shot = 0; shot < 10; shot++)
            {
                recoil.Kick(rifle, 1, shot);
                recoil.Tick(rifle.FireInterval);
            }

            Assert.That(
                recoil.PitchUpDegrees,
                Is.EqualTo(10f * rifle.RecoilPitchDegrees).Within(1e-4f),
                "the fire interval is shorter than the recovery delay, so a burst does not recover");
            Assert.That(
                recoil.PitchUpDegrees,
                Is.GreaterThan(Math.Abs(recoil.YawDegrees)),
                "the burst climbs up more than it drifts sideways");

            recoil.Tick(recoil.RecoveryDelaySeconds + (recoil.PitchUpDegrees / rifle.RecoilRecoveryPerSecond) + 0.5f);
            Assert.That(recoil.IsAtRest, Is.True, "letting go brings the aim back");
        }

        [Test]
        public void TheHandgunKicksHarderThanTheRifleAndRecoversBeforeTheNextFewShots()
        {
            var rifle = WeaponAssetBuilder.RifleSpec();
            var handgun = WeaponAssetBuilder.HandgunSpec();
            Assert.That(handgun.RecoilPitchDegrees, Is.GreaterThan(rifle.RecoilPitchDegrees * 2f), "one handgun shot jumps more");
            Assert.That(handgun.RecoilRecoveryPerSecond, Is.GreaterThan(rifle.RecoilRecoveryPerSecond), "the handgun settles faster");

            var recoil = new RecoilState();
            recoil.Kick(handgun, 1, 0);
            Assert.That(recoil.PitchUpDegrees, Is.EqualTo(handgun.RecoilPitchDegrees));

            recoil.Tick(0.3f);
            Assert.That(recoil.IsAtRest, Is.True, "a single kick is gone within 0.3 s");
        }

        [Test]
        public void TheHandgunDoesNotClimbWhenFiredAtItsFastestInterval()
        {
            var handgun = WeaponAssetBuilder.HandgunSpec();
            var recoil = new RecoilState();
            for (var shot = 0; shot < 12; shot++)
            {
                recoil.Kick(handgun, 1, 0);
                Assert.That(recoil.PitchUpDegrees, Is.EqualTo(handgun.RecoilPitchDegrees), "shot " + shot);
                recoil.Tick(0.2f);
            }
        }

        [Test]
        public void AimingTightensTheSpreadAndMovingWidensIt()
        {
            foreach (var spec in new[] { WeaponAssetBuilder.RifleSpec(), WeaponAssetBuilder.HandgunSpec() })
            {
                var hip = SpreadPattern.ConeDegrees(spec, 0f, 0f);
                var ads = SpreadPattern.ConeDegrees(spec, 1f, 0f);
                var moving = SpreadPattern.ConeDegrees(spec, 0f, 1f);

                Assert.That(ads, Is.GreaterThan(0f).And.LessThan(hip), spec.LogName);
                Assert.That(moving, Is.GreaterThan(hip), spec.LogName);
                Assert.That(spec.BlindFireSpreadDegrees, Is.GreaterThan(hip), spec.LogName + ": blind fire is still the widest");
            }

            Assert.That(
                WeaponAssetBuilder.RifleAdsSpreadDegrees,
                Is.LessThan(WeaponAssetBuilder.HandgunAdsSpreadDegrees),
                "the rifle groups tighter when aimed");
        }

        [Test]
        public void TheRepositoryWeaponAssetsStoreTheBuiltRecoilAndSpread()
        {
            AssertRecoilAndSpread(
                WeaponAssetBuilder.LoadDefinition(WeaponAssetBuilder.RiflePath), WeaponAssetBuilder.RifleSpec(), WeaponAssetBuilder.RiflePath);
            AssertRecoilAndSpread(
                WeaponAssetBuilder.LoadDefinition(WeaponAssetBuilder.HandgunPath), WeaponAssetBuilder.HandgunSpec(), WeaponAssetBuilder.HandgunPath);
        }

        private static void AssertRecoilAndSpread(WeaponDefinition asset, WeaponSpec expected, string path)
        {
            var message = $"{path}; run {nameof(WeaponAssetBuilder)}.Build if the committed asset predates the values";
            Assert.That(asset, Is.Not.Null, message);
            Assert.That(asset.RecoilPitchDegrees, Is.EqualTo(expected.RecoilPitchDegrees), message);
            Assert.That(asset.RecoilYawDegrees, Is.EqualTo(expected.RecoilYawDegrees), message);
            Assert.That(asset.RecoilRecoveryPerSecond, Is.EqualTo(expected.RecoilRecoveryPerSecond), message);
            Assert.That(asset.RecoilRecoveryDelaySeconds, Is.EqualTo(expected.RecoilRecoveryDelaySeconds), message);
            Assert.That(asset.HipSpreadDegrees, Is.EqualTo(expected.HipSpreadDegrees), message);
            Assert.That(asset.AdsSpreadDegrees, Is.EqualTo(expected.AdsSpreadDegrees), message);
            Assert.That(asset.MoveSpreadDegrees, Is.EqualTo(expected.MoveSpreadDegrees), message);

            var spec = asset.ToSpec();
            Assert.That(spec.RecoilPitchDegrees, Is.EqualTo(expected.RecoilPitchDegrees), message);
            Assert.That(spec.RecoilYawDegrees, Is.EqualTo(expected.RecoilYawDegrees), message);
            Assert.That(spec.RecoilRecoveryPerSecond, Is.EqualTo(expected.RecoilRecoveryPerSecond), message);
            Assert.That(spec.RecoilRecoveryDelaySeconds, Is.EqualTo(expected.RecoilRecoveryDelaySeconds), message);
            Assert.That(spec.HipSpreadDegrees, Is.EqualTo(expected.HipSpreadDegrees), message);
            Assert.That(spec.AdsSpreadDegrees, Is.EqualTo(expected.AdsSpreadDegrees), message);
            Assert.That(spec.MoveSpreadDegrees, Is.EqualTo(expected.MoveSpreadDegrees), message);
        }

        [Test]
        public void RefusesToOverwriteAnAssetOfAnotherType()
        {
            Build();
            AssetDatabase.DeleteAsset(_handgunPath);
            var other = ScriptableObject.CreateInstance<WeaponCatalogAsset>();
            AssetDatabase.CreateAsset(other, _handgunPath);

            Assert.Throws<InvalidOperationException>(() => Build());
            Assert.That(AssetDatabase.LoadMainAssetAtPath(_handgunPath), Is.InstanceOf<WeaponCatalogAsset>());
        }

        [Test]
        public void RejectsInvalidOrSharedPaths()
        {
            Assert.Throws<ArgumentException>(() => WeaponAssetBuilder.BuildAt("Rifle.asset", _handgunPath, _catalogPath, _loadoutPath));
            Assert.Throws<ArgumentException>(() => WeaponAssetBuilder.BuildAt(_riflePath, _riflePath, _catalogPath, _loadoutPath));
            Assert.Throws<ArgumentException>(() => WeaponAssetBuilder.BuildAt(_riflePath, _handgunPath, _catalogPath, "DefaultLoadout.asset"));
            Assert.Throws<ArgumentException>(() => WeaponAssetBuilder.BuildAt(_riflePath, _handgunPath, _catalogPath, _catalogPath));
            Assert.That(AssetDatabase.IsValidFolder(_folder), Is.False);
        }

        [Test]
        public void ConfigureDoesNotSaveAnInvalidCatalog()
        {
            var catalog = ScriptableObject.CreateInstance<WeaponCatalogAsset>();
            try
            {
                Assert.Throws<WeaponDefinitionException>(
                    () => WeaponAssetBuilder.Configure(catalog, Array.Empty<WeaponDefinition>()));
                Assert.Throws<WeaponDefinitionException>(
                    () => SceneBuildHelpers.CreateOrUpdateAsset<WeaponCatalogAsset>(
                        _catalogPath, c => WeaponAssetBuilder.Configure(c, Array.Empty<WeaponDefinition>())));
                Assert.That(AssetDatabase.LoadMainAssetAtPath(_catalogPath), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(catalog);
            }
        }

        private WeaponCatalogAsset Build()
        {
            return WeaponAssetBuilder.BuildAt(_riflePath, _handgunPath, _catalogPath, _loadoutPath);
        }
    }
}
