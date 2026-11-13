using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary><see cref="MissionCatalog"/> の検証と、<see cref="CampaignLaunchSelection"/> でメニューの選択をシーンへ渡すことを確かめる。</summary>
    public sealed class MissionCatalogTests
    {
        private const string ScenePath = "Assets/game/Scenes/Campaign01.unity";

        private readonly List<Object> _created = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            CampaignLaunchSelection.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            CampaignLaunchSelection.Clear();
            foreach (var created in _created)
            {
                Object.DestroyImmediate(created);
            }

            _created.Clear();
        }

        [Test]
        public void FindsAMissionById()
        {
            var catalog = NewCatalog(Entry("Campaign01"), Entry("Campaign02"));

            Assert.That(catalog.TryFind("Campaign02", out var entry), Is.True);
            Assert.That(entry.Id, Is.EqualTo("Campaign02"));
            Assert.That(entry.Definition.name, Is.EqualTo("Campaign02"));
            Assert.That(catalog.TryFind("campaign02", out _), Is.False, "ids are case-sensitive");
            Assert.That(catalog.TryFind("Missing", out var missing), Is.False);
            Assert.That(missing, Is.Null);
        }

        [Test]
        public void SetMissionsCopiesTheEntries()
        {
            var entries = new List<MissionCatalogEntry> { Entry("Campaign01") };
            var catalog = NewCatalog(entries.ToArray());

            entries.Add(Entry("Campaign02"));

            Assert.That(catalog.Missions, Has.Count.EqualTo(1));
            Assert.That(catalog.Missions[0], Is.Not.SameAs(entries[0]));
        }

        [Test]
        public void RejectsAnEmptyCatalog()
        {
            var catalog = NewCatalog();

            Assert.That(() => catalog.Validate(), Throws.InstanceOf<MissionCatalogException>().With.Message.Contains("has no missions"));
            Assert.That(() => catalog.TryFind("Campaign01", out _), Throws.InstanceOf<MissionCatalogException>());
        }

        [Test]
        public void RejectsANullEntryAndDuplicateIds()
        {
            Assert.That(
                () => NewCatalog(Entry("Campaign01"), null).Validate(),
                Throws.InstanceOf<MissionCatalogException>().With.Message.Contains("mission #1 is empty"));

            var first = Entry("Campaign01");
            var again = new MissionCatalogEntry("Campaign01", "Again", ScenePath, first.Definition);
            Assert.That(
                () => NewCatalog(first, again).Validate(),
                Throws.InstanceOf<MissionCatalogException>().With.Message.Contains("'Campaign01' more than once"));
        }

        [Test]
        public void RejectsAnIdThatIsNotTheDefinitionsName()
        {
            var definition = Definition("Campaign01");
            var catalog = NewCatalog(new MissionCatalogEntry("Campaign02", "Two", ScenePath, definition));

            Assert.That(
                () => catalog.Validate(),
                Throws.InstanceOf<MissionCatalogException>().With.Message.Contains("must equal the definition's asset name"));
        }

        [Test]
        public void RejectsBrokenEntries()
        {
            var definition = Definition("Campaign01");
            var invalid = new[]
            {
                (Entry: new MissionCatalogEntry("Campaign 01", "One", ScenePath, definition), Reason: "letters, digits"),
                (Entry: new MissionCatalogEntry("Campaign01", " ", ScenePath, definition), Reason: "no display name"),
                (Entry: new MissionCatalogEntry("Campaign01", "One", "Scenes/Campaign01.unity", definition), Reason: "must be an Assets/ path"),
                (Entry: new MissionCatalogEntry("Campaign01", "One", "Assets/game/Scenes/Campaign01.asset", definition), Reason: "must be an Assets/ path"),
                (Entry: new MissionCatalogEntry("Campaign01", "One", ScenePath, null), Reason: "has no MissionDefinition"),
            };

            foreach (var (entry, reason) in invalid)
            {
                Assert.That(
                    () => NewCatalog(entry).Validate(),
                    Throws.InstanceOf<MissionCatalogException>().With.Message.Contains(reason),
                    reason);
            }
        }

        [Test]
        public void RejectsADefinitionWithoutObjectives()
        {
            var definition = ScriptableObject.CreateInstance<MissionDefinition>();
            definition.name = "Campaign01";
            _created.Add(definition);
            var catalog = NewCatalog(new MissionCatalogEntry("Campaign01", "One", ScenePath, definition));

            Assert.That(
                () => catalog.Validate(),
                Throws.InstanceOf<MissionCatalogException>().With.Message.Contains("has no objectives"));
        }

        [Test]
        public void LaunchSelectsTheMissionAndOpensItsScene()
        {
            var catalog = NewCatalog(Entry("Campaign01"));
            var loaded = new List<string>();

            LogAssert.Expect(LogType.Log, "[Campaign] selected launch mission=Campaign01 difficulty=hard");
            var entry = CampaignLaunchSelection.Launch(catalog, "Campaign01", CampaignDifficulty.Hard, loaded.Add);

            Assert.That(entry.Id, Is.EqualTo("Campaign01"));
            Assert.That(loaded, Is.EqualTo(new[] { ScenePath }));
            var current = CampaignLaunchSelection.Current;
            Assert.That(current, Is.Not.Null);
            Assert.That(current.MissionId, Is.EqualTo("Campaign01"));
            Assert.That(current.Difficulty, Is.EqualTo(CampaignDifficulty.Hard));
        }

        [Test]
        public void AFailedLaunchKeepsThePreviousSelection()
        {
            var catalog = NewCatalog(Entry("Campaign01"));
            var previous = new CampaignLaunch("Campaign01", CampaignDifficulty.Easy);
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Campaign\] selected launch mission=Campaign01 difficulty=easy$"));
            CampaignLaunchSelection.Select(previous);

            Assert.Throws<ArgumentException>(
                () => CampaignLaunchSelection.Launch(catalog, "Missing", CampaignDifficulty.Hard, _ => Assert.Fail("must not load")));
            Assert.That(CampaignLaunchSelection.Current, Is.SameAs(previous));

            Assert.Throws<ArgumentException>(
                () => CampaignLaunchSelection.Launch(catalog, "bad id", CampaignDifficulty.Hard, _ => Assert.Fail("must not load")));
            Assert.That(CampaignLaunchSelection.Current, Is.SameAs(previous));

            LogAssert.Expect(LogType.Log, new Regex(@"^\[Campaign\] selected launch mission=Campaign01 difficulty=hard$"));
            Assert.Throws<InvalidOperationException>(
                () => CampaignLaunchSelection.Launch(
                    catalog, "Campaign01", CampaignDifficulty.Hard, _ => throw new InvalidOperationException("load failed")));
            Assert.That(CampaignLaunchSelection.Current, Is.SameAs(previous), "a scene that fails to load does not leave its selection behind");

            Assert.Throws<ArgumentNullException>(() => CampaignLaunchSelection.Launch(null, "Campaign01", CampaignDifficulty.Hard, _ => { }));
            Assert.Throws<ArgumentNullException>(() => CampaignLaunchSelection.Select(null));
        }

        [Test]
        public void LaunchingASceneOutsideBuildSettingsIsRefused()
        {
            var definition = Definition("NotBuilt");
            var catalog = NewCatalog(new MissionCatalogEntry("NotBuilt", "Not built", "Assets/__not_in_build_settings__/NotBuilt.unity", definition));

            Assert.Throws<InvalidOperationException>(() => CampaignLaunchSelection.Launch(catalog, "NotBuilt", CampaignDifficulty.Normal));
            Assert.That(CampaignLaunchSelection.Current, Is.Null);
        }

        [Test]
        public void ClearForgetsTheSelection()
        {
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Campaign\] selected launch "));
            CampaignLaunchSelection.Select(new CampaignLaunch("Campaign01", CampaignDifficulty.Normal));

            CampaignLaunchSelection.Clear();

            Assert.That(CampaignLaunchSelection.Current, Is.Null);
        }

        private MissionCatalog NewCatalog(params MissionCatalogEntry[] entries)
        {
            var catalog = ScriptableObject.CreateInstance<MissionCatalog>();
            catalog.name = "TestCatalog";
            catalog.SetMissions(entries);
            _created.Add(catalog);
            return catalog;
        }

        private MissionCatalogEntry Entry(string id)
        {
            return new MissionCatalogEntry(id, id + " name", ScenePath, Definition(id));
        }

        private MissionDefinition Definition(string id)
        {
            var definition = ScriptableObject.CreateInstance<MissionDefinition>();
            definition.name = id;
            definition.SetObjectives(new[] { new ObjectiveDefinition("reach", "") });
            _created.Add(definition);
            return definition;
        }
    }
}
