using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class VehicleSpeedFieldOfViewSpecTests
    {
        private const float _tolerance = 1e-4f;

        [Test]
        public void TheDefaultAddsNothing()
        {
            var spec = VehicleSpeedFieldOfViewSpec.Default;

            Assert.That(spec.StartSpeed, Is.EqualTo(VehicleSpeedFieldOfViewSpec.DefaultStartSpeed));
            Assert.That(spec.FullSpeed, Is.EqualTo(VehicleSpeedFieldOfViewSpec.DefaultFullSpeed));
            Assert.That(spec.ExtraDegrees, Is.EqualTo(0f));
            Assert.That(spec.BlendSeconds, Is.EqualTo(VehicleSpeedFieldOfViewSpec.DefaultBlendSeconds));
            Assert.That(spec.IsEnabled, Is.False);
            Assert.That(spec.ExtraDegreesAt(1000f), Is.EqualTo(0f));
        }

        [Test]
        public void NothingIsAddedAtOrBelowTheStartSpeed()
        {
            var spec = new VehicleSpeedFieldOfViewSpec(10f, 30f, 15f);

            Assert.That(spec.ExtraDegreesAt(0f), Is.EqualTo(0f));
            Assert.That(spec.ExtraDegreesAt(5f), Is.EqualTo(0f));
            Assert.That(spec.ExtraDegreesAt(10f), Is.EqualTo(0f));
            Assert.That(spec.ExtraDegreesAt(-40f), Is.EqualTo(0f), "reversing does not widen");
            Assert.That(spec.ExtraDegreesAt(float.NaN), Is.EqualTo(0f));
        }

        [Test]
        public void EverythingIsAddedFromTheFullSpeed()
        {
            var spec = new VehicleSpeedFieldOfViewSpec(10f, 30f, 15f);

            Assert.That(spec.ExtraDegreesAt(30f), Is.EqualTo(15f));
            Assert.That(spec.ExtraDegreesAt(100f), Is.EqualTo(15f));
            Assert.That(spec.ExtraDegreesAt(float.PositiveInfinity), Is.EqualTo(15f));
        }

        [Test]
        public void BetweenTheSpeedsItGrowsLinearly()
        {
            var spec = new VehicleSpeedFieldOfViewSpec(10f, 30f, 15f);

            Assert.That(spec.ExtraDegreesAt(15f), Is.EqualTo(3.75f).Within(_tolerance));
            Assert.That(spec.ExtraDegreesAt(20f), Is.EqualTo(7.5f).Within(_tolerance));
            Assert.That(spec.ExtraDegreesAt(25f), Is.EqualTo(11.25f).Within(_tolerance));
        }

        [Test]
        public void BlendMovesTowardTheGoalOverTime()
        {
            var spec = new VehicleSpeedFieldOfViewSpec(10f, 30f, 15f, 0.5f);

            Assert.That(spec.Blend(0f, 10f, 0f), Is.EqualTo(0f), "no time, no change");
            Assert.That(spec.Blend(0f, 10f, 0.5f), Is.EqualTo(10f * (1f - (float)Math.Exp(-1d))).Within(_tolerance));
            Assert.That(spec.Blend(10f, 0f, 0.5f), Is.EqualTo(10f * (float)Math.Exp(-1d)).Within(_tolerance));
            Assert.That(spec.Blend(0f, 10f, -1f), Is.EqualTo(0f), "negative time is treated as none");
            Assert.That(spec.Blend(float.NaN, 10f, 0.1f), Is.EqualTo(10f));
        }

        [Test]
        public void TheSameTimeInSmallerStepsReachesTheSamePlace()
        {
            var spec = new VehicleSpeedFieldOfViewSpec(10f, 30f, 15f, 0.5f);

            var once = spec.Blend(0f, 10f, 0.2f);
            var twice = spec.Blend(spec.Blend(0f, 10f, 0.1f), 10f, 0.1f);

            Assert.That(twice, Is.EqualTo(once).Within(_tolerance));
        }

        [Test]
        public void ZeroBlendSecondsJumpsToTheGoal()
        {
            var spec = new VehicleSpeedFieldOfViewSpec(10f, 30f, 15f, 0f);

            Assert.That(spec.Blend(0f, 12f, 0.01f), Is.EqualTo(12f));
        }

        [Test]
        public void RejectsInvalidValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleSpeedFieldOfViewSpec(startSpeed: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleSpeedFieldOfViewSpec(startSpeed: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleSpeedFieldOfViewSpec(20f, 20f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleSpeedFieldOfViewSpec(20f, 10f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleSpeedFieldOfViewSpec(10f, float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleSpeedFieldOfViewSpec(extraDegrees: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleSpeedFieldOfViewSpec(extraDegrees: 91f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleSpeedFieldOfViewSpec(extraDegrees: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleSpeedFieldOfViewSpec(blendSeconds: -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleSpeedFieldOfViewSpec(blendSeconds: float.PositiveInfinity));
        }

        [Test]
        public void AVehicleSpecKeepsItOrFallsBackToTheDefault()
        {
            var seats = new[] { new VehicleSeatSpec(VehicleSeatRole.Driver) };
            var widening = new VehicleSpeedFieldOfViewSpec(5f, 25f, 15f, 0.3f);

            Assert.That(new VehicleSpec("car", 100, seats).SpeedFieldOfView, Is.SameAs(VehicleSpeedFieldOfViewSpec.Default));
            Assert.That(new VehicleSpec("car", 100, seats, speedFieldOfView: widening).SpeedFieldOfView, Is.SameAs(widening));
        }
    }
}
