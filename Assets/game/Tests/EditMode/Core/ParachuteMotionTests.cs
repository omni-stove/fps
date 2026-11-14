using System;
using System.Numerics;
using Fps.Core;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class ParachuteMotionTests
    {
        private const float Epsilon = 1e-4f;

        [Test]
        public void DefaultsDescendSlowerThanTheSafeFallSpeed()
        {
            var motion = new ParachuteMotion();

            Assert.That(motion.MinOpenHeight, Is.EqualTo(ParachuteMotion.DefaultMinOpenHeight));
            Assert.That(motion.DescentSpeed, Is.EqualTo(ParachuteMotion.DefaultDescentSpeed));
            Assert.That(motion.HorizontalSpeed, Is.EqualTo(ParachuteMotion.DefaultHorizontalSpeed));
            Assert.That(motion.SteerAcceleration, Is.EqualTo(ParachuteMotion.DefaultSteerAcceleration));
            Assert.That(motion.DescentSpeed, Is.LessThan(new MotorSpec(5f, 8f, 1.2f, 20f, 0.1f, 2.5f).SafeFallSpeed));
        }

        [TestCase(0f, 4f, 6f, 8f)]
        [TestCase(float.PositiveInfinity, 4f, 6f, 8f)]
        [TestCase(6f, 0f, 6f, 8f)]
        [TestCase(6f, float.NaN, 6f, 8f)]
        [TestCase(6f, 4f, -1f, 8f)]
        [TestCase(6f, 4f, float.PositiveInfinity, 8f)]
        [TestCase(6f, 4f, 6f, 0f)]
        public void RejectsInvalidValues(float minOpenHeight, float descentSpeed, float horizontalSpeed, float steerAcceleration)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new ParachuteMotion(minOpenHeight, descentSpeed, horizontalSpeed, steerAcceleration));
        }

        [Test]
        public void AllowsNoSidewaysSpeed()
        {
            var motion = new ParachuteMotion(horizontalSpeed: 0f);
            motion.Open(new Vector2(3f, 0f));

            var velocity = motion.Step(new Vector2(0f, 1f), 0f, 1f);

            Assert.That(velocity, Is.EqualTo(new Vector3(0f, -motion.DescentSpeed, 0f)));
        }

        [Test]
        public void OpensOnlyWithTheRuleOnAndHighEnough()
        {
            var motion = new ParachuteMotion(minOpenHeight: 6f);

            Assert.That(motion.Evaluate(Parachute.On, false, false, 6f), Is.EqualTo(ParachuteOpenVerdict.Open));
            Assert.That(motion.Evaluate(Parachute.On, false, false, float.PositiveInfinity), Is.EqualTo(ParachuteOpenVerdict.Open));
            Assert.That(motion.Evaluate(Parachute.On, false, false, 5.99f), Is.EqualTo(ParachuteOpenVerdict.TooLow));
            Assert.That(motion.Evaluate(Parachute.On, false, false, 0f), Is.EqualTo(ParachuteOpenVerdict.TooLow), "standing on the ground");
            Assert.That(motion.Evaluate(Parachute.Off, false, false, 100f), Is.EqualTo(ParachuteOpenVerdict.RuleOff));
            Assert.That(motion.Evaluate(Parachute.On, true, false, 100f), Is.EqualTo(ParachuteOpenVerdict.AlreadyOpen));
            Assert.That(motion.Evaluate(Parachute.On, false, true, 100f), Is.EqualTo(ParachuteOpenVerdict.Busy));
        }

        [Test]
        public void TheRuleIsCheckedFirstThenOpenThenBusyThenHeight()
        {
            var motion = new ParachuteMotion();

            Assert.That(motion.Evaluate(Parachute.Off, true, true, 0f), Is.EqualTo(ParachuteOpenVerdict.RuleOff));
            Assert.That(motion.Evaluate(Parachute.On, true, true, 0f), Is.EqualTo(ParachuteOpenVerdict.AlreadyOpen));
            Assert.That(motion.Evaluate(Parachute.On, false, true, 0f), Is.EqualTo(ParachuteOpenVerdict.Busy));
        }

        [Test]
        public void EvaluateRejectsAnInvalidRuleOrHeight()
        {
            var motion = new ParachuteMotion();

            Assert.Throws<ArgumentOutOfRangeException>(() => motion.Evaluate((Parachute)99, false, false, 10f));
            Assert.Throws<ArgumentOutOfRangeException>(() => motion.Evaluate(Parachute.On, false, false, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => motion.Evaluate(Parachute.On, false, false, float.NaN));
        }

        [Test]
        public void OpeningKeepsTheSidewaysSpeedUpToTheLimit()
        {
            var motion = new ParachuteMotion(horizontalSpeed: 6f);

            motion.Open(new Vector2(3f, 0f));
            Assert.That(motion.HorizontalVelocity, Is.EqualTo(new Vector2(3f, 0f)));

            motion.Open(new Vector2(0f, 20f));
            Assert.That(motion.HorizontalVelocity.Length(), Is.EqualTo(6f).Within(Epsilon));
            Assert.That(motion.HorizontalVelocity.Y, Is.GreaterThan(0f), "the direction is kept");

            Assert.Throws<ArgumentOutOfRangeException>(() => motion.Open(new Vector2(float.NaN, 0f)));
        }

        [Test]
        public void DescendsAtTheDescentSpeed()
        {
            var motion = new ParachuteMotion(descentSpeed: 4f);
            motion.Open(Vector2.Zero);

            var velocity = motion.Step(Vector2.Zero, 0f, 0.1f);

            Assert.That(velocity.Y, Is.EqualTo(-4f));
            Assert.That(motion.Velocity.Y, Is.EqualTo(-4f));
        }

        [Test]
        public void SteersTowardsTheInputAtTheSteerAcceleration()
        {
            var motion = new ParachuteMotion(horizontalSpeed: 6f, steerAcceleration: 8f);
            motion.Open(Vector2.Zero);

            // 前 (Y) への入力で、向き 0 ならワールドの +Z (水平の Y) へ、1 秒あたり 8 m/s ずつ速くなる。
            var first = motion.Step(new Vector2(0f, 1f), 0f, 0.25f);
            Assert.That(first.Z, Is.EqualTo(2f).Within(Epsilon));
            Assert.That(first.X, Is.EqualTo(0f).Within(Epsilon));

            for (var i = 0; i < 10; i++)
            {
                motion.Step(new Vector2(0f, 1f), 0f, 0.25f);
            }

            Assert.That(motion.HorizontalVelocity.Y, Is.EqualTo(6f).Within(Epsilon), "it stops at the horizontal speed");
        }

        [Test]
        public void TurnsWithTheInputAndTheYaw()
        {
            var motion = new ParachuteMotion(horizontalSpeed: 6f, steerAcceleration: 1000f);
            motion.Open(new Vector2(0f, 6f));

            // 右 (X) への入力で、向き 0 ならワールドの +X へ向きを変える。
            motion.Step(new Vector2(1f, 0f), 0f, 0.1f);
            Assert.That(motion.HorizontalVelocity.X, Is.EqualTo(6f).Within(Epsilon));
            Assert.That(motion.HorizontalVelocity.Y, Is.EqualTo(0f).Within(Epsilon));

            // 右に 90 度向いて前へ入れても、ワールドの +X へ進む。
            motion.Open(Vector2.Zero);
            motion.Step(new Vector2(0f, 1f), (float)(Math.PI / 2d), 0.1f);
            Assert.That(motion.HorizontalVelocity.X, Is.EqualTo(6f).Within(Epsilon));
            Assert.That(motion.HorizontalVelocity.Y, Is.EqualTo(0f).Within(Epsilon));
        }

        [Test]
        public void SlowsDownWithoutInputAndClampsTheInput()
        {
            var motion = new ParachuteMotion(horizontalSpeed: 6f, steerAcceleration: 1000f);
            motion.Open(new Vector2(6f, 0f));

            motion.Step(Vector2.Zero, 0f, 0.1f);
            Assert.That(motion.HorizontalVelocity, Is.EqualTo(Vector2.Zero));

            motion.Step(new Vector2(0f, 5f), 0f, 0.1f);
            Assert.That(motion.HorizontalVelocity.Length(), Is.EqualTo(6f).Within(Epsilon), "an input longer than 1 does not go faster");

            motion.Step(new Vector2(0f, 0.5f), 0f, 0.1f);
            Assert.That(motion.HorizontalVelocity.Length(), Is.EqualTo(3f).Within(Epsilon), "a half input goes half as fast");
        }

        [Test]
        public void ResetStopsTheSidewaysMotion()
        {
            var motion = new ParachuteMotion();
            motion.Open(new Vector2(3f, 3f));

            motion.Reset();

            Assert.That(motion.HorizontalVelocity, Is.EqualTo(Vector2.Zero));
        }

        [Test]
        public void StepRejectsInvalidArguments()
        {
            var motion = new ParachuteMotion();

            Assert.Throws<ArgumentOutOfRangeException>(() => motion.Step(new Vector2(float.NaN, 0f), 0f, 0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => motion.Step(Vector2.Zero, float.PositiveInfinity, 0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => motion.Step(Vector2.Zero, 0f, -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => motion.Step(Vector2.Zero, 0f, float.NaN));
        }
    }
}
