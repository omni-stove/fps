using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ProneSpecTests
    {
        [Test]
        public void DefaultHasTheDefaultValues()
        {
            var spec = ProneSpec.Default;

            Assert.That(spec.Height, Is.EqualTo(ProneSpec.DefaultHeight));
            Assert.That(spec.Speed, Is.EqualTo(ProneSpec.DefaultSpeed));
            Assert.That(spec.MaxPitchDegrees, Is.EqualTo(ProneSpec.DefaultMaxPitchDegrees));
            Assert.That(spec.SpreadScale, Is.EqualTo(ProneSpec.DefaultSpreadScale));
            Assert.That(spec.DiveDistance, Is.EqualTo(ProneSpec.DefaultDiveDistance));
            Assert.That(spec.DiveSeconds, Is.EqualTo(ProneSpec.DefaultDiveSeconds));
        }

        [Test]
        public void TheDefaultsLieLowMoveSlowlyAndSteadyTheAim()
        {
            var spec = ProneSpec.Default;

            Assert.That(spec.Speed, Is.LessThan(2.5f), "slower than the crouch of the player");
            Assert.That(spec.MaxPitchDegrees, Is.LessThan(LookAngles.DefaultMaxPitch));
            Assert.That(spec.SpreadScale, Is.LessThan(1f));
        }

        [Test]
        public void KeepsTheGivenValues()
        {
            var spec = new ProneSpec(0.8f, 1f, 20f, 0.25f, 2f, 0.4f);

            Assert.That(spec.Height, Is.EqualTo(0.8f));
            Assert.That(spec.Speed, Is.EqualTo(1f));
            Assert.That(spec.MaxPitchDegrees, Is.EqualTo(20f));
            Assert.That(spec.SpreadScale, Is.EqualTo(0.25f));
            Assert.That(spec.DiveDistance, Is.EqualTo(2f));
            Assert.That(spec.DiveSeconds, Is.EqualTo(0.4f));
        }

        [Test]
        public void RejectsValuesOutOfRange()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ProneSpec(height: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ProneSpec(height: float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ProneSpec(speed: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ProneSpec(speed: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ProneSpec(maxPitchDegrees: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ProneSpec(maxPitchDegrees: 91f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ProneSpec(spreadScale: -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ProneSpec(spreadScale: 1.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ProneSpec(diveDistance: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ProneSpec(diveSeconds: 0f));
        }

        [Test]
        public void MotorStepAtSpeedMovesAtTheGivenSpeed()
        {
            var motor = new PlayerMotor(new MotorSpec(5f, 8f, 1.2f, 20f, 0.1f, 2.5f));

            var displacement = motor.StepAtSpeed(new Vector2(0f, 1f), 0f, ProneSpec.DefaultSpeed, false, true, 0.5f);

            Assert.That(displacement.Z, Is.EqualTo(ProneSpec.DefaultSpeed * 0.5f).Within(1e-5f));
            Assert.That(motor.Velocity.Z, Is.EqualTo(ProneSpec.DefaultSpeed).Within(1e-5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => motor.StepAtSpeed(Vector2.Zero, 0f, -1f, false, true, 0.1f));
        }
    }
}
