using System;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    public sealed class HealthRegenTests
    {
        private GameObject _player;
        private Damageable _damageable;
        private HealthRegen _regen;

        private static GameRules Rules(float delaySeconds, float perSecond)
        {
            return new GameRules(
                FriendlyFire.Off,
                RespawnMode.Individual,
                GameRules.DefaultWaveIntervalSeconds,
                delaySeconds,
                perSecond,
                DeathBody.Vanish,
                DeathCamera.None,
                false,
                1f,
                Destruction.Off);
        }

        [SetUp]
        public void SetUp()
        {
            _player = new GameObject("Player");
            _damageable = _player.AddComponent<Damageable>();
            _damageable.Initialize("Player", 100);
            _regen = _player.AddComponent<HealthRegen>();
        }

        [TearDown]
        public void TearDown()
        {
            // Edit Mode では OnDestroy が走らないので、購読はここで外す。
            _regen.Unbind();
            Object.DestroyImmediate(_player);
        }

        [Test]
        public void DoesNothingUntilBound()
        {
            _damageable.TakeDamage(30);

            Assert.That(_regen.IsBound, Is.False);
            Assert.That(_regen.Tick(100f), Is.Zero);
            Assert.That(_damageable.Health.Current, Is.EqualTo(70));
        }

        [Test]
        public void RegeneratesAfterTheDelaySinceTheLastHit()
        {
            _regen.Bind(_damageable, Rules(3f, 20f));

            _damageable.TakeDamage(50);
            Assert.That(_regen.Tick(2f), Is.Zero);

            _damageable.TakeDamage(10);
            Assert.That(_regen.Tick(2.5f), Is.Zero, "a new hit restarts the delay");
            Assert.That(_regen.Tick(1f), Is.EqualTo(10), "0.5s into the regen at 20/s");
            Assert.That(_damageable.Health.Current, Is.EqualTo(50));

            Assert.That(_regen.Tick(10f), Is.EqualTo(50), "heals up to the max");
            Assert.That(_damageable.Health.Current, Is.EqualTo(100));
        }

        [Test]
        public void ZeroRegenNeverHeals()
        {
            _regen.Bind(_damageable, GameRules.Default);

            _damageable.TakeDamage(40);

            Assert.That(_regen.Tick(1000f), Is.Zero);
            Assert.That(_damageable.Health.Current, Is.EqualTo(60));
        }

        [Test]
        public void ReattachesWhenTheHealthIsRebuilt()
        {
            _regen.Bind(_damageable, Rules(1f, 10f));
            var before = _regen.Regenerator;

            _damageable.RestoreHealth(40);

            Assert.That(_regen.Regenerator, Is.Not.SameAs(before));
            Assert.That(_regen.Regenerator.Health, Is.SameAs(_damageable.Health), "follows the new Health");
            Assert.That(_regen.Tick(1f), Is.EqualTo(10), "a rebuilt health is not a hit, so there is no delay");
            Assert.That(_damageable.Health.Current, Is.EqualTo(50));

            _damageable.TakeDamage(20);
            Assert.That(_regen.Tick(1f), Is.Zero, "hits on the new Health restart the delay");
            Assert.That(_regen.Tick(1f), Is.EqualTo(10));
        }

        [Test]
        public void DoesNotHealTheDead()
        {
            _regen.Bind(_damageable, Rules(0f, 50f));

            LogAssert.Expect(LogType.Log, "[Combat] target died id=Player");
            _damageable.Kill();

            Assert.That(_regen.Tick(10f), Is.Zero);
            Assert.That(_damageable.IsDead, Is.True);

            _damageable.ResetHealth();
            Assert.That(_damageable.Health.Current, Is.EqualTo(100), "a respawn starts full");
        }

        [Test]
        public void RebindingReplacesTheRulesAndTheTarget()
        {
            var other = new GameObject("Other").AddComponent<Damageable>();
            try
            {
                other.Initialize("Other", 100);
                _regen.Bind(_damageable, Rules(0f, 10f));
                _regen.Bind(other, Rules(0f, 30f));

                _damageable.TakeDamage(50);
                other.TakeDamage(50);
                _regen.Tick(1f);

                Assert.That(_regen.Target, Is.SameAs(other));
                Assert.That(other.Health.Current, Is.EqualTo(80));
                Assert.That(_damageable.Health.Current, Is.EqualTo(50), "the previous target is no longer healed");

                _damageable.RestoreHealth(10);
                Assert.That(_regen.Regenerator.Health, Is.SameAs(other.Health), "the previous target's resets are not followed");
            }
            finally
            {
                _regen.Unbind();
                Object.DestroyImmediate(other.gameObject);
            }
        }

        [Test]
        public void RejectsInvalidArguments()
        {
            Assert.Throws<ArgumentNullException>(() => _regen.Bind(null, GameRules.Default));
            Assert.Throws<ArgumentNullException>(() => _regen.Bind(_damageable, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => _regen.Tick(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _regen.Tick(float.NaN));
            Assert.That(_regen.IsBound, Is.False);
        }
    }
}
