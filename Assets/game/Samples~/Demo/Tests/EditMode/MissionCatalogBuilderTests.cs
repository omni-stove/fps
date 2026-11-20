using System.Linq;
using System;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Shooter.Tests.EditMode
{
    // リポジトリの Assets/game/Missions を触らないよう、一時フォルダに走らせる。
    public sealed class MissionCatalogBuilderTests
    {
        private string _folder;
        private string _catalogPath;
        private string _definitionPath;

        [SetUp]
        public void SetUp()
        {
            _folder = "Assets/__MissionCatalogBuilderTests_" + Guid.NewGuid().ToString("N");
            _catalogPath = _folder + "/MissionCatalog.asset";
            _definitionPath = _folder + "/" + MissionCatalogBuilder.Campaign01Id + ".asset";
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
        public void DefaultMissionsAreCampaign01ThenCampaign02()
        {
            Assert.That(MissionCatalogBuilder.CatalogPath, Is.EqualTo(DemoSample.Root + "/Missions/MissionCatalog.asset"));
            var missions = MissionCatalogBuilder.DefaultMissions();

            Assert.That(missions.Count(), Is.EqualTo(2));
            Assert.That(missions[0].Id, Is.EqualTo("Campaign01"));
            Assert.That(missions[0].NameKey, Is.EqualTo(CampaignSceneBuilder.MissionNameKey));
            Assert.That(missions[0].ScenePath, Is.EqualTo(CampaignSceneBuilder.ScenePath));
            Assert.That(missions[0].DefinitionPath, Is.EqualTo(CampaignSceneBuilder.DefinitionPath));

            Assert.That(missions[1].Id, Is.EqualTo(MissionCatalogBuilder.Campaign02Id));
            Assert.That(missions[1].Id, Is.EqualTo("Campaign02"));
            Assert.That(missions[1].NameKey, Is.EqualTo(Campaign02Mission.MissionNameKey));
            Assert.That(missions[1].ScenePath, Is.EqualTo(BuildSceneList.Campaign02Scene));
            Assert.That(missions[1].DefinitionPath, Is.EqualTo(Campaign02Mission.DefinitionPath));
        }

        [Test]
        public void BuildsACatalogWithBothMissionsInOrder()
        {
            var first = CreateDefinition();
            var secondPath = _folder + "/" + MissionCatalogBuilder.Campaign02Id + ".asset";
            var second = MissionDefinitionAssets.CreateOrUpdate(
                secondPath,
                d => d.SetObjectives(new[] { new ObjectiveDefinition("reach", "") }));
            var missions = new[]
            {
                new MissionCatalogBuilder.MissionSource(
                    MissionCatalogBuilder.Campaign01Id, CampaignSceneBuilder.MissionNameKey, BuildSceneList.CampaignScene, _definitionPath),

                // シーンは開かずに有無だけを見るので、リポジトリにある Campaign01 のシーンを指す。
                new MissionCatalogBuilder.MissionSource(
                    MissionCatalogBuilder.Campaign02Id, Campaign02Mission.MissionNameKey, BuildSceneList.CampaignScene, secondPath),
            };

            var catalog = MissionCatalogBuilder.BuildAt(_catalogPath, missions);

            Assert.That(catalog.Missions.Count(), Is.EqualTo(2));
            Assert.That(catalog.Missions[0].Definition, Is.SameAs(first));
            Assert.That(catalog.Missions[1].Id, Is.EqualTo("Campaign02"));
            Assert.That(catalog.Missions[1].Definition, Is.SameAs(second));
            Assert.That(catalog.TryFind("Campaign02", out var found), Is.True);
            Assert.That(found.DisplayNameKey, Is.EqualTo(Campaign02Mission.MissionNameKey));
            Assert.That(Localizer.Resolve(DisplayNames.Mission(found.DisplayNameKey)), Is.EqualTo("Campaign 02"));
        }

        [Test]
        public void BuildsTheCatalogAndKeepsItsGuid()
        {
            var definition = CreateDefinition();

            var catalog = MissionCatalogBuilder.BuildAt(_catalogPath, Missions(MissionCatalogBuilder.Campaign01Id));
            var guid = AssetDatabase.AssetPathToGUID(_catalogPath);

            Assert.That(catalog, Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<MissionCatalog>(_catalogPath), Is.SameAs(catalog));
            Assert.That(catalog.Missions.Count(), Is.EqualTo(1));
            var entry = catalog.Missions[0];
            Assert.That(entry.Id, Is.EqualTo("Campaign01"));
            Assert.That(entry.DisplayNameKey, Is.EqualTo(CampaignSceneBuilder.MissionNameKey));
            Assert.That(entry.ScenePath, Is.EqualTo(BuildSceneList.CampaignScene));
            Assert.That(entry.Definition, Is.SameAs(definition));
            Assert.That(catalog.TryFind("Campaign01", out _), Is.True);

            MissionCatalogBuilder.BuildAt(_catalogPath, Missions(MissionCatalogBuilder.Campaign01Id));
            Assert.That(AssetDatabase.AssetPathToGUID(_catalogPath), Is.EqualTo(guid), "rebuilding keeps the GUID");
        }

        [Test]
        public void AMissingDefinitionSavesNothing()
        {
            Assert.That(
                () => MissionCatalogBuilder.BuildAt(_catalogPath, Missions(MissionCatalogBuilder.Campaign01Id)),
                Throws.InvalidOperationException.With.Message.Contains("was not found"));
            Assert.That(AssetDatabase.LoadMainAssetAtPath(_catalogPath), Is.Null);
        }

        [Test]
        public void AMissingSceneSavesNothing()
        {
            CreateDefinition();
            var missions = new[]
            {
                new MissionCatalogBuilder.MissionSource(
                    MissionCatalogBuilder.Campaign01Id, CampaignSceneBuilder.MissionNameKey, _folder + "/Missing.unity", _definitionPath),
            };

            Assert.That(
                () => MissionCatalogBuilder.BuildAt(_catalogPath, missions),
                Throws.InvalidOperationException.With.Message.Contains("Missing.unity"));
            Assert.That(AssetDatabase.LoadMainAssetAtPath(_catalogPath), Is.Null);
        }

        [Test]
        public void AnInvalidCatalogKeepsTheSavedOne()
        {
            var definition = CreateDefinition();
            var catalog = MissionCatalogBuilder.BuildAt(_catalogPath, Missions(MissionCatalogBuilder.Campaign01Id));

            Assert.That(
                () => MissionCatalogBuilder.BuildAt(_catalogPath, Missions("bad id")),
                Throws.InstanceOf<MissionCatalogException>());
            Assert.That(catalog.Missions.Count(), Is.EqualTo(1));
            Assert.That(catalog.Missions[0].Id, Is.EqualTo("Campaign01"), "the in-memory catalog is not left with the rejected list");
            Assert.That(definition.Id, Is.EqualTo("Campaign01"), "the rejected id is not left on the definition");
        }

        [Test]
        public void GivesEachDefinitionItsMissionIdAndKeepsItsAssetName()
        {
            var renamedPath = _folder + "/RenamedCampaign01.asset";
            var definition = MissionDefinitionAssets.CreateOrUpdate(
                renamedPath,
                d => d.SetObjectives(new[] { new ObjectiveDefinition("reach", "") }));
            Assert.That(definition.Id, Is.Empty, "a definition made before it had an id");
            var missions = new[]
            {
                new MissionCatalogBuilder.MissionSource(
                    MissionCatalogBuilder.Campaign01Id, CampaignSceneBuilder.MissionNameKey, BuildSceneList.CampaignScene, renamedPath),
            };

            var catalog = MissionCatalogBuilder.BuildAt(_catalogPath, missions);

            Assert.That(definition.Id, Is.EqualTo("Campaign01"));
            Assert.That(definition.name, Is.EqualTo("RenamedCampaign01"), "the builder does not rename the asset");
            Assert.That(EditorUtility.IsDirty(definition), Is.False, "the id is saved with the catalog");
            Assert.That(catalog.TryFind("Campaign01", out var entry), Is.True);
            Assert.That(entry.Definition, Is.SameAs(definition));
        }

        [Test]
        public void ANewCatalogHasTheDefaultCampaignTimings()
        {
            CreateDefinition();

            var catalog = MissionCatalogBuilder.BuildAt(_catalogPath, Missions(MissionCatalogBuilder.Campaign01Id));

            Assert.That(catalog.ReloadDelaySeconds, Is.EqualTo(MissionCatalog.DefaultReloadDelaySeconds));
            Assert.That(catalog.ReloadDelaySeconds, Is.EqualTo(3f));
            Assert.That(catalog.SavedNoticeSeconds, Is.EqualTo(MissionCatalog.DefaultSavedNoticeSeconds));
            Assert.That(catalog.SavedNoticeSeconds, Is.EqualTo(2.5f));
        }

        [Test]
        public void RejectsBadArguments()
        {
            Assert.Throws<ArgumentNullException>(() => MissionCatalogBuilder.BuildAt(_catalogPath, null));
            Assert.Throws<ArgumentException>(() => MissionCatalogBuilder.BuildAt("MissionCatalog.asset", Missions("Campaign01")));
            Assert.Throws<ArgumentNullException>(() => MissionCatalogBuilder.Configure(null, Array.Empty<MissionCatalogEntry>()));

            var catalog = ScriptableObject.CreateInstance<MissionCatalog>();
            try
            {
                Assert.Throws<ArgumentNullException>(() => MissionCatalogBuilder.Configure(catalog, null));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        private MissionCatalogBuilder.MissionSource[] Missions(string id)
        {
            return new[]
            {
                new MissionCatalogBuilder.MissionSource(id, CampaignSceneBuilder.MissionNameKey, BuildSceneList.CampaignScene, _definitionPath),
            };
        }

        private MissionDefinition CreateDefinition()
        {
            return MissionDefinitionAssets.CreateOrUpdate(
                _definitionPath,
                d => d.SetObjectives(new[] { new ObjectiveDefinition("reach", "") }));
        }
    }
}
