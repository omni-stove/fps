using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class MissionSnapshotTests
    {
        [Test]
        public void SnapshotListsObjectivesInDefinitionOrder()
        {
            var mission = Chain();
            mission.Progress("kills", 2);

            Assert.That(Describe(mission.Snapshot()), Is.EqualTo("kills:Active:2 exit:Locked:0 bonus:Active:0"));
        }

        [Test]
        public void RestoreReturnsToSnapshot()
        {
            var original = Chain();
            original.Progress("kills", 3);
            original.Fail("bonus");
            var snapshot = original.Snapshot();

            var restored = Chain();
            restored.Restore(snapshot);

            Assert.That(Describe(restored.Snapshot()), Is.EqualTo(Describe(snapshot)));
            Assert.That(restored.GetState("exit"), Is.EqualTo(ObjectiveState.Active));
            Assert.That(restored.State, Is.EqualTo(MissionState.Active));
        }

        [Test]
        public void SnapshotIsNotAffectedByLaterProgress()
        {
            var mission = Chain();
            var snapshot = mission.Snapshot();

            mission.Progress("kills");

            Assert.That(snapshot[0].Count, Is.Zero);
        }

        [Test]
        public void RestoreRecomputesLockedAndActive()
        {
            var mission = new Mission(new[]
            {
                new Objective("a", ""),
                new Objective("b", "", prerequisites: Ids("a")),
                new Objective("c", "", prerequisites: Ids("b")),
            });

            mission.Restore(new[]
            {
                new ObjectiveProgress("a", ObjectiveState.Completed, 1),
                new ObjectiveProgress("b", ObjectiveState.Locked, 0),
                new ObjectiveProgress("c", ObjectiveState.Active, 0),
            });

            Assert.That(mission.GetState("b"), Is.EqualTo(ObjectiveState.Active));
            Assert.That(mission.GetState("c"), Is.EqualTo(ObjectiveState.Locked));
            Assert.That(mission.State, Is.EqualTo(MissionState.Active));
        }

        [Test]
        public void RestorePropagatesFailedPrerequisites()
        {
            var mission = new Mission(new[]
            {
                new Objective("main", ""),
                new Objective("intel", "", optional: true),
                new Objective("bonus", "", optional: true, prerequisites: Ids("intel")),
                new Objective("required", "", prerequisites: Ids("bonus")),
            });

            mission.Restore(new[]
            {
                new ObjectiveProgress("main", ObjectiveState.Active, 0),
                new ObjectiveProgress("intel", ObjectiveState.Failed, 0),
                new ObjectiveProgress("bonus", ObjectiveState.Locked, 0),
                new ObjectiveProgress("required", ObjectiveState.Locked, 0),
            });

            Assert.That(mission.GetState("bonus"), Is.EqualTo(ObjectiveState.Failed));
            Assert.That(mission.GetState("required"), Is.EqualTo(ObjectiveState.Failed));
            Assert.That(mission.State, Is.EqualTo(MissionState.Failed));
        }

        [Test]
        public void RestoreFiresNoEvents()
        {
            var mission = Chain();
            var log = Record(mission);

            mission.Restore(Progress(
                ("kills", ObjectiveState.Completed, 3),
                ("exit", ObjectiveState.Completed, 1),
                ("bonus", ObjectiveState.Active, 0)));

            Assert.That(mission.State, Is.EqualTo(MissionState.Completed));
            Assert.That(log(), Is.Empty);
        }

        [Test]
        public void EventsAfterRestoreFollowRestoredState()
        {
            var mission = Chain();
            mission.Restore(Progress(
                ("kills", ObjectiveState.Active, 2),
                ("exit", ObjectiveState.Locked, 0),
                ("bonus", ObjectiveState.Active, 0)));
            var log = Record(mission);

            mission.Progress("kills");
            mission.Progress("exit");

            Assert.That(log(), Is.EqualTo("kills:Completed exit:Active exit:Completed MissionCompleted"));
        }

        [Test]
        public void RestoringCheckpointRevivesFailedMission()
        {
            var mission = Chain();
            mission.Progress("kills", 3);
            var checkpoint = mission.Snapshot();
            mission.Fail("exit");
            Assert.That(mission.State, Is.EqualTo(MissionState.Failed));

            mission.Restore(checkpoint);
            var log = Record(mission);

            Assert.That(mission.State, Is.EqualTo(MissionState.Active));
            Assert.That(mission.GetState("exit"), Is.EqualTo(ObjectiveState.Active));

            mission.Fail("exit");
            Assert.That(log(), Is.EqualTo("exit:Failed MissionFailed"));
        }

        [Test]
        public void RestoredFinishedMissionIgnoresOperations()
        {
            var mission = Chain();
            mission.Restore(Progress(
                ("kills", ObjectiveState.Completed, 3),
                ("exit", ObjectiveState.Failed, 0),
                ("bonus", ObjectiveState.Active, 0)));
            var log = Record(mission);

            mission.Progress("bonus");

            Assert.That(mission.State, Is.EqualTo(MissionState.Failed));
            Assert.That(mission.GetCount("bonus"), Is.Zero);
            Assert.That(log(), Is.Empty);
        }

        [Test]
        public void FinishedMissionRoundTripsExactly()
        {
            // 完了の瞬間に Locked の目標は Active にならないので、戻したあとも Locked のまま。
            var completed = new Mission(new[]
            {
                new Objective("main", ""),
                new Objective("bonus", "", optional: true, prerequisites: Ids("main")),
            });
            completed.Progress("main");

            // 必須の目標が失敗した瞬間は、それを前提にしていた目標は Locked のまま。
            var failed = new Mission(new[]
            {
                new Objective("main", ""),
                new Objective("next", "", prerequisites: Ids("main")),
            });
            failed.Fail("main");

            foreach (var original in new[] { completed, failed })
            {
                var snapshot = original.Snapshot();
                var restored = new Mission(original.Objectives);
                restored.Restore(snapshot);

                Assert.That(Describe(restored.Snapshot()), Is.EqualTo(Describe(snapshot)));
                Assert.That(restored.State, Is.EqualTo(original.State));
            }
        }

        [Test]
        public void RestoreRejectsNull()
        {
            var mission = Chain();

            Assert.Throws<ArgumentNullException>(() => mission.Restore(null));
            Assert.Throws<ArgumentException>(() => mission.Restore(new ObjectiveProgress[] { null }));
        }

        // 知らない目標 / 重複 / 欠け / 進み具合が多すぎる / 必要数に届いたのに未完了 / 届かないのに完了 / 前提より先に完了
        [TestCase("kills:Active:0 exit:Locked:0 bonus:Active:0 other:Active:0")]
        [TestCase("kills:Active:0 kills:Active:0 exit:Locked:0 bonus:Active:0")]
        [TestCase("kills:Active:0 exit:Locked:0")]
        [TestCase("kills:Active:4 exit:Locked:0 bonus:Active:0")]
        [TestCase("kills:Active:3 exit:Locked:0 bonus:Active:0")]
        [TestCase("kills:Completed:2 exit:Locked:0 bonus:Active:0")]
        [TestCase("kills:Active:0 exit:Completed:1 bonus:Active:0")]
        public void RestoreRejectsInconsistentProgressWithoutChanging(string progress)
        {
            var mission = Chain();
            mission.Progress("kills");
            var before = Describe(mission.Snapshot());
            var entries = Parse(progress);

            Assert.Throws<ArgumentException>(() => mission.Restore(entries));

            Assert.That(Describe(mission.Snapshot()), Is.EqualTo(before));
            Assert.That(mission.State, Is.EqualTo(MissionState.Active));
        }

        [TestCase(ObjectiveState.Locked)]
        [TestCase(ObjectiveState.Active)]
        public void RestoreRejectsProgressBeforePrerequisites(ObjectiveState savedState)
        {
            var mission = new Mission(new[]
            {
                new Objective("a", ""),
                new Objective("b", "", 3, prerequisites: Ids("a")),
            });

            Assert.Throws<ArgumentException>(() => mission.Restore(Progress(
                ("a", ObjectiveState.Active, 0),
                ("b", savedState, 1))));
            Assert.That(mission.GetCount("b"), Is.Zero);
        }

        [Test]
        public void ObjectiveProgressRejectsInvalidValues()
        {
            Assert.Throws<ArgumentNullException>(() => new ObjectiveProgress(null, ObjectiveState.Active, 0));
            Assert.Throws<ArgumentException>(() => new ObjectiveProgress("", ObjectiveState.Active, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ObjectiveProgress("a", ObjectiveState.Active, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ObjectiveProgress("a", (ObjectiveState)99, 0));
        }

        /// <summary>kills (3 回) → exit の必須の流れと、任意の bonus。</summary>
        private static Mission Chain()
        {
            return new Mission(new[]
            {
                new Objective("kills", "", 3),
                new Objective("exit", "", prerequisites: Ids("kills")),
                new Objective("bonus", "", optional: true),
            });
        }

        private static string[] Ids(params string[] ids)
        {
            return ids;
        }

        private static ObjectiveProgress[] Progress(params (string Id, ObjectiveState State, int Count)[] entries)
        {
            return entries.Select(e => new ObjectiveProgress(e.Id, e.State, e.Count)).ToArray();
        }

        /// <summary>"id:状態:進み具合" の空白区切りを読む。</summary>
        private static ObjectiveProgress[] Parse(string text)
        {
            return text.Split(' ')
                .Select(part => part.Split(':'))
                .Select(p => new ObjectiveProgress(
                    p[0], Enum.Parse<ObjectiveState>(p[1]), int.Parse(p[2], CultureInfo.InvariantCulture)))
                .ToArray();
        }

        private static string Describe(IEnumerable<ObjectiveProgress> progress)
        {
            return string.Join(" ", progress.Select(p => p.Id + ":" + p.State + ":" + p.Count));
        }

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
