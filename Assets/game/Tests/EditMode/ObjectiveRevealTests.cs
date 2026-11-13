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
    /// <summary>Edit Mode では Start/Update が走らないので、それらが呼ぶ Bind と Refresh を直接呼んで確かめる。</summary>
    public sealed class ObjectiveRevealTests
    {
        private readonly List<Object> _created = new List<Object>();
        private MissionRunner _runner;
        private ObjectiveReveal _reveal;
        private GameObject _a;
        private GameObject _b;

        [SetUp]
        public void SetUp()
        {
            var definition = Track(ScriptableObject.CreateInstance<MissionDefinition>());
            definition.name = "RevealTestMission";
            definition.SetObjectives(new[]
            {
                new ObjectiveDefinition("use", ""),
                new ObjectiveDefinition("kill", "", 2, false, "use"),
            });

            _runner = Track(new GameObject("Runner")).AddComponent<MissionRunner>();
            _runner.Definition = definition;
            Assert.That(_runner.TryStart(), Is.True);

            var root = Track(new GameObject("Reinforcements"));
            _a = new GameObject("A");
            _a.transform.SetParent(root.transform);
            _a.SetActive(false);
            _b = new GameObject("B");
            _b.transform.SetParent(root.transform);
            _b.SetActive(false);
            _reveal = root.AddComponent<ObjectiveReveal>();
            _reveal.ObjectiveId = "kill";
            _reveal.SetTargets(_a, _b);
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
        public void RevealsTheTargetsOnceTheObjectiveIsActive()
        {
            Assert.That(_reveal.Bind(_runner), Is.True);
            Assert.That(_reveal.Refresh(), Is.False, "the objective is still locked");
            Assert.That(_a.activeSelf, Is.False);
            Assert.That(_b.activeSelf, Is.False);

            _runner.Progress("use");
            LogAssert.Expect(LogType.Log, "[Mission] revealed objective=kill targets=2");
            Assert.That(_reveal.Refresh(), Is.True);
            Assert.That(_reveal.IsRevealed, Is.True);
            Assert.That(_a.activeSelf, Is.True);
            Assert.That(_b.activeSelf, Is.True);
            Assert.That(_reveal.enabled, Is.False, "nothing is left to watch");
        }

        [Test]
        public void RevealsAfterARestoreThatMadeTheObjectiveActive()
        {
            Assert.That(_reveal.Bind(_runner), Is.True);

            // チェックポイントからの再開はイベントを呼ばないので、状態を読み直して出す。
            _runner.Mission.Restore(new[]
            {
                new ObjectiveProgress("use", ObjectiveState.Completed, 1),
                new ObjectiveProgress("kill", ObjectiveState.Active, 1),
            });
            LogAssert.Expect(LogType.Log, "[Mission] revealed objective=kill targets=2");
            Assert.That(_reveal.Refresh(), Is.True);
            Assert.That(_a.activeSelf, Is.True);
        }

        [Test]
        public void StaysHiddenWhenTheObjectiveWasAlreadyCompleted()
        {
            Assert.That(_reveal.Bind(_runner), Is.True);
            _runner.Mission.Restore(new[]
            {
                new ObjectiveProgress("use", ObjectiveState.Completed, 1),
                new ObjectiveProgress("kill", ObjectiveState.Completed, 2),
            });

            LogAssert.Expect(LogType.Log, "[Mission] reveal skipped objective=kill state=Completed");
            Assert.That(_reveal.Refresh(), Is.False);
            Assert.That(_reveal.enabled, Is.False);
            Assert.That(_a.activeSelf, Is.False, "the restored mission already killed them");
            Assert.That(_b.activeSelf, Is.False);
        }

        [Test]
        public void RefusesToBindWithoutTargetsOrAnUnknownObjective()
        {
            _reveal.SetTargets();
            LogAssert.Expect(LogType.Error, new Regex(@"^\[Mission\] ObjectiveReveal 'Reinforcements' に出す GameObject がありません"));
            Assert.That(_reveal.Bind(_runner), Is.False);
            Assert.That(_reveal.IsBound, Is.False);

            _reveal.SetTargets(_a);
            _reveal.ObjectiveId = "missing";
            LogAssert.Expect(LogType.Error, new Regex(@"^\[Mission\] .*目標 'missing' がありません"));
            Assert.That(_reveal.Bind(_runner), Is.False);
            Assert.That(_reveal.Refresh(), Is.False);
            Assert.That(_a.activeSelf, Is.False);
        }

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}
