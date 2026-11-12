using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class ShotBudgetTests
    {
        [Test]
        public void StartsFullAndAllowsABurst()
        {
            var budget = new ShotBudget(0.1f, burst: 3);

            Assert.That(budget.TryConsume(10d), Is.True);
            Assert.That(budget.TryConsume(10d), Is.True);
            Assert.That(budget.TryConsume(10d), Is.True);
            Assert.That(budget.TryConsume(10d), Is.False);
        }

        [Test]
        public void RefillsOneShotPerInterval()
        {
            var budget = new ShotBudget(0.1f, burst: 1);

            Assert.That(budget.TryConsume(0d), Is.True);
            Assert.That(budget.TryConsume(0.05d), Is.False);
            Assert.That(budget.TryConsume(0.1d), Is.True);
            Assert.That(budget.TryConsume(0.2d), Is.True);
        }

        [Test]
        public void SustainedFireAtTheIntervalIsAccepted()
        {
            var budget = new ShotBudget(0.1f, burst: 1);

            for (var i = 0; i < 100; i++)
            {
                Assert.That(budget.TryConsume(i * 0.1d), Is.True, "shot " + i);
            }
        }

        [Test]
        public void FiringFasterThanTheIntervalIsEventuallyRejected()
        {
            var budget = new ShotBudget(0.1f, burst: 3);
            var accepted = 0;
            for (var i = 0; i < 20; i++)
            {
                if (budget.TryConsume(i * 0.05d))
                {
                    accepted++;
                }
            }

            // 0.95 秒で回復するのは 9.5 発ぶんと最初の 3 発
            Assert.That(accepted, Is.LessThanOrEqualTo(13));
            Assert.That(accepted, Is.GreaterThanOrEqualTo(10));
        }

        [Test]
        public void DoesNotRefillBeyondTheBurst()
        {
            var budget = new ShotBudget(0.1f, burst: 2);

            Assert.That(budget.TryConsume(0d), Is.True);
            Assert.That(budget.TryConsume(100d), Is.True);
            Assert.That(budget.TryConsume(100d), Is.True);
            Assert.That(budget.TryConsume(100d), Is.False);
        }

        [Test]
        public void TimeGoingBackwardsDoesNotRefill()
        {
            var budget = new ShotBudget(0.1f, burst: 1);

            Assert.That(budget.TryConsume(5d), Is.True);
            Assert.That(budget.TryConsume(1d), Is.False);
            Assert.That(budget.TryConsume(5.05d), Is.False);
            Assert.That(budget.TryConsume(5.1d), Is.True);
        }

        [Test]
        public void ZeroIntervalNeverLimits()
        {
            var budget = new ShotBudget(0f, burst: 1);

            for (var i = 0; i < 10; i++)
            {
                Assert.That(budget.TryConsume(0d), Is.True);
            }
        }

        [Test]
        public void RejectsInvalidSettingsAndTime()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ShotBudget(-0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ShotBudget(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ShotBudget(float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ShotBudget(0.1f, burst: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ShotBudget(0.1f).TryConsume(double.NaN));
        }
    }
}
