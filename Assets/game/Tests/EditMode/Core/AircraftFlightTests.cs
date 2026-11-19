using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class AircraftFlightTests
    {
        private const float _epsilon = 1e-4f;

        private readonly AircraftSpec _spec = new AircraftSpec(
            mass: 1000f,
            maxThrust: 10f,
            maxSpeed: 100f,
            liftSpeed: 40f,
            controlSpeed: 20f,
            minControl: 0.25f,
            pitchRate: 90f,
            rollRate: 180f,
            yawRate: 45f,
            responsiveness: 2f,
            alignment: 1f,
            airBrakeDrag: 3f);

        [Test]
        public void TheAxesAreThrottlePitchRollYaw()
        {
            Assert.That(AircraftFlight.ThrottleAxis, Is.EqualTo(0));
            Assert.That(AircraftFlight.PitchAxis, Is.EqualTo(1));
            Assert.That(AircraftFlight.RollAxis, Is.EqualTo(2));
            Assert.That(AircraftFlight.YawAxis, Is.EqualTo(3));
        }

        [Test]
        public void LiftGrowsWithTheSquareOfSpeedUpToTheWeight()
        {
            Assert.That(AircraftFlight.LiftRatio(_spec, 20f), Is.EqualTo(0.25f).Within(_epsilon));
            Assert.That(AircraftFlight.LiftRatio(_spec, 40f), Is.EqualTo(1f).Within(_epsilon));
            Assert.That(AircraftFlight.LiftRatio(_spec, 80f), Is.EqualTo(1f));
            Assert.That(AircraftFlight.LiftRatio(_spec, -5f), Is.EqualTo(0f));
        }

        [Test]
        public void ControlGrowsWithSpeedAndKeepsTheMinimum()
        {
            Assert.That(AircraftFlight.Control(_spec, 0f), Is.EqualTo(0.25f));
            Assert.That(AircraftFlight.Control(_spec, 2f), Is.EqualTo(0.25f));
            Assert.That(AircraftFlight.Control(_spec, 10f), Is.EqualTo(0.5f).Within(_epsilon));
            Assert.That(AircraftFlight.Control(_spec, 20f), Is.EqualTo(1f));
            Assert.That(AircraftFlight.Control(_spec, 50f), Is.EqualTo(1f));
        }

        [Test]
        public void TheTopSpeedAddsWhatGravityPushesInADive()
        {
            Assert.That(_spec.DragCoefficient, Is.EqualTo(0.001f).Within(1e-7f));
            Assert.That(AircraftFlight.TopSpeed(_spec, 0f), Is.EqualTo(100f).Within(_epsilon));
            Assert.That(AircraftFlight.TopSpeed(_spec, 10f), Is.EqualTo(141.4214f).Within(1e-3f));
        }

        [Test]
        public void FullThrottleAtTopSpeedHoldsLevelFlight()
        {
            var command = AircraftFlight.Compute(_spec, new VehicleMotorInput(1f), new Vector3(0f, 0f, 100f), Vector3.Zero, 10f);

            AssertNear(command.LocalAcceleration, new Vector3(0f, 10f, 0f));
            AssertNear(command.LocalAngularAcceleration, Vector3.Zero);
            Assert.That(command.LiftRatio, Is.EqualTo(1f));
            Assert.That(command.Control, Is.EqualTo(1f));
        }

        [Test]
        public void ANegativeThrottleIsAnAirBrake()
        {
            var command = AircraftFlight.Compute(_spec, new VehicleMotorInput(-1f), new Vector3(0f, 0f, 50f), Vector3.Zero, 10f);

            AssertNear(command.LocalAcceleration, new Vector3(0f, 10f, -10f));
        }

        [Test]
        public void SideAndVerticalDriftIsPulledTowardTheNose()
        {
            var command = AircraftFlight.Compute(_spec, VehicleMotorInput.Idle, new Vector3(2f, -4f, 20f), Vector3.Zero, 0f);

            AssertNear(command.LocalAcceleration, new Vector3(-2.040988f, 4.081976f, -0.409878f));
        }

        [Test]
        public void TheSticksTurnTheAircraftAtTheirRates()
        {
            var input = new VehicleMotorInput(0f, 1f, 1f, -1f);

            var command = AircraftFlight.Compute(_spec, input, new Vector3(0f, 0f, 20f), Vector3.Zero, 0f);

            AssertNear(command.LocalAngularAcceleration, new Vector3(-3.141593f, -1.570796f, -6.283185f));
        }

        [Test]
        public void ASlowAircraftTurnsWithTheMinimumControlAndDampsItsSpin()
        {
            var command = AircraftFlight.Compute(_spec, new VehicleMotorInput(0f, 1f), Vector3.Zero, new Vector3(1f, 0f, 0f), 0f);

            AssertNear(command.LocalAngularAcceleration, new Vector3(-2.785398f, 0f, 0f));
            Assert.That(command.Control, Is.EqualTo(0.25f));
            Assert.That(command.LiftRatio, Is.EqualTo(0f));
        }

        [Test]
        public void ComputeRejectsBadArguments()
        {
            Assert.Throws<ArgumentNullException>(() => AircraftFlight.Compute(null, VehicleMotorInput.Idle, Vector3.Zero, Vector3.Zero, 9.8f));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => AircraftFlight.Compute(_spec, VehicleMotorInput.Idle, new Vector3(float.NaN, 0f, 0f), Vector3.Zero, 9.8f));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => AircraftFlight.Compute(_spec, VehicleMotorInput.Idle, Vector3.Zero, new Vector3(0f, float.PositiveInfinity, 0f), 9.8f));
            Assert.Throws<ArgumentOutOfRangeException>(() => AircraftFlight.Compute(_spec, VehicleMotorInput.Idle, Vector3.Zero, Vector3.Zero, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => AircraftFlight.TopSpeed(_spec, float.NaN));
        }

        [Test]
        public void TheSpecKeepsItsDefaultsAndRejectsBadValues()
        {
            var spec = AircraftSpec.Default;

            Assert.That(spec.Mass, Is.EqualTo(AircraftSpec.DefaultMass));
            Assert.That(spec.MaxSpeed, Is.EqualTo(AircraftSpec.DefaultMaxSpeed));
            Assert.That(spec.LiftSpeed, Is.EqualTo(AircraftSpec.DefaultLiftSpeed));
            Assert.That(spec.MinControl, Is.EqualTo(AircraftSpec.DefaultMinControl));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftSpec(mass: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftSpec(maxThrust: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftSpec(maxSpeed: float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftSpec(minControl: 1.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftSpec(rollRate: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftSpec(airBrakeDrag: float.NaN));
        }

        private static void AssertNear(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.X, Is.EqualTo(expected.X).Within(_epsilon), "x");
            Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(_epsilon), "y");
            Assert.That(actual.Z, Is.EqualTo(expected.Z).Within(_epsilon), "z");
        }
    }
}
