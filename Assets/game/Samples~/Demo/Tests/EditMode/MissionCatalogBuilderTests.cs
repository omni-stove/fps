using System;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// <see cref="MissionCatalogBuilder"/> を一時フォルダに走らせて、出力と GUID の保持を確かめる。
    /// リポジトリの Assets/game/Missions は触らない。シーンはリポジトリの Campaign01 を指すだけで開かない。
    /// </summary>
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

            Assert.That(missions, Has.Count.EqualTo(2));
            Assert.That(missions[0].Id, Is.EqualTo("Campaign01"));
            Assert.That(missions[0].Id, Is.EqualTo(CampaignSaveSlots.LegacyMissionId), "Campaign01 is the mission that owned the legacy save slot");
            Assert.That(missions[0].NameKey, Is.EqualTo(CampaignSceneBuilder.MissionNameKey));
            Assert.That(missions[0].ScenePath, Is.EqualTo(CampaignSceneBuilder.ScenePath));
            Assert.That(missions[0].DefinitionPath, Is.EqualTo(CampaignSceneBuilder.DefinitionPath));

            Assert.That(missions[1].Id, Is.EqualTo(MissionCatalogBuilder.Campaign02Id));
            Assert.That(missions[1].Id, Is.EqualTo("Campaign02"));
            Assert.That(CampaignSaveSlots.ReadsLegacy(missions[1].Id), Is.False, "only Campaign01 reads the legacy save slot");
            Assert.That(missions[1].NameKey, Is.EqualTo(Campaign02Mission.MissionNameKey));
            Assert.That(missions[1].ScenePath, Is.EqualTo(BuildSceneList.Campaign02Scene));
            Assert.That(missions[1].DefinitionPath, Is.EqualTo(Campaign02Mission.DefinitionPath));
        }

        [Test]
        public void BuildsACatalogWithBothMissionsInOrder()
        {
            var first = CreateDefinition();
            var secondPath = _folder + "/" + MissionCatalogBuilder.Campaign02Id + ".asset";
            var second = SceneBuildHelpers.CreateOrUpdateMissionDefinition(
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

            Assert.That(catalog.Missions, Has.Count.EqualTo(2));
            Assert.That(catalog.Missions[0].Definition, Is.SameAs(first));
            Assert.That(catalog.Missions[1].Id, Is.EqualTo("Campaign02"));
            Assert.That(catalog.Missions[1].Definition, Is.SameAs(second));
            Assert.That(catalog.TryFind("Campaign02", out var found), Is.True);
            Assert.That(found.DisplayNameKey, Is.EqualTo(Campaign02Mission.MissionNameKey));
            Assert.That(Localizer.Resolve(found.DisplayName), Is.EqualTo("Campaign 02"));
        }

        [Test]
        public void BuildsTheCatalogAndKeepsItsGuid()
        {
            var definition = CreateDefinition();

            var catalog = MissionCatalogBuilder.BuildAt(_catalogPath, Missions(MissionCatalogBuilder.Campaign01Id));
            var guid = AssetDatabase.AssetPathToGUID(_catalogPath);

            Assert.That(catalog, Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<MissionCatalog>(_catalogPath), Is.SameAs(catalog));
            Assert.That(catalog.Missions, Has.Count.EqualTo(1));
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
            CreateDefinition();
            var catalog = MissionCatalogBuilder.BuildAt(_catalogPath, Missions(MissionCatalogBuilder.Campaign01Id));

            // Id が定義のアセット名と違う。
            Assert.That(
                () => MissionCatalogBuilder.BuildAt(_catalogPath, Missions("Campaign02")),
                Throws.InstanceOf<MissionCatalogException>());
            Assert.That(catalog.Missions, Has.Count.EqualTo(1));
            Assert.That(catalog.Missions[0].Id, Is.EqualTo("Campaign01"), "the in-memory catalog is not left with the rejected list");
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
            return SceneBuildHelpers.CreateOrUpdateMissionDefinition(
                _definitionPath,
                d => d.SetObjectives(new[] { new ObjectiveDefinition("reach", "") }));
        }
    }
}
