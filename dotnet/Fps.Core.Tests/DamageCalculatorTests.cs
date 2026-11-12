using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class DamageCalculatorTests
    {
        // 10 以下は 1.0、30 以上は 0.5、20 で 0.75
        private static readonly DamageFalloff _falloff = new DamageFalloff(10f, 30f, 0.5f);

        [TestCase(HitZone.Head, 200)]
        [TestCase(HitZone.Body, 100)]
        [TestCase(HitZone.Limb, 75)]
        public void AppliesZoneMultiplier(HitZone zone, int expected)
        {
            Assert.That(DamageCalculator.Calculate(100, zone, 0f, _falloff), Is.EqualTo(expected));
        }

        [TestCase(0f, 100)]
        [TestCase(10f, 100)]
        [TestCase(20f, 75)]
        [TestCase(25f, 63)]
        [TestCase(30f, 50)]
        [TestCase(1000f, 50)]
        public void AppliesDistanceFalloff(float distance, int expected)
        {
            Assert.That(DamageCalculator.Calculate(100, HitZone.Body, distance, _falloff), Is.EqualTo(expected));
        }

        [Test]
        public void InfiniteDistanceUsesMinMultiplier()
        {
            Assert.That(DamageCalculator.Calculate(100, HitZone.Body, float.PositiveInfinity, _falloff), Is.EqualTo(50));
        }

        [Test]
        public void CombinesZoneAndFalloff()
        {
            // 40 × 2.0 × 0.75 = 60
            Assert.That(DamageCalculator.Calculate(40, HitZone.Head, 20f, _falloff), Is.EqualTo(60));
        }

        [Test]
        public void SameStartAndEndSwitchesAtThatDistance()
        {
            var step = new DamageFalloff(10f, 10f, 0.5f);

            Assert.That(DamageCalculator.Calculate(100, HitZone.Body, 10f, step), Is.EqualTo(100));
            Assert.That(DamageCalculator.Calculate(100, HitZone.Body, 10.5f, step), Is.EqualTo(50));
        }

        [Test]
        public void NoneDoesNotFalloff()
        {
            Assert.That(DamageCalculator.Calculate(100, HitZone.Body, 100000f, DamageFalloff.None), Is.EqualTo(100));
        }

        [TestCase(14, HitZone.Limb, 0f, 11)] // 10.5 → 11（偶数丸めなら 10）
        [TestCase(10, HitZone.Body, 20f, 8)] // 7.5 → 8
        [TestCase(5, HitZone.Body, 20f, 4)] // 3.75 → 4
        [TestCase(9, HitZone.Limb, 0f, 7)] // 6.75 → 7
        [TestCase(7, HitZone.Limb, 0f, 5)] // 5.25 → 5
        public void RoundsHalfAwayFromZero(int baseDamage, HitZone zone, float distance, int expected)
        {
            Assert.That(DamageCalculator.Calculate(baseDamage, zone, distance, _falloff), Is.EqualTo(expected));
        }

        [Test]
        public void NonZeroBaseDamageIsAtLeastOne()
        {
            var harsh = new DamageFalloff(0f, 1f, 0.25f);

            // 1 × 0.75 × 0.25 = 0.1875
            Assert.That(DamageCalculator.Calculate(1, HitZone.Limb, 5f, harsh), Is.EqualTo(1));
        }

        [Test]
        public void ZeroMinMultiplierStillDealsOne()
        {
            var full = new DamageFalloff(0f, 1f, 0f);

            Assert.That(DamageCalculator.Calculate(50, HitZone.Body, 5f, full), Is.EqualTo(1));
        }

        [TestCase(HitZone.Head)]
        [TestCase(HitZone.Body)]
        [TestCase(HitZone.Limb)]
        public void ZeroBaseDamageIsZero(HitZone zone)
        {
            Assert.That(DamageCalculator.Calculate(0, zone, 50f, _falloff), Is.Zero);
        }

        [Test]
        public void HugeDamageIsClampedToIntMax()
        {
            Assert.That(DamageCalculator.Calculate(int.MaxValue, HitZone.Head, 0f, _falloff), Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void ResultCanBePassedToHealth()
        {
            var health = new Health(100);

            health.TakeDamage(DamageCalculator.Calculate(30, HitZone.Head, 0f, _falloff));

            Assert.That(health.Current, Is.EqualTo(40));
        }

        [Test]
        public void RejectsNegativeBaseDamage()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => DamageCalculator.Calculate(-1, HitZone.Body, 0f, _falloff));
        }

        [TestCase(-0.01f)]
        [TestCase(float.NegativeInfinity)]
        [TestCase(float.NaN)]
        public void RejectsInvalidDistance(float distance)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => DamageCalculator.Calculate(10, HitZone.Body, distance, _falloff));
        }

        [Test]
        public void RejectsNegativeDistanceEvenWithZeroBaseDamage()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => DamageCalculator.Calculate(0, HitZone.Body, -1f, _falloff));
        }

        [Test]
        public void RejectsUnknownZone()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => DamageCalculator.Calculate(10, (HitZone)99, 0f, _falloff));
        }

        [Test]
        public void RejectsNullFalloff()
        {
            Assert.Throws<ArgumentNullException>(() => DamageCalculator.Calculate(10, HitZone.Body, 0f, null!));
        }

        [Test]
        public void ZoneMultipliers()
        {
            Assert.That(DamageCalculator.ZoneMultiplier(HitZone.Head), Is.EqualTo(2.0));
            Assert.That(DamageCalculator.ZoneMultiplier(HitZone.Body), Is.EqualTo(1.0));
            Assert.That(DamageCalculator.ZoneMultiplier(HitZone.Limb), Is.EqualTo(0.75));
        }
    }

    public sealed class DamageFalloffTests
    {
        [Test]
        public void KeepsValues()
        {
            var falloff = new DamageFalloff(5f, 20f, 0.4f);

            Assert.That(falloff.StartDistance, Is.EqualTo(5f));
            Assert.That(falloff.EndDistance, Is.EqualTo(20f));
            Assert.That(falloff.MinMultiplier, Is.EqualTo(0.4f));
        }

        [Test]
        public void InterpolatesLinearly()
        {
            var falloff = new DamageFalloff(10f, 30f, 0.5f);

            Assert.That(falloff.MultiplierAt(10f), Is.EqualTo(1.0));
            Assert.That(falloff.MultiplierAt(15f), Is.EqualTo(0.875));
            Assert.That(falloff.MultiplierAt(20f), Is.EqualTo(0.75));
            Assert.That(falloff.MultiplierAt(30f), Is.EqualTo(0.5));
        }

        [Test]
        public void RejectsEndLessThanStart()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DamageFalloff(20f, 10f, 0.5f));
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void RejectsInvalidStart(float start)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DamageFalloff(start, 100f, 0.5f));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void RejectsInvalidEnd(float end)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DamageFalloff(0f, end, 0.5f));
        }

        [TestCase(-0.1f)]
        [TestCase(1.1f)]
        [TestCase(float.NaN)]
        public void RejectsInvalidMinMultiplier(float min)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DamageFalloff(0f, 10f, min));
        }

        [Test]
        public void RejectsNegativeDistance()
        {
            var falloff = new DamageFalloff(0f, 10f, 0.5f);

            Assert.Throws<ArgumentOutOfRangeException>(() => falloff.MultiplierAt(-1f));
        }
    }
}
