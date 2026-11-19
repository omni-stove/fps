using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class AircraftAeroTests
    {
        private const float _epsilon = 1e-4f;
        private const float _gravity = 10f;

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
        public void TheCoefficientGrowsWithTheAngleUpToTheCriticalAngle()
        {
            Assert.That(_spec.Aero.MaxLiftCoefficient, Is.EqualTo(1f).Within(_epsilon));
            Assert.That(AircraftAero.LiftCoefficient(_spec.Aero, 0f), Is.EqualTo(0f));
            Assert.That(AircraftAero.LiftCoefficient(_spec.Aero, Radians(5f)), Is.EqualTo(0.5f).Within(_epsilon));
            Assert.That(AircraftAero.LiftCoefficient(_spec.Aero, Radians(-5f)), Is.EqualTo(-0.5f).Within(_epsilon), "air from above pushes down");
            Assert.That(AircraftAero.LiftCoefficient(_spec.Aero, Radians(9.9f)), Is.EqualTo(0.99f).Within(_epsilon));
        }

        [Test]
        public void TheCoefficientDropsPastTheCriticalAngle()
        {
            var before = AircraftAero.LiftCoefficient(_spec.Aero, Radians(9.9f));
            var after = AircraftAero.LiftCoefficient(_spec.Aero, Radians(10.5f));

            Assert.That(after, Is.LessThan(before));
            Assert.That(after, Is.EqualTo(0.179184f).Within(_epsilon), "0.5 x sin(21 deg)");
            Assert.That(AircraftAero.LiftCoefficient(_spec.Aero, Radians(-10.5f)), Is.EqualTo(-0.179184f).Within(_epsilon));
            Assert.That(AircraftAero.LiftCoefficient(_spec.Aero, Radians(45f)), Is.EqualTo(0.5f).Within(_epsilon), "the post-stall peak");
            Assert.That(AircraftAero.LiftCoefficient(_spec.Aero, Radians(90f)), Is.EqualTo(0f).Within(_epsilon), "flat against the wind");
        }

        [Test]
        public void PullingUpLoadsMoreThanOneG()
        {
            var level = AircraftFlight.Compute(_spec, VehicleMotorInput.Idle, AtAngle(0f, 60f), Vector3.Zero, _gravity);
            var pulled = AircraftFlight.Compute(_spec, VehicleMotorInput.Idle, AtAngle(8f, 60f), Vector3.Zero, _gravity);

            Assert.That(level.State.LoadFactor, Is.EqualTo(0f).Within(_epsilon), "no angle of attack, no lift");
            Assert.That(pulled.LiftRatio, Is.EqualTo(1.8f).Within(1e-3f), "0.8 of the top coefficient at 1.5 times the lift speed");
            Assert.That(pulled.State.LoadFactor, Is.GreaterThan(1f));
            Assert.That(pulled.State.LoadFactor, Is.EqualTo(1.852626f).Within(1e-3f));
        }

        [Test]
        public void PastTheCriticalAngleTheWingsCannotHoldTheWeight()
        {
            var climbing = AircraftFlight.Compute(_spec, VehicleMotorInput.Idle, AtAngle(9.5f, 60f), Vector3.Zero, _gravity);
            var stalled = AircraftFlight.Compute(_spec, VehicleMotorInput.Idle, AtAngle(12f, 60f), Vector3.Zero, _gravity);

            Assert.That(climbing.LiftRatio, Is.EqualTo(2.1375f).Within(1e-3f));
            Assert.That(stalled.LiftRatio, Is.EqualTo(0.457579f).Within(1e-3f));
            Assert.That(stalled.State.LoadFactor, Is.LessThan(1f));
            Assert.That(stalled.State.LoadFactor, Is.EqualTo(0.524363f).Within(1e-3f));
        }

        [Test]
        public void TheLiftIsNotCappedAtTheWeight()
        {
            Assert.That(AircraftAero.LiftRatio(_spec.Aero, AtAngle(5f, 40f)), Is.EqualTo(0.5f).Within(_epsilon));
            Assert.That(AircraftAero.LiftRatio(_spec.Aero, AtAngle(5f, 20f)), Is.EqualTo(0.125f).Within(_epsilon));
            Assert.That(AircraftAero.LiftRatio(_spec.Aero, AtAngle(8f, 80f)), Is.EqualTo(3.2f).Within(1e-3f));
            Assert.That(AircraftAero.LiftRatio(_spec.Aero, AtAngle(-5f, 40f)), Is.EqualTo(-0.5f).Within(_epsilon));
        }

        [Test]
        public void TheLiftStandsAcrossTheWindAndTheSideFlowDoesNotLift()
        {
            var velocity = AtAngle(5f, 40f);
            var across = Across(velocity);

            var forces = AircraftAero.Compute(_spec, VehicleMotorInput.Idle, velocity, Vector3.Zero, _gravity);
            var slipping = AircraftAero.LiftRatio(_spec.Aero, velocity + new Vector3(30f, 0f, 0f));

            Assert.That(Vector3.Dot(forces.LocalAcceleration, across), Is.EqualTo(5f).Within(_epsilon), "half the weight");
            Assert.That(slipping, Is.EqualTo(0.5f).Within(_epsilon));
        }

        [Test]
        public void TheInducedDragGrowsWithTheSquareOfTheLift()
        {
            var parasitic = _spec.DragCoefficient * 60f * 60f;

            var gentle = AlongDrag(AtAngle(4f, 60f));
            var hard = AlongDrag(AtAngle(8f, 60f));

            Assert.That(parasitic, Is.EqualTo(3.6f).Within(_epsilon));
            Assert.That(gentle, Is.EqualTo(3.96f).Within(1e-3f), "0.1 x 0.4 x 9 on top of the parasitic drag");
            Assert.That(hard, Is.EqualTo(5.04f).Within(1e-3f), "0.1 x 0.8 x 18 on top of the parasitic drag");
            Assert.That((hard - parasitic) / (gentle - parasitic), Is.EqualTo(4f).Within(1e-2f), "twice the lift, four times the drag");
        }

        [Test]
        public void ASideslipTurnsTheNoseIntoTheWindAndLevelsTheWings()
        {
            var right = AircraftAero.Compute(_spec, VehicleMotorInput.Idle, Slipping(0.1f, 40f), Vector3.Zero, _gravity);
            var left = AircraftAero.Compute(_spec, VehicleMotorInput.Idle, Slipping(-0.1f, 40f), Vector3.Zero, _gravity);

            AssertNear(right.LocalAngularAcceleration, new Vector3(0f, 0.2f, 0.1f));
            AssertNear(left.LocalAngularAcceleration, new Vector3(0f, -0.2f, -0.1f));
        }

        [Test]
        public void TheWeathervaneAndDihedralGrowWithTheSquareOfSpeed()
        {
            var forces = AircraftAero.Compute(_spec, VehicleMotorInput.Idle, Slipping(0.1f, 80f), Vector3.Zero, _gravity);

            AssertNear(forces.LocalAngularAcceleration, new Vector3(0f, 0.8f, 0.4f));
        }

        [Test]
        public void AWingAtHalfLiftRollsTowardItself()
        {
            var velocity = AtAngle(5f, 40f);

            var intact = AircraftAero.Compute(_spec, VehicleMotorInput.Idle, velocity, Vector3.Zero, _gravity);
            var leftHurt = AircraftAero.Compute(
                _spec, VehicleMotorInput.Idle, velocity, Vector3.Zero, _gravity, new AircraftCondition(leftLift: 0.5f));
            var rightHurt = AircraftAero.Compute(
                _spec, VehicleMotorInput.Idle, velocity, Vector3.Zero, _gravity, new AircraftCondition(rightLift: 0.5f));

            AssertNear(intact.LocalAngularAcceleration, Vector3.Zero);
            AssertNear(leftHurt.LocalAngularAcceleration, new Vector3(0f, 0f, 0.625f));
            AssertNear(rightHurt.LocalAngularAcceleration, new Vector3(0f, 0f, -0.625f));
            Assert.That(Vector3.Dot(leftHurt.LocalAcceleration, Across(velocity)), Is.EqualTo(3.75f).Within(_epsilon));
        }

        [Test]
        public void WithoutGravityTheWingsDoNotLift()
        {
            var forces = AircraftAero.Compute(_spec, VehicleMotorInput.Idle, AtAngle(5f, 40f), Vector3.Zero, 0f, new AircraftCondition(leftLift: 0f));

            Assert.That(forces.LiftRatio, Is.EqualTo(0.5f).Within(_epsilon));
            Assert.That(Vector3.Dot(forces.LocalAcceleration, Across(AtAngle(5f, 40f))), Is.EqualTo(0f).Within(_epsilon));
            Assert.That(forces.LocalAngularAcceleration.Z, Is.EqualTo(0f).Within(_epsilon));
        }

        [Test]
        public void TheAeroRejectsBadArguments()
        {
            Assert.Throws<ArgumentNullException>(() => AircraftAero.LiftCoefficient(null, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => AircraftAero.LiftCoefficient(_spec.Aero, float.NaN));
            Assert.Throws<ArgumentNullException>(() => AircraftAero.LiftRatio(null, Vector3.Zero));
            Assert.Throws<ArgumentOutOfRangeException>(() => AircraftAero.LiftRatio(_spec.Aero, new Vector3(0f, float.NaN, 10f)));
        }

        [Test]
        public void TheSpecKeepsTheWingDefaultsAndRejectsBadValues()
        {
            var aero = AircraftAeroSpec.Default;

            Assert.That(aero.LiftSlope, Is.EqualTo(AircraftAeroSpec.DefaultLiftSlope));
            Assert.That(aero.CriticalAngle, Is.EqualTo(AircraftAeroSpec.DefaultCriticalAngle));
            Assert.That(aero.PostStallLift, Is.EqualTo(AircraftAeroSpec.DefaultPostStallLift));
            Assert.That(aero.InducedDrag, Is.EqualTo(AircraftAeroSpec.DefaultInducedDrag));
            Assert.That(aero.Weathervane, Is.EqualTo(AircraftAeroSpec.DefaultWeathervane));
            Assert.That(aero.Dihedral, Is.EqualTo(AircraftAeroSpec.DefaultDihedral));
            Assert.That(aero.WingRoll, Is.EqualTo(AircraftAeroSpec.DefaultWingRoll));
            Assert.That(new AircraftAeroSpec(criticalAngle: 89f).CriticalAngle, Is.EqualTo(89f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftAeroSpec(liftSlope: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftAeroSpec(criticalAngle: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftAeroSpec(criticalAngle: 90f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftAeroSpec(criticalAngle: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftAeroSpec(postStallLift: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftAeroSpec(inducedDrag: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftAeroSpec(weathervane: float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftAeroSpec(dihedral: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftAeroSpec(wingRoll: -0.5f));
        }

        [Test]
        public void TheDefaultAircraftLiftsOffJustAboveThirtyFiveMetersPerSecond()
        {
            var spec = AircraftSpec.Default;

            var slow = AircraftAero.LiftRatio(spec.Aero, AtAngle(15f, 35f));
            var fast = AircraftAero.LiftRatio(spec.Aero, AtAngle(15f, 40f));

            Assert.That(slow, Is.LessThan(1f));
            Assert.That(fast, Is.GreaterThan(1f));
            Assert.That(AircraftAero.LiftRatio(spec.Aero, AtAngle(AircraftAeroSpec.DefaultCriticalAngle - 0.1f, 35.2f)), Is.GreaterThan(1f));
        }

        private float AlongDrag(Vector3 velocity)
        {
            var forces = AircraftAero.Compute(_spec, VehicleMotorInput.Idle, velocity, Vector3.Zero, _gravity);
            return -Vector3.Dot(forces.LocalAcceleration, Vector3.Normalize(velocity));
        }

        private static float Radians(float degrees)
        {
            return (float)(degrees * Math.PI / 180.0);
        }

        private static Vector3 AtAngle(float degrees, float speed)
        {
            var radians = degrees * Math.PI / 180.0;
            return new Vector3(0f, (float)(-speed * Math.Sin(radians)), (float)(speed * Math.Cos(radians)));
        }

        private static Vector3 Slipping(float radians, float speed)
        {
            return new Vector3((float)(speed * Math.Sin(radians)), 0f, (float)(speed * Math.Cos(radians)));
        }

        private static Vector3 Across(Vector3 velocity)
        {
            var plane = new Vector3(0f, velocity.Y, velocity.Z);
            return new Vector3(0f, velocity.Z, -velocity.Y) / plane.Length();
        }

        private static void AssertNear(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.X, Is.EqualTo(expected.X).Within(_epsilon), "x");
            Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(_epsilon), "y");
            Assert.That(actual.Z, Is.EqualTo(expected.Z).Within(_epsilon), "z");
        }
    }
}
