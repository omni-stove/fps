using System;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    public sealed class EnemyDefinitionTests
    {
        private EnemyDefinition _definition;
        private WeaponDefinition _rifle;
        private GameObject _host;
        private EnemyAgent _agent;

        [SetUp]
        public void SetUp()
        {
            _definition = ScriptableObject.CreateInstance<EnemyDefinition>();
            _definition.name = "Grunt";

            _rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            _rifle.name = "Rifle";
            WeaponAssetBuilder.Configure(_rifle, WeaponAssetBuilder.RifleSpec());

            _host = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _host.name = "Enemy";
            _agent = _host.AddComponent<EnemyAgent>();
            _agent.WeaponDefinition = _rifle;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
            Object.DestroyImmediate(_rifle);
            Object.DestroyImmediate(_definition);
        }

        [Test]
        public void NewDefinitionHasTheDefaultSpecValues()
        {
            var spec = _definition.ToSpec();
            var expected = EnemySpec.Default;

            Assert.That(spec.SightRange, Is.EqualTo(expected.SightRange));
            Assert.That(spec.AttackRange, Is.EqualTo(expected.AttackRange));
            Assert.That(spec.LoseSightSeconds, Is.EqualTo(expected.LoseSightSeconds));
            Assert.That(spec.SearchSeconds, Is.EqualTo(expected.SearchSeconds));
            Assert.That(spec.ReactionSeconds, Is.EqualTo(expected.ReactionSeconds));
            Assert.That(spec.OpeningSpreadDegrees, Is.EqualTo(expected.OpeningSpreadDegrees));
            Assert.That(spec.SettledSpreadDegrees, Is.EqualTo(expected.SettledSpreadDegrees));
            Assert.That(spec.OpeningShots, Is.EqualTo(expected.OpeningShots));
            Assert.That(spec.CoverHideSeconds, Is.EqualTo(expected.CoverHideSeconds));
            Assert.That(spec.CoverPeekSeconds, Is.EqualTo(expected.CoverPeekSeconds));
            Assert.That(spec.CoverHealthFraction, Is.EqualTo(expected.CoverHealthFraction));
            Assert.That(spec.StaggerResistance, Is.EqualTo(expected.StaggerResistance));
            Assert.That(spec.Archetype, Is.EqualTo(expected.Archetype));
            Assert.That(spec.Sniper.AimSeconds, Is.EqualTo(expected.Sniper.AimSeconds));
            Assert.That(spec.Charger.ChargeSpeed, Is.EqualTo(expected.Charger.ChargeSpeed));
            Assert.That(spec.Bomber.FuseSeconds, Is.EqualTo(expected.Bomber.FuseSeconds));
            Assert.That(spec.Shield.HoldDistance, Is.EqualTo(expected.Shield.HoldDistance));
        }

        [Test]
        public void NewDefinitionMatchesANewAgentsInspectorValues()
        {
            var fromAgent = _agent.CreateSpec();
            var fromDefinition = _definition.ToSpec();

            Assert.That(fromDefinition.SightRange, Is.EqualTo(fromAgent.SightRange));
            Assert.That(fromDefinition.AttackRange, Is.EqualTo(fromAgent.AttackRange));
            Assert.That(fromDefinition.ReactionSeconds, Is.EqualTo(fromAgent.ReactionSeconds));
            Assert.That(fromDefinition.OpeningSpreadDegrees, Is.EqualTo(fromAgent.OpeningSpreadDegrees));
            Assert.That(fromDefinition.OpeningShots, Is.EqualTo(fromAgent.OpeningShots));
            Assert.That(fromDefinition.CoverHealthFraction, Is.EqualTo(fromAgent.CoverHealthFraction));
        }

        [Test]
        public void CopyFromThenToSpecKeepsEveryValue()
        {
            var sniper = new SniperSpec(2f, 3f, 1f, 5f);
            var source = new EnemySpec(
                35f, 30f, 1.5f, 4f, 0.25f, 8f, 1f, 5, 2.5f, 3f, 0.3f,
                EnemyArchetype.Sniper, sniper: sniper, staggerResistance: 1.5f);

            _definition.CopyFrom(source);
            var spec = _definition.ToSpec();

            Assert.That(spec.SightRange, Is.EqualTo(35f));
            Assert.That(spec.AttackRange, Is.EqualTo(30f));
            Assert.That(spec.LoseSightSeconds, Is.EqualTo(1.5f));
            Assert.That(spec.SearchSeconds, Is.EqualTo(4f));
            Assert.That(spec.ReactionSeconds, Is.EqualTo(0.25f));
            Assert.That(spec.OpeningSpreadDegrees, Is.EqualTo(8f));
            Assert.That(spec.SettledSpreadDegrees, Is.EqualTo(1f));
            Assert.That(spec.OpeningShots, Is.EqualTo(5));
            Assert.That(spec.CoverHideSeconds, Is.EqualTo(2.5f));
            Assert.That(spec.CoverPeekSeconds, Is.EqualTo(3f));
            Assert.That(spec.CoverHealthFraction, Is.EqualTo(0.3f));
            Assert.That(spec.Archetype, Is.EqualTo(EnemyArchetype.Sniper));
            Assert.That(spec.StaggerResistance, Is.EqualTo(1.5f));
            Assert.That(spec.Sniper.AimSeconds, Is.EqualTo(2f));
            Assert.That(spec.Sniper.ShotDamageMultiplier, Is.EqualTo(3f));
            Assert.That(spec.Sniper.ReaimDelaySeconds, Is.EqualTo(1f));
            Assert.That(spec.Sniper.RelocateGiveUpSeconds, Is.EqualTo(5f));
        }

        [Test]
        public void CopyFromRejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => _definition.CopyFrom(null));
        }

        [Test]
        public void InvalidValuesAreReportedWithTheAssetName()
        {
            _definition.AttackRange = _definition.SightRange + 1f;

            var e = Assert.Throws<EnemyDefinitionException>(() => _definition.ToSpec());

            Assert.That(e, Is.InstanceOf<ArgumentException>());
            Assert.That(e.Message, Does.Contain("Grunt"));
            Assert.That(e.InnerException, Is.InstanceOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void AgentUsesTheDefinitionInsteadOfItsInspectorValues()
        {
            _definition.SightRange = 35f;
            _definition.AttackRange = 25f;
            _definition.ReactionSeconds = 0.25f;
            _definition.Archetype = EnemyArchetype.Sniper;
            _agent.Definition = _definition;

            Assert.That(_agent.TryInitialize(), Is.True);

            Assert.That(_agent.Brain.Spec.SightRange, Is.EqualTo(35f));
            Assert.That(_agent.Brain.Spec.AttackRange, Is.EqualTo(25f));
            Assert.That(_agent.Brain.Spec.ReactionSeconds, Is.EqualTo(0.25f));
            Assert.That(_agent.SightRange, Is.EqualTo(35f), "perception reads the sight from the definition too");
            Assert.That(_agent.AttackRange, Is.EqualTo(25f));
            Assert.That(_agent.Archetype, Is.EqualTo(EnemyArchetype.Sniper));
        }

        [Test]
        public void AgentWithoutADefinitionKeepsTheDefaults()
        {
            Assert.That(_agent.Definition, Is.Null);
            Assert.That(_agent.TryInitialize(), Is.True);

            Assert.That(_agent.Brain.Spec.SightRange, Is.EqualTo(EnemySpec.DefaultSightRange));
            Assert.That(_agent.Brain.Spec.ReactionSeconds, Is.EqualTo(EnemySpec.DefaultReactionSeconds));
            Assert.That(_agent.SightRange, Is.EqualTo(EnemySpec.DefaultSightRange));
        }

        [Test]
        public void CreateSpecBeforeInitializeReadsTheDefinition()
        {
            _definition.SightRange = 40f;
            _agent.Definition = _definition;

            Assert.That(_agent.CreateSpec().SightRange, Is.EqualTo(40f));
            Assert.That(_agent.IsInitialized, Is.False);
        }

        [Test]
        public void AppliedSpecWinsOverTheDefinition()
        {
            _definition.ReactionSeconds = 1f;
            _agent.Definition = _definition;
            var strengthened = WaveStrength.Apply(_agent.CreateSpec(), 2f);

            _agent.ApplySpec(strengthened);

            Assert.That(_agent.CreateSpec().ReactionSeconds, Is.EqualTo(0.5f));
            Assert.That(_agent.TryInitialize(), Is.True);
            Assert.That(_agent.Brain.Spec.ReactionSeconds, Is.EqualTo(0.5f));
        }

        [Test]
        public void ChangedSpecWinsOverTheDefinitionAfterInitialize()
        {
            _definition.SightRange = 30f;
            _definition.AttackRange = 20f;
            _agent.Definition = _definition;
            Assert.That(_agent.TryInitialize(), Is.True);

            _agent.ChangeSpec(new EnemySpec(15f, 10f, 1f, 2f, 0.1f));

            Assert.That(_agent.Brain.Spec.SightRange, Is.EqualTo(15f));
            Assert.That(_agent.SightRange, Is.EqualTo(15f));
            Assert.That(_agent.CreateSpec().SightRange, Is.EqualTo(15f));
        }

        [Test]
        public void SettingTheDefinitionAgainUsesItOverAnEarlierAppliedSpec()
        {
            _agent.ApplySpec(new EnemySpec(15f, 10f, 1f, 2f, 0.1f));
            _definition.SightRange = 45f;

            _agent.Definition = _definition;

            Assert.That(_agent.CreateSpec().SightRange, Is.EqualTo(45f));
        }

        [Test]
        public void InvalidDefinitionIsReportedAndRejected()
        {
            _definition.AttackRange = _definition.SightRange + 1f;
            _agent.Definition = _definition;

            LogAssert.Expect(LogType.Error, new Regex(@"^\[AI\] id=.* の設定が不正です: enemy 'Grunt' is invalid"));
            Assert.That(_agent.TryInitialize(), Is.False);
            Assert.That(_agent.IsInitialized, Is.False);
        }

        [Test]
        public void DefinitionCannotBeChangedAfterInitialize()
        {
            Assert.That(_agent.TryInitialize(), Is.True);

            Assert.Throws<InvalidOperationException>(() => _agent.Definition = _definition);
            Assert.That(_agent.Definition, Is.Null);
        }
    }
}
