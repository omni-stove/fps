using System.Text.RegularExpressions;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class EnemyStealthMeleeDefinitionTests
    {
        private GameObject _host;
        private EnemyAgent _agent;
        private EnemyStealth _stealth;
        private EnemyMelee _melee;
        private EnemyDefinition _definition;
        private WeaponDefinition _rifle;

        [SetUp]
        public void SetUp()
        {
            _rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            _rifle.name = "Rifle";
            WeaponAssets.Configure(_rifle, TestWeapons.RifleSpec());
            _definition = ScriptableObject.CreateInstance<EnemyDefinition>();
            _definition.name = "Grunt";

            _host = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _host.name = "enemy-1";
            _agent = _host.AddComponent<EnemyAgent>();
            _agent.WeaponDefinition = _rifle;
            _agent.Id = "enemy-1";
            _host.GetComponent<Damageable>().Initialize("enemy-1", 10);
            _stealth = _host.AddComponent<EnemyStealth>();
            _melee = _host.AddComponent<EnemyMelee>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
            Object.DestroyImmediate(_definition);
            Object.DestroyImmediate(_rifle);
        }

        [Test]
        public void TheStealthsOwnGainIsUsedWithOrWithoutADefinition()
        {
            SetStealthField("_settings._gainPerSecond", 0.5f);
            _agent.Definition = _definition;

            Assert.That(_agent.EffectiveDefinition, Is.SameAs(_definition));
            Assert.That(_stealth.TryInitialize(), Is.True);
            Assert.That(_stealth.Awareness.Spec.GainPerSecond, Is.EqualTo(0.5f));
        }

        [Test]
        public void AnAppliedAwarenessSpecWinsOverTheInspector()
        {
            SetStealthField("_settings._gainPerSecond", 0.5f);
            _agent.Definition = _definition;

            _stealth.ApplySpec(new AwarenessSpec(gainPerSecond: 3f));

            Assert.That(_stealth.TryInitialize(), Is.True);
            Assert.That(_stealth.Awareness.Spec.GainPerSecond, Is.EqualTo(3f));
        }

        [Test]
        public void TheDefinitionsMeleeIsUsedOverTheInspector()
        {
            _definition.Melee.CopyFrom(new MeleeSpec(80, 2f, 120f, 0.6f, 1.5f, 0.3f, 0.9f));
            SetMeleeField("_damage", 10);
            _agent.Definition = _definition;

            var spec = _melee.Spec;

            Assert.That(spec.Damage, Is.EqualTo(80));
            Assert.That(spec.Reach, Is.EqualTo(2f));
            Assert.That(spec.ArcDegrees, Is.EqualTo(120f));
            Assert.That(spec.SwingSeconds, Is.EqualTo(0.6f));
            Assert.That(spec.CooldownSeconds, Is.EqualTo(1.5f));
            Assert.That(spec.ImpactSeconds, Is.EqualTo(0.3f));
            Assert.That(spec.LungeDistance, Is.EqualTo(0.9f));
        }

        [Test]
        public void WithoutADefinitionTheInspectorMeleeIsUsed()
        {
            SetMeleeField("_damage", 10);

            Assert.That(_melee.Spec.Damage, Is.EqualTo(10));
        }

        [Test]
        public void AnInvalidDefinitionMeleeIsReportedAndTheDefaultIsUsed()
        {
            var serialized = new SerializedObject(_definition);
            serialized.FindProperty("_melee._impactSeconds").floatValue = 1f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            SetMeleeField("_damage", 10);
            _agent.Definition = _definition;

            LogAssert.Expect(LogType.Error, new Regex(@"^\[Melee\] 'enemy-1' の近接攻撃の値が不正なので既定値で振ります: enemy 'Grunt' has invalid melee"));
            Assert.That(_melee.Spec.Damage, Is.EqualTo(MeleeSpec.DefaultDamage));
        }

        private void SetStealthField(string field, float value)
        {
            var serialized = new SerializedObject(_stealth);
            serialized.FindProperty(field).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void SetMeleeField(string field, int value)
        {
            var serialized = new SerializedObject(_melee);
            serialized.FindProperty(field).intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
