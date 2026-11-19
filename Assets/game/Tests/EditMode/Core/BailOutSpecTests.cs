using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class BailOutSpecTests
    {
        private const float _tolerance = 1e-4f;

        [Test]
        public void TheDefaultBailsOutAboveTenMetresAndKeepsAThirdOfTheSpeed()
        {
            var spec = BailOutSpec.Default;

            Assert.That(spec.Enabled, Is.True);
            Assert.That(spec.MinHeight, Is.EqualTo(10f));
            Assert.That(spec.InheritedSpeedFraction, Is.EqualTo(0.3f));
            Assert.That(spec.ExitHeight, Is.EqualTo(1f));
            Assert.That(spec.ExitSideDistance, Is.EqualTo(1f));
            Assert.That(spec.MinHeight, Is.GreaterThan(ParachuteMotion.DefaultMinOpenHeight), "a bail-out is high enough to open the parachute");
        }

        [Test]
        public void BelowTheMinimumHeightTheRiderStepsOut()
        {
            var spec = new BailOutSpec(minHeight: 10f);

            Assert.That(spec.Evaluate(0f), Is.EqualTo(BailOutVerdict.TooLow));
            Assert.That(spec.Evaluate(9.99f), Is.EqualTo(BailOutVerdict.TooLow));
        }

        [Test]
        public void AtOrAboveTheMinimumHeightTheRiderBailsOut()
        {
            var spec = new BailOutSpec(minHeight: 10f);

            Assert.That(spec.Evaluate(10f), Is.EqualTo(BailOutVerdict.BailOut));
            Assert.That(spec.Evaluate(500f), Is.EqualTo(BailOutVerdict.BailOut));
        }

        [Test]
        public void WithNoGroundBelowTheRiderStepsOut()
        {
            Assert.That(BailOutSpec.Default.Evaluate(null), Is.EqualTo(BailOutVerdict.NoGround));
        }

        [Test]
        public void ASwitchedOffSpecNeverBailsOut()
        {
            var spec = new BailOutSpec(enabled: false);

            Assert.That(spec.Evaluate(500f), Is.EqualTo(BailOutVerdict.Off));
            Assert.That(spec.Evaluate(null), Is.EqualTo(BailOutVerdict.Off));
        }

        [Test]
        public void TheInheritedVelocityIsTheFractionOfTheVehicles()
        {
            var spec = new BailOutSpec(inheritedSpeedFraction: 0.25f);

            var inherited = spec.InheritedVelocity(new Vector3(40f, -8f, 20f));

            Assert.That(inherited.X, Is.EqualTo(10f).Within(_tolerance));
            Assert.That(inherited.Y, Is.EqualTo(-2f).Within(_tolerance));
            Assert.That(inherited.Z, Is.EqualTo(5f).Within(_tolerance));
            Assert.That(new BailOutSpec(inheritedSpeedFraction: 0f).InheritedVelocity(new Vector3(40f, 0f, 0f)), Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void InvalidValuesAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BailOutSpec(minHeight: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BailOutSpec(minHeight: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BailOutSpec(minHeight: float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BailOutSpec(inheritedSpeedFraction: -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BailOutSpec(inheritedSpeedFraction: 1.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BailOutSpec(inheritedSpeedFraction: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BailOutSpec(exitHeight: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BailOutSpec(exitSideDistance: float.PositiveInfinity));

            var spec = BailOutSpec.Default;
            Assert.Throws<ArgumentOutOfRangeException>(() => spec.Evaluate(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => spec.Evaluate(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => spec.Evaluate(float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => spec.InheritedVelocity(new Vector3(float.NaN, 0f, 0f)));
        }

        [Test]
        public void AVehicleSpecWithoutABailOutUsesTheDefault()
        {
            var spec = new VehicleSpec("car", 100, new[] { new VehicleSeatSpec(VehicleSeatRole.Driver) });
            var custom = new BailOutSpec(minHeight: 30f);

            Assert.That(spec.BailOut, Is.SameAs(BailOutSpec.Default));
            Assert.That(
                new VehicleSpec("jet", 100, new[] { new VehicleSeatSpec(VehicleSeatRole.Driver) }, bailOut: custom).BailOut,
                Is.SameAs(custom));
        }
    }
}
