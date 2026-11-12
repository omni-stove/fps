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
    /// <summary>
    /// Edit Mode では Awake/Start/OnTrigger* が走らないので、それらが呼ぶ公開メソッドを直接呼んで確かめる。
    /// </summary>
    public sealed class MissionRunnerTests
    {
        private readonly List<Object> _created = new List<Object>();
        private MissionDefinition _definition;
        private MissionRunner _runner;

        [SetUp]
        public void SetUp()
        {
            _definition = Track(ScriptableObject.CreateInstance<MissionDefinition>());
            _definition.name = "RunnerTestMission";
            _definition.SetObjectives(new[]
            {
                new ObjectiveDefinition("reach", ""),
                new ObjectiveDefinition("kill", "", 2, false, "reach"),
                new ObjectiveDefinition("extract", "", 1, false, "kill"),
            });

            _runner = Track(new GameObject("Runner")).AddComponent<MissionRunner>();
            _runner.Definition = _definition;
        }

        [TearDown]
        public void TearDown()
        {
            for (var i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                {
                    Object.DestroyImmediate(_created[i]);
                }
            }

            _created.Clear();
        }

        [Test]
        public void StartLogsEveryObjectiveState()
        {
            ExpectMission("objective=reach state=Active");
            ExpectMission("objective=kill state=Locked");
            ExpectMission("objective=extract state=Locked");

            Assert.That(_runner.TryStart(), Is.True);
            Assert.That(_runner.IsStarted, Is.True);
            Assert.That(_runner.Mission.Objectives, Has.Count.EqualTo(3));

            Assert.That(_runner.TryStart(), Is.True, "starting twice keeps the running mission");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void StartFailsWithoutDefinition()
        {
            _runner.Definition = null;
            LogAssert.Expect(LogType.Error, new Regex(@"^\[Mission\] .*MissionDefinition"));

            Assert.That(_runner.TryStart(), Is.False);
            Assert.That(_runner.Mission, Is.Null);
        }

        [Test]
        public void StartFailsWithInvalidDefinition()
        {
            _definition.SetObjectives(new[] { new ObjectiveDefinition("a", "", 1, false, "missing") });
            LogAssert.Expect(LogType.Error, new Regex(@"^\[Mission\] mission 'RunnerTestMission' is invalid"));

            Assert.That(_runner.TryStart(), Is.False);
            Assert.That(_runner.IsStarted, Is.False);
        }

        [Test]
        public void LogsChangesCompletionAndFailure()
        {
            StartQuietly();
            ExpectMission("objective=reach state=Completed");
            ExpectMission("objective=kill state=Active");
            _runner.Progress("reach");

            ExpectMission("objective=kill state=Failed");
            ExpectMission("objective=extract state=Failed");
            LogAssert.Expect(LogType.Log, MissionReporter.FailedLine);
            _runner.Fail("kill");

            Assert.That(_runner.Mission.State, Is.EqualTo(MissionState.Failed));
        }

        [Test]
        public void ProgressOnUnknownObjectiveLogsAnError()
        {
            StartQuietly();
            LogAssert.Expect(LogType.Error, new Regex(@"^\[Mission\] .*'nope'"));

            _runner.Progress("nope");

            Assert.That(_runner.Mission.GetState("reach"), Is.EqualTo(ObjectiveState.Active));
        }

        [Test]
        public void ResolveStartsTheRunnerAndChecksTheObjective()
        {
            Assert.That(MissionRunner.Resolve(_runner, "reach", null), Is.SameAs(_runner));
            Assert.That(_runner.IsStarted, Is.True);

            LogAssert.Expect(LogType.Error, new Regex(@"^\[Mission\] .*'missing'"));
            Assert.That(MissionRunner.Resolve(_runner, "missing", null), Is.Null);
        }

        [Test]
        public void ReachAreaProgressesOnlyForThePlayer()
        {
            StartQuietly();
            var area = CreateArea<ReachAreaTrigger>("reach");
            var player = CreateCollider("Player", GameBootstrap.PlayerTag);
            var stranger = CreateCollider("Stranger", "Untagged");

            area.HandleEnter(stranger);
            Assert.That(_runner.Mission.GetState("reach"), Is.EqualTo(ObjectiveState.Active));

            ExpectMission("objective=reach state=Completed");
            area.HandleEnter(player);
            Assert.That(_runner.Mission.GetState("reach"), Is.EqualTo(ObjectiveState.Completed));
        }

        [Test]
        public void ReachAreaCountsAColliderOnce()
        {
            StartQuietly();
            var area = CreateArea<ReachAreaTrigger>("extract");
            var player = CreateCollider("Player", GameBootstrap.PlayerTag);

            area.HandleEnter(player);
            area.HandleEnter(player);
            Assert.That(area.Zone.Occupants, Is.EqualTo(1));

            area.HandleExit(player);
            area.HandleExit(player);
            Assert.That(area.Zone.Occupants, Is.Zero);
        }

        [Test]
        public void AreaRefusesToBindWithoutTriggerCollider()
        {
            StartQuietly();
            var host = Track(new GameObject("Solid"));
            host.AddComponent<BoxCollider>();
            var area = host.AddComponent<ReachAreaTrigger>();
            area.ObjectiveId = "reach";
            LogAssert.Expect(LogType.Error, new Regex(@"^\[Mission\] ReachAreaTrigger 'Solid' .*isTrigger"));

            Assert.That(area.Bind(_runner), Is.False);
            Assert.That(area.IsBound, Is.False);
        }

        [Test]
        public void InteractNeedsThePlayerInsideAndAnActiveObjective()
        {
            StartQuietly();
            var area = CreateArea<InteractObjective>("reach");
            var player = CreateCollider("Player", GameBootstrap.PlayerTag);

            Assert.That(area.Interact(), Is.False, "nobody inside");
            area.HandleEnter(player);
            Assert.That(_runner.Mission.GetState("reach"), Is.EqualTo(ObjectiveState.Active), "entering alone does not progress");

            ExpectMission("objective=reach state=Completed");
            LogAssert.Expect(LogType.Log, "[Mission] interacted objective=reach");
            Assert.That(area.Interact(), Is.True);

            area.HandleExit(player);
            Assert.That(area.Interact(), Is.False);
        }

        [Test]
        public void KillTargetProgressesOnEachDeath()
        {
            StartQuietly();
            _runner.Progress("reach");
            var root = Track(new GameObject("Targets"));
            var a = CreateTarget(root.transform, "A");
            var b = CreateTarget(root.transform, "B");
            var kill = root.AddComponent<KillTargetObjective>();
            kill.ObjectiveId = "kill";

            Assert.That(kill.Bind(_runner), Is.True);
            Assert.That(kill.BoundTargets, Is.EquivalentTo(new[] { a, b }), "children are the default targets");

            LogAssert.Expect(LogType.Log, "[Combat] target died id=A");
            a.Kill();
            Assert.That(_runner.Mission.GetCount("kill"), Is.EqualTo(1));
            Assert.That(a.gameObject.activeSelf, Is.False, "killed targets are hidden");

            a.Kill();
            Assert.That(_runner.Mission.GetCount("kill"), Is.EqualTo(1), "a dead target does not die again");

            LogAssert.Expect(LogType.Log, "[Combat] target died id=B");
            ExpectMission("objective=kill state=Completed");
            ExpectMission("objective=extract state=Active");
            b.TakeDamage(b.MaxHealth);
            Assert.That(_runner.Mission.GetState("kill"), Is.EqualTo(ObjectiveState.Completed));
        }

        [Test]
        public void KillTargetUsesExplicitTargetsAndIgnoresDuplicates()
        {
            StartQuietly();
            var root = Track(new GameObject("Targets"));
            var a = CreateTarget(root.transform, "A");
            CreateTarget(root.transform, "Ignored");
            var kill = root.AddComponent<KillTargetObjective>();
            kill.ObjectiveId = "kill";
            kill.SetTargets(a, a, null);

            Assert.That(kill.Bind(_runner), Is.True);
            Assert.That(kill.BoundTargets, Is.EqualTo(new[] { a }));
        }

        [Test]
        public void KillTargetRefusesToBindWithoutTargets()
        {
            StartQuietly();
            var kill = Track(new GameObject("Empty")).AddComponent<KillTargetObjective>();
            kill.ObjectiveId = "kill";
            LogAssert.Expect(LogType.Error, new Regex(@"^\[Mission\] KillTargetObjective 'Empty' .*Damageable"));

            Assert.That(kill.Bind(_runner), Is.False);
            Assert.That(kill.IsBound, Is.False);
        }

        [Test]
        public void DamageableKeepsSubscribersAcrossHealthReset()
        {
            var target = CreateTarget(null, "Dummy");
            var deaths = 0;
            target.Died += _ => deaths++;

            LogAssert.Expect(LogType.Log, "[Combat] target died id=Dummy");
            target.Kill();
            target.ResetHealth();
            Assert.That(target.IsDead, Is.False);
            Assert.That(target.Health.Current, Is.EqualTo(target.MaxHealth));

            LogAssert.Expect(LogType.Log, "[Combat] target died id=Dummy");
            target.Kill();

            Assert.That(deaths, Is.EqualTo(2));
        }

        private void StartQuietly()
        {
            ExpectMission("objective=reach state=Active");
            ExpectMission("objective=kill state=Locked");
            ExpectMission("objective=extract state=Locked");
            Assert.That(_runner.TryStart(), Is.True);
        }

        private static void ExpectMission(string rest)
        {
            LogAssert.Expect(LogType.Log, MissionReporter.Tag + " " + rest);
        }

        private T CreateArea<T>(string objectiveId)
            where T : ObjectiveAreaBase
        {
            var host = Track(new GameObject(typeof(T).Name));
            host.AddComponent<BoxCollider>().isTrigger = true;
            var area = host.AddComponent<T>();
            area.ObjectiveId = objectiveId;
            Assert.That(area.Bind(_runner), Is.True);
            return area;
        }

        private Collider CreateCollider(string name, string tag)
        {
            var host = Track(new GameObject(name));
            host.tag = tag;
            return host.AddComponent<CapsuleCollider>();
        }

        private Damageable CreateTarget(Transform parent, string name)
        {
            var host = Track(new GameObject(name));
            host.transform.SetParent(parent, false);
            return host.AddComponent<Damageable>();
        }

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}
