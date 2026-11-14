using System;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    // 着地すると NavMeshAgent を有効に戻すが、Edit Mode には焼いた NavMesh が無いので、着地と NavMesh への寄せは KnockbackSmokeRun で確かめる。
    public sealed class EnemyAgentKnockbackTests
    {
        private GameObject _host;
        private EnemyAgent _agent;
        private Damageable _damageable;
        private WeaponDefinition _rifle;

        [SetUp]
        public void SetUp()
        {
            _rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            _rifle.name = "Rifle";
            WeaponAssets.Configure(_rifle, TestWeapons.RifleSpec());

            _host = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _host.name = "Enemy";
            _agent = _host.AddComponent<EnemyAgent>();
            _agent.WeaponDefinition = _rifle;
            _agent.Id = "enemy-1";
            _damageable = _host.GetComponent<Damageable>();
            _damageable.Initialize("enemy-1", 10);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
            Object.DestroyImmediate(_rifle);
        }

        [Test]
        public void LaunchNeedsAnInitializedAgent()
        {
            Assert.Throws<InvalidOperationException>(() => _agent.Launch(Vector3.up, DamageKind.Explosion));
        }

        [Test]
        public void ALaunchDetachesTheAgentAndHoldsTheBrain()
        {
            Assert.That(_agent.TryInitialize(), Is.True);

            LogAssert.Expect(LogType.Log, "[Knockback] id=enemy-1 launched kind=Explosion velocity=(5.0, 3.0, 0.0)");
            LogAssert.Expect(LogType.Log, "[AI] id=enemy-1 interrupted reason=knockback seconds=0.40");
            Assert.That(_agent.Launch(new Vector3(5f, 3f, 0f), DamageKind.Explosion), Is.True);

            Assert.That(_agent.IsKnockedBack, Is.True);
            Assert.That(_host.GetComponent<NavMeshAgent>().enabled, Is.False, "the agent would pull the body back onto the NavMesh");
            Assert.That(_agent.IsInterrupted, Is.True);
            Assert.That(_agent.InterruptReason, Is.EqualTo(EnemyAgent.ReasonKnockback));
            Assert.That(_agent.KnockbackVelocity, Is.EqualTo(new Vector3(5f, 3f, 0f)));
        }

        [Test]
        public void TickingTheFlightMovesTheBodyThroughTheAir()
        {
            Assert.That(_agent.TryInitialize(), Is.True);
            Assert.That(_agent.TickKnockback(0.1f), Is.False, "nothing to do on the ground");

            _agent.Launch(new Vector3(5f, 3f, 0f), DamageKind.Explosion);
            var start = _host.transform.position;

            Assert.That(_agent.TickKnockback(0.05f), Is.True);

            var moved = _host.transform.position - start;
            Assert.That(moved.x, Is.EqualTo(0.25f).Within(1e-4f));
            Assert.That(moved.y, Is.EqualTo((3f + 3f - (KnockbackSpec.DefaultGravity * 0.05f)) * 0.5f * 0.05f).Within(1e-4f));
            Assert.That(_agent.IsKnockedBack, Is.True, "no floor to land on yet");
        }

        [Test]
        public void AWallStopsTheSidewaysFlight()
        {
            Assert.That(_agent.TryInitialize(), Is.True);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                wall.transform.position = _host.transform.position + new Vector3(0.8f, 0f, 0f);
                wall.transform.localScale = new Vector3(0.2f, 4f, 4f);
                Physics.SyncTransforms();

                _agent.Launch(new Vector3(20f, 0.5f, 0f), DamageKind.Explosion);
                _agent.TickKnockback(0.05f);

                Assert.That(_agent.KnockbackVelocity.x, Is.Zero, "stopped by the wall");
                Assert.That(_host.transform.position.x, Is.LessThan(wall.transform.position.x), "did not pass through the wall");
            }
            finally
            {
                Object.DestroyImmediate(wall);
            }
        }

        [Test]
        public void AMeleeHitLaunchesTheEnemy()
        {
            Assert.That(_agent.TryInitialize(), Is.True);
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Knockback\] id=enemy-1 launched kind=Melee velocity="));

            _damageable.TakeDamage(1, Vector3.zero, Vector3.forward, DamageKind.Melee);

            Assert.That(_agent.IsKnockedBack, Is.True);
            Assert.That(_agent.KnockbackVelocity.z, Is.GreaterThan(0f));
        }

        [Test]
        public void TheDeadAreNotLaunchedAndDyingStopsTheFlight()
        {
            Assert.That(_agent.TryInitialize(), Is.True);
            _agent.Launch(new Vector3(5f, 3f, 0f), DamageKind.Explosion);

            _damageable.Kill();

            Assert.That(_agent.IsKnockedBack, Is.False);
            Assert.That(_agent.Launch(new Vector3(5f, 3f, 0f), DamageKind.Explosion), Is.False);
        }

        [Test]
        public void TheTunedForceAndEnemyWeightSetHowFarTheEnemyFlies()
        {
            Assert.That(_agent.TryInitialize(), Is.True);
            var tuning = new KnockbackSpec(
                explosion: new KnockbackForce(12f, 0f, 1f),
                recoverSeconds: 1.25f,
                playerWeight: 0.5f,
                enemyWeight: 4f);

            _agent.ApplyKnockbackTuning(tuning);

            Assert.That(_agent.KnockbackSpec, Is.SameAs(tuning));
            Assert.That(_agent.KnockbackWeight, Is.EqualTo(4f), "the enemy reads the enemy weight, not the player one");
            LogAssert.Expect(LogType.Log, "[Knockback] id=enemy-1 launched kind=Explosion velocity=(3.0, 0.0, 0.0)");
            LogAssert.Expect(LogType.Log, "[AI] id=enemy-1 interrupted reason=knockback seconds=1.25");
            Assert.That(Knockback.Apply(_damageable, DamageKind.Explosion, Vector3.right, 1f), Is.True);
            Assert.That(_agent.KnockbackVelocity.magnitude, Is.EqualTo(12f / 4f).Within(1e-4f));
        }

        [Test]
        public void TuningDoesNotOverwriteValuesSetByCode()
        {
            var set = new KnockbackSpec(gravity: 30f);
            _agent.KnockbackSpec = set;
            _agent.KnockbackWeight = 2f;

            _agent.ApplyKnockbackTuning(new KnockbackSpec(gravity: 10f, enemyWeight: 5f));

            Assert.That(_agent.KnockbackSpec, Is.SameAs(set));
            Assert.That(_agent.KnockbackWeight, Is.EqualTo(2f));
            Assert.Throws<ArgumentNullException>(() => _agent.ApplyKnockbackTuning(null));
        }

        [Test]
        public void RejectsInvalidValues()
        {
            Assert.That(_agent.TryInitialize(), Is.True);

            Assert.Throws<ArgumentException>(() => _agent.Launch(new Vector3(0f, float.PositiveInfinity, 0f), DamageKind.Explosion));
            Assert.Throws<ArgumentOutOfRangeException>(() => _agent.TickKnockback(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _agent.KnockbackWeight = 0f);
            Assert.Throws<ArgumentNullException>(() => _agent.KnockbackSpec = null);
            Assert.That(_agent.IsKnockedBack, Is.False);
        }
    }
}
