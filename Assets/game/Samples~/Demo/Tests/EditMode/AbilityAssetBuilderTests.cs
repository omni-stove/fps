using System;
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
    }
}
