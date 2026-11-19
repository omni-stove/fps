using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class AircraftAimTests
    {
        private const float _epsilon = 1e-4f;

        private readonly AircraftAimSpec _spec = new AircraftAimSpec(pitchGain: 2f, yawGain: 1f, rollGain: 3f, levelGain: 1f, throttleRate: 0.5f);

        [Test]
        public void TheThrottleOpensAndClosesAtItsRateWithinZeroAndOne()
        {
            Assert.That(AircraftAim.StepThrottle(_spec, 0.5f, 1f, 0.2f), Is.EqualTo(0.6f).Within(_epsilon));
            Assert.That(AircraftAim.StepThrottle(_spec, 0.5f, -0.5f, 0.4f), Is.EqualTo(0.4f).Within(_epsilon));
            Assert.That(AircraftAim.StepThrottle(_spec, 0.5f, -1f, 2f), Is.EqualTo(0f));
            Assert.That(AircraftAim.StepThrottle(_spec, 0.5f, 1f, 10f), Is.EqualTo(1f));
            Assert.That(AircraftAim.StepThrottle(_spec, 0.5f, 0f, 1f), Is.EqualTo(0.5f));
        }

        [Test]
        public void PullingBackOnAClosedThrottleIsTheAirBrake()
        {
            Assert.That(AircraftAim.ThrottleAxis(0.6f, -1f), Is.EqualTo(0.6f));
            Assert.That(AircraftAim.ThrottleAxis(0f, -1f), Is.EqualTo(-1f));
            Assert.That(AircraftAim.ThrottleAxis(0f, -0.5f), Is.EqualTo(-0.5f));
            Assert.That(AircraftAim.ThrottleAxis(0f, 0.5f), Is.EqualTo(0f));
            Assert.That(AircraftAim.ThrottleAxis(0f, 0f), Is.EqualTo(0f));
        }

        [Test]
        public void LookingAheadOnlyPassesTheThrottle()
        {
            var input = AircraftAim.Compute(_spec, Vector3.UnitZ, 0f, 0f, 0.7f);

            Assert.That(input, Is.EqualTo(new VehicleMotorInput(0.7f)));
        }

        [Test]
        public void LookingUpAndRightPitchesYawsAndBanksTowardIt()
        {
            var input = AircraftAim.Compute(_spec, new Vector3(0.1f, 0.2f, 1f), 0f, 0f, 0f);

            Assert.That(input[AircraftFlight.PitchAxis], Is.EqualTo(0.39036f).Within(_epsilon));
            Assert.That(input[AircraftFlight.YawAxis], Is.EqualTo(0.09759f).Within(_epsilon));
            Assert.That(input[AircraftFlight.RollAxis], Is.EqualTo(0.29277f).Within(_epsilon));
        }

        [Test]
        public void ABankedAircraftRollsBackToLevelAndTheRollKeysAddToIt()
        {
            Assert.That(AircraftAim.Compute(_spec, Vector3.UnitZ, 0.5f, 0f, 0f)[AircraftFlight.RollAxis], Is.EqualTo(0.5f).Within(_epsilon));
            Assert.That(AircraftAim.Compute(_spec, Vector3.UnitZ, 0.5f, -1f, 0f)[AircraftFlight.RollAxis], Is.EqualTo(-0.5f).Within(_epsilon));
            Assert.That(AircraftAim.Compute(_spec, Vector3.Zero, -0.25f, 0f, 0f)[AircraftFlight.RollAxis], Is.EqualTo(-0.25f).Within(_epsilon));
        }

        [Test]
        public void EachAxisIsClampedToOne()
        {
            var input = AircraftAim.Compute(_spec, Vector3.UnitX, 0f, 0f, 2f);

            Assert.That(input, Is.EqualTo(new VehicleMotorInput(1f, 0f, 1f, 1f)));
        }

        [Test]
        public void LookingBehindTurnsAtFullStick()
        {
            Assert.That(AircraftAim.Compute(_spec, -Vector3.UnitZ, 0f, 0f, 0f), Is.EqualTo(new VehicleMotorInput(0f, 1f, 0f, 0f)), "straight behind pulls up");
            Assert.That(
                AircraftAim.Compute(_spec, new Vector3(-0.1f, 0f, -1f), 0f, 0f, 0f),
                Is.EqualTo(new VehicleMotorInput(0f, 0f, -1f, -1f)),
                "behind and to the left turns left");
        }

        [Test]
        public void RejectsBadArguments()
        {
            Assert.Throws<ArgumentNullException>(() => AircraftAim.Compute(null, Vector3.UnitZ, 0f, 0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => AircraftAim.Compute(_spec, new Vector3(float.NaN, 0f, 1f), 0f, 0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => AircraftAim.Compute(_spec, Vector3.UnitZ, 1.5f, 0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => AircraftAim.StepThrottle(_spec, 1.2f, 0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => AircraftAim.StepThrottle(_spec, 0f, 2f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => AircraftAim.StepThrottle(_spec, 0f, 0f, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftAimSpec(pitchGain: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftAimSpec(throttleRate: float.PositiveInfinity));
        }
    }
}
