using System;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
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

            Assert.That(mission.Objectives, Has.Count.EqualTo(3));
            var kill = mission.Objectives[1];
            Assert.That(kill.Id, Is.EqualTo("kill"));
            Assert.That(kill.Description, Is.EqualTo("Kill 3"));
            Assert.That(kill.RequiredCount, Is.EqualTo(3));
            Assert.That(kill.Optional, Is.False);
            Assert.That(kill.Prerequisites, Is.EqualTo(new[] { "reach" }));
            Assert.That(mission.Objectives[2].Optional, Is.True);

            Assert.That(mission.GetState("reach"), Is.EqualTo(ObjectiveState.Active));
            Assert.That(mission.GetState("kill"), Is.EqualTo(ObjectiveState.Locked));
            Assert.That(mission.GetState("intel"), Is.EqualTo(ObjectiveState.Active));
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
            // 必須の目標が無い・Id の重複・未知の前提・循環は Fps.Core.Mission が見つける。
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
            _definition.DisplayName = "Op";
            _definition.SetObjectives(new[]
            {
                new ObjectiveDefinition("a", "first"),
                new ObjectiveDefinition("b", "second", 2, true, "a"),
            });

            var copy = Object.Instantiate(_definition);
            try
            {
                Assert.That(copy.DisplayName, Is.EqualTo("Op"));
                Assert.That(copy.Objectives, Has.Count.EqualTo(2));
                var b = copy.Objectives[1];
                Assert.That(b.Id, Is.EqualTo("b"));
                Assert.That(b.Description, Is.EqualTo("second"));
                Assert.That(b.RequiredCount, Is.EqualTo(2));
                Assert.That(b.Optional, Is.True);
                Assert.That(b.Prerequisites, Is.EqualTo(new[] { "a" }));
                Assert.That(copy.CreateMission().Objectives, Has.Count.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(copy);
            }
        }
    }
}
