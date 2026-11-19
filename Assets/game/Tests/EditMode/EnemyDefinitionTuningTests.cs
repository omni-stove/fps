using System;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class EnemyDefinitionTuningTests
    {
        private EnemyDefinition _definition;
        private GameObject _host;

        [SetUp]
        public void SetUp()
        {
            _definition = ScriptableObject.CreateInstance<EnemyDefinition>();
            _definition.name = "Grunt";
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null)
            {
                Object.DestroyImmediate(_host);
            }

            Object.DestroyImmediate(_definition);
        }

        [Test]
        public void SpecDefaultsMatchTheConstantsUsedToday()
        {
            Assert.That(EnemySpec.DefaultArrivalDistance, Is.EqualTo(0.5f));
            Assert.That(EnemySpec.DefaultSprintRatio, Is.EqualTo(1.6f));
            Assert.That(EnemySpec.DefaultCrouchRatio, Is.EqualTo(0.5f));
            Assert.That(EnemySpec.DefaultRecoilControl, Is.EqualTo(EnemyAgent.RecoilControl));

            Assert.That(EnemySpec.Default.ArrivalDistance, Is.EqualTo(0.5f));
            Assert.That(EnemySpec.Default.SprintRatio, Is.EqualTo(1.6f));
            Assert.That(EnemySpec.Default.CrouchRatio, Is.EqualTo(0.5f));
            Assert.That(EnemySpec.Default.RecoilControl, Is.EqualTo(0.75f));
        }

        [Test]
        public void SpecBuiltWithoutTheNewArgumentsUsesTheDefaults()
        {
            var spec = new EnemySpec(20f, 10f, 1f, 2f, 0.5f);

            Assert.That(spec.ArrivalDistance, Is.EqualTo(0.5f));
            Assert.That(spec.SprintRatio, Is.EqualTo(1.6f));
            Assert.That(spec.CrouchRatio, Is.EqualTo(0.5f));
            Assert.That(spec.RecoilControl, Is.EqualTo(0.75f));
        }

        [TestCase(0f, 1.6f, 0.5f, 0.75f)]
        [TestCase(-1f, 1.6f, 0.5f, 0.75f)]
        [TestCase(float.PositiveInfinity, 1.6f, 0.5f, 0.75f)]
        [TestCase(0.5f, 0f, 0.5f, 0.75f)]
        [TestCase(0.5f, float.NaN, 0.5f, 0.75f)]
        [TestCase(0.5f, 1.6f, 0f, 0.75f)]
        [TestCase(0.5f, 1.6f, -0.5f, 0.75f)]
        [TestCase(0.5f, 1.6f, 0.5f, -0.01f)]
        [TestCase(0.5f, 1.6f, 0.5f, 1.01f)]
        [TestCase(0.5f, 1.6f, 0.5f, float.NaN)]
        public void SpecRejectsOutOfRangeMovementAndRecoil(float arrival, float sprint, float crouch, float recoil)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EnemySpec(
                20f, 10f, 1f, 2f, 0.5f, arrivalDistance: arrival, sprintRatio: sprint, crouchRatio: crouch, recoilControl: recoil));
        }

        [TestCase(0f)]
        [TestCase(1f)]
        public void SpecAcceptsRecoilControlAtTheEnds(float recoil)
        {
            var spec = new EnemySpec(20f, 10f, 1f, 2f, 0.5f, recoilControl: recoil);

            Assert.That(spec.RecoilControl, Is.EqualTo(recoil));
        }

        [Test]
        public void NewDefinitionHasTodaysBehaviorValues()
        {
            Assert.That(_definition.ArrivalDistance, Is.EqualTo(0.5f));
            Assert.That(_definition.SprintRatio, Is.EqualTo(1.6f));
            Assert.That(_definition.CrouchRatio, Is.EqualTo(0.5f));
            Assert.That(_definition.RecoilControl, Is.EqualTo(EnemyAgent.RecoilControl));
            Assert.That(_definition.TurnSpeed, Is.EqualTo(360f));
            Assert.That(_definition.EyeHeight, Is.EqualTo(1.6f));
            Assert.That(_definition.CrouchHeight, Is.EqualTo(1f));
            Assert.That(_definition.CrouchEyeHeight, Is.EqualTo(0.8f));

            var spec = _definition.ToSpec();
            Assert.That(spec.ArrivalDistance, Is.EqualTo(0.5f));
            Assert.That(spec.SprintRatio, Is.EqualTo(1.6f));
            Assert.That(spec.CrouchRatio, Is.EqualTo(0.5f));
            Assert.That(spec.RecoilControl, Is.EqualTo(0.75f));
        }

        [Test]
        public void NewDefinitionBodyMatchesANewAgentsInspectorValues()
        {
            _host = new GameObject("Enemy");
            var agent = new SerializedObject(_host.AddComponent<EnemyAgent>());

            Assert.That(_definition.TurnSpeed, Is.EqualTo(agent.FindProperty("_turnSpeed").floatValue));
            Assert.That(_definition.EyeHeight, Is.EqualTo(agent.FindProperty("_eyeHeight").floatValue));
            Assert.That(_definition.CrouchHeight, Is.EqualTo(agent.FindProperty("_crouchHeight").floatValue));
            Assert.That(_definition.CrouchEyeHeight, Is.EqualTo(agent.FindProperty("_crouchEyeHeight").floatValue));
        }

        [Test]
        public void NewDefinitionMeleeMatchesNewComponentsInspectorValues()
        {
            _host = new GameObject("Enemy");
            _host.AddComponent<EnemyAgent>();
            var melee = new SerializedObject(_host.AddComponent<EnemyMelee>());

            var swing = _definition.ToMeleeSpec();
            Assert.That(swing.Damage, Is.EqualTo(melee.FindProperty("_damage").intValue));
            Assert.That(swing.Reach, Is.EqualTo(melee.FindProperty("_reach").floatValue));
            Assert.That(swing.ArcDegrees, Is.EqualTo(melee.FindProperty("_arcDegrees").floatValue));
            Assert.That(swing.SwingSeconds, Is.EqualTo(melee.FindProperty("_swingSeconds").floatValue));
            Assert.That(swing.CooldownSeconds, Is.EqualTo(melee.FindProperty("_cooldownSeconds").floatValue));
            Assert.That(swing.ImpactSeconds, Is.EqualTo(melee.FindProperty("_impactSeconds").floatValue));
            Assert.That(swing.LungeDistance, Is.EqualTo(melee.FindProperty("_lungeDistance").floatValue));
        }

        [Test]
        public void NewDefinitionMeleeMatchesTheEnemyMeleeDefaults()
        {
            var spec = _definition.ToMeleeSpec();

            Assert.That(spec.Damage, Is.EqualTo(50));
            Assert.That(spec.Reach, Is.EqualTo(2f));
            Assert.That(spec.ArcDegrees, Is.EqualTo(90f));
            Assert.That(spec.SwingSeconds, Is.EqualTo(0.5f));
            Assert.That(spec.CooldownSeconds, Is.EqualTo(0.4f));
            Assert.That(spec.ImpactSeconds, Is.EqualTo(0.2f));
            Assert.That(spec.LungeDistance, Is.EqualTo(0.75f));
        }

        [Test]
        public void ValuesWrittenInTheDefinitionReachTheSpec()
        {
            _definition.ArrivalDistance = 1.25f;
            _definition.SprintRatio = 2f;
            _definition.CrouchRatio = 0.3f;
            _definition.RecoilControl = 0.4f;

            var spec = _definition.ToSpec();

            Assert.That(spec.ArrivalDistance, Is.EqualTo(1.25f));
            Assert.That(spec.SprintRatio, Is.EqualTo(2f));
            Assert.That(spec.CrouchRatio, Is.EqualTo(0.3f));
            Assert.That(spec.RecoilControl, Is.EqualTo(0.4f));
        }

        [Test]
        public void MeleeWrittenInTheDefinitionReachesTheMeleeSpec()
        {
            _definition.Melee.CopyFrom(new MeleeSpec(80, 1.5f, 120f, 0.8f, 1f, 0.3f, 0f));

            var spec = _definition.ToMeleeSpec();

            Assert.That(spec.Damage, Is.EqualTo(80));
            Assert.That(spec.Reach, Is.EqualTo(1.5f));
            Assert.That(spec.ArcDegrees, Is.EqualTo(120f));
            Assert.That(spec.SwingSeconds, Is.EqualTo(0.8f));
            Assert.That(spec.CooldownSeconds, Is.EqualTo(1f));
            Assert.That(spec.ImpactSeconds, Is.EqualTo(0.3f));
            Assert.That(spec.LungeDistance, Is.EqualTo(0f));
        }

        [Test]
        public void InvalidValuesNameTheDefinition()
        {
            _definition.ArrivalDistance = 0f;
            var brain = Assert.Throws<EnemyDefinitionException>(() => _definition.ToSpec());
            Assert.That(brain.Message, Does.Contain("Grunt").And.Contain("arrivalDistance"));

            var serialized = new SerializedObject(_definition);
            serialized.FindProperty("_melee._impactSeconds").floatValue = 1f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var melee = Assert.Throws<EnemyDefinitionException>(() => _definition.ToMeleeSpec());
            Assert.That(melee.Message, Does.Contain("Grunt").And.Contain("impactSeconds"));
        }

        [Test]
        public void CopyFromKeepsTheNewSpecValues()
        {
            var source = new EnemySpec(
                20f, 10f, 1f, 2f, 0.5f, arrivalDistance: 0.8f, sprintRatio: 1.2f, crouchRatio: 0.7f, recoilControl: 0.1f);

            _definition.CopyFrom(source);

            Assert.That(_definition.ArrivalDistance, Is.EqualTo(0.8f));
            Assert.That(_definition.SprintRatio, Is.EqualTo(1.2f));
            Assert.That(_definition.CrouchRatio, Is.EqualTo(0.7f));
            Assert.That(_definition.RecoilControl, Is.EqualTo(0.1f));
        }

        [Test]
        public void WaveStrengthKeepsTheNewSpecValues()
        {
            var source = new EnemySpec(
                20f, 10f, 1f, 2f, 0.5f, arrivalDistance: 0.8f, sprintRatio: 1.2f, crouchRatio: 0.7f, recoilControl: 0.1f);

            var scaled = WaveStrength.Apply(source, 2f);

            Assert.That(scaled.ArrivalDistance, Is.EqualTo(0.8f));
            Assert.That(scaled.SprintRatio, Is.EqualTo(1.2f));
            Assert.That(scaled.CrouchRatio, Is.EqualTo(0.7f));
            Assert.That(scaled.RecoilControl, Is.EqualTo(0.1f));
        }

        [Test]
        public void AgentKeepsTheNewSpecValuesAfterApplySpec()
        {
            _host = new GameObject("Enemy");
            var agent = _host.AddComponent<EnemyAgent>();
            var source = new EnemySpec(
                20f, 10f, 1f, 2f, 0.5f, arrivalDistance: 0.8f, sprintRatio: 1.2f, crouchRatio: 0.7f, recoilControl: 0.1f);

            agent.ApplySpec(source);
            var spec = agent.CreateSpec();

            Assert.That(spec.ArrivalDistance, Is.EqualTo(0.8f));
            Assert.That(spec.SprintRatio, Is.EqualTo(1.2f));
            Assert.That(spec.CrouchRatio, Is.EqualTo(0.7f));
            Assert.That(spec.RecoilControl, Is.EqualTo(0.1f));
        }

        [Test]
        public void AgentExposesTheDefinitionItUses()
        {
            _host = new GameObject("Enemy");
            var agent = _host.AddComponent<EnemyAgent>();
            agent.Definition = _definition;

            Assert.That(agent.EffectiveDefinition, Is.SameAs(_definition));
            Assert.That(agent.CreateSpec().RecoilControl, Is.EqualTo(_definition.RecoilControl));
        }
    }
}
