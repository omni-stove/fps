using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ImpactSpecDamageMultiplierTests
    {
        [Test]
        public void TheDamageIsScaledAndRoundedAndTheRestIsKept()
        {
            var falloff = new DamageFalloff(10f, 30f, 0.5f);
            var impact = new ImpactSpec(25, falloff, 2f);

            var scaled = impact.WithDamageMultiplier(0.5f);

            Assert.That(scaled.Damage, Is.EqualTo(13), "12.5 rounds away from zero");
            Assert.That(scaled.Falloff, Is.SameAs(falloff));
            Assert.That(scaled.StructureRadius, Is.EqualTo(2f));
            Assert.That(impact.Damage, Is.EqualTo(25));
        }

        [Test]
        public void OneKeepsTheSameSpec()
        {
            Assert.That(ImpactSpec.Default.WithDamageMultiplier(1f), Is.SameAs(ImpactSpec.Default));
        }

        [TestCase(-0.1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void ABadMultiplierThrows(float multiplier)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ImpactSpec.Default.WithDamageMultiplier(multiplier));
        }
    }
}
