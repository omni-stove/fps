using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class MissionTests
    {
        [Test]
        public void ObjectiveDefaults()
        {
            var objective = new Objective("a", "Reach the gate");

            Assert.That(objective.RequiredCount, Is.EqualTo(1));
            Assert.That(objective.Optional, Is.False);
            Assert.That(objective.Prerequisites, Is.Empty);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void ObjectiveRejectsNonPositiveRequiredCount(int requiredCount)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Objective("a", "", requiredCount));
        }

        [Test]
        public void ObjectiveRejectsMissingId()
        {
            Assert.Throws<ArgumentNullException>(() => new Objective(null, ""));
            Assert.Throws<ArgumentException>(() => new Objective("", ""));
            Assert.Throws<ArgumentException>(() => new Objective("a", "", prerequisites: Ids((string)null)));
        }

        [Test]
        public void StartsWithRootsActiveAndDependentsLocked()
        {
            var mission = new Mission(new[]
            {
                new Objective("a", ""),
                new Objective("b", "", prerequisites: Ids("a")),
            });

            Assert.That(mission.State, Is.EqualTo(MissionState.Active));
            Assert.That(mission.GetState("a"), Is.EqualTo(ObjectiveState.Active));
            Assert.That(mission.GetState("b"), Is.EqualTo(ObjectiveState.Locked));
        }

        [Test]
        public void CompletingPrerequisiteUnlocksDependent()
        {
            var mission = new Mission(new[]
            {
                new Objective("a", ""),
                new Objective("b", "", prerequisites: Ids("a")),
            });

            mission.Progress("a");

            Assert.That(mission.GetState("a"), Is.EqualTo(ObjectiveState.Completed));
            Assert.That(mission.GetState("b"), Is.EqualTo(ObjectiveState.Active));
        }

        [Test]
        public void DependentNeedsAllPrerequisites()
        {
            var mission = new Mission(new[]
            {
                new Objective("a", ""),
                new Objective("b", ""),
                new Objective("c", "", prerequisites: Ids("a", "b")),
            });

            mission.Progress("a");
            Assert.That(mission.GetState("c"), Is.EqualTo(ObjectiveState.Locked));

            mission.Progress("b");
            Assert.That(mission.GetState("c"), Is.EqualTo(ObjectiveState.Active));
        }

        [Test]
        public void LockedObjectiveDoesNotProgress()
        {
            var mission = new Mission(new[]
            {
                new Objective("a", ""),
                new Objective("b", "", prerequisites: Ids("a")),
            });

            mission.Progress("b");

            Assert.That(mission.GetCount("b"), Is.Zero);
            Assert.That(mission.GetState("b"), Is.EqualTo(ObjectiveState.Locked));
        }

        [Test]
        public void CompletesWhenCountReached()
        {
            var mission = new Mission(new[] { new Objective("kills", "", 3) });

            mission.Progress("kills");
            mission.Progress("kills");
            Assert.That(mission.GetCount("kills"), Is.EqualTo(2));
            Assert.That(mission.GetState("kills"), Is.EqualTo(ObjectiveState.Active));

            mission.Progress("kills");
            Assert.That(mission.GetCount("kills"), Is.EqualTo(3));
            Assert.That(mission.GetState("kills"), Is.EqualTo(ObjectiveState.Completed));
        }

        [Test]
        public void CountDoesNotExceedRequired()
        {
            var mission = new Mission(new[]
            {
                new Objective("kills", "", 3),
                new Objective("exit", ""),
            });

            mission.Progress("kills", int.MaxValue);

            Assert.That(mission.GetCount("kills"), Is.EqualTo(3));
            Assert.That(mission.GetState("kills"), Is.EqualTo(ObjectiveState.Completed));
        }

        [Test]
        public void CompletedObjectiveDoesNotProgress()
        {
            var mission = new Mission(new[]
            {
                new Objective("a", ""),
                new Objective("b", ""),
            });
            mission.Progress("a");

            mission.Progress("a");

            Assert.That(mission.GetCount("a"), Is.EqualTo(1));
        }

        [Test]
        public void RejectsNegativeAmountAndUnknownId()
        {
            var mission = new Mission(new[] { new Objective("a", "") });

            Assert.Throws<ArgumentOutOfRangeException>(() => mission.Progress("a", -1));
            Assert.Throws<ArgumentException>(() => mission.Progress("missing"));
            Assert.Throws<ArgumentException>(() => mission.Fail("missing"));
            Assert.Throws<ArgumentException>(() => mission.GetState("missing"));
            Assert.Throws<ArgumentNullException>(() => mission.Progress(null));
        }

        [Test]
        public void CompletesWithOptionalRemaining()
        {
            var mission = new Mission(new[]
            {
                new Objective("main", ""),
                new Objective("bonus", "", optional: true),
            });
            var completed = 0;
            mission.MissionCompleted += () => completed++;

            mission.Progress("main");

            Assert.That(mission.State, Is.EqualTo(MissionState.Completed));
            Assert.That(mission.GetState("bonus"), Is.EqualTo(ObjectiveState.Active));
            Assert.That(completed, Is.EqualTo(1));
        }

        [Test]
        public void OptionalFailureDoesNotFailMission()
        {
            var mission = new Mission(new[]
            {
                new Objective("main", ""),
                new Objective("bonus", "", optional: true),
            });

            mission.Fail("bonus");

            Assert.That(mission.GetState("bonus"), Is.EqualTo(ObjectiveState.Failed));
            Assert.That(mission.State, Is.EqualTo(MissionState.Active));

            mission.Progress("main");
            Assert.That(mission.State, Is.EqualTo(MissionState.Completed));
        }

        [Test]
        public void RequiredFailureFailsMission()
        {
            var mission = new Mission(new[]
            {
                new Objective("main", ""),
                new Objective("escort", ""),
            });
            var failed = 0;
            mission.MissionFailed += () => failed++;

            mission.Fail("escort");

            Assert.That(mission.GetState("escort"), Is.EqualTo(ObjectiveState.Failed));
            Assert.That(mission.State, Is.EqualTo(MissionState.Failed));
            Assert.That(mission.IsFinished, Is.True);
            Assert.That(failed, Is.EqualTo(1));
        }

        [Test]
        public void LockedRequiredObjectiveCanFail()
        {
            var mission = new Mission(new[]
            {
                new Objective("a", ""),
                new Objective("b", "", prerequisites: Ids("a")),
            });

            mission.Fail("b");

            Assert.That(mission.State, Is.EqualTo(MissionState.Failed));
        }

        [Test]
        public void FailingOptionalPrerequisiteFailsUnreachableDependents()
        {
            var mission = new Mission(new[]
            {
                new Objective("main", ""),
                new Objective("intel", "", optional: true),
                new Objective("bonus", "", optional: true, prerequisites: Ids("intel")),
                new Objective("required", "", prerequisites: Ids("bonus")),
            });

            mission.Fail("intel");

            Assert.That(mission.GetState("bonus"), Is.EqualTo(ObjectiveState.Failed));
            Assert.That(mission.GetState("required"), Is.EqualTo(ObjectiveState.Failed));
            Assert.That(mission.State, Is.EqualTo(MissionState.Failed));
        }

        [Test]
        public void CompletedObjectiveCannotFail()
        {
            var mission = new Mission(new[]
            {
                new Objective("a", ""),
                new Objective("b", ""),
            });
            mission.Progress("a");

            mission.Fail("a");

            Assert.That(mission.GetState("a"), Is.EqualTo(ObjectiveState.Completed));
            Assert.That(mission.State, Is.EqualTo(MissionState.Active));
        }

        [Test]
        public void RejectsDuplicateIds()
        {
            Assert.Throws<ArgumentException>(() => new Mission(new[]
            {
                new Objective("a", ""),
                new Objective("a", ""),
            }));
        }

        [Test]
        public void RejectsUnknownPrerequisite()
        {
            Assert.Throws<ArgumentException>(() => new Mission(new[]
            {
                new Objective("a", "", prerequisites: Ids("missing")),
            }));
        }

        [Test]
        public void RejectsCycle()
        {
            Assert.Throws<ArgumentException>(() => new Mission(new[]
            {
                new Objective("root", ""),
                new Objective("a", "", prerequisites: Ids("c")),
                new Objective("b", "", prerequisites: Ids("a")),
                new Objective("c", "", prerequisites: Ids("b")),
            }));
        }

        [Test]
        public void RejectsSelfPrerequisite()
        {
            Assert.Throws<ArgumentException>(() => new Mission(new[]
            {
                new Objective("a", "", prerequisites: Ids("a")),
            }));
        }

        [Test]
        public void RejectsEmptyOrAllOptionalOrNull()
        {
            Assert.Throws<ArgumentNullException>(() => new Mission(null));
            Assert.Throws<ArgumentException>(() => new Mission(Array.Empty<Objective>()));
            Assert.Throws<ArgumentException>(() => new Mission(new[] { new Objective("a", "", optional: true) }));
            Assert.Throws<ArgumentException>(() => new Mission(new Objective[] { null }));
        }

        [Test]
        public void IgnoresOperationsAfterCompleted()
        {
            var mission = new Mission(new[]
            {
                new Objective("main", ""),
                new Objective("bonus", "", 2, optional: true),
            });
            mission.Progress("main");
            var events = 0;
            mission.ObjectiveChanged += (_, _) => events++;
            mission.MissionCompleted += () => events++;
            mission.MissionFailed += () => events++;

            mission.Progress("bonus");
            mission.Fail("bonus");

            Assert.That(mission.State, Is.EqualTo(MissionState.Completed));
            Assert.That(mission.GetState("bonus"), Is.EqualTo(ObjectiveState.Active));
            Assert.That(mission.GetCount("bonus"), Is.Zero);
            Assert.That(events, Is.Zero);
        }

        [Test]
        public void IgnoresOperationsAfterFailed()
        {
            var mission = new Mission(new[]
            {
                new Objective("main", ""),
                new Objective("escort", ""),
            });
            mission.Fail("escort");
            var events = 0;
            mission.ObjectiveChanged += (_, _) => events++;
            mission.MissionCompleted += () => events++;
            mission.MissionFailed += () => events++;

            mission.Progress("main");

            Assert.That(mission.State, Is.EqualTo(MissionState.Failed));
            Assert.That(mission.GetState("main"), Is.EqualTo(ObjectiveState.Active));
            Assert.That(events, Is.Zero);
        }

        [Test]
        public void EventsFireInOrderWhenUnlocking()
        {
            var mission = new Mission(new[]
            {
                new Objective("a", ""),
                new Objective("b", "", prerequisites: Ids("a")),
                new Objective("c", "", optional: true, prerequisites: Ids("a")),
            });
            var log = Record(mission);

            mission.Progress("a");

            Assert.That(log(), Is.EqualTo("a:Completed b:Active c:Active"));
        }

        [Test]
        public void EventsFireInOrderWhenCompleting()
        {
            var mission = new Mission(new[]
            {
                new Objective("a", ""),
                new Objective("b", "", prerequisites: Ids("a")),
            });
            var log = Record(mission);

            mission.Progress("a");
            mission.Progress("b");

            Assert.That(log(), Is.EqualTo("a:Completed b:Active b:Completed MissionCompleted"));
        }

        [Test]
        public void EventsFireInOrderWhenFailing()
        {
            var mission = new Mission(new[]
            {
                new Objective("main", ""),
                new Objective("intel", "", optional: true),
                new Objective("bonus", "", optional: true, prerequisites: Ids("intel")),
                new Objective("required", "", prerequisites: Ids("bonus")),
            });
            var log = Record(mission);

            mission.Fail("intel");

            Assert.That(log(), Is.EqualTo("intel:Failed bonus:Failed required:Failed MissionFailed"));
        }

        [Test]
        public void ProgressWithoutCompletionFiresNoEvent()
        {
            var mission = new Mission(new[] { new Objective("kills", "", 3) });
            var log = Record(mission);

            mission.Progress("kills");

            Assert.That(log(), Is.Empty);
        }

        private static string[] Ids(params string[] ids)
        {
            return ids;
        }

        /// <summary>通知を "id:状態" と "MissionCompleted" / "MissionFailed" の空白区切りで記録する。</summary>
        private static Func<string> Record(Mission mission)
        {
            var log = new List<string>();
            mission.ObjectiveChanged += (objective, state) => log.Add(objective.Id + ":" + state);
            mission.MissionCompleted += () => log.Add("MissionCompleted");
            mission.MissionFailed += () => log.Add("MissionFailed");
            return () => string.Join(" ", log);
        }
    }
}
