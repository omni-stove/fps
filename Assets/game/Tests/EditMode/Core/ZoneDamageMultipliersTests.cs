using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ZoneDamageMultipliersTests
    {
        [Test]
        public void DefaultMatchesTheCalculatorAndGivesTheWeakPointItsOwnMultiplier()
        {
            var defaults = ZoneDamageMultipliers.Default;

            Assert.That(defaults.For(HitZone.Head), Is.EqualTo(DamageCalculator.HeadMultiplier));
            Assert.That(defaults.For(HitZone.Body), Is.EqualTo(DamageCalculator.BodyMultiplier));
            Assert.That(defaults.For(HitZone.Limb), Is.EqualTo(DamageCalculator.LimbMultiplier));
            Assert.That(defaults.For(HitZone.WeakPoint), Is.EqualTo(DamageCalculator.WeakPointMultiplier));
            Assert.That(DamageCalculator.ZoneMultiplier(HitZone.WeakPoint), Is.EqualTo(DamageCalculator.WeakPointMultiplier));
        }

        [Test]
        public void TheOldCalculateKeepsTheDefaultMultipliers()
        {
            Assert.That(DamageCalculator.Calculate(30, HitZone.Head, 0f, DamageFalloff.None), Is.EqualTo(60));
            Assert.That(DamageCalculator.Calculate(30, HitZone.WeakPoint, 0f, DamageFalloff.None), Is.EqualTo(60));
        }

        [Test]
        public void ABossCanShrinkEveryZoneButTheWeakPoint()
        {
            var boss = new ZoneDamageMultipliers(head: 0.25, body: 0.2, limb: 0.1, weakPoint: 3.0);

            Assert.That(DamageCalculator.Calculate(30, HitZone.Body, 0f, DamageFalloff.None, boss), Is.EqualTo(6));
            Assert.That(DamageCalculator.Calculate(30, HitZone.Head, 0f, DamageFalloff.None, boss), Is.EqualTo(8));
            Assert.That(DamageCalculator.Calculate(30, HitZone.Limb, 0f, DamageFalloff.None, boss), Is.EqualTo(3));
            Assert.That(DamageCalculator.Calculate(30, HitZone.WeakPoint, 0f, DamageFalloff.None, boss), Is.EqualTo(90));
        }

        [Test]
        public void ASmallButNonZeroMultiplierStillDealsOne()
        {
            var tough = new ZoneDamageMultipliers(0.01, 0.01, 0.01, 1.0);

            Assert.That(DamageCalculator.Calculate(10, HitZone.Body, 0f, DamageFalloff.None, tough), Is.EqualTo(1));
        }

        [Test]
        public void AZeroMultiplierMakesTheZoneImmune()
        {
            var armored = new ZoneDamageMultipliers(0.0, 0.0, 0.0, 2.0);

            Assert.That(DamageCalculator.Calculate(50, HitZone.Body, 0f, DamageFalloff.None, armored), Is.Zero);
            Assert.That(DamageCalculator.Calculate(50, HitZone.WeakPoint, 0f, DamageFalloff.None, armored), Is.EqualTo(100));
        }

        [Test]
        public void CalculateRefusesNullMultipliers()
        {
            Assert.Throws<ArgumentNullException>(() => DamageCalculator.Calculate(10, HitZone.Body, 0f, DamageFalloff.None, null));
        }

        [TestCase(-0.1)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void RefusesNegativeOrNonFiniteMultipliers(double value)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ZoneDamageMultipliers(value, 1, 1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ZoneDamageMultipliers(1, value, 1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ZoneDamageMultipliers(1, 1, value, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ZoneDamageMultipliers(1, 1, 1, value));
        }

        [Test]
        public void RefusesAnUnknownZone()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ZoneDamageMultipliers.Default.For((HitZone)99));
        }
    }
}
