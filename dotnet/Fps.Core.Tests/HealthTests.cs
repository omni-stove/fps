using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class HealthTests
    {
        private static readonly string[] _changedThenDied = { "changed:0", "died" };

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
        public void ChangedFiresOnlyWhenCurrentChanges()
        {
            var health = new Health(10);
            var changed = 0;
            health.Changed += () => changed++;

            health.Heal(5);
            health.TakeDamage(0);
            Assert.That(changed, Is.Zero);

            health.TakeDamage(3);
            health.Heal(1);
            Assert.That(changed, Is.EqualTo(2));

            health.TakeDamage(100);
            health.TakeDamage(1);
            Assert.That(changed, Is.EqualTo(3));
        }

        [Test]
        public void ChangedFiresBeforeDied()
        {
            var health = new Health(10);
            var order = new List<string>();
            health.Changed += () => order.Add("changed:" + health.Current);
            health.Died += () => order.Add("died");

            health.TakeDamage(10);

            Assert.That(order, Is.EqualTo(_changedThenDied));
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
