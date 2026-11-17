using System;
using System.Text.RegularExpressions;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class BossDefinitionTests
    {
        private const int MaxHealth = 100;

        private static readonly ZoneDamageMultipliers InspectorMultipliers = new ZoneDamageMultipliers(1.0, 1.0, 1.0, 1.0);
        private static readonly ZoneDamageMultipliers DefinitionMultipliers = new ZoneDamageMultipliers(0.25, 0.2, 0.1, 3.0);

        private GameObject _root;
        private Damageable _damageable;
        private BossWeakPoint _weakPoint;
        private BossController _boss;
        private BossDefinition _definition;

        [SetUp]
        public void SetUp()
        {
            CombatEvents.ClearSubscribers();

            _root = new GameObject("Boss");
            _damageable = _root.AddComponent<Damageable>();
            _damageable.Initialize("Boss", MaxHealth);

            var part = new GameObject("WeakPoint");
            part.transform.SetParent(_root.transform, false);
            _weakPoint = part.AddComponent<BossWeakPoint>();
            _weakPoint.Initialize("Core");

            _boss = _root.AddComponent<BossController>();
            _boss.Configure(
                new[]
                {
                    new BossPhaseEntry("InspectorA", 1f, 1f, Array.Empty<string>()),
                    new BossPhaseEntry("InspectorB", 0.5f, 1.5f, new[] { "Core" }),
                },
                InspectorMultipliers);

            _definition = ScriptableObject.CreateInstance<BossDefinition>();
            _definition.name = "TestBoss";
            _definition.Configure(
                new[]
                {
                    new BossPhaseEntry("Guarded", 1f, 1f, Array.Empty<string>()),
                    new BossPhaseEntry("Exposed", 0.9f, 2f, new[] { "Core" }),
                },
                DefinitionMultipliers,
                2f,
                4f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_definition);
            CombatEvents.ClearSubscribers();
        }

        [Test]
        public void ANewDefinitionHasTheSameValuesAsTheComponentDefaults()
        {
            var fresh = ScriptableObject.CreateInstance<BossDefinition>();
            try
            {
                Assert.That(fresh.Phases.Count, Is.EqualTo(1));
                Assert.That(fresh.Phases[0].Id, Is.EqualTo(BossController.DefaultPhaseId));
                Assert.That(fresh.CreateZoneMultipliers().Head, Is.EqualTo(DamageCalculator.HeadMultiplier).Within(1e-6));
                Assert.That(fresh.CreateZoneMultipliers().WeakPoint, Is.EqualTo(DamageCalculator.WeakPointMultiplier).Within(1e-6));
                Assert.That(fresh.StrafeSpeed, Is.EqualTo(BossStrafeMotion.DefaultSpeed));
                Assert.That(fresh.StrafeHalfWidth, Is.EqualTo(BossStrafeMotion.DefaultHalfWidth));
            }
            finally
            {
                Object.DestroyImmediate(fresh);
            }
        }

        [Test]
        public void ThePhaseChangesAtTheDefinitionsThreshold()
        {
            _boss.Definition = _definition;
            Assert.That(_boss.TryInitialize(), Is.True);
            Assert.That(_boss.CurrentPhase.Id, Is.EqualTo("Guarded"));

            _damageable.TakeDamage(9);
            Assert.That(_boss.CurrentPhase.Id, Is.EqualTo("Guarded"), "0.91 is above the 0.9 threshold");

            LogAssert.Expect(
                LogType.Log,
                new Regex(@"^\[Boss\] id=Boss phase=0->1 from=Guarded to=Exposed reason=health<=0\.9 health=0\.9 speed=2 exposed=Core$"));

            _damageable.TakeDamage(1);

            Assert.That(_boss.CurrentPhase.Id, Is.EqualTo("Exposed"));
            Assert.That(_boss.MoveSpeedMultiplier, Is.EqualTo(2f));
            Assert.That(_weakPoint.IsExposed, Is.True);
        }

        [Test]
        public void TheDefinitionsZoneMultipliersReplaceTheInspectorOnes()
        {
            _boss.Definition = _definition;
            Assert.That(_boss.TryInitialize(), Is.True);

            Assert.That(_damageable.ZoneMultipliers.Body, Is.EqualTo(0.2).Within(1e-6));
            Assert.That(_damageable.ZoneMultipliers.WeakPoint, Is.EqualTo(3.0).Within(1e-6));
            Assert.That(_boss.PhaseEntries[1].Id, Is.EqualTo("Exposed"));
        }

        [Test]
        public void WithoutADefinitionTheInspectorValuesAreUsed()
        {
            Assert.That(_boss.Definition, Is.Null);
            Assert.That(_boss.TryInitialize(), Is.True);

            Assert.That(_boss.CurrentPhase.Id, Is.EqualTo("InspectorA"));
            Assert.That(_damageable.ZoneMultipliers.Body, Is.EqualTo(1.0).Within(1e-6));

            _damageable.TakeDamage(10);
            Assert.That(_boss.CurrentPhase.Id, Is.EqualTo("InspectorA"));

            _damageable.TakeDamage(40);
            Assert.That(_boss.CurrentPhase.Id, Is.EqualTo("InspectorB"));
        }

        [Test]
        public void AssigningADefinitionWhileRunningRestartsFromItsPhases()
        {
            Assert.That(_boss.TryInitialize(), Is.True);
            _damageable.TakeDamage(10);
            Assert.That(_boss.CurrentPhase.Id, Is.EqualTo("InspectorA"));

            _boss.Definition = _definition;

            Assert.That(_boss.CurrentPhase.Id, Is.EqualTo("Exposed"), "health is already at 0.9");
            Assert.That(_damageable.ZoneMultipliers.Body, Is.EqualTo(0.2).Within(1e-6));
            Assert.That(_weakPoint.IsExposed, Is.True);
        }

        [Test]
        public void ABrokenDefinitionIsReportedWithItsNameAndTheBossIsNotStarted()
        {
            var serialized = new SerializedObject(_definition);
            serialized.FindProperty("_phases").arraySize = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            _boss.Definition = _definition;

            LogAssert.Expect(LogType.Error, new Regex(@"^\[Boss\] id=Boss の設定が不正です \(定義 'TestBoss'\): "));

            Assert.That(_boss.TryInitialize(), Is.False);
            Assert.That(_boss.IsInitialized, Is.False);
        }

        [Test]
        public void AssigningABrokenDefinitionWhileRunningThrowsAndKeepsTheCurrentPhases()
        {
            Assert.That(_boss.TryInitialize(), Is.True);
            var serialized = new SerializedObject(_definition);
            serialized.FindProperty("_phases").arraySize = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var e = Assert.Throws<InvalidOperationException>(() => _boss.Definition = _definition);

            StringAssert.Contains("'TestBoss'", e.Message);
            Assert.That(_boss.Definition, Is.Null);
            Assert.That(_boss.CurrentPhase.Id, Is.EqualTo("InspectorA"));
        }

        [Test]
        public void ConfigureRefusesBadValuesAndKeepsThePreviousOnes()
        {
            Assert.Throws<ArgumentException>(
                () => _definition.Configure(Array.Empty<BossPhaseEntry>(), DefinitionMultipliers, 1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _definition.Configure(_definition.Phases, DefinitionMultipliers, -1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _definition.Configure(_definition.Phases, DefinitionMultipliers, 1f, float.PositiveInfinity));
            Assert.Throws<ArgumentNullException>(() => _definition.Configure(null, DefinitionMultipliers, 1f, 1f));
            Assert.Throws<ArgumentNullException>(() => _definition.Configure(_definition.Phases, null, 1f, 1f));

            Assert.That(_definition.Phases.Count, Is.EqualTo(2));
            Assert.That(_definition.Phases[1].HealthFraction, Is.EqualTo(0.9f));
            Assert.That(_definition.StrafeSpeed, Is.EqualTo(2f));
            Assert.That(_definition.StrafeHalfWidth, Is.EqualTo(4f));
        }

        [Test]
        public void StrafingReadsTheSpeedAndWidthFromTheDefinition()
        {
            _boss.Definition = _definition;
            Assert.That(_boss.TryInitialize(), Is.True);
            var strafe = _root.AddComponent<BossStrafeMotion>();
            strafe.Configure(1f, 10f);

            strafe.Tick(1f);

            Assert.That(strafe.Speed, Is.EqualTo(2f));
            Assert.That(strafe.HalfWidth, Is.EqualTo(4f));
            Assert.That(strafe.Offset, Is.EqualTo(2f).Within(1e-4f));
        }

        [Test]
        public void StrafingKeepsTheInspectorValuesWithoutADefinition()
        {
            Assert.That(_boss.TryInitialize(), Is.True);
            var strafe = _root.AddComponent<BossStrafeMotion>();
            strafe.Configure(1f, 10f);

            strafe.Tick(1f);

            Assert.That(strafe.Speed, Is.EqualTo(1f));
            Assert.That(strafe.HalfWidth, Is.EqualTo(10f));
            Assert.That(strafe.Offset, Is.EqualTo(1f).Within(1e-4f));
        }
    }
}
