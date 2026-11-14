using System;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    // Update は Edit Mode では走らないので、止めている間の動きは Core の EnemyInterruption のテストと Play での確認に任せる。
    public sealed class EnemyAgentInterruptTests
    {
        private GameObject _host;
        private EnemyAgent _agent;
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
            _host.GetComponent<Damageable>().Initialize("enemy-1", 10);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
            Object.DestroyImmediate(_rifle);
        }

        [Test]
        public void InterruptNeedsAnInitializedAgent()
        {
            Assert.Throws<InvalidOperationException>(() => _agent.Interrupt(1f, "flinch"));
        }

        [Test]
        public void InterruptStopsTheAgentAndLogsTheReason()
        {
            Assert.That(_agent.TryInitialize(), Is.True);

            LogAssert.Expect(LogType.Log, "[AI] id=enemy-1 interrupted reason=flinch seconds=0.50");
            Assert.That(_agent.Interrupt(0.5f, "flinch"), Is.True);

            Assert.That(_agent.IsInterrupted, Is.True);
            Assert.That(_agent.InterruptReason, Is.EqualTo("flinch"));
            Assert.That(_agent.InterruptSecondsRemaining, Is.EqualTo(0.5f));
        }

        [Test]
        public void ShorterInterruptDoesNotCutALongerOneShort()
        {
            Assert.That(_agent.TryInitialize(), Is.True);
            _agent.Interrupt(3f, "takedown");

            Assert.That(_agent.Interrupt(0.5f, "flinch"), Is.False);
            Assert.That(_agent.InterruptReason, Is.EqualTo("takedown"));
        }

        [Test]
        public void InvalidArgumentsAreRejected()
        {
            Assert.That(_agent.TryInitialize(), Is.True);

            Assert.Throws<ArgumentOutOfRangeException>(() => _agent.Interrupt(0f, "flinch"));
            Assert.Throws<ArgumentException>(() => _agent.Interrupt(1f, ""));
            Assert.That(_agent.IsInterrupted, Is.False);
        }

        [Test]
        public void DeadAgentIsNotInterruptedAndDyingClearsTheInterrupt()
        {
            Assert.That(_agent.TryInitialize(), Is.True);
            _agent.Interrupt(2f, "flinch");

            LogAssert.Expect(LogType.Log, "[Combat] target died id=enemy-1");
            LogAssert.Expect(LogType.Log, "[AI] id=enemy-1 state=Dead prev=Idle");
            _host.GetComponent<Damageable>().TakeDamage(10);

            Assert.That(_agent.IsInterrupted, Is.False);
            Assert.That(_agent.Interrupt(1f, "flinch"), Is.False);
        }

        [Test]
        public void ArchetypeComesFromTheSerializedFieldAndTheSpec()
        {
            Assert.That(_agent.Archetype, Is.EqualTo(EnemyArchetype.Rifleman));

            var serialized = new SerializedObject(_agent);
            Assert.That(serialized.FindProperty("_archetype"), Is.Not.Null);

            _agent.ApplySpec(new EnemySpec(20f, 10f, 1f, 2f, 0.5f, archetype: EnemyArchetype.Rifleman));
            Assert.That(_agent.TryInitialize(), Is.True);
            Assert.That(_agent.Brain.Spec.Archetype, Is.EqualTo(EnemyArchetype.Rifleman));
        }
    }
}
