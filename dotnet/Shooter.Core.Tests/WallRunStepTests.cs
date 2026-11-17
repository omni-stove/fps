using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class WallRunStepTests
    {
        // 2 進で割り切れる tick にして、秒数と速度の期待値をちょうどの値で書く
        private const float _tick = 0.125f;

        private static readonly Vector3 _rightWall = new Vector3(-1f, 0f, 0f);
        private static readonly Vector3 _leftWall = new Vector3(1f, 0f, 0f);
        private static readonly WallContact _onRight = new WallContact(WallRunSide.Right, _rightWall, 0.6f);
        private static readonly WallContact _onLeft = new WallContact(WallRunSide.Left, _leftWall, 0.6f);
        private static readonly Vector2 _forward = new Vector2(0f, 1f);
        private static readonly WallRunTuning _tuning = new WallRunTuning(true, 0.5f, 5f, 0.25f, 7f);
        private static readonly CharacterWallRunState _running = new CharacterWallRunState(WallRunSide.Right, _tick, _rightWall);

        [Test]
        public void StartsBesideAWallWhenFastEnoughAndPressingForward()
        {
            var result = Step(CharacterWallRunState.None, _onRight, new Vector3(0.5f, -3f, 8f));

            Assert.That(result.Motion, Is.EqualTo(WallRunMotion.Running));
            Assert.That(result.State, Is.EqualTo(new CharacterWallRunState(WallRunSide.Right, 0.125f, _rightWall)));
            Assert.That(result.Velocity, Is.EqualTo(new Vector3(0f, -0.625f, 8f)), "the push into the wall and the fall are dropped, gravity is a quarter");
            Assert.That(result.Displacement, Is.EqualTo(new Vector3(0f, -0.0390625f, 1f)));
        }

        [Test]
        public void StartingWhileRisingKeepsTheRise()
        {
            var result = Step(CharacterWallRunState.None, _onRight, new Vector3(0f, 3f, 8f));

            Assert.That(result.Motion, Is.EqualTo(WallRunMotion.Running));
            Assert.That(result.Velocity, Is.EqualTo(new Vector3(0f, 2.375f, 8f)));
        }

        [Test]
        public void DoesNotStartWhenSwitchedOff()
        {
            var result = WallRunStep.Step(
                CharacterWallRunState.None, _onRight, new Vector3(0f, 0f, 8f), _forward, false, false, MotorSpec.Default, WallRunTuning.Default, _tick);

            Assert.That(WallRunTuning.Default.Enabled, Is.False);
            Assert.That(result.Motion, Is.EqualTo(WallRunMotion.None));
            Assert.That(result.State, Is.EqualTo(CharacterWallRunState.None));
            Assert.That(result.Velocity, Is.EqualTo(Vector3.Zero));
            Assert.That(result.Displacement, Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void DoesNotStartWithoutEachCondition()
        {
            var fast = new Vector3(0f, 0f, 8f);

            Assert.That(Step(CharacterWallRunState.None, _onRight, fast, grounded: true).Motion, Is.EqualTo(WallRunMotion.None), "on the ground");
            Assert.That(Step(CharacterWallRunState.None, WallContact.None, fast).Motion, Is.EqualTo(WallRunMotion.None), "no wall");
            Assert.That(Step(CharacterWallRunState.None, _onRight, new Vector3(0f, 0f, 4.9f)).Motion, Is.EqualTo(WallRunMotion.None), "too slow");
            Assert.That(
                Step(CharacterWallRunState.None, _onRight, new Vector3(8f, 0f, 0f)).Motion, Is.EqualTo(WallRunMotion.None), "fast only into the wall");
            Assert.That(Step(CharacterWallRunState.None, _onRight, fast, Vector2.Zero).Motion, Is.EqualTo(WallRunMotion.None), "no forward input");
            Assert.That(Step(CharacterWallRunState.None, _onRight, fast, new Vector2(1f, 0f)).Motion, Is.EqualTo(WallRunMotion.None), "sideways input");
            Assert.That(Step(CharacterWallRunState.None, _onRight, fast, jump: true).Motion, Is.EqualTo(WallRunMotion.None), "jump pressed");
        }

        [Test]
        public void StartsAtExactlyTheMinimumSpeed()
        {
            Assert.That(Step(CharacterWallRunState.None, _onRight, new Vector3(0f, 0f, 5f)).Motion, Is.EqualTo(WallRunMotion.Running));
        }

        [Test]
        public void ContinuesAlongTheWall()
        {
            var result = Step(_running, _onRight, new Vector3(0f, -0.625f, 8f));

            Assert.That(result.Motion, Is.EqualTo(WallRunMotion.Running));
            Assert.That(result.State, Is.EqualTo(new CharacterWallRunState(WallRunSide.Right, 0.25f, _rightWall)));
            Assert.That(result.Velocity, Is.EqualTo(new Vector3(0f, -1.25f, 8f)));
            Assert.That(result.Displacement, Is.EqualTo(new Vector3(0f, -0.1171875f, 1f)));
        }

        [Test]
        public void ContinuesWithoutInput()
        {
            Assert.That(Step(_running, _onRight, new Vector3(0f, 0f, 8f), Vector2.Zero).Motion, Is.EqualTo(WallRunMotion.Running));
        }

        [Test]
        public void FollowsTheWallAsItTurns()
        {
            var turned = Vector3.Normalize(new Vector3(-1f, 0f, -0.1f));
            var result = Step(_running, new WallContact(WallRunSide.Right, turned, 0.6f), new Vector3(0f, 0f, 8f));

            Assert.That(result.Motion, Is.EqualTo(WallRunMotion.Running));
            Assert.That(result.State.Normal, Is.EqualTo(turned));
            Assert.That(Vector3.Dot(result.Velocity, turned), Is.EqualTo(0f).Within(1e-5f), "it runs along the turned wall");
        }

        [Test]
        public void EndsAfterTheLongestTime()
        {
            var state = CharacterWallRunState.None;
            var velocity = new Vector3(0f, 0f, 8f);
            var runningTicks = 0;
            WallRunResult result;
            while ((result = Step(state, _onRight, velocity)).Motion == WallRunMotion.Running)
            {
                runningTicks++;
                state = result.State;
                velocity = result.Velocity;
                Assert.That(runningTicks, Is.LessThan(100), "the run should end");
            }

            Assert.That(runningTicks, Is.EqualTo(4), "0.5 seconds of 0.125 second ticks");
            Assert.That(state.Elapsed, Is.EqualTo(0.5f));
            Assert.That(result.State, Is.EqualTo(CharacterWallRunState.LeftWall(_rightWall)));
        }

        [Test]
        public void EndsWhenTheWallRunsOut()
        {
            var result = Step(_running, WallContact.None, new Vector3(0f, 0f, 8f));

            Assert.That(result.Motion, Is.EqualTo(WallRunMotion.None));
            Assert.That(result.State, Is.EqualTo(CharacterWallRunState.LeftWall(_rightWall)));
        }

        [Test]
        public void EndsWhenTheWallIsOnTheOtherSide()
        {
            Assert.That(Step(_running, _onLeft, new Vector3(0f, 0f, 8f)).State, Is.EqualTo(CharacterWallRunState.LeftWall(_rightWall)));
        }

        [Test]
        public void EndsWhenTheSpeedDrops()
        {
            var result = Step(_running, _onRight, new Vector3(0f, 0f, 4f));

            Assert.That(result.Motion, Is.EqualTo(WallRunMotion.None));
            Assert.That(result.State, Is.EqualTo(CharacterWallRunState.LeftWall(_rightWall)));
        }

        [Test]
        public void EndsOnBackwardInput()
        {
            var result = Step(_running, _onRight, new Vector3(0f, 0f, 8f), new Vector2(0f, -1f));

            Assert.That(result.Motion, Is.EqualTo(WallRunMotion.None));
            Assert.That(result.State, Is.EqualTo(CharacterWallRunState.LeftWall(_rightWall)));
        }

        [Test]
        public void EndsWhenSwitchedOffMidRun()
        {
            var result = WallRunStep.Step(
                _running, _onRight, new Vector3(0f, 0f, 8f), _forward, false, false, MotorSpec.Default, WallRunTuning.Default, _tick);

            Assert.That(result.Motion, Is.EqualTo(WallRunMotion.None));
            Assert.That(result.State, Is.EqualTo(CharacterWallRunState.LeftWall(_rightWall)));
        }

        [Test]
        public void LandingEndsTheRunAndForgetsTheWall()
        {
            Assert.That(Step(_running, _onRight, new Vector3(0f, 0f, 8f), grounded: true).State, Is.EqualTo(CharacterWallRunState.None));
            Assert.That(
                Step(CharacterWallRunState.LeftWall(_rightWall), _onRight, new Vector3(0f, 0f, 8f), grounded: true).State,
                Is.EqualTo(CharacterWallRunState.None));
        }

        [Test]
        public void JumpsAwayFromTheWall()
        {
            var result = Step(_running, _onRight, new Vector3(0f, -1f, 8f), jump: true);
            var up = MathF.Sqrt(2f * MotorSpec.DefaultGravity * MotorSpec.DefaultJumpHeight);

            Assert.That(result.Motion, Is.EqualTo(WallRunMotion.JumpedOff));
            Assert.That(result.State, Is.EqualTo(CharacterWallRunState.LeftWall(_rightWall)));
            Assert.That(result.Velocity.X, Is.EqualTo(-7f), "away from the wall at the jump-off speed");
            Assert.That(result.Velocity.Z, Is.EqualTo(8f), "the speed along the wall is kept");
            Assert.That(result.Velocity.Y, Is.EqualTo(up - 2.5f).Within(1e-5f), "up as high as a ground jump, under full gravity");
            Assert.That(result.Displacement.X, Is.EqualTo(-0.875f));
            Assert.That(result.Displacement.Z, Is.EqualTo(1f));
            Assert.That(result.Displacement.Y, Is.EqualTo((up - 1.25f) * 0.125f).Within(1e-5f));
        }

        [Test]
        public void JumpsOffEvenWhenTheWallRanOutThisTick()
        {
            Assert.That(Step(_running, WallContact.None, new Vector3(0f, 0f, 8f), jump: true).Motion, Is.EqualTo(WallRunMotion.JumpedOff));
        }

        [Test]
        public void DoesNotRunOnTheSameWallAgainBeforeLanding()
        {
            var left = CharacterWallRunState.LeftWall(_rightWall);

            var again = Step(left, _onRight, new Vector3(0f, 0f, 8f));
            var other = Step(left, _onLeft, new Vector3(0f, 0f, 8f));

            Assert.That(again.Motion, Is.EqualTo(WallRunMotion.None));
            Assert.That(again.State, Is.EqualTo(left), "the last wall is kept until landing");
            Assert.That(other.Motion, Is.EqualTo(WallRunMotion.Running), "the wall across is another wall");
            Assert.That(other.State, Is.EqualTo(new CharacterWallRunState(WallRunSide.Left, 0.125f, _leftWall)));
        }

        [Test]
        public void StopEndsARunWithoutTheStep()
        {
            Assert.That(WallRunStep.Stop(_running, false), Is.EqualTo(CharacterWallRunState.LeftWall(_rightWall)));
            Assert.That(WallRunStep.Stop(_running, true), Is.EqualTo(CharacterWallRunState.None));
            Assert.That(WallRunStep.Stop(CharacterWallRunState.LeftWall(_rightWall), false), Is.EqualTo(CharacterWallRunState.LeftWall(_rightWall)));
            Assert.That(WallRunStep.Stop(CharacterWallRunState.None, false), Is.EqualTo(CharacterWallRunState.None));
        }

        [Test]
        public void RejectsMissingSettingsAndABadTick()
        {
            Assert.Throws<ArgumentNullException>(() => WallRunStep.Step(
                _running, _onRight, Vector3.Zero, _forward, false, false, null, _tuning, _tick));
            Assert.Throws<ArgumentNullException>(() => WallRunStep.Step(
                _running, _onRight, Vector3.Zero, _forward, false, false, MotorSpec.Default, null, _tick));
            Assert.Throws<ArgumentOutOfRangeException>(() => WallRunStep.Step(
                _running, _onRight, Vector3.Zero, _forward, false, false, MotorSpec.Default, _tuning, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => WallRunStep.Step(
                _running, _onRight, Vector3.Zero, _forward, false, false, MotorSpec.Default, _tuning, float.PositiveInfinity));
        }

        private static WallRunResult Step(
            CharacterWallRunState state, WallContact wall, Vector3 velocity, Vector2? move = null, bool jump = false, bool grounded = false)
        {
            return WallRunStep.Step(state, wall, velocity, move ?? _forward, jump, grounded, MotorSpec.Default, _tuning, _tick);
        }
    }
}
