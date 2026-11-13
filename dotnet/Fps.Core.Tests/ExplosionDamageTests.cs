using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class ExplosionDamageTests
    {
        private static ExplosionDamage Create(int maxDamage = 100, float radius = 6f, float fullDamageRadius = 2f)
        {
            return new ExplosionDamage(maxDamage, radius, fullDamageRadius);
        }

        [TestCase(0f)]
        [TestCase(1f)]
        [TestCase(2f)]
        public void GivesTheMaxDamageInsideTheFullDamageRadius(float distance)
        {
            Assert.That(Create().At(distance, obstructed: false), Is.EqualTo(100));
        }

        [TestCase(3f, 75)]
        [TestCase(4f, 50)]
        [TestCase(5f, 25)]
        public void FallsOffLinearlyToTheRadius(float distance, int expected)
        {
            Assert.That(Create().At(distance, obstructed: false), Is.EqualTo(expected));
        }

        [TestCase(6f)]
        [TestCase(10f)]
        [TestCase(float.PositiveInfinity)]
        public void GivesNothingAtOrBeyondTheRadius(float distance)
        {
            var explosion = Create();

            Assert.That(explosion.Reaches(distance), Is.False);
            Assert.That(explosion.At(distance, obstructed: false), Is.Zero);
        }

        [Test]
        public void ReachesJustInsideTheRadius()
        {
            Assert.That(Create().Reaches(5.999f), Is.True);
        }

        [TestCase(0f)]
        [TestCase(4f)]
        public void GivesNothingWhenObstructed(float distance)
        {
            Assert.That(Create().At(distance, obstructed: true), Is.Zero);
        }

        [Test]
        public void FallsOffFromTheCenterWithoutAFullDamageRadius()
        {
            var explosion = Create(maxDamage: 100, radius: 4f, fullDamageRadius: 0f);

            Assert.That(explosion.At(0f, false), Is.EqualTo(100));
            Assert.That(explosion.At(1f, false), Is.EqualTo(75));
            Assert.That(explosion.At(2f, false), Is.EqualTo(50));
        }

        [Test]
        public void RoundsHalfUp()
        {
            // 3 * (1 - 0.5) = 1.5
            Assert.That(Create(maxDamage: 3, radius: 2f, fullDamageRadius: 0f).At(1f, false), Is.EqualTo(2));
        }

        [Test]
        public void NeverExceedsTheMaxDamageOrGoesBelowZero()
        {
            var explosion = Create(maxDamage: 7, radius: 3f, fullDamageRadius: 0.5f);
            for (var distance = 0f; distance < 4f; distance += 0.01f)
            {
                var damage = explosion.At(distance, false);
                Assert.That(damage, Is.InRange(0, 7), $"distance {distance}");
            }
        }

        [Test]
        public void DamageDoesNotGrowWithDistance()
        {
            var explosion = Create(maxDamage: 150, radius: 6f, fullDamageRadius: 1.5f);
            var previous = int.MaxValue;
            for (var distance = 0f; distance < 7f; distance += 0.05f)
            {
                var damage = explosion.At(distance, false);
                Assert.That(damage, Is.LessThanOrEqualTo(previous), $"distance {distance}");
                previous = damage;
            }
        }

        [Test]
        public void ZeroMaxDamageGivesNothing()
        {
            Assert.That(Create(maxDamage: 0).At(0f, false), Is.Zero);
        }

        [Test]
        public void KeepsTheValues()
        {
            var explosion = Create(maxDamage: 120, radius: 5f, fullDamageRadius: 1f);

            Assert.That(explosion.MaxDamage, Is.EqualTo(120));
            Assert.That(explosion.Radius, Is.EqualTo(5f));
            Assert.That(explosion.FullDamageRadius, Is.EqualTo(1f));
        }

        [Test]
        public void RejectsANegativeMaxDamage()
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(() => Create(maxDamage: -1));
            Assert.That(e.ParamName, Is.EqualTo("maxDamage"));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void RejectsAnInvalidRadius(float radius)
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(() => new ExplosionDamage(100, radius));
            Assert.That(e.ParamName, Is.EqualTo("radius"));
        }

        [TestCase(-0.1f)]
        [TestCase(float.NaN)]
        [TestCase(6f)]
        [TestCase(7f)]
        public void RejectsAFullDamageRadiusOutsideTheRadius(float fullDamageRadius)
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(() => Create(radius: 6f, fullDamageRadius: fullDamageRadius));
            Assert.That(e.ParamName, Is.EqualTo("fullDamageRadius"));
        }

        [TestCase(-0.01f)]
        [TestCase(float.NaN)]
        [TestCase(float.NegativeInfinity)]
        public void RejectsAnInvalidDistance(float distance)
        {
            var explosion = Create();

            Assert.Throws<ArgumentOutOfRangeException>(() => explosion.At(distance, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => explosion.At(distance, true));
            Assert.Throws<ArgumentOutOfRangeException>(() => explosion.Reaches(distance));
        }
    }
}
