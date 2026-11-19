using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class HelicopterFlightTests
    {
        private const float _epsilon = 1e-4f;
        private const float _gravity = 10f;

        private static readonly Vector3 _north = Vector3.UnitZ;
        private static readonly Vector3 _east = Vector3.UnitX;
        private static readonly Vector3 _up = Vector3.UnitY;

        private readonly HelicopterSpec _spec = new HelicopterSpec(
            mass: 1000f,
            maxSpeed: 20f,
            horizontalResponse: 2f,
            maxHorizontalAcceleration: 10f,
            maxClimbRate: 5f,
            maxDescentRate: 4f,
            verticalResponse: 2f,
            maxVerticalAcceleration: 6f,
            groundEffectHeight: 4f,
            groundEffectBoost: 0.5f,
            maxTiltAngle: 30f,
            tiltStiffness: 40f,
            tiltDamping: 10f,
            maxYawRate: 90f,
            yawResponse: 4f,
            yawAimAngle: 45f,
            spoolSeconds: 2f);

        [Test]
        public void TheAxesAreCollectivePitchRollYawEngine()
        {
            Assert.That(HelicopterFlight.CollectiveAxis, Is.EqualTo(0));
            Assert.That(HelicopterFlight.PitchAxis, Is.EqualTo(1));
            Assert.That(HelicopterFlight.RollAxis, Is.EqualTo(2));
            Assert.That(HelicopterFlight.YawAxis, Is.EqualTo(3));
            Assert.That(HelicopterFlight.EngineAxis, Is.EqualTo(4));
        }

        [Test]
        public void TheDefaultSpecKeepsTheDefaults()
        {
            var spec = HelicopterSpec.Default;

            Assert.That(spec.Mass, Is.EqualTo(3000f));
            Assert.That(spec.MaxSpeed, Is.EqualTo(30f));
            Assert.That(spec.HorizontalResponse, Is.EqualTo(1.5f));
            Assert.That(spec.MaxHorizontalAcceleration, Is.EqualTo(8f));
            Assert.That(spec.MaxClimbRate, Is.EqualTo(8f));
            Assert.That(spec.MaxDescentRate, Is.EqualTo(6f));
            Assert.That(spec.VerticalResponse, Is.EqualTo(3f));
            Assert.That(spec.MaxVerticalAcceleration, Is.EqualTo(6f));
            Assert.That(spec.GroundEffectHeight, Is.EqualTo(4f));
            Assert.That(spec.GroundEffectBoost, Is.EqualTo(0.5f));
            Assert.That(spec.MaxTiltAngle, Is.EqualTo(20f));
            Assert.That(spec.TiltStiffness, Is.EqualTo(40f));
            Assert.That(spec.TiltDamping, Is.EqualTo(12f));
            Assert.That(spec.MaxYawRate, Is.EqualTo(90f));
            Assert.That(spec.YawResponse, Is.EqualTo(4f));
            Assert.That(spec.YawAimAngle, Is.EqualTo(30f));
            Assert.That(spec.SpoolSeconds, Is.EqualTo(2f));
        }

        [Test]
        public void TheSpecRejectsValuesOutOfRange()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new HelicopterSpec(mass: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HelicopterSpec(maxSpeed: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HelicopterSpec(horizontalResponse: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HelicopterSpec(maxHorizontalAcceleration: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HelicopterSpec(maxClimbRate: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HelicopterSpec(maxDescentRate: float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HelicopterSpec(verticalResponse: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HelicopterSpec(maxVerticalAcceleration: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HelicopterSpec(groundEffectHeight: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HelicopterSpec(groundEffectBoost: -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HelicopterSpec(maxTiltAngle: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HelicopterSpec(maxTiltAngle: 61f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HelicopterSpec(tiltStiffness: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HelicopterSpec(tiltDamping: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HelicopterSpec(maxYawRate: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HelicopterSpec(yawResponse: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HelicopterSpec(yawAimAngle: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HelicopterSpec(yawAimAngle: 181f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HelicopterSpec(spoolSeconds: -1f));
        }

        [Test]
        public void NeutralHoversInPlace()
        {
            var command = Level(Engine(0f, 0f, 0f, 0f), Vector3.Zero);

            AssertVector(command.Acceleration, new Vector3(0f, _gravity, 0f), "the rotor lifts exactly the weight");
            AssertVector(command.AngularAcceleration, Vector3.Zero, "a level helicopter stays level");
            Assert.That(command.Upright, Is.EqualTo(1f));
            Assert.That(command.GroundEffect, Is.EqualTo(0f));
        }

        [Test]
        public void NeutralBrakesADriftToAStop()
        {
            var command = Level(Engine(0f, 0f, 0f, 0f), new Vector3(3f, -1f, 0f));

            Assert.That(command.Acceleration.X, Is.EqualTo(-6f).Within(_epsilon));
            Assert.That(command.Acceleration.Y, Is.EqualTo(_gravity + 2f).Within(_epsilon), "the sink is caught");
            Assert.That(command.Acceleration.Z, Is.EqualTo(0f).Within(_epsilon));
        }

        [Test]
        public void TheCollectiveClimbsAndDescends()
        {
            var up = Level(Engine(1f, 0f, 0f, 0f), Vector3.Zero);
            var down = Level(Engine(-1f, 0f, 0f, 0f), Vector3.Zero);
            var easing = Level(Engine(0.5f, 0f, 0f, 0f), new Vector3(0f, 5f, 0f));
            var atTheClimbRate = Level(Engine(1f, 0f, 0f, 0f), new Vector3(0f, 5f, 0f));

            Assert.That(up.Acceleration.Y, Is.EqualTo(_gravity + 6f).Within(_epsilon), "5 m/s × 2 is capped at 6 m/s²");
            Assert.That(down.Acceleration.Y, Is.EqualTo(_gravity - 6f).Within(_epsilon));
            Assert.That(easing.Acceleration.Y, Is.EqualTo(_gravity - 5f).Within(_epsilon), "2.5 m/s wanted while climbing at 5");
            Assert.That(atTheClimbRate.Acceleration.Y, Is.EqualTo(_gravity).Within(_epsilon), "no faster than the climb rate");
        }

        [Test]
        public void TheRotorNeverPullsDown()
        {
            var command = Level(Engine(-1f, 0f, 0f, 0f), new Vector3(0f, 20f, 0f), gravity: 2f);

            Assert.That(command.Acceleration.Y, Is.EqualTo(0f));
        }

        [Test]
        public void TheGroundEffectHelpsOnlyWhenPushingUp()
        {
            var lifting = Level(Engine(1f, 0f, 0f, 0f), Vector3.Zero, groundHeight: 1f);
            var neutral = Level(Engine(0f, 0f, 0f, 0f), Vector3.Zero, groundHeight: 1f);
            var landing = Level(Engine(0f, 0f, 0f, 0f), new Vector3(0f, -2f, 0f), groundHeight: 2f);
            var descending = Level(Engine(-1f, 0f, 0f, 0f), Vector3.Zero, groundHeight: 1f);

            Assert.That(lifting.GroundEffect, Is.EqualTo(0.75f).Within(_epsilon));
            Assert.That(lifting.Acceleration.Y, Is.EqualTo(_gravity + (6f * 1.375f)).Within(_epsilon));
            Assert.That(neutral.Acceleration.Y, Is.EqualTo(_gravity).Within(_epsilon), "a parked helicopter does not float up");
            Assert.That(landing.Acceleration.Y, Is.EqualTo(_gravity + (4f * 1.25f)).Within(_epsilon), "the cushion slows the touchdown");
            Assert.That(descending.Acceleration.Y, Is.EqualTo(_gravity - 6f).Within(_epsilon));
        }

        [Test]
        public void TheGroundEffectFadesWithHeight()
        {
            Assert.That(HelicopterFlight.GroundEffect(_spec, 0f), Is.EqualTo(1f));
            Assert.That(HelicopterFlight.GroundEffect(_spec, -0.5f), Is.EqualTo(1f));
            Assert.That(HelicopterFlight.GroundEffect(_spec, 2f), Is.EqualTo(0.5f).Within(_epsilon));
            Assert.That(HelicopterFlight.GroundEffect(_spec, 4f), Is.EqualTo(0f));
            Assert.That(HelicopterFlight.GroundEffect(_spec, float.PositiveInfinity), Is.EqualTo(0f));
            Assert.That(HelicopterFlight.GroundEffect(new HelicopterSpec(groundEffectHeight: 0f), 0f), Is.EqualTo(0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => HelicopterFlight.GroundEffect(_spec, float.NaN));
        }

        [Test]
        public void TiltingForwardAcceleratesTowardTheNose()
        {
            var north = Level(Engine(0f, 1f, 0f, 0f), Vector3.Zero);
            var east = HelicopterFlight.Compute(_spec, Engine(0f, 1f, 0f, 0f), 1f, _east, _up, Vector3.Zero, Vector3.Zero, _gravity);
            var back = Level(Engine(0f, -0.25f, 0f, 0f), Vector3.Zero);

            AssertVector(north.Acceleration, new Vector3(0f, _gravity, 10f), "20 m/s × 2 is capped at 10 m/s²");
            AssertVector(east.Acceleration, new Vector3(10f, _gravity, 0f), "facing east, forward is east");
            AssertVector(back.Acceleration, new Vector3(0f, _gravity, -10f), "5 m/s back × 2");
        }

        [Test]
        public void TiltingSidewaysAcceleratesToThatSide()
        {
            var right = Level(Engine(0f, 0f, 1f, 0f), Vector3.Zero);
            var left = HelicopterFlight.Compute(_spec, Engine(0f, 0f, -1f, 0f), 1f, _east, _up, Vector3.Zero, Vector3.Zero, _gravity);
            var diagonal = Level(Engine(0f, 1f, 1f, 0f), Vector3.Zero);

            AssertVector(right.Acceleration, new Vector3(10f, _gravity, 0f));
            AssertVector(left.Acceleration, new Vector3(0f, _gravity, 10f), "facing east, left is north");
            var half = 10f / (float)Math.Sqrt(2.0);
            AssertVector(diagonal.Acceleration, new Vector3(half, _gravity, half), "a diagonal is no faster than straight");
        }

        [Test]
        public void TheSpeedTopsOutAtTheMaximum()
        {
            var atTop = Level(Engine(0f, 1f, 0f, 0f), new Vector3(0f, 0f, 20f));
            var past = Level(Engine(0f, 1f, 0f, 0f), new Vector3(0f, 0f, 25f));
            var diagonalAtTop = Level(Engine(0f, 1f, 1f, 0f), Vector3.Normalize(new Vector3(1f, 0f, 1f)) * 20f);

            AssertVector(atTop.Acceleration, new Vector3(0f, _gravity, 0f), "full tilt at top speed pushes no more");
            Assert.That(past.Acceleration.Z, Is.EqualTo(-10f).Within(_epsilon), "faster than the top speed slows down");
            AssertVector(diagonalAtTop.Acceleration, new Vector3(0f, _gravity, 0f));
        }

        [Test]
        public void TheStickTiltsTheBodyAndTheTiltSettles()
        {
            var forward = Level(Engine(0f, 1f, 0f, 0f), Vector3.Zero);
            var right = Level(Engine(0f, 0f, 1f, 0f), Vector3.Zero);
            var tiltedForward = HelicopterFlight.Compute(
                _spec,
                Engine(0f, 1f, 0f, 0f),
                1f,
                new Vector3(0f, -0.5f, (float)Math.Sqrt(0.75)),
                new Vector3(0f, (float)Math.Sqrt(0.75), 0.5f),
                Vector3.Zero,
                Vector3.Zero,
                _gravity);
            var swinging = HelicopterFlight.Compute(_spec, Engine(0f, 0f, 0f, 0f), 1f, _north, _up, Vector3.Zero, new Vector3(1f, 0f, -2f), _gravity);

            AssertVector(forward.AngularAcceleration, new Vector3(20f, 0f, 0f), "sin 30 × 40 about the right axis tips the nose down");
            AssertVector(right.AngularAcceleration, new Vector3(0f, 0f, -20f), "rolling right turns about -z");
            AssertVector(tiltedForward.AngularAcceleration, Vector3.Zero, "already at the full tilt");
            Assert.That(tiltedForward.Upright, Is.EqualTo(1f).Within(_epsilon), "the full tilt keeps the full lift");
            AssertVector(swinging.AngularAcceleration, new Vector3(-10f, 0f, 20f), "the damping stops the swing");
        }

        [Test]
        public void TheYawTurnsAtTheRate()
        {
            var turning = Level(Engine(0f, 0f, 0f, 1f), Vector3.Zero);
            var atRate = HelicopterFlight.Compute(
                _spec, Engine(0f, 0f, 0f, 1f), 1f, _north, _up, Vector3.Zero, new Vector3(0f, (float)(Math.PI / 2.0), 0f), _gravity);
            var released = HelicopterFlight.Compute(
                _spec, Engine(0f, 0f, 0f, 0f), 1f, _north, _up, Vector3.Zero, new Vector3(0f, 1f, 0f), _gravity);

            Assert.That(turning.AngularAcceleration.Y, Is.EqualTo((float)(Math.PI * 2.0)).Within(_epsilon), "90 deg/s × 4");
            Assert.That(atRate.AngularAcceleration.Y, Is.EqualTo(0f).Within(_epsilon));
            Assert.That(released.AngularAcceleration.Y, Is.EqualTo(-4f).Within(_epsilon));
        }

        [Test]
        public void AStoppedRotorNeitherLiftsNorSteers()
        {
            var stopped = HelicopterFlight.Compute(_spec, Engine(1f, 1f, 1f, 1f), 0f, _north, _up, Vector3.Zero, Vector3.Zero, _gravity);
            var half = HelicopterFlight.Compute(_spec, Engine(0f, 0f, 0f, 0f), 0.5f, _north, _up, Vector3.Zero, Vector3.Zero, _gravity);

            AssertVector(stopped.Acceleration, Vector3.Zero);
            AssertVector(stopped.AngularAcceleration, Vector3.Zero);
            Assert.That(half.Acceleration.Y, Is.EqualTo(_gravity * 0.5f).Within(_epsilon));
        }

        [Test]
        public void TippingPastTheTiltFadesTheLift()
        {
            var sixty = new Vector3((float)Math.Sin(Math.PI / 3.0), 0.5f, 0f);

            Assert.That(HelicopterFlight.Upright(_spec, _up), Is.EqualTo(1f));
            Assert.That(HelicopterFlight.Upright(_spec, sixty), Is.EqualTo(0.5f / (float)Math.Cos(Math.PI / 6.0)).Within(_epsilon));
            Assert.That(HelicopterFlight.Upright(_spec, _east), Is.EqualTo(0f).Within(_epsilon));
            Assert.That(HelicopterFlight.Upright(_spec, -_up), Is.EqualTo(0f));

            var onItsSide = HelicopterFlight.Compute(_spec, Engine(1f, 0f, 0f, 0f), 1f, _north, -_east, Vector3.Zero, Vector3.Zero, _gravity);
            AssertVector(onItsSide.Acceleration, Vector3.Zero, "a helicopter on its side does not fly");
        }

        [Test]
        public void TheRotorSpoolsUpAndDown()
        {
            Assert.That(HelicopterFlight.StepRotor(_spec, 0f, true, 0.5f), Is.EqualTo(0.25f).Within(_epsilon));
            Assert.That(HelicopterFlight.StepRotor(_spec, 0.9f, true, 1f), Is.EqualTo(1f));
            Assert.That(HelicopterFlight.StepRotor(_spec, 1f, false, 0.5f), Is.EqualTo(0.75f).Within(_epsilon));
            Assert.That(HelicopterFlight.StepRotor(_spec, 0.1f, false, 1f), Is.EqualTo(0f));
            Assert.That(HelicopterFlight.StepRotor(new HelicopterSpec(spoolSeconds: 0f), 0f, true, 0f), Is.EqualTo(1f));
            Assert.That(HelicopterFlight.StepRotor(new HelicopterSpec(spoolSeconds: 0f), 1f, false, 0f), Is.EqualTo(0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => HelicopterFlight.StepRotor(_spec, 0f, true, -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => HelicopterFlight.StepRotor(_spec, 1.5f, true, 0.1f));
        }

        [Test]
        public void TheYawFollowsTheAimAroundTheHeading()
        {
            var quarter = Math.PI / 8.0;

            Assert.That(HelicopterFlight.YawToward(_spec, _north, _up, _north), Is.EqualTo(0f).Within(_epsilon));
            Assert.That(
                HelicopterFlight.YawToward(_spec, _north, _up, new Vector3((float)Math.Sin(quarter), 0f, (float)Math.Cos(quarter))),
                Is.EqualTo(0.5f).Within(_epsilon),
                "22.5 of 45 degrees to the right");
            Assert.That(HelicopterFlight.YawToward(_spec, _north, _up, _east), Is.EqualTo(1f));
            Assert.That(HelicopterFlight.YawToward(_spec, _north, _up, -_north), Is.EqualTo(1f).Or.EqualTo(-1f));
            Assert.That(HelicopterFlight.YawToward(_spec, _east, _up, _north), Is.EqualTo(-1f), "facing east, north is to the left");
            Assert.That(HelicopterFlight.YawToward(_spec, _north, _up, new Vector3(1f, -20f, 1f)), Is.EqualTo(1f), "looking down still turns");
            Assert.That(HelicopterFlight.YawToward(_spec, _north, _up, -_up), Is.EqualTo(0f), "straight down has no heading");
        }

        [Test]
        public void TheHeadingComesFromTheUpWhenTheNoseIsVertical()
        {
            AssertVector(HelicopterFlight.Heading(new Vector3(0f, -1f, 0f), _north), _north, "nose down, the roof faces where the nose was");
            AssertVector(HelicopterFlight.Heading(_up, -_north), _north, "nose up, the roof faces back");
            AssertVector(HelicopterFlight.Heading(_up, _up), _north);
            AssertVector(HelicopterFlight.Heading(new Vector3(3f, 1f, 0f), _up), _east);
        }

        [Test]
        public void ComputeRejectsBadArguments()
        {
            Assert.Throws<ArgumentNullException>(() => HelicopterFlight.Compute(null, VehicleMotorInput.Idle, 1f, _north, _up, Vector3.Zero, Vector3.Zero, _gravity));
            Assert.Throws<ArgumentOutOfRangeException>(() => HelicopterFlight.Compute(_spec, VehicleMotorInput.Idle, 1.1f, _north, _up, Vector3.Zero, Vector3.Zero, _gravity));
            Assert.Throws<ArgumentOutOfRangeException>(() => HelicopterFlight.Compute(_spec, VehicleMotorInput.Idle, 1f, _north, _up, Vector3.Zero, Vector3.Zero, -1f));
            Assert.Throws<ArgumentException>(() => HelicopterFlight.Compute(_spec, VehicleMotorInput.Idle, 1f, _north, _up, new Vector3(float.NaN, 0f, 0f), Vector3.Zero, _gravity));
            Assert.Throws<ArgumentOutOfRangeException>(() => HelicopterFlight.Compute(_spec, VehicleMotorInput.Idle, 1f, _north, _up, Vector3.Zero, Vector3.Zero, _gravity, float.NaN));
        }

        private HelicopterFlightCommand Level(VehicleMotorInput input, Vector3 velocity, float gravity = _gravity, float groundHeight = float.PositiveInfinity)
        {
            return HelicopterFlight.Compute(_spec, input, 1f, _north, _up, velocity, Vector3.Zero, gravity, groundHeight);
        }

        private static VehicleMotorInput Engine(float collective, float pitch, float roll, float yaw)
        {
            return new VehicleMotorInput(collective, pitch, roll, yaw, 1f);
        }

        private static void AssertVector(Vector3 actual, Vector3 expected, string message = "")
        {
            Assert.That(actual.X, Is.EqualTo(expected.X).Within(_epsilon), message + " (x)");
            Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(_epsilon), message + " (y)");
            Assert.That(actual.Z, Is.EqualTo(expected.Z).Within(_epsilon), message + " (z)");
        }
    }
}
