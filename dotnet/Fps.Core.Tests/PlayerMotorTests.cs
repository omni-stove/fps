using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class PlayerMotorTests
    {
        private const float Dt = 1f / 60f;
        private const float Tolerance = 1e-4f;

        private static MotorSpec Spec(float airControl = 0.5f)
        {
            return new MotorSpec(walkSpeed: 4f, sprintSpeed: 8f, jumpHeight: 1.5f, gravity: 9.81f, airControl: airControl);
        }

        [Test]
        public void Yaw0MovesForwardAlongPositiveZ()
        {
            var motor = new PlayerMotor(Spec());

            var d = motor.Step(new Vector2(0f, 1f), 0f, false, false, true, 1f);

            Assert.That(d.X, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(d.Z, Is.EqualTo(4f).Within(Tolerance));
        }

        [Test]
        public void Yaw0MovesRightAlongPositiveX()
        {
            var motor = new PlayerMotor(Spec());

            var d = motor.Step(new Vector2(1f, 0f), 0f, false, false, true, 1f);

            Assert.That(d.X, Is.EqualTo(4f).Within(Tolerance));
            Assert.That(d.Z, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void Yaw90MovesForwardAlongPositiveX()
        {
            var motor = new PlayerMotor(Spec());

            var d = motor.Step(new Vector2(0f, 1f), (float)(Math.PI / 2), false, false, true, 1f);

            Assert.That(d.X, Is.EqualTo(4f).Within(Tolerance));
            Assert.That(d.Z, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void Yaw90MovesRightAlongNegativeZ()
        {
            var motor = new PlayerMotor(Spec());

            var d = motor.Step(new Vector2(1f, 0f), (float)(Math.PI / 2), false, false, true, 1f);

            Assert.That(d.X, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(d.Z, Is.EqualTo(-4f).Within(Tolerance));
        }

        [Test]
        public void DiagonalIsNotFaster()
        {
            var motor = new PlayerMotor(Spec());

            motor.Step(new Vector2(1f, 1f), 0.3f, false, false, true, Dt);

            var horizontal = new Vector2(motor.Velocity.X, motor.Velocity.Z);
            Assert.That(horizontal.Length(), Is.EqualTo(4f).Within(Tolerance));
        }

        [Test]
        public void PartialInputIsNotScaledUp()
        {
            var motor = new PlayerMotor(Spec());

            motor.Step(new Vector2(0f, 0.5f), 0f, false, false, true, Dt);

            Assert.That(motor.Velocity.Z, Is.EqualTo(2f).Within(Tolerance));
        }

        [Test]
        public void SprintUsesSprintSpeed()
        {
            var motor = new PlayerMotor(Spec());

            var d = motor.Step(new Vector2(0f, 1f), 0f, true, false, true, 1f);

            Assert.That(d.Z, Is.EqualTo(8f).Within(Tolerance));
            Assert.That(motor.Velocity.Z, Is.EqualTo(8f).Within(Tolerance));
        }

        [Test]
        public void GroundedWithoutJumpSticksToGround()
        {
            var motor = new PlayerMotor(Spec());

            var d = motor.Step(Vector2.Zero, 0f, false, false, true, Dt);

            Assert.That(motor.Velocity.Y, Is.EqualTo(PlayerMotor.GroundedVerticalSpeed));
            Assert.That(d.Y, Is.EqualTo(PlayerMotor.GroundedVerticalSpeed * Dt).Within(Tolerance));
        }

        [TestCase(1f / 30f)]
        [TestCase(1f / 60f)]
        [TestCase(1f / 144f)]
        public void JumpReachesJumpHeight(float dt)
        {
            var spec = Spec();
            var motor = new PlayerMotor(spec);
            var jumped = 0;
            motor.Jumped += () => jumped++;

            var height = motor.Step(Vector2.Zero, 0f, false, true, true, dt).Y;
            var peak = height;
            while (motor.Velocity.Y > 0f)
            {
                height += motor.Step(Vector2.Zero, 0f, false, false, false, dt).Y;
                peak = Math.Max(peak, height);
            }

            Assert.That(jumped, Is.EqualTo(1));
            Assert.That(peak, Is.EqualTo(spec.JumpHeight).Within(0.01f));
        }

        [Test]
        public void JumpInitialSpeedMatchesFormula()
        {
            var spec = Spec();
            var motor = new PlayerMotor(spec);

            motor.Step(Vector2.Zero, 0f, false, true, true, 0f);

            Assert.That(motor.Velocity.Y, Is.EqualTo((float)Math.Sqrt(2.0 * spec.Gravity * spec.JumpHeight)).Within(Tolerance));
        }

        [Test]
        public void JumpIsIgnoredInAir()
        {
            var motor = new PlayerMotor(Spec());
            var jumped = 0;
            motor.Jumped += () => jumped++;

            motor.Step(Vector2.Zero, 0f, false, true, false, Dt);

            Assert.That(jumped, Is.Zero);
            Assert.That(motor.Velocity.Y, Is.LessThan(0f));
        }

        [Test]
        public void GravityAcceleratesDownInAir()
        {
            var spec = Spec();
            var motor = new PlayerMotor(spec);

            var first = motor.Step(Vector2.Zero, 0f, false, false, false, 0.5f);
            var second = motor.Step(Vector2.Zero, 0f, false, false, false, 0.5f);

            Assert.That(motor.Velocity.Y, Is.EqualTo(-spec.Gravity).Within(Tolerance));
            Assert.That(first.Y + second.Y, Is.EqualTo(-0.5f * spec.Gravity).Within(Tolerance));
            Assert.That(second.Y, Is.LessThan(first.Y));
        }

        [Test]
        public void ZeroAirControlKeepsHorizontalVelocity()
        {
            var motor = new PlayerMotor(Spec(airControl: 0f));
            motor.Step(new Vector2(0f, 1f), 0f, false, false, true, Dt);

            var d = motor.Step(new Vector2(1f, 0f), 0f, true, false, false, 1f);

            Assert.That(motor.Velocity.X, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(motor.Velocity.Z, Is.EqualTo(4f).Within(Tolerance));
            Assert.That(d.X, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(d.Z, Is.EqualTo(4f).Within(Tolerance));
        }

        [Test]
        public void AirControlBlendsTowardInput()
        {
            var motor = new PlayerMotor(Spec(airControl: 0.25f));
            motor.Step(new Vector2(0f, 1f), 0f, false, false, true, Dt);

            motor.Step(new Vector2(1f, 0f), 0f, false, false, false, Dt);

            Assert.That(motor.Velocity.X, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(motor.Velocity.Z, Is.EqualTo(3f).Within(Tolerance));
        }

        [Test]
        public void FullAirControlFollowsInputImmediately()
        {
            var motor = new PlayerMotor(Spec(airControl: 1f));
            motor.Step(new Vector2(0f, 1f), 0f, false, false, true, Dt);

            motor.Step(new Vector2(1f, 0f), 0f, false, false, false, Dt);

            Assert.That(motor.Velocity.X, Is.EqualTo(4f).Within(Tolerance));
            Assert.That(motor.Velocity.Z, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void ZeroDeltaDoesNotMove()
        {
            var motor = new PlayerMotor(Spec());

            var d = motor.Step(new Vector2(0f, 1f), 0f, true, false, true, 0f);

            Assert.That(d, Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void ResetVelocityClearsVelocity()
        {
            var motor = new PlayerMotor(Spec());
            motor.Step(new Vector2(0f, 1f), 0f, false, true, true, Dt);

            motor.ResetVelocity();

            Assert.That(motor.Velocity, Is.EqualTo(Vector3.Zero));
        }

        [TestCase(-0.001f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void RejectsInvalidDelta(float dt)
        {
            var motor = new PlayerMotor(Spec());

            Assert.Throws<ArgumentOutOfRangeException>(() => motor.Step(Vector2.Zero, 0f, false, false, true, dt));
        }

        [Test]
        public void RejectsNonFiniteInputs()
        {
            var motor = new PlayerMotor(Spec());

            Assert.Throws<ArgumentOutOfRangeException>(() => motor.Step(new Vector2(float.NaN, 0f), 0f, false, false, true, Dt));
            Assert.Throws<ArgumentOutOfRangeException>(() => motor.Step(Vector2.Zero, float.NaN, false, false, true, Dt));
        }

        [Test]
        public void RejectsNullSpec()
        {
            Assert.Throws<ArgumentNullException>(() => new PlayerMotor(null));
        }

        [TestCase(-1f, 8f, 1.5f, 9.81f, 0.5f)]
        [TestCase(4f, -1f, 1.5f, 9.81f, 0.5f)]
        [TestCase(4f, 8f, -1f, 9.81f, 0.5f)]
        [TestCase(4f, 8f, 1.5f, 0f, 0.5f)]
        [TestCase(4f, 8f, 1.5f, -9.81f, 0.5f)]
        [TestCase(4f, 8f, 1.5f, 9.81f, -0.1f)]
        [TestCase(4f, 8f, 1.5f, 9.81f, 1.1f)]
        [TestCase(float.NaN, 8f, 1.5f, 9.81f, 0.5f)]
        [TestCase(4f, 8f, 1.5f, 9.81f, float.NaN)]
        public void SpecRejectsInvalidValues(float walk, float sprint, float jump, float gravity, float air)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MotorSpec(walk, sprint, jump, gravity, air));
        }
    }
}
