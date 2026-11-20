using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Net.Tests
{
    // 能力のアセットは一時フォルダに作り、リポジトリの Abilities と server-data を触らない。
    public sealed class ServerRulesExporterAbilitiesTests
    {
        private const string Placeable =
            "{\"types\": [{\"id\": \"deploy-beacon\", \"role\": \"Beacon\"}, {\"id\": \"deploy-shield\", \"role\": \"Shield\"},"
            + " {\"id\": \"deploy-drone\", \"role\": \"Drone\"}]}";

        private readonly List<Object> _created = new List<Object>();
        private string _folder;
        private string _directory;
        private DeployCatalog _deployables;
        private BeaconDeployDefinition _beacon;
        private ShieldDeployDefinition _shield;
        private DroneDeployDefinition _drone;
        private VisionGogglesDefinition _goggles;

        [SetUp]
        public void SetUp()
        {
            // プロジェクトの Resources/ShooterKit に置かれた入口の欄に左右されないよう、空の入口に替えておく。
            GameContent.Use(Create<GameContent>("Empty"));
            _folder = "Assets/__ServerRulesExporterAbilitiesTests_" + Guid.NewGuid().ToString("N");
            _directory = Path.Combine(Path.GetTempPath(), "ServerRulesExporterAbilitiesTests-" + Guid.NewGuid().ToString("N"));
            _deployables = DeployCatalogJson.Parse(Placeable, "deployables.json");
            _beacon = AbilityAssetBuilder.BuildBeaconAt(_folder + "/DeployBeacon.asset");
            _shield = AbilityAssetBuilder.BuildShieldAt(_folder + "/DeployShield.asset");
            _drone = AbilityAssetBuilder.BuildDroneAt(_folder + "/DeployDrone.asset");
            _goggles = AbilityAssetBuilder.BuildVisionGogglesAt(_folder + "/VisionGoggles.asset");
        }

        [TearDown]
        public void TearDown()
        {
            GameContent.Use(null);
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
            if (AssetDatabase.IsValidFolder(_folder))
            {
                AssetDatabase.DeleteAsset(_folder);
            }

            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, true);
            }
        }

        [Test]
        public void TheExportCanBeCalledWithExecuteMethod()
        {
            var method = typeof(ServerRulesExporter).GetMethod(
                nameof(ServerRulesExporter.Export), BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);

            Assert.That(method, Is.Not.Null, "Shooter.Editor.ServerRulesExporter.Export needs to be public, static and take no arguments");
            Assert.That(method.ReturnType, Is.EqualTo(typeof(void)));
            Assert.That(typeof(ServerRulesExporter).FullName, Is.EqualTo("Shooter.Editor.ServerRulesExporter"));
        }

        [Test]
        public void TheDeployablesAreReadFromTheFileTheServerReads()
        {
            Assert.That(ServerRulesExporter.DeployablesFileName, Is.EqualTo("deployables.json"));
            Assert.That(ServerRulesExporter.TuningFolder, Is.EqualTo("server-data/tuning"));
            Assert.That(
                ServerRulesExporter.DeployablesPath,
                Is.EqualTo(Path.Combine(ServerRulesExporter.ProjectRoot, "server-data/tuning", "deployables.json")));
        }

        [Test]
        public void TheCommittedDeployablesLetTheBeaconShieldTurretAndDecoyBePlacedOnline()
        {
            var deployables = ServerRulesExporter.LoadDeployables(ServerRulesExporter.DeployablesPath);

            Assert.That(ServerRulesExporter.IsPlaceableOnline(deployables, AbilityAssetBuilder.BeaconAbilityId), Is.True);
            Assert.That(ServerRulesExporter.IsPlaceableOnline(deployables, AbilityAssetBuilder.ShieldAbilityId), Is.True);
            Assert.That(ServerRulesExporter.IsPlaceableOnline(deployables, AbilityAssetBuilder.TurretAbilityId), Is.True);
            Assert.That(ServerRulesExporter.IsPlaceableOnline(deployables, AbilityAssetBuilder.DecoyAbilityId), Is.True);
            Assert.That(ServerRulesExporter.IsPlaceableOnline(deployables, AbilityAssetBuilder.VisionGogglesAbilityId), Is.False);
            Assert.That(ServerRulesExporter.IsPlaceableOnline(deployables, AbilityAssetBuilder.DroneAbilityId), Is.False);
            Assert.That(ServerRulesExporter.IsPlaceableOnline(deployables, AbilityAssetBuilder.GrappleAbilityId), Is.False);
        }

        [Test]
        public void AMissingDeployablesFilePlacesNothingAndABrokenOneIsRefused()
        {
            Directory.CreateDirectory(_directory);
            var missing = Path.Combine(_directory, "missing.json");
            var broken = Path.Combine(_directory, "deployables.json");
            File.WriteAllText(broken, "{\"types\": [{\"id\": \"b\", \"role\": \"Nothing\"}]}");

            Assert.That(ServerRulesExporter.LoadDeployables(missing).Count, Is.Zero);
            var e = Assert.Throws<InvalidOperationException>(() => ServerRulesExporter.LoadDeployables(broken));
            Assert.That(e.Message, Does.StartWith(ServerRulesExporter.LogTag).And.Contain("deployables"));
            Assert.That(e.InnerException, Is.InstanceOf<TuningJsonException>());
            Assert.Throws<ArgumentException>(() => ServerRulesExporter.LoadDeployables(" "));
        }

        [Test]
        public void OnlyTypesTheServerPlacesCountAsPlaceable()
        {
            Assert.That(ServerRulesExporter.IsPlaceableOnline(_deployables, "deploy-beacon"), Is.True);
            Assert.That(ServerRulesExporter.IsPlaceableOnline(_deployables, "deploy-shield"), Is.True);
            Assert.That(ServerRulesExporter.IsPlaceableOnline(_deployables, "deploy-drone"), Is.False, "the server does not fly drones");
            Assert.That(ServerRulesExporter.IsPlaceableOnline(_deployables, "deploy-turret"), Is.False, "not in the file");
            Assert.That(ServerRulesExporter.IsPlaceableOnline(_deployables, string.Empty), Is.False);
            Assert.That(ServerRulesExporter.IsPlaceableOnline(_deployables, null), Is.False);
            Assert.Throws<ArgumentNullException>(() => ServerRulesExporter.IsPlaceableOnline(null, "deploy-beacon"));
        }

        [Test]
        public void TheLoadoutDefaultsComeFirstThenTheOtherCatalogAbilitiesTheServerPlaces()
        {
            var loadout = Loadout(_shield, _goggles);
            var catalog = Catalog(_beacon, _goggles, _drone, _shield);

            var abilities = ServerRulesExporter.ToLoadoutAbilities(loadout, catalog, _deployables);

            Assert.That(abilities, Is.EqualTo(new[] { "deploy-shield", "deploy-beacon" }), "the goggles and the drone are not placed by the server");
        }

        [Test]
        public void ADroneInTheLoadoutIsLeftOut()
        {
            var abilities = ServerRulesExporter.ToLoadoutAbilities(Loadout(_drone, _shield), Catalog(_drone, _shield), _deployables);

            Assert.That(abilities, Is.EqualTo(new[] { "deploy-shield" }));
        }

        [Test]
        public void AnAbilityTheCatalogDoesNotOfferIsLeftOut()
        {
            var abilities = ServerRulesExporter.ToLoadoutAbilities(Loadout(_beacon, _shield), Catalog(_shield), _deployables);

            Assert.That(abilities, Is.EqualTo(new[] { "deploy-shield" }), "the menu would not offer the beacon either");
        }

        [Test]
        public void WithoutACatalogOnlyTheLoadoutAbilitiesAreWritten()
        {
            Assert.That(AbilityHost.SlotCount, Is.EqualTo(2));

            var abilities = ServerRulesExporter.ToLoadoutAbilities(Loadout(_beacon), null, _deployables);

            Assert.That(abilities, Is.EqualTo(new[] { "deploy-beacon" }));
        }

        [Test]
        public void EmptyRepeatedAndExtraSlotsAreLeftOutAsTheMenuWouldChooseThem()
        {
            Assert.That(
                ServerRulesExporter.ToLoadoutAbilities(Loadout(null, _beacon), null, _deployables),
                Is.EqualTo(new[] { "deploy-beacon" }), "the server never lists an empty Id as a candidate");
            Assert.That(
                ServerRulesExporter.ToLoadoutAbilities(Loadout(_beacon, _beacon), null, _deployables),
                Is.EqualTo(new[] { "deploy-beacon" }), "one ability cannot fill two slots");
            Assert.That(
                ServerRulesExporter.ToLoadoutAbilities(Loadout(_beacon, _shield, _drone), null, _deployables),
                Is.EqualTo(new[] { "deploy-beacon", "deploy-shield" }), "a slot past AbilityHost.SlotCount is dropped");
            Assert.That(
                ServerRulesExporter.ToLoadoutAbilities(Loadout(), null, _deployables),
                Is.Empty);
        }

        [Test]
        public void WithoutAnythingToPlaceTheListIsEmpty()
        {
            var abilities = ServerRulesExporter.ToLoadoutAbilities(Loadout(_beacon, _shield), Catalog(_beacon, _shield), DeployCatalog.Empty);

            Assert.That(abilities, Is.Empty);
        }

        [Test]
        public void ABrokenCatalogOrAMissingArgumentIsRefused()
        {
            var empty = Create<AbilityCatalogAsset>("EmptyCatalog");

            var e = Assert.Throws<InvalidOperationException>(() => ServerRulesExporter.ToLoadoutAbilities(Loadout(_beacon), empty, _deployables));

            Assert.That(e.Message, Does.StartWith(ServerRulesExporter.LogTag).And.Contain("'EmptyCatalog' has no abilities"));
            Assert.That(e.InnerException, Is.InstanceOf<AbilityDefinitionException>());
            Assert.Throws<ArgumentNullException>(() => ServerRulesExporter.ToLoadoutAbilities(null, null, _deployables));
            Assert.Throws<ArgumentNullException>(() => ServerRulesExporter.ToLoadoutAbilities(Loadout(_beacon), null, null));
        }

        [Test]
        public void TheRulesFilesCarryTheLoadoutAbilitiesTheServerReadsBack()
        {
            var match = NetSceneBuilder.LoadOrBuildMatch();
            var loadout = NetMatchDefinition.PickLoadout(match);
            Assert.That(loadout, Is.Not.Null);
            var content = Create<GameContent>("Game");
            content.AbilityCatalog = Catalog(_beacon, _shield, _drone);
            GameContent.Use(content);
            var expected = ServerRulesExporter.ToLoadoutAbilities(loadout, content.AbilityCatalog, _deployables);

            var written = ServerRulesExporter.WriteRules(_directory, match, new[] { Rules("TeamDeathmatch") }, _deployables);

            var text = File.ReadAllText(written[0]);
            var file = NetMatchFile.Parse(text, written[0]);
            Assert.That(text, Does.Contain("\"" + NetMatchFile.AbilitiesItem + "\": ["));
            Assert.That(file.LoadoutAbilities, Is.EqualTo(expected));
            Assert.That(file.LoadoutAbilities, Has.No.Member(string.Empty).And.No.Member("deploy-drone"));
        }

        [Test]
        public void TheDemoBeaconGoesOnlineInItsSlot()
        {
            var match = NetSceneBuilder.LoadOrBuildMatch();
            var loadout = NetMatchDefinition.PickLoadout(match);
            Assert.That(LoadoutSelectionAssets.DefaultAbilityIds(loadout)[AbilityAssetBuilder.BeaconLoadoutSlot], Is.EqualTo(AbilityAssetBuilder.BeaconAbilityId),
                $"run {AbilityAssetBuilder.MenuPath}");

            var written = ServerRulesExporter.WriteRules(_directory, match, new[] { Rules("TeamDeathmatch") }, _deployables);

            var file = NetMatchFile.Parse(File.ReadAllText(written[0]), written[0]);
            Assert.That(file.LoadoutAbilities[AbilityAssetBuilder.BeaconLoadoutSlot], Is.EqualTo(AbilityAssetBuilder.BeaconAbilityId));
        }

        [Test]
        public void WithNothingToPlaceTheRulesFilesWriteAnEmptyList()
        {
            var match = NetSceneBuilder.LoadOrBuildMatch();

            var written = ServerRulesExporter.WriteRules(_directory, match, new[] { Rules("TeamDeathmatch") }, DeployCatalog.Empty);

            var file = NetMatchFile.Parse(File.ReadAllText(written[0]), written[0]);
            Assert.That(file.LoadoutAbilities, Is.Not.Null.And.Empty, "an empty list places nothing, unlike a missing list");
            Assert.Throws<ArgumentNullException>(() => ServerRulesExporter.WriteRules(_directory, match, new[] { Rules("Other") }, null));
        }

        [Test]
        public void ToFileWithoutAbilitiesLeavesTheItemOut()
        {
            var match = NetSceneBuilder.LoadOrBuildMatch();

            var file = ServerRulesExporter.ToFile(match, Rules("TeamDeathmatch"), match.ToMotor());

            Assert.That(file.LoadoutAbilities, Is.Null);
            Assert.That(NetMatchFile.Write(file), Does.Not.Contain("\"" + NetMatchFile.AbilitiesItem + "\": ["));
        }

        private LoadoutDefinition Loadout(params AbilityDefinition[] abilities)
        {
            var loadout = Create<LoadoutDefinition>("Loadout");
            loadout.SetAbilities(abilities);
            return loadout;
        }

        private AbilityCatalogAsset Catalog(params AbilityDefinition[] abilities)
        {
            var catalog = Create<AbilityCatalogAsset>("AbilityCatalog");
            catalog.SetAbilities(abilities);
            return catalog;
        }

        private GameRulesDefinition Rules(string assetName)
        {
            var asset = Create<GameRulesDefinition>(assetName);
            GameRulesAssetBuilder.Configure(asset, GameRulesAssetBuilder.TeamDeathmatchRules());
            return asset;
        }

        private T Create<T>(string assetName)
            where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            asset.name = assetName;
            _created.Add(asset);
            return asset;
        }
    }
}
