using System;
using System.Collections.Generic;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    /// <summary>
    /// <see cref="MissionCatalog"/> がミッションを <see cref="MissionDefinition.Id"/> で引き (アセット名は見ない)、
    /// 空の Id と重なった Id を拒むこと、キャンペーンの秒数を持つことを確かめる。
    /// </summary>
    public sealed class MissionCatalogIdTests
    {
        private const string ScenePath = "Assets/game/Scenes/Campaign01.unity";

        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created)
            {
                Object.DestroyImmediate(created);
            }

            _created.Clear();
        }

        [Test]
        public void FindsAMissionByAnIdThatIsNotItsAssetName()
        {
            var first = Definition("RenamedFirstAsset", "Campaign01");
            var second = Definition("Mission_B_v2", "Campaign02");
            var catalog = NewCatalog(Entry("Campaign01", first), Entry("Campaign02", second));

            Assert.That(catalog.TryFind("Campaign02", out var entry), Is.True);
            Assert.That(entry.Definition, Is.SameAs(second));
            Assert.That(entry.Id, Is.EqualTo("Campaign02"));
            Assert.That(catalog.TryFind("Mission_B_v2", out _), Is.False, "the asset name is not an id");
            Assert.That(catalog.TryFind("RenamedFirstAsset", out _), Is.False, "the asset name is not an id");
        }

        [Test]
        public void RenamingTheDefinitionAssetKeepsTheMissionFindable()
        {
            var definition = Definition("Campaign01", "Campaign01");
            var catalog = NewCatalog(Entry("Campaign01", definition));

            definition.name = "Campaign01_Renamed";

            Assert.That(catalog.TryFind("Campaign01", out var entry), Is.True);
            Assert.That(entry.Definition, Is.SameAs(definition));
        }

        [Test]
        public void RejectsADefinitionWithAnEmptyId()
        {
            foreach (var empty in new[] { string.Empty, null, "   " })
            {
                var definition = Definition("Campaign01", empty);
                var catalog = NewCatalog(Entry("Campaign01", definition));

                Assert.That(
                    () => catalog.Validate(),
                    Throws.InstanceOf<MissionCatalogException>().With.Message.Contains("MissionDefinition 'Campaign01', which has no id"),
                    $"id '{empty}'");
                Assert.That(() => catalog.TryFind("Campaign01", out _), Throws.InstanceOf<MissionCatalogException>());
            }
        }

        [Test]
        public void EmptyIdIsTheDefaultOfANewDefinition()
        {
            var definition = ScriptableObject.CreateInstance<MissionDefinition>();
            _created.Add(definition);

            Assert.That(definition.Id, Is.Empty);
            definition.Id = null;
            Assert.That(definition.Id, Is.Empty);
        }

        [Test]
        public void RejectsTwoDefinitionsWithTheSameId()
        {
            var first = Definition("FirstAsset", "Campaign01");
            var second = Definition("SecondAsset", "Campaign01");
            var catalog = NewCatalog(Entry("Campaign01", first), Entry("Campaign01", second));

            Assert.That(
                () => catalog.Validate(),
                Throws.InstanceOf<MissionCatalogException>()
                    .With.Message.EqualTo("mission catalog 'TestCatalog' has mission id 'Campaign01' more than once"));
            Assert.That(() => catalog.TryFind("Campaign01", out _), Throws.InstanceOf<MissionCatalogException>());
        }

        [Test]
        public void RejectsAnEntryWhoseIdDiffersFromItsDefinitionsId()
        {
            var definition = Definition("Campaign02", "Campaign01");
            var catalog = NewCatalog(Entry("Campaign02", definition));

            Assert.That(
                () => catalog.Validate(),
                Throws.InstanceOf<MissionCatalogException>().With.Message.EqualTo(
                    "mission catalog 'TestCatalog' mission #0: mission 'Campaign02' uses MissionDefinition 'Campaign02' whose id is 'Campaign01'; the ids must be equal"));
        }

        [Test]
        public void HoldsTheCampaignTimingsWithTheOldDefaults()
        {
            var catalog = NewCatalog(Entry("Campaign01", Definition("Campaign01", "Campaign01")));

            Assert.That(catalog.ReloadDelaySeconds, Is.EqualTo(3f));
            Assert.That(catalog.SavedNoticeSeconds, Is.EqualTo(2.5f));
            Assert.That(MissionCatalog.DefaultReloadDelaySeconds, Is.EqualTo(3f));
            Assert.That(MissionCatalog.DefaultSavedNoticeSeconds, Is.EqualTo(2.5f));

            catalog.ReloadDelaySeconds = 0f;
            catalog.SavedNoticeSeconds = 4f;
            Assert.That(catalog.ReloadDelaySeconds, Is.EqualTo(0f));
            Assert.That(catalog.SavedNoticeSeconds, Is.EqualTo(4f));
            Assert.That(() => catalog.Validate(), Throws.Nothing);
        }

        [Test]
        public void RejectsTimingsThatAreNotSeconds()
        {
            var catalog = NewCatalog(Entry("Campaign01", Definition("Campaign01", "Campaign01")));

            foreach (var bad in new[] { -1f, float.NaN, float.PositiveInfinity })
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => catalog.ReloadDelaySeconds = bad, $"reload {bad}");
                Assert.Throws<ArgumentOutOfRangeException>(() => catalog.SavedNoticeSeconds = bad, $"notice {bad}");
            }

            Assert.That(catalog.ReloadDelaySeconds, Is.EqualTo(3f), "a rejected value is not kept");
            Assert.That(catalog.SavedNoticeSeconds, Is.EqualTo(2.5f), "a rejected value is not kept");
        }

        private MissionCatalog NewCatalog(params MissionCatalogEntry[] entries)
        {
            var catalog = ScriptableObject.CreateInstance<MissionCatalog>();
            catalog.name = "TestCatalog";
            catalog.SetMissions(entries);
            _created.Add(catalog);
            return catalog;
        }

        private static MissionCatalogEntry Entry(string id, MissionDefinition definition)
        {
            return new MissionCatalogEntry(id, MissionDefinition.NameKeyOf(id), ScenePath, definition);
        }

        private MissionDefinition Definition(string assetName, string id)
        {
            var definition = ScriptableObject.CreateInstance<MissionDefinition>();
            definition.name = assetName;
            definition.Id = id;
            definition.SetObjectives(new[] { new ObjectiveDefinition("reach", "") });
            _created.Add(definition);
            return definition;
        }
    }
}
