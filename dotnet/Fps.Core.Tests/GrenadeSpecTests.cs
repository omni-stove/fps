using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class GrenadeSpecTests
    {
        [Test]
        public void TheDefaultsAreTheValuesTheGrenadeHasAlwaysUsed()
        {
            var spec = GrenadeSpec.Default;

            Assert.That(spec.FuseSeconds, Is.EqualTo(2.5f));
            Assert.That(spec.ThrowSeconds, Is.EqualTo(0.6f));
            Assert.That(spec.ThrowSpeed, Is.EqualTo(14f));
            Assert.That(spec.ThrowLiftSpeed, Is.EqualTo(3f));
            Assert.That(spec.MaxDamage, Is.EqualTo(150));
            Assert.That(spec.DamageRadius, Is.EqualTo(6f));
            Assert.That(spec.FullDamageRadius, Is.EqualTo(1.5f));
            Assert.That(spec.StructureDamageAtCenter, Is.EqualTo(150));
        }

        [Test]
        public void OmittedValuesUseTheDefaults()
        {
            var spec = new GrenadeSpec(maxDamage: 80);

            Assert.That(spec.MaxDamage, Is.EqualTo(80));
            Assert.That(spec.FuseSeconds, Is.EqualTo(GrenadeSpec.DefaultFuseSeconds));
            Assert.That(spec.DamageRadius, Is.EqualTo(GrenadeSpec.DefaultDamageRadius));
            Assert.That(spec.StructureDamageAtCenter, Is.EqualTo(GrenadeSpec.DefaultStructureDamageAtCenter));
        }

        [Test]
        public void KeepsEveryGivenValue()
        {
            var spec = new GrenadeSpec(1f, 0.3f, 10f, 2f, 90, 4f, 1f, 70);

            Assert.That(spec.FuseSeconds, Is.EqualTo(1f));
            Assert.That(spec.ThrowSeconds, Is.EqualTo(0.3f));
            Assert.That(spec.ThrowSpeed, Is.EqualTo(10f));
            Assert.That(spec.ThrowLiftSpeed, Is.EqualTo(2f));
            Assert.That(spec.MaxDamage, Is.EqualTo(90));
            Assert.That(spec.DamageRadius, Is.EqualTo(4f));
            Assert.That(spec.FullDamageRadius, Is.EqualTo(1f));
            Assert.That(spec.StructureDamageAtCenter, Is.EqualTo(70));
        }

        [Test]
        public void TheExplosionUsesTheDamageAndRadii()
        {
            var spec = new GrenadeSpec(maxDamage: 100, damageRadius: 6f, fullDamageRadius: 2f);

            Assert.That(spec.Explosion.MaxDamage, Is.EqualTo(100));
            Assert.That(spec.Explosion.Radius, Is.EqualTo(6f));
            Assert.That(spec.Explosion.FullDamageRadius, Is.EqualTo(2f));
            Assert.That(spec.Explosion.At(4f, obstructed: false), Is.EqualTo(50));
        }

        [Test]
        public void LowerMaxDamageHurtsLessAtTheSameDistance()
        {
            var weaker = new GrenadeSpec(maxDamage: GrenadeSpec.DefaultMaxDamage / 2);

            Assert.That(weaker.Explosion.At(1f, obstructed: false), Is.LessThan(GrenadeSpec.Default.Explosion.At(1f, obstructed: false)));
            Assert.That(weaker.Explosion.At(4f, obstructed: false), Is.LessThan(GrenadeSpec.Default.Explosion.At(4f, obstructed: false)));
        }

        [Test]
        public void AcceptsZeroWhereZeroIsAllowed()
        {
            var spec = new GrenadeSpec(0f, 0f, 0f, 0f, 0, 0.01f, 0f, 0);

            Assert.That(spec.FuseSeconds, Is.Zero);
            Assert.That(spec.ThrowSeconds, Is.Zero);
            Assert.That(spec.ThrowSpeed, Is.Zero);
            Assert.That(spec.ThrowLiftSpeed, Is.Zero);
            Assert.That(spec.MaxDamage, Is.Zero);
            Assert.That(spec.FullDamageRadius, Is.Zero);
            Assert.That(spec.StructureDamageAtCenter, Is.Zero);
        }

        [TestCase(-0.1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void RejectsANegativeOrNonFiniteFuse(float value)
        {
            AssertRejected("fuseSeconds", () => new GrenadeSpec(fuseSeconds: value));
        }

        [TestCase(-0.1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void RejectsANegativeOrNonFiniteThrowTime(float value)
        {
            AssertRejected("throwSeconds", () => new GrenadeSpec(throwSeconds: value));
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void RejectsANegativeOrNonFiniteThrowSpeed(float value)
        {
            AssertRejected("throwSpeed", () => new GrenadeSpec(throwSpeed: value));
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void RejectsANegativeOrNonFiniteLiftSpeed(float value)
        {
            AssertRejected("throwLiftSpeed", () => new GrenadeSpec(throwLiftSpeed: value));
        }

        [Test]
        public void RejectsANegativeMaxDamage()
        {
            AssertRejected("maxDamage", () => new GrenadeSpec(maxDamage: -1));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void RejectsANonPositiveOrNonFiniteDamageRadius(float value)
        {
            AssertRejected("damageRadius", () => new GrenadeSpec(damageRadius: value, fullDamageRadius: 0f));
        }

        [TestCase(-0.1f)]
        [TestCase(float.NaN)]
        [TestCase(6f)]
        [TestCase(7f)]
        public void RejectsAFullDamageRadiusOutsideTheDamageRadius(float value)
        {
            AssertRejected("fullDamageRadius", () => new GrenadeSpec(damageRadius: 6f, fullDamageRadius: value));
        }

        [Test]
        public void RejectsANegativeStructureDamage()
        {
            AssertRejected("structureDamageAtCenter", () => new GrenadeSpec(structureDamageAtCenter: -1));
        }

        private static void AssertRejected(string paramName, Action create)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => create());
            Assert.That(ex.ParamName, Is.EqualTo(paramName));
        }
    }
}
