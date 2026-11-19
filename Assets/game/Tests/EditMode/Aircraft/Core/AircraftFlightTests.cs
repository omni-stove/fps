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
            aero: new AircraftAeroSpec(
                maxSpeed: 100f,
                liftSpeed: 40f,
                controlSpeed: 20f,
                minControl: 0.25f,
                pitchRate: 90f,
                rollRate: 180f,
                yawRate: 45f,
                responsiveness: 2f,
                alignment: 1f,
                airBrakeDrag: 3f,
                liftSlope: 0.1f,
                criticalAngle: 10f,
                postStallLift: 0.5f,
                inducedDrag: 0.1f,
                weathervane: 2f,
                dihedral: 1f,
                wingRoll: 0.5f));

        [Test]
        public void TheAxesAreThrottlePitchRollYaw()
        {
            Assert.That(AircraftFlight.ThrottleAxis, Is.EqualTo(0));
            Assert.That(AircraftFlight.PitchAxis, Is.EqualTo(1));
            Assert.That(AircraftFlight.RollAxis, Is.EqualTo(2));
            Assert.That(AircraftFlight.YawAxis, Is.EqualTo(3));
        }

        [Test]
        public void LiftGrowsWithTheAngleOfAttackAndTheSquareOfSpeed()
        {
            Assert.That(AircraftAero.LiftRatio(_spec.Aero, new Vector3(0f, 0f, 40f)), Is.EqualTo(0f), "air along the nose does not lift");
            Assert.That(AircraftAero.LiftRatio(_spec.Aero, AtAngle(5f, 20f)), Is.EqualTo(0.125f).Within(_epsilon));
            Assert.That(AircraftAero.LiftRatio(_spec.Aero, AtAngle(5f, 40f)), Is.EqualTo(0.5f).Within(_epsilon));
            Assert.That(AircraftAero.LiftRatio(_spec.Aero, AtAngle(9f, 40f)), Is.EqualTo(0.9f).Within(_epsilon));
            Assert.That(AircraftAero.LiftRatio(_spec.Aero, AtAngle(9f, 80f)), Is.EqualTo(3.6f).Within(1e-3f), "past the weight");
            Assert.That(AircraftAero.LiftRatio(_spec.Aero, Vector3.Zero), Is.EqualTo(0f));
        }

        [Test]
        public void ControlGrowsWithSpeedAndKeepsTheMinimum()
        {
            Assert.That(AircraftAero.Control(_spec.Aero, 0f), Is.EqualTo(0.25f));
            Assert.That(AircraftAero.Control(_spec.Aero, 2f), Is.EqualTo(0.25f));
            Assert.That(AircraftAero.Control(_spec.Aero, 10f), Is.EqualTo(0.5f).Within(_epsilon));
            Assert.That(AircraftAero.Control(_spec.Aero, 20f), Is.EqualTo(1f));
            Assert.That(AircraftAero.Control(_spec.Aero, 50f), Is.EqualTo(1f));
        }

        [Test]
        public void TheTopSpeedAddsWhatGravityPushesInADive()
        {
            Assert.That(_spec.DragCoefficient, Is.EqualTo(0.001f).Within(1e-7f));
            Assert.That(AircraftAero.TopSpeed(_spec, 0f), Is.EqualTo(100f).Within(_epsilon));
            Assert.That(AircraftAero.TopSpeed(_spec, 10f), Is.EqualTo(141.4214f).Within(1e-3f));
        }

        [Test]
        public void FullThrottleAtTopSpeedAlongTheNoseBalancesTheDrag()
        {
            var command = AircraftFlight.Compute(_spec, new VehicleMotorInput(1f), new Vector3(0f, 0f, 100f), Vector3.Zero, 10f);

            AssertNear(command.LocalAcceleration, Vector3.Zero);
            AssertNear(command.LocalAngularAcceleration, Vector3.Zero);
            Assert.That(command.LiftRatio, Is.EqualTo(0f), "no angle of attack, no lift");
            Assert.That(command.Control, Is.EqualTo(1f));
        }

        [Test]
        public void ANegativeThrottleIsAnAirBrake()
        {
            var command = AircraftFlight.Compute(_spec, new VehicleMotorInput(-1f), new Vector3(0f, 0f, 50f), Vector3.Zero, 10f);

            AssertNear(command.LocalAcceleration, new Vector3(0f, 0f, -10f));
        }

        [Test]
        public void SideDriftIsPulledTowardTheNoseButVerticalDriftIsLeftToTheLift()
        {
            var command = AircraftFlight.Compute(_spec, VehicleMotorInput.Idle, new Vector3(2f, -4f, 20f), Vector3.Zero, 0f);

            AssertNear(command.LocalAcceleration, new Vector3(-2.040988f, 0.081976f, -0.409878f));
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
            Assert.Throws<ArgumentOutOfRangeException>(() => AircraftAero.TopSpeed(_spec, float.NaN));
            Assert.Throws<ArgumentNullException>(() => AircraftAero.LiftRatio(null, new Vector3(0f, 0f, 10f)));
            Assert.Throws<ArgumentNullException>(() => AircraftAero.Control(null, 10f));
        }

        [Test]
        public void TheSpecKeepsItsDefaultsAndRejectsBadValues()
        {
            var spec = AircraftSpec.Default;

            Assert.That(spec.Mass, Is.EqualTo(AircraftSpec.DefaultMass));
            Assert.That(spec.MaxThrust, Is.EqualTo(AircraftSpec.DefaultMaxThrust));
            Assert.That(spec.Aero.MaxSpeed, Is.EqualTo(AircraftAeroSpec.DefaultMaxSpeed));
            Assert.That(spec.Aero.LiftSpeed, Is.EqualTo(AircraftAeroSpec.DefaultLiftSpeed));
            Assert.That(spec.Aero.MinControl, Is.EqualTo(AircraftAeroSpec.DefaultMinControl));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftSpec(mass: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftSpec(maxThrust: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftAeroSpec(maxSpeed: float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftAeroSpec(minControl: 1.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftAeroSpec(rollRate: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftAeroSpec(airBrakeDrag: float.NaN));
        }

        [Test]
        public void TheFlightIsThrustPlusTheAeroForces()
        {
            var velocity = new Vector3(2f, -4f, 20f);
            var spin = new Vector3(0.5f, 0f, -0.25f);
            var input = new VehicleMotorInput(0.5f, 1f, -1f, 0.5f);

            var aero = AircraftAero.Compute(_spec, input, velocity, spin, 10f);
            var command = AircraftFlight.Compute(_spec, input, velocity, spin, 10f);

            AssertNear(command.LocalAcceleration, aero.LocalAcceleration + new Vector3(0f, 0f, 5f));
            AssertNear(command.LocalAngularAcceleration, aero.LocalAngularAcceleration);
            Assert.That(command.LiftRatio, Is.EqualTo(aero.LiftRatio));
            Assert.That(command.Control, Is.EqualTo(aero.Control));
        }

        [Test]
        public void TheGroundContactIsAddedOnTopAndMarksTheStateGrounded()
        {
            var ground = new AircraftGroundContact(true, new Vector3(0f, 0f, -3f), new Vector3(0f, 0.5f, 0f));

            var free = AircraftFlight.Compute(_spec, new VehicleMotorInput(1f), new Vector3(0f, 0f, 10f), Vector3.Zero, 10f);
            var rolling = AircraftFlight.Compute(_spec, new VehicleMotorInput(1f), new Vector3(0f, 0f, 10f), Vector3.Zero, 10f, ground);

            AssertNear(rolling.LocalAcceleration, free.LocalAcceleration + new Vector3(0f, 0f, -3f));
            AssertNear(rolling.LocalAngularAcceleration, free.LocalAngularAcceleration + new Vector3(0f, 0.5f, 0f));
            Assert.That(free.State.Grounded, Is.False);
            Assert.That(rolling.State.Grounded, Is.True);
        }

        [Test]
        public void TheDefaultGroundAndConditionChangeNothing()
        {
            var input = new VehicleMotorInput(-0.5f, 0.3f, 1f, -0.2f);
            var velocity = new Vector3(1f, 2f, 30f);
            var spin = new Vector3(0.1f, -0.2f, 0.3f);

            var plain = AircraftFlight.Compute(_spec, input, velocity, spin, 9.81f);
            var explicitDefaults = AircraftFlight.Compute(
                _spec, input, velocity, spin, 9.81f, AircraftGroundContact.Airborne, new AircraftCondition(1f, 1f, 1f, 1f));

            Assert.That(explicitDefaults.LocalAcceleration, Is.EqualTo(plain.LocalAcceleration));
            Assert.That(explicitDefaults.LocalAngularAcceleration, Is.EqualTo(plain.LocalAngularAcceleration));
            Assert.That(AircraftCondition.Intact, Is.EqualTo(new AircraftCondition()));
            Assert.That(default(AircraftCondition).Thrust, Is.EqualTo(1f));
        }

        [Test]
        public void TheConditionScalesLiftRudderAndThrust()
        {
            var condition = new AircraftCondition(leftLift: 0.5f, rightLift: 1f, control: 0.5f, thrust: 0.25f);

            var climb = AtAngle(5f, 40f);
            var across = new Vector3(0f, climb.Z, -climb.Y) / climb.Length();

            var level = AircraftFlight.Compute(_spec, new VehicleMotorInput(1f), new Vector3(0f, 0f, 100f), Vector3.Zero, 10f, default, condition);
            var lifting = AircraftFlight.Compute(_spec, VehicleMotorInput.Idle, climb, Vector3.Zero, 10f, default, condition);
            var turning = AircraftFlight.Compute(
                _spec, new VehicleMotorInput(0f, 1f, 1f, -1f), new Vector3(0f, 0f, 20f), Vector3.Zero, 0f, default, condition);

            Assert.That(condition.Lift, Is.EqualTo(0.75f).Within(_epsilon));
            Assert.That(Vector3.Dot(lifting.LocalAcceleration, across), Is.EqualTo(3.75f).Within(_epsilon), "lift is the sum of both wings");
            Assert.That(lifting.LocalAngularAcceleration.Z, Is.EqualTo(0.625f).Within(_epsilon), "the stronger right wing rolls it left");
            Assert.That(level.LocalAcceleration.Z, Is.EqualTo(-7.5f).Within(_epsilon), "a quarter of the thrust loses to the drag at top speed");
            Assert.That(level.LocalAcceleration.X, Is.EqualTo(0f).Within(_epsilon));
            AssertNear(turning.LocalAngularAcceleration, new Vector3(-1.570796f, -0.785398f, -3.141593f));
        }

        [Test]
        public void TheConditionRejectsMultipliersOutsideZeroAndOne()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftCondition(leftLift: -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftCondition(rightLift: 1.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftCondition(control: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftCondition(thrust: 2f));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new AircraftGroundContact(true, new Vector3(float.NaN, 0f, 0f), Vector3.Zero));
        }

        [Test]
        public void TheCommandCarriesTheFlightState()
        {
            var command = AircraftFlight.Compute(_spec, new VehicleMotorInput(1f), AtAngle(5f, 40f), Vector3.Zero, 10f);

            Assert.That(command.State.Airspeed, Is.EqualTo(40f).Within(_epsilon));
            Assert.That(command.State.AngleOfAttackRadians, Is.EqualTo((float)(5.0 * Math.PI / 180.0)).Within(_epsilon));
            Assert.That(command.State.SideslipRadians, Is.EqualTo(0f).Within(_epsilon));
            Assert.That(command.State.LoadFactor, Is.EqualTo(0.514221f).Within(_epsilon), "half the weight across the wind, tilted up, plus the drag");
            Assert.That(command.State.Grounded, Is.False);
        }

        private static Vector3 AtAngle(float degrees, float speed)
        {
            var radians = degrees * Math.PI / 180.0;
            return new Vector3(0f, (float)(-speed * Math.Sin(radians)), (float)(speed * Math.Cos(radians)));
        }

        private static void AssertNear(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.X, Is.EqualTo(expected.X).Within(_epsilon), "x");
            Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(_epsilon), "y");
            Assert.That(actual.Z, Is.EqualTo(expected.Z).Within(_epsilon), "z");
        }
    }
}
