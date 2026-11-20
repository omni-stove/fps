using System;
using System.Reflection;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Shooter.Tests.EditMode
{
    // リポジトリの Abilities を触らないよう、一時フォルダに走らせる。
    public sealed class AbilityAssetBuilderTests
    {
        private string _folder;

        [SetUp]
        public void SetUp()
        {
            _folder = "Assets/__AbilityAssetBuilderTests_" + Guid.NewGuid().ToString("N");
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
        public void TheGogglesAndTheDroneAreSavedNextToTheOtherAbilities()
        {
            Assert.That(AbilityAssetBuilder.VisionGogglesAbilityPath, Is.EqualTo(AbilityAssetBuilder.Folder + "/VisionGoggles.asset"));
            Assert.That(AbilityAssetBuilder.DroneAbilityPath, Is.EqualTo(AbilityAssetBuilder.Folder + "/DeployDrone.asset"));
            Assert.That(AbilityAssetBuilder.Folder, Is.EqualTo(System.IO.Path.GetDirectoryName(AbilityAssetBuilder.ShieldAbilityPath)?.Replace('\\', '/')));
            Assert.That(AbilityAssetBuilder.VisionGogglesAbilityId, Is.Not.EqualTo(AbilityAssetBuilder.DroneAbilityId));
        }

        [Test]
        public void BuildsGogglesThatGoFromNightToThermalAndCostAChargeToPutOn()
        {
            var path = _folder + "/VisionGoggles.asset";

            var goggles = AbilityAssetBuilder.BuildVisionGogglesAt(path);

            Assert.That(AssetDatabase.LoadAssetAtPath<VisionGogglesDefinition>(path), Is.SameAs(goggles));
            Assert.That(goggles.Id, Is.EqualTo("vision-goggles"));
            Assert.That(goggles.ToMode(), Is.EqualTo(VisionMode.Night));
            Assert.That(goggles.ToModes(), Is.EqualTo(new[] { VisionMode.Night, VisionMode.Thermal }));
            Assert.That(AbilityAssetBuilder.VisionGogglesModes, Is.EqualTo(new[] { VisionMode.Night, VisionMode.Thermal }));
            Assert.That(goggles.MaxCharges, Is.EqualTo(1));
            Assert.That(goggles.RechargeSeconds, Is.EqualTo(5f));
            Assert.That(goggles.CooldownSeconds, Is.EqualTo(1f));
            Assert.That(goggles.WindupSeconds, Is.EqualTo(0.4f));
            Assert.DoesNotThrow(() => goggles.ToSpec());

            var ability = goggles.CreateAbility();
            Assert.That(ability, Is.InstanceOf<VisionGogglesAbility>());
            Assert.That(((VisionGogglesAbility)ability).Mode, Is.EqualTo(VisionMode.Night));
            Assert.That(((VisionGogglesAbility)ability).Modes, Is.EqualTo(new[] { VisionMode.Night, VisionMode.Thermal }));
        }

        [Test]
        public void BuildsAReconDroneWithTheKitDefaultsAndACodeBuiltBody()
        {
            var path = _folder + "/DeployDrone.asset";

            var drone = AbilityAssetBuilder.BuildDroneAt(path);

            Assert.That(AssetDatabase.LoadAssetAtPath<DroneDeployDefinition>(path), Is.SameAs(drone));
            Assert.That(drone.Id, Is.EqualTo("deploy-drone"));
            Assert.That(drone.MaxCharges, Is.EqualTo(1));
            Assert.That(drone.RechargeSeconds, Is.EqualTo(30f));
            Assert.That(drone.CooldownSeconds, Is.EqualTo(1f));
            Assert.That(drone.WindupSeconds, Is.Zero);
            Assert.That(drone.MaxActive, Is.EqualTo(DroneSpec.DefaultMaxActive));
            Assert.That(drone.MaxHealth, Is.EqualTo(DroneSpec.DefaultMaxHealth));
            Assert.That(drone.Size, Is.EqualTo(new Vector3(DroneSpec.DefaultWidth, DroneSpec.DefaultHeight, DroneSpec.DefaultDepth)));
            Assert.That(drone.Prefab, Is.Null, "the drone body is built in code until a game sets its look");
            Assert.That(drone.PreviewPrefab, Is.Null);
            Assert.That(drone.DisplayNameKey, Is.EqualTo("deployable.reconDrone"));
            Assert.That(drone.Detonates, Is.True, "a short press recalls the drone");
            Assert.That(drone.DetonateTapSeconds, Is.EqualTo(DroneSpec.DefaultRecallTapSeconds));

            var spec = drone.ToDroneSpec();
            Assert.That(spec.Speed, Is.EqualTo(DroneSpec.DefaultSpeed));
            Assert.That(spec.ControlRange, Is.EqualTo(DroneSpec.DefaultControlRange));
            Assert.That(spec.BatterySeconds, Is.EqualTo(DroneSpec.DefaultBatterySeconds));
            Assert.That(spec.SpotRadius, Is.EqualTo(DroneSpec.DefaultSpotRadius));
            Assert.That(spec.SpotIntervalSeconds, Is.EqualTo(DroneSpec.DefaultSpotIntervalSeconds));
            Assert.That(spec.RevealSeconds, Is.EqualTo(DroneSpec.DefaultRevealSeconds));
            Assert.DoesNotThrow(() => drone.ToDeploySpec());
        }

        [Test]
        public void ASecondRunKeepsTheGuidsAndRestoresTheValues()
        {
            var gogglesPath = _folder + "/VisionGoggles.asset";
            var dronePath = _folder + "/DeployDrone.asset";
            var goggles = AbilityAssetBuilder.BuildVisionGogglesAt(gogglesPath);
            var drone = AbilityAssetBuilder.BuildDroneAt(dronePath);
            var gogglesGuid = AssetDatabase.AssetPathToGUID(gogglesPath);
            var droneGuid = AssetDatabase.AssetPathToGUID(dronePath);
            goggles.Modes = new[] { VisionMode.Thermal };
            goggles.MaxCharges = 4;
            drone.Speed = 99f;
            drone.MaxActive = 3;

            var rebuiltGoggles = AbilityAssetBuilder.BuildVisionGogglesAt(gogglesPath);
            var rebuiltDrone = AbilityAssetBuilder.BuildDroneAt(dronePath);

            Assert.That(AssetDatabase.AssetPathToGUID(gogglesPath), Is.EqualTo(gogglesGuid));
            Assert.That(AssetDatabase.AssetPathToGUID(dronePath), Is.EqualTo(droneGuid));
            Assert.That(rebuiltGoggles.Modes, Is.EqualTo(new[] { VisionMode.Night, VisionMode.Thermal }));
            Assert.That(rebuiltGoggles.MaxCharges, Is.EqualTo(AbilityAssetBuilder.VisionGogglesMaxCharges));
            Assert.That(rebuiltDrone.Speed, Is.EqualTo(DroneSpec.DefaultSpeed));
            Assert.That(rebuiltDrone.MaxActive, Is.EqualTo(DroneSpec.DefaultMaxActive));
        }

        [Test]
        public void RejectsAPathOutsideTheAssets()
        {
            Assert.Throws<ArgumentException>(() => AbilityAssetBuilder.BuildVisionGogglesAt("Elsewhere/VisionGoggles.asset"));
            Assert.Throws<ArgumentException>(() => AbilityAssetBuilder.BuildDroneAt("Elsewhere/DeployDrone.asset"));
        }

        [Test]
        public void TheBeaconIsSavedNextToTheOtherAbilitiesAndGoesInTheFirstSlot()
        {
            Assert.That(AbilityAssetBuilder.BeaconAbilityPath, Is.EqualTo(AbilityAssetBuilder.Folder + "/DeployBeacon.asset"));
            Assert.That(AbilityAssetBuilder.BeaconAbilityId, Is.EqualTo("deploy-beacon"));
            Assert.That(AbilityAssetBuilder.BeaconLoadoutSlot, Is.Zero);
        }

        [Test]
        public void BuildsARespawnBeaconWithTheKitDefaultsAndACodeBuiltBody()
        {
            var path = _folder + "/DeployBeacon.asset";

            var beacon = AbilityAssetBuilder.BuildBeaconAt(path);

            Assert.That(AssetDatabase.LoadAssetAtPath<BeaconDeployDefinition>(path), Is.SameAs(beacon));
            Assert.That(beacon.Id, Is.EqualTo("deploy-beacon"));
            Assert.That(beacon.MaxCharges, Is.EqualTo(1));
            Assert.That(beacon.RechargeSeconds, Is.EqualTo(45f));
            Assert.That(beacon.CooldownSeconds, Is.EqualTo(1f));
            Assert.That(beacon.WindupSeconds, Is.Zero);
            Assert.That(beacon.MaxActive, Is.EqualTo(1));
            Assert.That(beacon.MaxHealth, Is.EqualTo(150));
            Assert.That(beacon.Size, Is.EqualTo(new Vector3(0.4f, 1.2f, 0.4f)));
            Assert.That(beacon.Prefab, Is.Null, "the pole and lamp are built in code until a game sets its look");
            Assert.That(beacon.PreviewPrefab, Is.Null);
            Assert.That(beacon.DisplayNameKey, Is.EqualTo("deployable.beacon"));

            var spec = beacon.ToBeaconSpec();
            Assert.That(spec.Uses, Is.EqualTo(3));
            Assert.That(spec.SpawnDistance, Is.EqualTo(1f));
            Assert.That(beacon.CreateAbility(), Is.InstanceOf<DeployAbility>());
        }

        [Test]
        public void ASecondBeaconRunKeepsTheGuidAndRestoresTheValues()
        {
            var path = _folder + "/DeployBeacon.asset";
            var beacon = AbilityAssetBuilder.BuildBeaconAt(path);
            var guid = AssetDatabase.AssetPathToGUID(path);
            beacon.Uses = 9;
            beacon.NameKey = "other";

            var rebuilt = AbilityAssetBuilder.BuildBeaconAt(path);

            Assert.That(rebuilt, Is.SameAs(beacon));
            Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
            Assert.That(rebuilt.Uses, Is.EqualTo(BeaconSpec.DefaultUses));
            Assert.That(rebuilt.DisplayNameKey, Is.EqualTo(BeaconSpec.DefaultNameKey));
        }

        [Test]
        public void TheBeaconGoesInTheLoadoutsFirstSlotAndKeepsTheOtherSlot()
        {
            var loadoutPath = BuildLoadout();
            var beacon = AbilityAssetBuilder.BuildBeaconAt(_folder + "/DeployBeacon.asset");
            var drone = AbilityAssetBuilder.BuildDroneAt(_folder + "/DeployDrone.asset");
            AssetDatabase.LoadAssetAtPath<LoadoutDefinition>(loadoutPath).SetAbilities(new AbilityDefinition[] { drone, drone });

            var loadout = AbilityAssetBuilder.EquipInLoadoutAt(loadoutPath, beacon, AbilityAssetBuilder.BeaconLoadoutSlot);

            Assert.That(loadout, Is.SameAs(AssetDatabase.LoadAssetAtPath<LoadoutDefinition>(loadoutPath)));
            Assert.That(loadout.Abilities, Is.EqualTo(new AbilityDefinition[] { beacon, drone }));
            Assert.That(loadout.Slots.Count, Is.EqualTo(2), "the weapons are left alone");

            AbilityAssetBuilder.EquipInLoadoutAt(loadoutPath, beacon, AbilityAssetBuilder.BeaconLoadoutSlot);
            Assert.That(loadout.Abilities, Is.EqualTo(new AbilityDefinition[] { beacon, drone }), "a second run changes nothing");
        }

        [Test]
        public void AnEmptyLoadoutIsPaddedUpToTheSlot()
        {
            var loadoutPath = BuildLoadout();
            var beacon = AbilityAssetBuilder.BuildBeaconAt(_folder + "/DeployBeacon.asset");

            var loadout = AbilityAssetBuilder.EquipInLoadoutAt(loadoutPath, beacon, 1);

            Assert.That(loadout.Abilities, Is.EqualTo(new AbilityDefinition[] { null, beacon }));
        }

        [Test]
        public void EquippingRejectsAMissingAbilityABadSlotAMissingLoadoutAndABadBeacon()
        {
            var loadoutPath = BuildLoadout();
            var beacon = AbilityAssetBuilder.BuildBeaconAt(_folder + "/DeployBeacon.asset");

            Assert.Throws<ArgumentNullException>(() => AbilityAssetBuilder.EquipInLoadoutAt(loadoutPath, null, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => AbilityAssetBuilder.EquipInLoadoutAt(loadoutPath, beacon, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => AbilityAssetBuilder.EquipInLoadoutAt(loadoutPath, beacon, AbilityHost.SlotCount));
            Assert.Throws<InvalidOperationException>(() => AbilityAssetBuilder.EquipInLoadoutAt(_folder + "/Missing.asset", beacon, 0));

            beacon.Uses = 0;
            Assert.Throws<AbilityDefinitionException>(() => AbilityAssetBuilder.EquipInLoadoutAt(loadoutPath, beacon, 0));
            Assert.That(AssetDatabase.LoadAssetAtPath<LoadoutDefinition>(loadoutPath).Abilities, Is.Empty, "nothing is saved when the beacon is invalid");
        }

        [Test]
        public void RejectsABeaconPathOutsideTheAssets()
        {
            Assert.Throws<ArgumentException>(() => AbilityAssetBuilder.BuildBeaconAt("Elsewhere/DeployBeacon.asset"));
        }

        [Test]
        public void TheCommittedBeaconMatchesTheBuilderAndIsInTheDemoLoadout()
        {
            var beacon = AssetDatabase.LoadAssetAtPath<BeaconDeployDefinition>(AbilityAssetBuilder.BeaconAbilityPath);
            var loadout = AssetDatabase.LoadAssetAtPath<LoadoutDefinition>(WeaponAssetBuilder.LoadoutPath);

            Assert.That(beacon, Is.Not.Null, $"run {AbilityAssetBuilder.MenuPath}");
            Assert.That(beacon.Id, Is.EqualTo(AbilityAssetBuilder.BeaconAbilityId));
            Assert.That(beacon.DisplayNameKey, Is.EqualTo(BeaconSpec.DefaultNameKey));
            Assert.DoesNotThrow(() => beacon.ToBeaconSpec());
            Assert.That(AbilityAssetBuilder.LoadOrBuildBeaconAbility(), Is.SameAs(beacon), "an existing asset is read, not rebuilt");

            Assert.That(loadout, Is.Not.Null);
            Assert.That(loadout.Abilities.Count, Is.GreaterThan(AbilityAssetBuilder.BeaconLoadoutSlot), $"run {AbilityAssetBuilder.MenuPath}");
            Assert.That(loadout.Abilities[AbilityAssetBuilder.BeaconLoadoutSlot], Is.SameAs(beacon), $"run {AbilityAssetBuilder.MenuPath}");
        }

        [Test]
        public void TheCommittedGogglesAndDroneMatchTheBuilder()
        {
            var goggles = AssetDatabase.LoadAssetAtPath<VisionGogglesDefinition>(AbilityAssetBuilder.VisionGogglesAbilityPath);
            var drone = AssetDatabase.LoadAssetAtPath<DroneDeployDefinition>(AbilityAssetBuilder.DroneAbilityPath);

            Assert.That(goggles, Is.Not.Null, $"run {AbilityAssetBuilder.MenuPath}");
            Assert.That(drone, Is.Not.Null, $"run {AbilityAssetBuilder.MenuPath}");
            Assert.That(goggles.Id, Is.EqualTo(AbilityAssetBuilder.VisionGogglesAbilityId));
            Assert.That(goggles.ToModes(), Is.EqualTo(AbilityAssetBuilder.VisionGogglesModes), $"run {AbilityAssetBuilder.MenuPath}");
            Assert.That(drone.Id, Is.EqualTo(AbilityAssetBuilder.DroneAbilityId));
            Assert.DoesNotThrow(() => drone.ToDroneSpec());
            Assert.That(AbilityAssetBuilder.LoadOrBuildVisionGogglesAbility(), Is.SameAs(goggles), "an existing asset is read, not rebuilt");
            Assert.That(AbilityAssetBuilder.LoadOrBuildDroneAbility(), Is.SameAs(drone));
        }

        [Test]
        public void TheCatalogIsSavedNextToTheOtherAbilitiesAndListsTheDemoAbilitiesInOrder()
        {
            Assert.That(AbilityAssetBuilder.AbilityCatalogPath, Is.EqualTo(AbilityAssetBuilder.Folder + "/AbilityCatalog.asset"));
            Assert.That(AbilityAssetBuilder.CatalogAbilityIds, Is.EqualTo(new[]
            {
                "deploy-beacon",
                "vision-goggles",
                "deploy-drone",
                "deploy-shield",
                "deploy-turret",
                "deploy-decoy",
                "deploy-mine",
                "deploy-remote-charge",
                "distraction-throw",
                "grapple",
            }));
            Assert.That(AbilityAssetBuilder.CatalogAbilityIds, Does.Not.Contain(AbilityAssetBuilder.NullAbilityId), "the do-nothing ability is only for tests");
        }

        [Test]
        public void BuildsACatalogInTheGivenOrder()
        {
            var path = _folder + "/AbilityCatalog.asset";
            var beacon = AbilityAssetBuilder.BuildBeaconAt(_folder + "/DeployBeacon.asset");
            var goggles = AbilityAssetBuilder.BuildVisionGogglesAt(_folder + "/VisionGoggles.asset");
            var drone = AbilityAssetBuilder.BuildDroneAt(_folder + "/DeployDrone.asset");

            var catalog = AbilityAssetBuilder.BuildCatalogAt(path, new AbilityDefinition[] { beacon, goggles, drone });

            Assert.That(AssetDatabase.LoadAssetAtPath<AbilityCatalogAsset>(path), Is.SameAs(catalog));
            Assert.That(catalog.name, Is.EqualTo("AbilityCatalog"));
            Assert.That(catalog.Abilities, Is.EqualTo(new AbilityDefinition[] { beacon, goggles, drone }));
            Assert.That(catalog.ToIds(), Is.EqualTo(new[] { "deploy-beacon", "vision-goggles", "deploy-drone" }));
            Assert.That(catalog.TryFind("vision-goggles", out var found), Is.True);
            Assert.That(found, Is.SameAs(goggles));
        }

        [Test]
        public void ASecondCatalogRunKeepsTheGuidAndReplacesTheList()
        {
            var path = _folder + "/AbilityCatalog.asset";
            var beacon = AbilityAssetBuilder.BuildBeaconAt(_folder + "/DeployBeacon.asset");
            var drone = AbilityAssetBuilder.BuildDroneAt(_folder + "/DeployDrone.asset");
            var catalog = AbilityAssetBuilder.BuildCatalogAt(path, new AbilityDefinition[] { beacon });
            var guid = AssetDatabase.AssetPathToGUID(path);

            var rebuilt = AbilityAssetBuilder.BuildCatalogAt(path, new AbilityDefinition[] { drone, beacon });

            Assert.That(rebuilt, Is.SameAs(catalog));
            Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
            Assert.That(rebuilt.ToIds(), Is.EqualTo(new[] { "deploy-drone", "deploy-beacon" }));
        }

        [Test]
        public void AnInvalidCatalogIsRefusedAndTheSavedOneIsKept()
        {
            var path = _folder + "/AbilityCatalog.asset";
            var beacon = AbilityAssetBuilder.BuildBeaconAt(_folder + "/DeployBeacon.asset");
            var drone = AbilityAssetBuilder.BuildDroneAt(_folder + "/DeployDrone.asset");
            var catalog = AbilityAssetBuilder.BuildCatalogAt(path, new AbilityDefinition[] { beacon, drone });

            var repeated = Assert.Throws<AbilityDefinitionException>(
                () => AbilityAssetBuilder.BuildCatalogAt(path, new AbilityDefinition[] { beacon, beacon }));
            Assert.That(repeated.Message, Does.Contain("'AbilityCatalog'").And.Contain("deploy-beacon"));
            Assert.Throws<AbilityDefinitionException>(() => AbilityAssetBuilder.BuildCatalogAt(path, new AbilityDefinition[] { beacon, null }));
            Assert.Throws<AbilityDefinitionException>(() => AbilityAssetBuilder.BuildCatalogAt(path, new AbilityDefinition[0]));
            Assert.Throws<ArgumentNullException>(() => AbilityAssetBuilder.BuildCatalogAt(path, null));

            Assert.That(catalog.Abilities, Is.EqualTo(new AbilityDefinition[] { beacon, drone }), "a refused list does not touch the saved catalog");
            Assert.That(catalog.ToIds(), Is.EqualTo(new[] { "deploy-beacon", "deploy-drone" }));
        }

        [Test]
        public void ACatalogWithoutAnyAbilityIsNotSaved()
        {
            var path = _folder + "/AbilityCatalog.asset";

            Assert.Throws<AbilityDefinitionException>(() => AbilityAssetBuilder.BuildCatalogAt(path, new AbilityDefinition[0]));

            Assert.That(AssetDatabase.LoadAssetAtPath<AbilityCatalogAsset>(path), Is.Null);
        }

        [Test]
        public void RejectsACatalogPathOutsideTheAssets()
        {
            var beacon = AbilityAssetBuilder.BuildBeaconAt(_folder + "/DeployBeacon.asset");

            Assert.Throws<ArgumentException>(() => AbilityAssetBuilder.BuildCatalogAt("Elsewhere/AbilityCatalog.asset", new AbilityDefinition[] { beacon }));
        }

        [Test]
        public void TheBuilderAndTheGameContentEntryCanBeCalledWithExecuteMethod()
        {
            foreach (var (type, name) in new[]
            {
                (typeof(AbilityAssetBuilder), nameof(AbilityAssetBuilder.Build)),
                (typeof(GameContentAssets), nameof(GameContentAssets.Create)),
            })
            {
                var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
                Assert.That(method, Is.Not.Null, $"{type.FullName}.{name} needs to be public, static and take no arguments");
                Assert.That(method.ReturnType, Is.EqualTo(typeof(void)));
                Assert.That(type.FullName, Is.EqualTo("Shooter.Editor." + type.Name), "-executeMethod names the type with its namespace");
            }
        }

        [Test]
        public void TheCommittedCatalogMatchesTheBuilderAndGameContentPicksItUp()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AbilityCatalogAsset>(AbilityAssetBuilder.AbilityCatalogPath);

            Assert.That(catalog, Is.Not.Null, $"run {AbilityAssetBuilder.MenuPath}");
            Assert.That(catalog.ToIds(), Is.EqualTo(AbilityAssetBuilder.CatalogAbilityIds), $"run {AbilityAssetBuilder.MenuPath}");
            Assert.That(catalog.Abilities[0], Is.SameAs(AssetDatabase.LoadAssetAtPath<BeaconDeployDefinition>(AbilityAssetBuilder.BeaconAbilityPath)));
            Assert.That(AbilityAssetBuilder.LoadOrBuildCatalog(), Is.SameAs(catalog), "an existing catalog is read, not rebuilt");

            var found = GameContentAssets.FindInProject(typeof(AbilityCatalogAsset));
            Assert.That(found, Is.EqualTo(new UnityEngine.Object[] { catalog }), "game-content fills the slot only when there is one catalog");

            var content = ScriptableObject.CreateInstance<GameContent>();
            try
            {
                var left = GameContentAssets.FillEmpty(content, t => t == typeof(AbilityCatalogAsset) ? found : Array.Empty<UnityEngine.Object>(), _ => Array.Empty<TextAsset>());
                Assert.That(left, Does.Not.Contain("AbilityCatalog"));
                Assert.That(content.AbilityCatalog, Is.SameAs(catalog));

                GameContent.Use(content);
                Assert.That(GameContent.Pick(c => c.AbilityCatalog), Is.SameAs(catalog));

                var loadout = AssetDatabase.LoadAssetAtPath<LoadoutDefinition>(WeaponAssetBuilder.LoadoutPath);
                Assert.That(LoadoutSelectionAssets.TryCreateAbilities(loadout, GameContent.Pick(c => c.AbilityCatalog), null, out var selection, out var error), Is.True, error);
                Assert.That(selection.SelectedAt(AbilityAssetBuilder.BeaconLoadoutSlot), Is.EqualTo(AbilityAssetBuilder.BeaconAbilityId), "the loadout's beacon is chosen first");
            }
            finally
            {
                GameContent.Use(null);
                UnityEngine.Object.DestroyImmediate(content);
            }
        }

        private string BuildLoadout()
        {
            var loadoutPath = _folder + "/Loadout.asset";
            WeaponAssetBuilder.BuildAt(_folder + "/Rifle.asset", _folder + "/Handgun.asset", _folder + "/Catalog.asset", loadoutPath);
            return loadoutPath;
        }
    }
}
