using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// EnemyAgent の組み立て (部品の解決・設定の検査・ログ・死亡の伝達) を確かめる。
    /// NavMesh 上を歩く部分は Play で確かめる (手順は PR 本文)。
    /// </summary>
    public sealed class EnemyAgentTests
    {
        private GameObject _host;
        private EnemyAgent _agent;

        [SetUp]
        public void SetUp()
        {
            _host = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _host.name = "Enemy";
            _agent = _host.AddComponent<EnemyAgent>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
        }

        [Test]
        public void RequiresNavMeshAgentAndDamageable()
        {
            Assert.That(_host.GetComponent<NavMeshAgent>(), Is.Not.Null);
            Assert.That(_host.GetComponent<Damageable>(), Is.Not.Null);
        }

        [Test]
        public void InitializeBuildsTheBrainFromTheDefaults()
        {
            Assert.That(_agent.TryInitialize(), Is.True);

            Assert.That(_agent.IsInitialized, Is.True);
            Assert.That(_agent.Brain.Spec.SightRange, Is.EqualTo(EnemyAgent.DefaultSightRange));
            Assert.That(_agent.Brain.Spec.AttackRange, Is.EqualTo(EnemyAgent.DefaultAttackRange));
            Assert.That(_agent.Brain.Spec.ReactionSeconds, Is.EqualTo(EnemyAgent.DefaultReactionSeconds));
            Assert.That(_agent.State, Is.EqualTo(EnemyState.Idle));
            Assert.That(_agent.Weapon, Is.Not.Null);
            Assert.That(_agent.TryInitialize(), Is.True, "a second call is a no-op");
        }

        [Test]
        public void InvalidSettingsAreReportedAndRejected()
        {
            var serialized = new SerializedObject(_agent);
            serialized.FindProperty("_attackRange").floatValue = EnemyAgent.DefaultSightRange + 1f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            LogAssert.Expect(LogType.Error, new Regex(@"^\[AI\] id=.* の設定が不正です"));
            Assert.That(_agent.TryInitialize(), Is.False);
            Assert.That(_agent.IsInitialized, Is.False);
        }

        [Test]
        public void IdFallsBackToTheDamageable()
        {
            _host.GetComponent<Damageable>().Initialize("grunt", 50);

            Assert.That(_agent.Id, Is.EqualTo("grunt"));

            _agent.Id = "enemy-7";
            Assert.That(_agent.Id, Is.EqualTo("enemy-7"));
        }

        [Test]
        public void StateChangesAreLoggedWithTheId()
        {
            _agent.Id = "enemy-1";
            Assert.That(_agent.TryInitialize(), Is.True);

            // NavMesh が無いので巡回地点はそのまま使われる (警告が出る)。
            LogAssert.Expect(LogType.Log, "[AI] id=enemy-1 state=Patrol prev=Idle");
            _agent.SetPatrolRoute(new[] { new Vector3(0f, 0f, 5f), new Vector3(5f, 0f, 5f) });

            Assert.That(_agent.State, Is.EqualTo(EnemyState.Patrol));
            Assert.That(_agent.PatrolRoute, Has.Count.EqualTo(2));
        }

        [Test]
        public void PatrolRouteSetBeforeInitializeIsKept()
        {
            _agent.SetPatrolRoute(new[] { new Vector3(1f, 0f, 2f) });

            Assert.That(_agent.PatrolRoute, Has.Count.EqualTo(1));
            Assert.That(_agent.PatrolRoute[0], Is.EqualTo(new Vector3(1f, 0f, 2f)));
            Assert.That(_agent.State, Is.EqualTo(EnemyState.Idle), "the brain receives the route on Start");
        }

        [Test]
        public void DeathKillsTheBrain()
        {
            _agent.Id = "enemy-1";
            var damageable = _host.GetComponent<Damageable>();
            damageable.Initialize("enemy-1", 10);
            Assert.That(_agent.TryInitialize(), Is.True);

            LogAssert.Expect(LogType.Log, "[Combat] died id=enemy-1");
            LogAssert.Expect(LogType.Log, "[AI] id=enemy-1 state=Dead prev=Idle");
            damageable.ApplyDamage(10);

            Assert.That(_agent.State, Is.EqualTo(EnemyState.Dead));
            Assert.That(_host.GetComponent<NavMeshAgent>().enabled, Is.False);
        }

        [Test]
        public void GroundAndEyePositionsUseTheBaseOffset()
        {
            _host.GetComponent<NavMeshAgent>().baseOffset = 1f;
            _host.transform.position = new Vector3(2f, 1f, 3f);
            Assert.That(_agent.TryInitialize(), Is.True);

            Assert.That(_agent.GroundPosition, Is.EqualTo(new Vector3(2f, 0f, 3f)));
            Assert.That(_agent.EyePosition, Is.EqualTo(new Vector3(2f, EnemyAgent.DefaultEyeHeight, 3f)));
        }

        [Test]
        public void FormatsStateLog()
        {
            Assert.That(
                EnemyAgent.FormatStateLog("enemy-2", EnemyState.Chase, EnemyState.Search),
                Is.EqualTo("[AI] id=enemy-2 state=Search prev=Chase"));
        }
    }
}
