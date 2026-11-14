using System;
using System.Linq;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    public sealed class PlayerStaggerTests
    {
        private GameObject _player;
        private Damageable _damageable;
        private GameObject _enemyHost;
        private Stagger _savedRule;

        [SetUp]
        public void SetUp()
        {
            _savedRule = StaggerReaction.ActiveRule;
            _player = new GameObject("Player");
            _player.AddComponent<PlayerController>();
            _damageable = _player.AddComponent<Damageable>();
            _damageable.Initialize("player", 100);
        }

        [TearDown]
        public void TearDown()
        {
            StaggerReaction.ActiveRule = _savedRule;
            Object.DestroyImmediate(_player);
            if (_enemyHost != null)
            {
                Object.DestroyImmediate(_enemyHost);
            }
        }

        [Test]
        public void TheInstallerIsRegistered()
        {
            Assert.That(PlayerActions.Installers.Count(i => i is StaggerActionInstaller), Is.EqualTo(1));
        }

        [Test]
        public void TheInstallerHandsTheRuleToThePlayerAndTheEnemies()
        {
            _enemyHost = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _enemyHost.AddComponent<EnemyAgent>();
            _enemyHost.SetActive(false);

            new StaggerActionInstaller().Install(_player, Rules(Stagger.On));

            Assert.That(StaggerReaction.ActiveRule, Is.EqualTo(Stagger.On));
            Assert.That(_player.GetComponent<PlayerStagger>().Rule, Is.EqualTo(Stagger.On));
            Assert.That(_enemyHost.GetComponent<StaggerReaction>(), Is.Not.Null, "inactive enemies get it too");

            new StaggerActionInstaller().Install(_player, Rules(Stagger.Off));

            Assert.That(StaggerReaction.ActiveRule, Is.EqualTo(Stagger.Off));
            Assert.That(_player.GetComponents<PlayerStagger>(), Has.Length.EqualTo(1));
            Assert.That(_player.GetComponent<PlayerStagger>().Rule, Is.EqualTo(Stagger.Off));
        }

        [Test]
        public void TheInstallerLeavesThingsThatDoNotMoveAlone()
        {
            var dummy = new GameObject("Dummy");
            try
            {
                dummy.AddComponent<Damageable>();

                new StaggerActionInstaller().Install(dummy, Rules(Stagger.On));

                Assert.That(dummy.GetComponent<PlayerStagger>(), Is.Null);
                Assert.That(dummy.GetComponent<PlayerController>(), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(dummy);
            }
        }

        [Test]
        public void MeleeStumblesThePlayerAndRaisesStaggered()
        {
            var stagger = Bound(Stagger.On);
            IMovementOverride asOverride = stagger;

            LogAssert.Expect(LogType.Log, "[Stagger] id=player staggered kind=Stumble seconds=0.40 zone=none damage=Melee");
            _damageable.TakeDamage(5, Vector3.zero, Vector3.forward, DamageKind.Melee);

            Assert.That(stagger.Current, Is.EqualTo(StaggerKind.Stumble));
            Assert.That(asOverride.IsActive, Is.True);
            Assert.That(asOverride.Conditions, Is.EqualTo(PlayerActionConditions.Staggered));

            LogAssert.Expect(LogType.Log, "[Stagger] id=player recovered kind=Stumble");
            stagger.Tick(StaggerSpec.DefaultPlayerStumbleSeconds);

            Assert.That(asOverride.IsActive, Is.False);
        }

        [Test]
        public void ExplosionsStaggerThePlayerButBulletsDoNot()
        {
            var stagger = Bound(Stagger.On);

            _damageable.ApplyHit(25, HitZone.Head, 5f, DamageFalloff.None);
            _damageable.ApplyHit(25, HitZone.Limb, 5f, DamageFalloff.None);
            Assert.That(stagger.IsStaggered, Is.False);

            LogAssert.Expect(LogType.Log, new Regex(@"^\[Stagger\] id=player staggered kind=Blast "));
            _damageable.TakeDamage(20, Vector3.zero, Vector3.forward, DamageKind.Explosion);

            Assert.That(stagger.Current, Is.EqualTo(StaggerKind.Blast));
        }

        [Test]
        public void NothingHappensWhileTheRuleIsOffAndTurningItOffEndsAStagger()
        {
            var stagger = Bound(Stagger.Off);

            _damageable.TakeDamage(5, Vector3.zero, Vector3.forward, DamageKind.Melee);
            Assert.That(stagger.IsStaggered, Is.False);

            stagger.Rule = Stagger.On;
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Stagger\] id=player staggered kind=Stumble "));
            _damageable.TakeDamage(5, Vector3.zero, Vector3.forward, DamageKind.Melee);

            LogAssert.Expect(LogType.Log, "[Stagger] id=player recovered kind=Stumble");
            stagger.Rule = Stagger.Off;

            Assert.That(stagger.IsStaggered, Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => stagger.Rule = (Stagger)99);
        }

        [Test]
        public void TheDefaultsMatchThePlayerSpec()
        {
            var spec = _player.AddComponent<PlayerStagger>().CreateSpec();
            var expected = StaggerSpec.PlayerDefault;

            Assert.That(spec.FlinchSeconds, Is.EqualTo(expected.FlinchSeconds));
            Assert.That(spec.KneelSeconds, Is.EqualTo(expected.KneelSeconds));
            Assert.That(spec.StumbleSeconds, Is.EqualTo(expected.StumbleSeconds));
            Assert.That(spec.BlastSeconds, Is.EqualTo(expected.BlastSeconds));
            Assert.That(spec.ImmunitySeconds, Is.EqualTo(expected.ImmunitySeconds));
            Assert.That(spec.MoveMultiplier, Is.EqualTo(expected.MoveMultiplier));
            Assert.That(spec.TurnMultiplier, Is.EqualTo(expected.TurnMultiplier));
        }

        [Test]
        public void TheTurnMultiplierRejectsNegativeOrInfiniteValues()
        {
            var look = new GameObject("Look").AddComponent<FirstPersonLook>();
            try
            {
                Assert.That(look.TurnMultiplier, Is.EqualTo(1f));
                look.TurnMultiplier = 0.25f;
                Assert.That(look.TurnMultiplier, Is.EqualTo(0.25f));
                Assert.Throws<ArgumentOutOfRangeException>(() => look.TurnMultiplier = -1f);
                Assert.Throws<ArgumentOutOfRangeException>(() => look.TurnMultiplier = float.PositiveInfinity);
                Assert.Throws<ArgumentOutOfRangeException>(() => look.TurnMultiplier = float.NaN);
            }
            finally
            {
                Object.DestroyImmediate(look.gameObject);
            }
        }

        private PlayerStagger Bound(Stagger rule)
        {
            var stagger = _player.AddComponent<PlayerStagger>();
            Assert.That(stagger.Bind(), Is.True);
            stagger.Rule = rule;
            return stagger;
        }

        private static GameRules Rules(Stagger stagger)
        {
            return GameRules.Default.WithActions(ActionRules.Default.WithStagger(stagger));
        }
    }
}
