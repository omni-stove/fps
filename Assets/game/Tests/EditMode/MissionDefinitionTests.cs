using System.Linq;
using System;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class MissionDefinitionTests
    {
        private MissionDefinition _definition;

        [SetUp]
        public void SetUp()
        {
            _definition = ScriptableObject.CreateInstance<MissionDefinition>();
            _definition.name = "TestDefinition";
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_definition);
        }

        [Test]
        public void ConvertsEveryFieldOfEachObjective()
        {
            _definition.SetObjectives(new[]
            {
                new ObjectiveDefinition("reach", "Reach the gate"),
                new ObjectiveDefinition("kill", "Kill 3", 3, false, "reach"),
                new ObjectiveDefinition("intel", "Grab intel", 1, true),
            });

            var mission = _definition.CreateMission();

            Assert.That(mission.Objectives.Count(), Is.EqualTo(3));
            var kill = mission.Objectives[1];
            Assert.That(kill.Id, Is.EqualTo("kill"));
            Assert.That(kill.DescriptionKey, Is.EqualTo("Kill 3"));
            Assert.That(kill.RequiredCount, Is.EqualTo(3));
            Assert.That(kill.Optional, Is.False);
            Assert.That(kill.Prerequisites, Is.EqualTo(new[] { "reach" }));
            Assert.That(mission.Objectives[2].Optional, Is.True);

            Assert.That(mission.GetState("reach"), Is.EqualTo(ObjectiveState.Active));
            Assert.That(mission.GetState("kill"), Is.EqualTo(ObjectiveState.Locked));
            Assert.That(mission.GetState("intel"), Is.EqualTo(ObjectiveState.Active));
        }

        [Test]
        public void KeysAreBuiltFromTheMissionAndObjectiveIds()
        {
            Assert.That(MissionDefinition.NameKeyOf("Campaign01"), Is.EqualTo("campaign.mission.Campaign01.name"));
            Assert.That(
                MissionDefinition.ObjectiveKeyOf("Campaign01", "reach_exit"),
                Is.EqualTo("campaign.mission.Campaign01.objective.reach_exit"));
            Assert.Throws<ArgumentException>(() => MissionDefinition.NameKeyOf(" "));
            Assert.Throws<ArgumentException>(() => MissionDefinition.ObjectiveKeyOf("Campaign01", null));
        }

        [Test]
        public void TheNameKeyIsKeptAsGiven()
        {
            Assert.That(_definition.DisplayNameKey, Is.Empty);

            _definition.DisplayNameKey = "campaign.mission.Op.name";
            Assert.That(_definition.DisplayNameKey, Is.EqualTo("campaign.mission.Op.name"));

            _definition.DisplayNameKey = null;
            Assert.That(_definition.DisplayNameKey, Is.Empty);
        }

        [Test]
        public void EachCallReturnsAFreshMission()
        {
            _definition.SetObjectives(new[] { new ObjectiveDefinition("a", "") });

            var first = _definition.CreateMission();
            first.Progress("a");
            var second = _definition.CreateMission();

            Assert.That(second, Is.Not.SameAs(first));
            Assert.That(second.State, Is.EqualTo(MissionState.Active));
        }

        [Test]
        public void TrimsIdsAndIgnoresBlankPrerequisites()
        {
            _definition.SetObjectives(new[]
            {
                new ObjectiveDefinition(" a ", ""),
                new ObjectiveDefinition("b", "", 1, false, " a", "", null),
            });

            var mission = _definition.CreateMission();

            Assert.That(mission.HasObjective("a"), Is.True);
            Assert.That(mission.Objectives[1].Prerequisites, Is.EqualTo(new[] { "a" }));
        }

        [Test]
        public void SetObjectivesCopiesTheInput()
        {
            var source = new[] { new ObjectiveDefinition("a", "") };
            _definition.SetObjectives(source);
            source[0] = new ObjectiveDefinition("b", "");

            Assert.That(_definition.Objectives[0].Id, Is.EqualTo("a"));
            Assert.That(_definition.Objectives[0], Is.Not.SameAs(source[0]));
        }

        [Test]
        public void RejectsEmptyDefinition()
        {
            var e = Assert.Throws<MissionDefinitionException>(() => _definition.CreateMission());
            Assert.That(e.Message, Does.Contain("TestDefinition"));

            _definition.SetObjectives(null);
            Assert.Throws<MissionDefinitionException>(() => _definition.CreateMission());
        }

        [Test]
        public void RejectsNullEntry()
        {
            _definition.SetObjectives(new[] { new ObjectiveDefinition("a", ""), null });

            var e = Assert.Throws<MissionDefinitionException>(() => _definition.CreateMission());
            Assert.That(e.Message, Does.Contain("#1"));
        }

        [TestCase("", 1)]
        [TestCase("   ", 1)]
        [TestCase("a", 0)]
        public void RejectsInvalidObjective(string id, int requiredCount)
        {
            _definition.SetObjectives(new[] { new ObjectiveDefinition(id, "", requiredCount) });

            var e = Assert.Throws<MissionDefinitionException>(() => _definition.CreateMission());
            Assert.That(e.Message, Does.Contain("TestDefinition").And.Contain("#0"));
            Assert.That(e.InnerException, Is.InstanceOf<ArgumentException>());
        }

        [Test]
        public void RejectsInvalidMissionStructure()
        {
            // 必須の目標が無い・Id の重複・未知の前提・循環は Shooter.Core.Mission が見つける。
            var cases = new[]
            {
                new[] { new ObjectiveDefinition("a", "", 1, true) },
                new[] { new ObjectiveDefinition("a", ""), new ObjectiveDefinition("a", "") },
                new[] { new ObjectiveDefinition("a", "", 1, false, "missing") },
                new[] { new ObjectiveDefinition("a", "", 1, false, "b"), new ObjectiveDefinition("b", "", 1, false, "a") },
            };

            foreach (var objectives in cases)
            {
                _definition.SetObjectives(objectives);

                var e = Assert.Throws<MissionDefinitionException>(() => _definition.CreateMission());
                Assert.That(e.Message, Does.Contain("TestDefinition"));
                Assert.That(e.InnerException, Is.InstanceOf<ArgumentException>());
            }
        }

        [Test]
        public void ObjectivesSurviveSerialization()
        {
            _definition.DisplayNameKey = "campaign.mission.Op.name";
            _definition.SetObjectives(new[]
            {
                new ObjectiveDefinition("a", "first"),
                new ObjectiveDefinition("b", "second", 2, true, "a"),
            });

            var copy = Object.Instantiate(_definition);
            try
            {
                Assert.That(copy.DisplayNameKey, Is.EqualTo("campaign.mission.Op.name"));
                Assert.That(copy.Objectives.Count(), Is.EqualTo(2));
                var b = copy.Objectives[1];
                Assert.That(b.Id, Is.EqualTo("b"));
                Assert.That(b.DescriptionKey, Is.EqualTo("second"));
                Assert.That(b.RequiredCount, Is.EqualTo(2));
                Assert.That(b.Optional, Is.True);
                Assert.That(b.Prerequisites, Is.EqualTo(new[] { "a" }));
                Assert.That(copy.CreateMission().Objectives.Count(), Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(copy);
            }
        }

        [Test]
        public void AnEmptyRulesOverrideKeepsTheBaseRules()
        {
            var baseRules = GameRules.Default.WithRegenPerSecond(20f);

            Assert.That(_definition.RulesOverride, Is.Empty);
            Assert.That(_definition.ApplyRulesOverride(baseRules), Is.SameAs(baseRules));

            _definition.RulesOverride = "  \n ";
            Assert.That(_definition.ApplyRulesOverride(baseRules), Is.SameAs(baseRules));

            _definition.RulesOverride = null;
            Assert.That(_definition.RulesOverride, Is.Empty);
        }

        [Test]
        public void TheRulesOverrideChangesOnlyTheItemsItNames()
        {
            var baseRules = GameRules.Default
                .WithRegenDelaySeconds(3f)
                .WithRegenPerSecond(20f)
                .WithActions(ActionRules.Default.WithActiveReload(ActiveReload.On));
            _definition.RulesOverride = "{\"viewMode\": \"ThirdPerson\", \"actions\": {\"cover\": \"On\"}}";

            var rules = _definition.ApplyRulesOverride(baseRules);

            Assert.That(rules.ViewMode, Is.EqualTo(ViewMode.ThirdPerson));
            Assert.That(rules.Actions.Cover, Is.EqualTo(Cover.On));
            Assert.That(rules.Actions.ActiveReload, Is.EqualTo(ActiveReload.On), "unnamed action items keep the base");
            Assert.That(rules.RegenDelaySeconds, Is.EqualTo(3f));
            Assert.That(rules.RegenPerSecond, Is.EqualTo(20f));
            Assert.That(baseRules.ViewMode, Is.EqualTo(ViewMode.FirstPerson), "the base rules are not changed");
        }

        [Test]
        public void AnInvalidRulesOverrideNamesTheMission()
        {
            _definition.RulesOverride = "{\"viewMode\": \"Overhead\"}";

            var e = Assert.Throws<MissionDefinitionException>(() => _definition.ApplyRulesOverride(GameRules.Default));
            Assert.That(e.Message, Does.Contain("TestDefinition"));
            Assert.That(e.Message, Does.Contain("viewMode"));
            Assert.That(e.InnerException, Is.InstanceOf<GameRulesJsonException>());

            _definition.RulesOverride = "{";
            Assert.Throws<MissionDefinitionException>(() => _definition.ApplyRulesOverride(GameRules.Default));
            Assert.Throws<ArgumentNullException>(() => _definition.ApplyRulesOverride(null));
        }

        [Test]
        public void TheRulesOverrideSurvivesSerialization()
        {
            _definition.RulesOverride = "{\"viewMode\": \"ThirdPerson\"}";

            var copy = Object.Instantiate(_definition);
            try
            {
                Assert.That(copy.RulesOverride, Is.EqualTo("{\"viewMode\": \"ThirdPerson\"}"));
            }
            finally
            {
                Object.DestroyImmediate(copy);
            }
        }
    }
}
