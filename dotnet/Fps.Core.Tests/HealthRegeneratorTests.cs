using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class HealthRegeneratorTests
    {
        private static Health Damaged(int max, int damage)
        {
            var health = new Health(max);
            health.TakeDamage(damage);
            return health;
        }

        private static GameRules Rules(float regenDelaySeconds, float regenPerSecond)
        {
            return new GameRules(
                FriendlyFire.Off,
                RespawnMode.Individual,
                GameRules.DefaultWaveIntervalSeconds,
                regenDelaySeconds,
                regenPerSecond,
                DeathBody.Vanish,
                DeathCamera.None,
                false,
                1f,
                Destruction.Off);
        }

        [Test]
        public void TakesDelayAndRateFromRules()
        {
            var regenerator = new HealthRegenerator(new Health(100), Rules(2f, 5f));

            Assert.That(regenerator.RegenDelaySeconds, Is.EqualTo(2f));
            Assert.That(regenerator.RegenPerSecond, Is.EqualTo(5f));
        }

        [Test]
        public void DoesNotHealUntilDelayHasPassed()
        {
            var health = Damaged(100, 50);
            var regenerator = new HealthRegenerator(health, 2f, 10f);
            regenerator.NotifyDamaged();

            Assert.That(regenerator.Tick(1.5f), Is.Zero);
            Assert.That(regenerator.Tick(0.5f), Is.Zero);
            Assert.That(health.Current, Is.EqualTo(50));
            Assert.That(regenerator.DelayRemainingSeconds, Is.Zero);

            Assert.That(regenerator.Tick(0.5f), Is.EqualTo(5));
            Assert.That(health.Current, Is.EqualTo(55));
        }

        [Test]
        public void HealsOnlyForTimeAfterDelayWithinOneTick()
        {
            var health = Damaged(100, 50);
            var regenerator = new HealthRegenerator(health, 2f, 10f);
            regenerator.NotifyDamaged();

            Assert.That(regenerator.Tick(2.5f), Is.EqualTo(5));
            Assert.That(health.Current, Is.EqualTo(55));
        }

        [Test]
        public void HealsWithoutWaitingBeforeAnyDamageIsNotified()
        {
            var health = Damaged(100, 50);
            var regenerator = new HealthRegenerator(health, 2f, 10f);

            Assert.That(regenerator.Tick(1f), Is.EqualTo(10));
        }

        [Test]
        public void HealsImmediatelyWithZeroDelay()
        {
            var health = Damaged(100, 50);
            var regenerator = new HealthRegenerator(health, 0f, 10f);
            regenerator.NotifyDamaged();

            Assert.That(regenerator.Tick(0.5f), Is.EqualTo(5));
        }

        [Test]
        public void CarriesFractionsOverToLaterTicks()
        {
            var health = Damaged(100, 50);
            var regenerator = new HealthRegenerator(health, 0f, 3f);

            // 0.25 秒で 0.75 ずつたまる: 0.75 → 1.5 (1 回復, 残り 0.5) → 1.25 (1 回復, 残り 0.25) → 1.0 (1 回復)
            Assert.That(regenerator.Tick(0.25f), Is.Zero);
            Assert.That(regenerator.Tick(0.25f), Is.EqualTo(1));
            Assert.That(regenerator.Tick(0.25f), Is.EqualTo(1));
            Assert.That(regenerator.Tick(0.25f), Is.EqualTo(1));
            Assert.That(health.Current, Is.EqualTo(53));
        }

        [Test]
        public void DamageResetsDelayAndDropsFraction()
        {
            var health = Damaged(100, 50);
            var regenerator = new HealthRegenerator(health, 1f, 2f);
            regenerator.NotifyDamaged();
            regenerator.Tick(1.25f);

            health.TakeDamage(10);
            regenerator.NotifyDamaged();

            Assert.That(regenerator.DelayRemainingSeconds, Is.EqualTo(1f));
            Assert.That(regenerator.Tick(1f), Is.Zero);

            // 被弾前にためた 0.5 は捨てているので、ここでは 0.5 だけで整数にならない
            Assert.That(regenerator.Tick(0.25f), Is.Zero);
            Assert.That(regenerator.Tick(0.25f), Is.EqualTo(1));
            Assert.That(health.Current, Is.EqualTo(41));
        }

        [Test]
        public void StopsAtMax()
        {
            var health = Damaged(100, 3);
            var regenerator = new HealthRegenerator(health, 0f, 10f);

            Assert.That(regenerator.Tick(1f), Is.EqualTo(3));
            Assert.That(health.Current, Is.EqualTo(100));
            Assert.That(regenerator.Tick(1f), Is.Zero);
            Assert.That(health.Current, Is.EqualTo(100));
        }

        [Test]
        public void DoesNotCarryFractionPastFullHealth()
        {
            var health = Damaged(100, 1);
            var regenerator = new HealthRegenerator(health, 0f, 3f);
            regenerator.Tick(0.5f);
            Assert.That(health.Current, Is.EqualTo(100));

            health.TakeDamage(1);

            // 満タンになったときの余り 0.5 を持ち越していれば、ここで 1 回復してしまう
            Assert.That(regenerator.Tick(0.25f), Is.Zero);
            Assert.That(health.Current, Is.EqualTo(99));
        }

        [Test]
        public void HandlesHugeTickWithoutOverflow()
        {
            var health = Damaged(100, 99);
            var regenerator = new HealthRegenerator(health, 0f, float.MaxValue);

            Assert.That(regenerator.Tick(float.MaxValue), Is.EqualTo(99));
            Assert.That(health.Current, Is.EqualTo(100));
        }

        [Test]
        public void DoesNothingWhileDead()
        {
            var health = new Health(100);
            health.TakeDamage(100);
            var regenerator = new HealthRegenerator(health, 0f, 10f);

            Assert.That(regenerator.Tick(10f), Is.Zero);
            Assert.That(health.IsDead, Is.True);
            Assert.That(health.Current, Is.Zero);
        }

        [Test]
        public void DoesNothingWithZeroRate()
        {
            var health = Damaged(100, 50);
            var regenerator = new HealthRegenerator(health, GameRules.Default);
            var changes = 0;
            health.Changed += () => changes++;

            Assert.That(regenerator.Tick(1000f), Is.Zero);
            Assert.That(health.Current, Is.EqualTo(50));
            Assert.That(changes, Is.Zero);
        }

        [Test]
        public void RaisesHealthChangedOnlyWhenWholePointsAreHealed()
        {
            var health = Damaged(100, 50);
            var regenerator = new HealthRegenerator(health, 0f, 2f);
            var changes = 0;
            health.Changed += () => changes++;

            regenerator.Tick(0.25f);
            Assert.That(changes, Is.Zero);

            regenerator.Tick(0.25f);
            Assert.That(changes, Is.EqualTo(1));
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        public void RejectsInvalidDelta(float deltaSeconds)
        {
            var regenerator = new HealthRegenerator(Damaged(100, 50), 0f, 1f);

            Assert.Throws<ArgumentOutOfRangeException>(() => regenerator.Tick(deltaSeconds));
        }

        [Test]
        public void RejectsInvalidConstruction()
        {
            var health = new Health(100);

            Assert.Throws<ArgumentNullException>(() => new HealthRegenerator(null, 0f, 1f));
            Assert.Throws<ArgumentNullException>(() => new HealthRegenerator(null, GameRules.Default));
            Assert.Throws<ArgumentNullException>(() => new HealthRegenerator(health, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HealthRegenerator(health, -1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HealthRegenerator(health, float.NaN, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HealthRegenerator(health, float.PositiveInfinity, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HealthRegenerator(health, 0f, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HealthRegenerator(health, 0f, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HealthRegenerator(health, 0f, float.PositiveInfinity));
        }
    }
}
