using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class VehicleImpactSpecTests
    {
        [Test]
        public void TheDefaultSpecKeepsTheDefaults()
        {
            var spec = VehicleImpactSpec.Default;

            Assert.That(spec.MinSpeed, Is.EqualTo(4f));
            Assert.That(spec.DamagePerSpeed, Is.EqualTo(10f));
            Assert.That(spec.MaxDamage, Is.EqualTo(200));
            Assert.That(spec.RepeatSeconds, Is.EqualTo(1f));
        }

        [Test]
        public void DamageGrowsWithTheSpeedOverTheMinimumAndStopsAtTheCap()
        {
            var spec = new VehicleImpactSpec(4f, 10f, 200, 1f);

            Assert.That(spec.DamageAt(0f), Is.EqualTo(0));
            Assert.That(spec.DamageAt(4f), Is.EqualTo(0));
            Assert.That(spec.DamageAt(4.05f), Is.EqualTo(0));
            Assert.That(spec.DamageAt(12f), Is.EqualTo(80));
            Assert.That(spec.DamageAt(12.39f), Is.EqualTo(83));
            Assert.That(spec.DamageAt(24f), Is.EqualTo(200));
            Assert.That(spec.DamageAt(100f), Is.EqualTo(200));
            Assert.That(spec.DamageAt(float.PositiveInfinity), Is.EqualTo(200));
            Assert.That(new VehicleImpactSpec(4f, 0f, 200, 1f).DamageAt(float.PositiveInfinity), Is.EqualTo(0));
        }

        [Test]
        public void RejectsValuesOutOfRange()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleImpactSpec(minSpeed: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleImpactSpec(damagePerSpeed: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleImpactSpec(maxDamage: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleImpactSpec(repeatSeconds: float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => VehicleImpactSpec.Default.DamageAt(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => VehicleImpactSpec.Default.DamageAt(float.NaN));
        }
    }
}
