using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    public sealed class BossControllerAgentTests
    {
        private GameObject _host;
        private EnemyAgent _agent;
        private Damageable _damageable;
        private BossController _boss;
        private WeaponDefinition _rifle;
        private EnemySpec _enraged;
        private PassBehavior _behavior;

        [SetUp]
        public void SetUp()
        {
            _rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            _rifle.name = "Rifle";
            WeaponAssets.Configure(_rifle, TestWeapons.RifleSpec());

            _host = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _host.name = "Boss";
            _agent = _host.AddComponent<EnemyAgent>();
            _agent.WeaponDefinition = _rifle;
            _agent.Id = "boss-1";
            _damageable = _host.GetComponent<Damageable>();
            _damageable.Initialize("boss-1", 100);
            _boss = _host.AddComponent<BossController>();

            _enraged = new EnemySpec(30f, 20f, 1f, 2f, 0.1f);
            _behavior = new PassBehavior();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
            Object.DestroyImmediate(_rifle);
        }

        [Test]
        public void EachPhaseSwapsTheSpecTheBehaviorAndTheSpeedAndTheFirstPhaseRestoresTheOriginalSpec()
        {
            Assert.That(_agent.TryInitialize(), Is.True);
            var original = _agent.Brain.Spec;
            Assert.That(_boss.TryInitialize(), Is.True);
            _boss.UsePhases(Phases());

            Assert.That(_agent.Brain.Spec, Is.SameAs(original));
            Assert.That(_agent.Brain.Behaviors, Is.Empty);
            Assert.That(_agent.MoveSpeedMultiplier, Is.EqualTo(1f));

            _damageable.TakeDamage(50);

            Assert.That(_agent.Brain.Spec, Is.SameAs(_enraged));
            Assert.That(_agent.CreateSpec().ReactionSeconds, Is.EqualTo(_enraged.ReactionSeconds), "the Inspector values follow the swap");
            Assert.That(_agent.Brain.Behaviors, Is.EqualTo(new IEnemyBehavior[] { _behavior }));
            Assert.That(_agent.MoveSpeedMultiplier, Is.EqualTo(2f));

            _damageable.ResetHealth();

            Assert.That(_agent.Brain.Spec, Is.SameAs(original));
            Assert.That(_agent.Brain.Behaviors, Is.Empty);
            Assert.That(_agent.MoveSpeedMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void ABossReadyBeforeItsAgentAppliesThePhaseOnceTheAgentIsUp()
        {
            Assert.That(_boss.TryInitialize(), Is.True);
            _boss.UsePhases(Phases());
            _boss.Advance();
            Assert.That(_agent.Brain, Is.Null);

            Assert.That(_agent.TryInitialize(), Is.True);
            _boss.Tick(0f);

            Assert.That(_agent.Brain.Spec, Is.SameAs(_enraged));
            Assert.That(_agent.Brain.Behaviors, Is.EqualTo(new IEnemyBehavior[] { _behavior }));
            Assert.That(_agent.MoveSpeedMultiplier, Is.EqualTo(2f));
        }

        [Test]
        public void MoveSpeedMultiplierRefusesNonPositiveValues()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => _agent.MoveSpeedMultiplier = 0f);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => _agent.MoveSpeedMultiplier = float.NaN);
        }

        private BossPhases Phases()
        {
            return new BossPhases(new[]
            {
                new BossPhase("Calm", null),
                new BossPhase("Enraged", BossPhaseTrigger.HealthAtOrBelow(0.5f), _enraged, _behavior, 2f),
            });
        }

        private sealed class PassBehavior : IEnemyBehavior
        {
            public bool TryOverride(EnemyBrain brain, float deltaSeconds, EnemyPerception perception, EnemyDecision planned, out EnemyDecision decision)
            {
                decision = planned;
                return false;
            }
        }
    }
}
