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
    // Update は Edit Mode では走らないので、Bind と Tick を直接呼んで進める。
    public sealed class StaggerReactionTests
    {
        private GameObject _host;
        private EnemyAgent _agent;
        private Damageable _damageable;
        private StaggerReaction _reaction;
        private WeaponDefinition _rifle;
        private Stagger _savedRule;

        [SetUp]
        public void SetUp()
        {
            _savedRule = StaggerReaction.ActiveRule;
            StaggerReaction.ActiveRule = Stagger.On;

            _rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            _rifle.name = "Rifle";
            WeaponAssets.Configure(_rifle, TestWeapons.RifleSpec());

            _host = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _host.name = "Enemy";
            _agent = _host.AddComponent<EnemyAgent>();
            _agent.WeaponDefinition = _rifle;
            _agent.Id = "enemy-1";
            _damageable = _host.GetComponent<Damageable>();
            _damageable.Initialize("enemy-1", 1000);
            Assert.That(_agent.TryInitialize(), Is.True);

            _reaction = _host.AddComponent<StaggerReaction>();
            Assert.That(_reaction.Bind(), Is.True);
        }

        [TearDown]
        public void TearDown()
        {
            StaggerReaction.ActiveRule = _savedRule;
            Object.DestroyImmediate(_host);
            Object.DestroyImmediate(_rifle);
        }

        [Test]
        public void ALegShotKneelsAndStopsTheEnemy()
        {
            LogAssert.Expect(LogType.Log, "[AI] id=enemy-1 interrupted reason=stagger-Kneel seconds=1.50");
            LogAssert.Expect(LogType.Log, "[Stagger] id=enemy-1 staggered kind=Kneel seconds=1.50 zone=Limb damage=Bullet");

            _damageable.ApplyHit(25, HitZone.Limb, 5f, DamageFalloff.None);

            Assert.That(_reaction.Current, Is.EqualTo(StaggerKind.Kneel));
            Assert.That(_agent.IsInterrupted, Is.True);
            Assert.That(_agent.InterruptReason, Is.EqualTo("stagger-Kneel"));
        }

        [Test]
        public void HeadShotsFlinchAndBodyShotsDoNot()
        {
            _damageable.ApplyHit(25, HitZone.Body, 5f, DamageFalloff.None);
            Assert.That(_reaction.IsStaggered, Is.False);
            Assert.That(_agent.IsInterrupted, Is.False);

            LogAssert.Expect(LogType.Log, new Regex(@"^\[AI\] id=enemy-1 interrupted reason=stagger-Flinch "));
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Stagger\] id=enemy-1 staggered kind=Flinch .* zone=Head damage=Bullet$"));
            _damageable.ApplyHit(25, HitZone.Head, 5f, DamageFalloff.None);

            Assert.That(_reaction.Current, Is.EqualTo(StaggerKind.Flinch));
        }

        [Test]
        public void MeleeStumbles()
        {
            LogAssert.Expect(LogType.Log, new Regex(@"^\[AI\] id=enemy-1 interrupted reason=stagger-Stumble "));
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Stagger\] id=enemy-1 staggered kind=Stumble .* zone=none damage=Melee$"));

            _damageable.TakeDamage(30, Vector3.zero, Vector3.forward, DamageKind.Melee);

            Assert.That(_reaction.Current, Is.EqualTo(StaggerKind.Stumble));
        }

        [Test]
        public void ShootingAgainRightAwayIsResistedAndRecoveryReportsTheShots()
        {
            LogAssert.Expect(LogType.Log, new Regex(@"^\[AI\] id=enemy-1 interrupted reason=stagger-Kneel "));
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Stagger\] id=enemy-1 staggered kind=Kneel "));
            _damageable.ApplyHit(25, HitZone.Limb, 5f, DamageFalloff.None);

            LogAssert.Expect(LogType.Log, "[Stagger] id=enemy-1 resisted kind=Kneel reason=recovering");
            _damageable.ApplyHit(25, HitZone.Limb, 5f, DamageFalloff.None);
            Assert.That(_reaction.Current, Is.EqualTo(StaggerKind.Kneel));

            _reaction.Tick(1f);
            Assert.That(_reaction.IsStaggered, Is.True);

            LogAssert.Expect(LogType.Log, "[Stagger] id=enemy-1 recovered kind=Kneel shots=0");
            _reaction.Tick(0.5f);
            Assert.That(_reaction.IsStaggered, Is.False);

            LogAssert.Expect(LogType.Log, "[Stagger] id=enemy-1 resisted kind=Kneel reason=recovering");
            _damageable.ApplyHit(25, HitZone.Limb, 5f, DamageFalloff.None);
            Assert.That(_reaction.IsStaggered, Is.False, "the immunity lasts past the stagger");
        }

        [Test]
        public void NothingHappensWhileTheRuleIsOff()
        {
            StaggerReaction.ActiveRule = Stagger.Off;

            _damageable.ApplyHit(25, HitZone.Limb, 5f, DamageFalloff.None);
            _damageable.TakeDamage(30, Vector3.zero, Vector3.forward, DamageKind.Melee);

            Assert.That(_reaction.IsStaggered, Is.False);
            Assert.That(_agent.IsInterrupted, Is.False);
        }

        [Test]
        public void ResistantEnemiesNeedMoreDamageAndStaggerShorter()
        {
            Object.DestroyImmediate(_host);
            _host = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _host.name = "Tough";
            _agent = _host.AddComponent<EnemyAgent>();
            _agent.WeaponDefinition = _rifle;
            _agent.Id = "tough";
            _damageable = _host.GetComponent<Damageable>();
            _damageable.Initialize("tough", 1000);
            _agent.ApplySpec(new EnemySpec(20f, 10f, 1f, 2f, 0.5f, staggerResistance: 2f));
            Assert.That(_agent.TryInitialize(), Is.True);
            Assert.That(_agent.StaggerResistance, Is.EqualTo(2f));
            _reaction = _host.AddComponent<StaggerReaction>();
            _reaction.Bind();

            _damageable.ApplyHit(10, HitZone.Limb, 5f, DamageFalloff.None);
            Assert.That(_reaction.IsStaggered, Is.False, "8 x 3 = 24 is needed; 8 is not enough");

            LogAssert.Expect(LogType.Log, "[AI] id=tough interrupted reason=stagger-Kneel seconds=0.50");
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Stagger\] id=tough staggered kind=Kneel seconds=0\.50 "));
            _damageable.ApplyHit(25, HitZone.Limb, 5f, DamageFalloff.None);
            Assert.That(_reaction.Current, Is.EqualTo(StaggerKind.Kneel));
        }

        [Test]
        public void DyingClearsTheStagger()
        {
            LogAssert.Expect(LogType.Log, new Regex(@"^\[AI\] id=enemy-1 interrupted "));
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Stagger\] id=enemy-1 staggered "));
            _damageable.ApplyHit(25, HitZone.Limb, 5f, DamageFalloff.None);

            LogAssert.Expect(LogType.Log, "[Combat] target died id=enemy-1");
            LogAssert.Expect(LogType.Log, new Regex(@"^\[AI\] id=enemy-1 state=Dead "));
            _damageable.Kill();

            Assert.That(_reaction.IsStaggered, Is.False);
        }

        [Test]
        public void TheSpecRoundTripsThroughTheSerializedFields()
        {
            var fresh = new GameObject("Fresh").AddComponent<StaggerReaction>();
            try
            {
                var spec = new StaggerSpec(1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f, 9f, 10f);
                fresh.ApplySpec(spec);
                var copy = fresh.CreateSpec();

                Assert.That(copy.KneelSeconds, Is.EqualTo(2f));
                Assert.That(copy.LimbDamage, Is.EqualTo(6f));
                Assert.That(copy.ImmunitySeconds, Is.EqualTo(10f));
                Assert.That(StaggerReaction.FormatStaggerLog("a", StaggerKind.Blast, 0.6f, null, DamageKind.Explosion),
                    Is.EqualTo("[Stagger] id=a staggered kind=Blast seconds=0.60 zone=none damage=Explosion"));
            }
            finally
            {
                Object.DestroyImmediate(fresh.gameObject);
            }

            Assert.Throws<System.InvalidOperationException>(() => _reaction.ApplySpec(StaggerSpec.EnemyDefault), "too late once bound");
        }
    }
}
