using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class AircraftGroundTests
    {
        private const float _epsilon = 1e-4f;

        private readonly AircraftGroundSpec _spec = new AircraftGroundSpec(
            maxSteerAngle: 40f,
            highSpeedSteerAngle: 10f,
            steerFadeSpeed: 20f,
            brakeTorque: 1000f,
            idleBrakeTorque: 300f,
            heightProbeDistance: 50f);

        [Test]
        public void TheNoseWheelTurnsFullyWhenStoppedAndNarrowsWithSpeed()
        {
            Assert.That(AircraftGround.SteerLimit(_spec, 0f), Is.EqualTo(40f).Within(_epsilon));
            Assert.That(AircraftGround.SteerLimit(_spec, 10f), Is.EqualTo(25f).Within(_epsilon));
            Assert.That(AircraftGround.SteerLimit(_spec, 20f), Is.EqualTo(10f).Within(_epsilon));
            Assert.That(AircraftGround.SteerLimit(_spec, 60f), Is.EqualTo(10f).Within(_epsilon), "faster than the fade speed stays at the high-speed angle");
            Assert.That(AircraftGround.SteerLimit(_spec, -10f), Is.EqualTo(25f).Within(_epsilon), "rolling backwards narrows the same way");
        }

        [Test]
        public void YawTurnsTheNoseWheelByTheSpeedLimitedAngle()
        {
            var right = AircraftGround.Compute(_spec, Yaw(1f), 0f, true);
            var halfLeftAtSpeed = AircraftGround.Compute(_spec, Yaw(-0.5f), 10f, true);
            var fullAtTakeoffSpeed = AircraftGround.Compute(_spec, Yaw(1f), 40f, true);

            Assert.That(right.SteerAngle, Is.EqualTo(40f).Within(_epsilon), "+ yaw points the nose wheel right");
            Assert.That(halfLeftAtSpeed.SteerAngle, Is.EqualTo(-12.5f).Within(_epsilon));
            Assert.That(fullAtTakeoffSpeed.SteerAngle, Is.EqualTo(10f).Within(_epsilon));
            Assert.That(Math.Abs(fullAtTakeoffSpeed.SteerAngle), Is.LessThan(Math.Abs(right.SteerAngle)));
        }

        [Test]
        public void AnIdleInputHoldsTheIdleBrake()
        {
            var parked = AircraftGround.Compute(_spec, VehicleMotorInput.Idle, 0f, true);

            Assert.That(parked.BrakeTorque, Is.EqualTo(300f).Within(_epsilon));
            Assert.That(parked.SteerAngle, Is.EqualTo(0f));
        }

        [Test]
        public void TheAirBrakeBrakesTheWheelsByHowFarTheThrottleIsBelowZero()
        {
            var half = AircraftGround.Compute(_spec, new VehicleMotorInput(-0.5f), 30f, true);
            var full = AircraftGround.Compute(_spec, new VehicleMotorInput(-1f), 30f, true);

            Assert.That(half.BrakeTorque, Is.EqualTo(500f).Within(_epsilon));
            Assert.That(full.BrakeTorque, Is.EqualTo(1000f).Within(_epsilon));
        }

        [Test]
        public void AFlownInputWithoutAirBrakeLetsTheWheelsRoll()
        {
            var throttleUp = AircraftGround.Compute(_spec, new VehicleMotorInput(0.8f), 5f, true);
            var closedButSteering = AircraftGround.Compute(_spec, Yaw(0.2f), 5f, true);

            Assert.That(throttleUp.BrakeTorque, Is.EqualTo(0f));
            Assert.That(closedButSteering.BrakeTorque, Is.EqualTo(0f), "someone is flying, so a closed throttle is not the parking brake");
        }

        [Test]
        public void InTheAirTheNoseWheelCentersAndTheBrakesRelease()
        {
            var steering = AircraftGround.Compute(_spec, new VehicleMotorInput(-1f, 0f, 0f, 1f), 0f, false);
            var unmanned = AircraftGround.Compute(_spec, VehicleMotorInput.Idle, 0f, false);

            Assert.That(steering, Is.EqualTo(default(AircraftGroundCommand)));
            Assert.That(unmanned, Is.EqualTo(default(AircraftGroundCommand)));
        }

        [Test]
        public void ComputeRejectsAMissingSpecOrABadSpeed()
        {
            Assert.Throws<ArgumentNullException>(() => AircraftGround.Compute(null, VehicleMotorInput.Idle, 0f, true));
            Assert.Throws<ArgumentNullException>(() => AircraftGround.SteerLimit(null, 0f));
            Assert.Throws<ArgumentNullException>(() => AircraftGround.BrakeTorque(null, VehicleMotorInput.Idle));
            Assert.Throws<ArgumentOutOfRangeException>(() => AircraftGround.Compute(_spec, VehicleMotorInput.Idle, float.NaN, true));
            Assert.Throws<ArgumentOutOfRangeException>(() => AircraftGround.Compute(_spec, VehicleMotorInput.Idle, float.PositiveInfinity, true));
        }

        [Test]
        public void TheDefaultSpecUsesTheDefaults()
        {
            var spec = AircraftGroundSpec.Default;

            Assert.That(spec.MaxSteerAngle, Is.EqualTo(AircraftGroundSpec.DefaultMaxSteerAngle));
            Assert.That(spec.HighSpeedSteerAngle, Is.EqualTo(AircraftGroundSpec.DefaultHighSpeedSteerAngle));
            Assert.That(spec.SteerFadeSpeed, Is.EqualTo(AircraftGroundSpec.DefaultSteerFadeSpeed));
            Assert.That(spec.BrakeTorque, Is.EqualTo(AircraftGroundSpec.DefaultBrakeTorque));
            Assert.That(spec.IdleBrakeTorque, Is.EqualTo(AircraftGroundSpec.DefaultIdleBrakeTorque));
            Assert.That(spec.HeightProbeDistance, Is.EqualTo(AircraftGroundSpec.DefaultHeightProbeDistance));
        }

        [Test]
        public void TheSpecRejectsValuesOutOfRange()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftGroundSpec(maxSteerAngle: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftGroundSpec(maxSteerAngle: 90f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftGroundSpec(maxSteerAngle: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftGroundSpec(maxSteerAngle: 20f, highSpeedSteerAngle: 25f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftGroundSpec(highSpeedSteerAngle: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftGroundSpec(steerFadeSpeed: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftGroundSpec(brakeTorque: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftGroundSpec(idleBrakeTorque: float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftGroundSpec(heightProbeDistance: 0f));
            Assert.DoesNotThrow(() => new AircraftGroundSpec(maxSteerAngle: 0f, highSpeedSteerAngle: 0f, brakeTorque: 0f, idleBrakeTorque: 0f));
        }

        [Test]
        public void TheContactCarriesTheHeightAboveGround()
        {
            var high = new AircraftGroundContact(false, Vector3.Zero, Vector3.Zero, 12.5f);
            var unknown = new AircraftGroundContact(false, Vector3.Zero, Vector3.Zero);

            Assert.That(high.HeightAboveGround, Is.EqualTo(12.5f));
            Assert.That(unknown.HeightAboveGround.HasValue, Is.False);
            Assert.That(AircraftGroundContact.Airborne.HeightAboveGround.HasValue, Is.False);
            Assert.That(high, Is.Not.EqualTo(unknown));
            Assert.That(unknown, Is.EqualTo(AircraftGroundContact.Airborne));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftGroundContact(false, Vector3.Zero, Vector3.Zero, -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftGroundContact(false, Vector3.Zero, Vector3.Zero, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftGroundContact(false, Vector3.Zero, Vector3.Zero, float.PositiveInfinity));
        }

        private static VehicleMotorInput Yaw(float yaw)
        {
            return VehicleMotorInput.Idle.With(AircraftFlight.YawAxis, yaw);
        }
    }
}
