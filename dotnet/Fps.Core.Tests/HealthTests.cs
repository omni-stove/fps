using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class HealthTests
    {
        [Test]
        public void StartsFull()
        {
            var health = new Health(100);

            Assert.That(health.Current, Is.EqualTo(100));
            Assert.That(health.IsDead, Is.False);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void RejectsNonPositiveMax(int max)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Health(max));
        }

        [Test]
        public void DamageDoesNotGoBelowZero()
        {
            var health = new Health(10);

            health.TakeDamage(25);

            Assert.That(health.Current, Is.Zero);
            Assert.That(health.IsDead, Is.True);
        }

        [Test]
        public void DiedFiresOnceAtZero()
        {
            var health = new Health(10);
            var died = 0;
            health.Died += () => died++;

            health.TakeDamage(10);
            health.TakeDamage(10);

            Assert.That(died, Is.EqualTo(1));
        }

        [Test]
        public void HealDoesNotExceedMax()
        {
            var health = new Health(10);
            health.TakeDamage(3);

            health.Heal(100);

            Assert.That(health.Current, Is.EqualTo(10));
        }

        [Test]
        public void DeadDoesNotHeal()
        {
            var health = new Health(10);
            health.TakeDamage(10);

            health.Heal(5);

            Assert.That(health.IsDead, Is.True);
        }

        [Test]
        public void RejectsNegativeAmounts()
        {
            var health = new Health(10);

            Assert.Throws<ArgumentOutOfRangeException>(() => health.TakeDamage(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => health.Heal(-1));
        }
    }
}
