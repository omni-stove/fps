using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class CarDriveTests
    {
        private static readonly CarSpec _spec = new CarSpec(
            mass: 1000f,
            motorTorque: 2000f,
            brakeTorque: 3000f,
            idleBrakeTorque: 100f,
            maxSteerAngle: 30f,
            maxForwardSpeed: 20f,
            maxReverseSpeed: 5f,
            flipAngle: 60f,
            rightingMaxSpeed: 2f,
            rightingLift: 1f);

        [Test]
        public void TheDefaultSpecKeepsTheDefaults()
        {
            var spec = CarSpec.Default;

            Assert.That(spec.Mass, Is.EqualTo(1200f));
            Assert.That(spec.MotorTorque, Is.EqualTo(2400f));
            Assert.That(spec.BrakeTorque, Is.EqualTo(3000f));
            Assert.That(spec.IdleBrakeTorque, Is.EqualTo(300f));
            Assert.That(spec.MaxSteerAngle, Is.EqualTo(30f));
            Assert.That(spec.MaxForwardSpeed, Is.EqualTo(20f));
            Assert.That(spec.MaxReverseSpeed, Is.EqualTo(6f));
            Assert.That(spec.FlipAngle, Is.EqualTo(60f));
            Assert.That(spec.RightingMaxSpeed, Is.EqualTo(2f));
            Assert.That(spec.RightingLift, Is.EqualTo(1f));
        }

        [Test]
        public void TheSpecRejectsValuesOutOfRange()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CarSpec(mass: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CarSpec(motorTorque: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CarSpec(brakeTorque: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CarSpec(idleBrakeTorque: float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CarSpec(maxSteerAngle: 90f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CarSpec(maxForwardSpeed: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CarSpec(maxReverseSpeed: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CarSpec(flipAngle: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CarSpec(flipAngle: 180f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CarSpec(rightingMaxSpeed: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CarSpec(rightingLift: -1f));
        }

        [Test]
        public void FullThrottleFromRestDrivesForward()
        {
            var command = CarDrive.Compute(_spec, new VehicleMotorInput(1f, 0f, 0f, 0f), 0f);

            Assert.That(command.MotorTorque, Is.EqualTo(2000f));
            Assert.That(command.BrakeTorque, Is.EqualTo(0f));
            Assert.That(command.SteerAngle, Is.EqualTo(0f));
        }

        [Test]
        public void ReverseThrottleFromRestBacksUp()
        {
            var command = CarDrive.Compute(_spec, new VehicleMotorInput(-0.5f, 0f, 0f, 0f), 0.2f);

            Assert.That(command.MotorTorque, Is.EqualTo(-1000f));
            Assert.That(command.BrakeTorque, Is.EqualTo(0f));
        }

        [Test]
        public void ThrottleAgainstTheMotionBrakesInsteadOfReversing()
        {
            var backWhileForward = CarDrive.Compute(_spec, new VehicleMotorInput(-0.5f, 0f, 0f, 0f), 10f);
            var forwardWhileBack = CarDrive.Compute(_spec, new VehicleMotorInput(1f, 0f, 0f, 0f), -3f);

            Assert.That(backWhileForward.MotorTorque, Is.EqualTo(0f));
            Assert.That(backWhileForward.BrakeTorque, Is.EqualTo(1500f));
            Assert.That(forwardWhileBack.MotorTorque, Is.EqualTo(0f));
            Assert.That(forwardWhileBack.BrakeTorque, Is.EqualTo(3000f));
        }

        [Test]
        public void TheMotorStopsPushingAtTheSpeedLimit()
        {
            Assert.That(CarDrive.Compute(_spec, new VehicleMotorInput(1f, 0f, 0f, 0f), 20f).MotorTorque, Is.EqualTo(0f));
            Assert.That(CarDrive.Compute(_spec, new VehicleMotorInput(1f, 0f, 0f, 0f), 19.9f).MotorTorque, Is.EqualTo(2000f));
            Assert.That(CarDrive.Compute(_spec, new VehicleMotorInput(-1f, 0f, 0f, 0f), -5f).MotorTorque, Is.EqualTo(0f));
            Assert.That(CarDrive.Compute(_spec, new VehicleMotorInput(-1f, 0f, 0f, 0f), -4.9f).MotorTorque, Is.EqualTo(-2000f));
        }

        [Test]
        public void SteeringTurnsTheFrontWheelsByTheAxis()
        {
            Assert.That(CarDrive.Compute(_spec, new VehicleMotorInput(0f, 1f, 0f, 0f), 5f).SteerAngle, Is.EqualTo(30f));
            Assert.That(CarDrive.Compute(_spec, new VehicleMotorInput(0f, -0.5f, 0f, 0f), 5f).SteerAngle, Is.EqualTo(-15f));
        }

        [Test]
        public void TheBrakeAndTheIdleBrakeHoldTheCar()
        {
            var braking = CarDrive.Compute(_spec, new VehicleMotorInput(1f, 0f, 1f, 0f), 5f);
            var halfBrake = CarDrive.Compute(_spec, new VehicleMotorInput(0f, 0f, 0.5f, 0f), 5f);
            var idle = CarDrive.Compute(_spec, VehicleMotorInput.Idle, 5f);

            Assert.That(braking.BrakeTorque, Is.EqualTo(3000f));
            Assert.That(braking.MotorTorque, Is.EqualTo(2000f));
            Assert.That(halfBrake.BrakeTorque, Is.EqualTo(1500f));
            Assert.That(idle.BrakeTorque, Is.EqualTo(100f));
            Assert.That(idle.MotorTorque, Is.EqualTo(0f));
        }

        [Test]
        public void ANegativeBrakeAxisDoesNotBrake()
        {
            var command = CarDrive.Compute(_spec, VehicleMotorInput.Idle.With(CarDrive.BrakeAxis, -1f), 5f);

            Assert.That(command.BrakeTorque, Is.EqualTo(100f), "only the idle brake holds the car");
        }

        [Test]
        public void ReadsEachAxisByItsNumber()
        {
            var input = VehicleMotorInput.Idle.With(CarDrive.ThrottleAxis, 1f).With(CarDrive.SteerAxis, 0.5f);

            var command = CarDrive.Compute(_spec, input, 5f);

            Assert.That(CarDrive.ThrottleAxis, Is.EqualTo(0));
            Assert.That(CarDrive.SteerAxis, Is.EqualTo(1));
            Assert.That(CarDrive.BrakeAxis, Is.EqualTo(2));
            Assert.That(CarDrive.RightAxis, Is.EqualTo(3));
            Assert.That(command.MotorTorque, Is.EqualTo(2000f));
            Assert.That(command.SteerAngle, Is.EqualTo(15f));
        }

        [Test]
        public void ComputeRejectsANullSpecOrAnUnknownSpeed()
        {
            Assert.Throws<ArgumentNullException>(() => CarDrive.Compute(null, VehicleMotorInput.Idle, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => CarDrive.Compute(_spec, VehicleMotorInput.Idle, float.NaN));
        }

        [Test]
        public void TheCarIsTippedPastTheFlipAngleAndCanBeRightedOnlyWhenSlow()
        {
            Assert.That(CarDrive.IsTipped(_spec, 60f), Is.False);
            Assert.That(CarDrive.IsTipped(_spec, 61f), Is.True);
            Assert.That(CarDrive.IsTipped(_spec, 180f), Is.True);
            Assert.That(CarDrive.CanRight(_spec, 170f, 2f), Is.True);
            Assert.That(CarDrive.CanRight(_spec, 170f, 2.5f), Is.False);
            Assert.That(CarDrive.CanRight(_spec, 10f, 0f), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => CarDrive.IsTipped(_spec, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => CarDrive.CanRight(_spec, 90f, -1f));
        }
    }
}
