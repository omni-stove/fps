using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class AircraftStickTests
    {
        private const float _epsilon = 1e-4f;

        private readonly AircraftStickSpec _spec = new AircraftStickSpec(deadzone: 0.2f, exponent: 2f, throttleRate: 0.5f, forwardPitchesDown: false);

        [Test]
        public void InsideTheDeadzoneIsZero()
        {
            Assert.That(AircraftStick.Shape(_spec, 0f), Is.EqualTo(0f));
            Assert.That(AircraftStick.Shape(_spec, 0.1f), Is.EqualTo(0f));
            Assert.That(AircraftStick.Shape(_spec, -0.2f), Is.EqualTo(0f));
        }

        [Test]
        public void PushedAllTheWayIsOne()
        {
            Assert.That(AircraftStick.Shape(_spec, 1f), Is.EqualTo(1f).Within(_epsilon));
            Assert.That(AircraftStick.Shape(_spec, -1f), Is.EqualTo(-1f).Within(_epsilon));
        }

        [Test]
        public void TheExponentMakesTheMiddleSmaller()
        {
            var linear = new AircraftStickSpec(deadzone: 0.2f, exponent: 1f);

            Assert.That(AircraftStick.Shape(linear, 0.6f), Is.EqualTo(0.5f).Within(_epsilon), "(0.6 - 0.2) / 0.8");
            Assert.That(AircraftStick.Shape(_spec, 0.6f), Is.EqualTo(0.25f).Within(_epsilon), "0.5 squared");
            Assert.That(AircraftStick.Shape(_spec, -0.6f), Is.EqualTo(-0.25f).Within(_epsilon));
        }

        [Test]
        public void TheThrottleMovesTowardItsTargetAtItsRate()
        {
            Assert.That(AircraftStick.StepThrottle(_spec, 0f, 1f, 0.2f), Is.EqualTo(0.1f).Within(_epsilon));
            Assert.That(AircraftStick.StepThrottle(_spec, 0.5f, 0f, 0.4f), Is.EqualTo(0.3f).Within(_epsilon));
            Assert.That(AircraftStick.StepThrottle(_spec, 0.9f, 1f, 1f), Is.EqualTo(1f), "it stops at the target");
            Assert.That(AircraftStick.StepThrottle(_spec, 0.1f, 0f, 1f), Is.EqualTo(0f));
            Assert.That(AircraftStick.StepThrottle(_spec, 0.4f, 0.5f, 10f), Is.EqualTo(0.5f));
        }

        [Test]
        public void ComputePutsEachStickOnItsAxis()
        {
            var input = AircraftStick.Compute(_spec, pitchStick: 1f, rollStick: -1f, rudder: 0.6f, throttle: 0.75f);

            Assert.That(input[AircraftFlight.ThrottleAxis], Is.EqualTo(0.75f));
            Assert.That(input[AircraftFlight.PitchAxis], Is.EqualTo(1f).Within(_epsilon));
            Assert.That(input[AircraftFlight.RollAxis], Is.EqualTo(-1f).Within(_epsilon));
            Assert.That(input[AircraftFlight.YawAxis], Is.EqualTo(0.25f).Within(_epsilon));
        }

        [Test]
        public void ForwardPitchesDownWhenTheSpecSaysSo()
        {
            var stick = new AircraftStickSpec(deadzone: 0f, exponent: 1f, forwardPitchesDown: true);

            var input = AircraftStick.Compute(stick, pitchStick: 0.5f, rollStick: 0f, rudder: 0f, throttle: 0f);

            Assert.That(input[AircraftFlight.PitchAxis], Is.EqualTo(-0.5f).Within(_epsilon));
        }

        [Test]
        public void TheDefaultSpecUsesTheDefaults()
        {
            var spec = AircraftStickSpec.Default;

            Assert.That(spec.Deadzone, Is.EqualTo(AircraftStickSpec.DefaultDeadzone));
            Assert.That(spec.Exponent, Is.EqualTo(AircraftStickSpec.DefaultExponent));
            Assert.That(spec.ThrottleRate, Is.EqualTo(AircraftStickSpec.DefaultThrottleRate));
            Assert.That(spec.ForwardPitchesDown, Is.EqualTo(AircraftStickSpec.DefaultForwardPitchesDown));
        }

        [Test]
        public void InvalidValuesAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftStickSpec(deadzone: 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftStickSpec(deadzone: -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftStickSpec(exponent: 0.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftStickSpec(exponent: float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftStickSpec(throttleRate: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => AircraftStick.Shape(_spec, 1.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => AircraftStick.Shape(_spec, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => AircraftStick.StepThrottle(_spec, 1.2f, 0f, 0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => AircraftStick.StepThrottle(_spec, 0f, -0.1f, 0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => AircraftStick.StepThrottle(_spec, 0f, 1f, -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => AircraftStick.Compute(_spec, 0f, 0f, 0f, -0.5f));
            Assert.Throws<ArgumentNullException>(() => AircraftStick.Shape(null, 0f));
        }
    }
}
