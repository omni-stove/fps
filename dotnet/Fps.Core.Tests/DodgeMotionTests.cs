using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class DodgeMotionTests
    {
        private const float Tolerance = 1e-4f;

        private static MotorSpec Spec(float dodgeDistance = 3f, float dodgeSeconds = 0.3f)
        {
            return new MotorSpec(5f, 8f, 1.2f, 20f, 0.1f, 2.5f, dodgeDistance: dodgeDistance, dodgeSeconds: dodgeSeconds);
        }

        [Test]
        public void ReadsDistanceAndSecondsFromTheSpec()
        {
            var motion = new DodgeMotion(Spec(4f, 0.5f));

            Assert.That(motion.Distance, Is.EqualTo(4f));
            Assert.That(motion.Seconds, Is.EqualTo(0.5f));
            Assert.That(motion.CooldownSeconds, Is.EqualTo(DodgeMotion.DefaultCooldownSeconds));
            Assert.That(DodgeMotion.DefaultCooldownSeconds, Is.EqualTo(0.5f));
        }

        [Test]
        public void StartsReadyAndStill()
        {
            var motion = new DodgeMotion(Spec());

            Assert.That(motion.IsReady, Is.True);
            Assert.That(motion.IsRolling, Is.False);
            Assert.That(motion.Velocity, Is.EqualTo(Vector2.Zero));
            Assert.That(motion.Step(0.1f), Is.EqualTo(Vector2.Zero));
        }

        [Test]
        public void RejectsInvalidArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new DodgeMotion(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DodgeMotion(Spec(), -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DodgeMotion(Spec(), float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DodgeMotion(Spec(), float.NaN));

            var motion = new DodgeMotion(Spec());
            Assert.Throws<ArgumentOutOfRangeException>(() => motion.TryStart(Vector2.Zero));
            Assert.Throws<ArgumentOutOfRangeException>(() => motion.TryStart(new Vector2(float.NaN, 1f)));
            Assert.Throws<ArgumentOutOfRangeException>(() => motion.Step(-0.01f));
            Assert.Throws<ArgumentOutOfRangeException>(() => motion.Step(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => motion.Step(float.PositiveInfinity));
            Assert.That(motion.IsRolling, Is.False);
        }

        [Test]
        public void MovesAtAConstantSpeedAlongTheNormalizedDirection()
        {
            var motion = new DodgeMotion(Spec(3f, 0.3f));

            Assert.That(motion.TryStart(new Vector2(0f, 5f)), Is.True);
            Assert.That(motion.IsRolling, Is.True);
            Assert.That(motion.Direction, Is.EqualTo(new Vector2(0f, 1f)));
            Assert.That(motion.Velocity.Y, Is.EqualTo(10f).Within(Tolerance));

            var step = motion.Step(0.1f);

            Assert.That(step.X, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(step.Y, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(motion.Elapsed, Is.EqualTo(0.1f).Within(Tolerance));
        }

        [TestCase(1f / 60f)]
        [TestCase(1f / 144f)]
        [TestCase(0.07f)]
        [TestCase(1f)]
        public void TotalDistanceDoesNotDependOnTheFrameRate(float deltaSeconds)
        {
            var motion = new DodgeMotion(Spec(3f, 0.35f));
            motion.TryStart(new Vector2(1f, 1f));

            var total = Vector2.Zero;
            var steps = 0;
            while (motion.IsRolling)
            {
                total += motion.Step(deltaSeconds);
                steps++;
                Assert.That(steps, Is.LessThan(10000));
            }

            Assert.That(total.Length(), Is.EqualTo(3f).Within(Tolerance));
            Assert.That(total.X, Is.EqualTo(total.Y).Within(Tolerance));
        }

        [Test]
        public void EndsAfterTheSecondsAndWaitsForTheCooldown()
        {
            var motion = new DodgeMotion(Spec(3f, 0.3f), 0.5f);
            motion.TryStart(new Vector2(1f, 0f));

            motion.Step(0.2f);
            Assert.That(motion.IsRolling, Is.True);

            motion.Step(0.1f);
            Assert.That(motion.IsRolling, Is.False);
            Assert.That(motion.Velocity, Is.EqualTo(Vector2.Zero));
            Assert.That(motion.CooldownRemaining, Is.EqualTo(0.5f).Within(Tolerance));
            Assert.That(motion.IsReady, Is.False);
            Assert.That(motion.TryStart(new Vector2(1f, 0f)), Is.False, "cannot start during the cooldown");

            Assert.That(motion.Step(0.49f), Is.EqualTo(Vector2.Zero));
            Assert.That(motion.IsReady, Is.False);

            motion.Step(0.011f);
            Assert.That(motion.IsReady, Is.True);
            Assert.That(motion.TryStart(new Vector2(-1f, 0f)), Is.True);
            Assert.That(motion.Direction, Is.EqualTo(new Vector2(-1f, 0f)));
        }

        [Test]
        public void TimeLeftOverFromTheLastStepCountsTowardsTheCooldown()
        {
            var motion = new DodgeMotion(Spec(3f, 0.3f), 0.5f);
            motion.TryStart(new Vector2(1f, 0f));

            var step = motion.Step(0.5f);

            Assert.That(step.X, Is.EqualTo(3f).Within(Tolerance), "stops at the full distance");
            Assert.That(motion.CooldownRemaining, Is.EqualTo(0.3f).Within(Tolerance));
        }

        [Test]
        public void CannotRestartWhileRolling()
        {
            var motion = new DodgeMotion(Spec());
            motion.TryStart(new Vector2(1f, 0f));

            Assert.That(motion.TryStart(new Vector2(0f, 1f)), Is.False);
            Assert.That(motion.Direction, Is.EqualTo(new Vector2(1f, 0f)));
        }

        [Test]
        public void MashingOnlyDodgesOncePerRollPlusCooldown()
        {
            var motion = new DodgeMotion(Spec(3f, 0.3f), 0.5f);
            const float frame = 0.01f;
            var starts = 0;

            // 2 秒間、毎フレーム押し続ける。1 回は 0.3 秒転がって 0.5 秒待つので 0.8 秒ごと。
            for (var i = 0; i < 200; i++)
            {
                if (motion.TryStart(new Vector2(1f, 0f)))
                {
                    starts++;
                }

                motion.Step(frame);
            }

            Assert.That(starts, Is.EqualTo(3));
        }

        [Test]
        public void CancelStopsTheRollAndStartsTheCooldown()
        {
            var motion = new DodgeMotion(Spec(), 0.5f);
            Assert.That(motion.Cancel(), Is.False);

            motion.TryStart(new Vector2(1f, 0f));
            motion.Step(0.1f);

            Assert.That(motion.Cancel(), Is.True);
            Assert.That(motion.IsRolling, Is.False);
            Assert.That(motion.CooldownRemaining, Is.EqualTo(0.5f));
            Assert.That(motion.Step(0.1f), Is.EqualTo(Vector2.Zero));
        }

        [Test]
        public void ResetClearsTheRollAndTheCooldown()
        {
            var motion = new DodgeMotion(Spec(), 0.5f);
            motion.TryStart(new Vector2(1f, 0f));
            motion.Cancel();

            motion.Reset();

            Assert.That(motion.IsRolling, Is.False);
            Assert.That(motion.IsReady, Is.True);
        }

        [Test]
        public void ZeroDistanceRollsInPlaceForTheSeconds()
        {
            var motion = new DodgeMotion(Spec(0f, 0.3f));
            motion.TryStart(new Vector2(1f, 0f));

            Assert.That(motion.Step(0.1f), Is.EqualTo(Vector2.Zero));
            Assert.That(motion.IsRolling, Is.True);
        }

        [Test]
        public void DirectionFollowsTheInputRotatedByYaw()
        {
            var right = DodgeMotion.ResolveDirection(new Vector2(1f, 0f), 0f);
            Assert.That(right.X, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(right.Y, Is.EqualTo(0f).Within(Tolerance));

            // 右 (+X) を向いているときの前入力は +X へ。
            var forward = DodgeMotion.ResolveDirection(new Vector2(0f, 1f), MathF.PI / 2f);
            Assert.That(forward.X, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(forward.Y, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void DirectionIgnoresTheInputMagnitude()
        {
            var light = DodgeMotion.ResolveDirection(new Vector2(0.2f, 0.2f), 0f);

            Assert.That(light.Length(), Is.EqualTo(1f).Within(Tolerance));
            Assert.That(light.X, Is.EqualTo(light.Y).Within(Tolerance));
        }

        [Test]
        public void NoInputRollsBackwards()
        {
            var still = DodgeMotion.ResolveDirection(Vector2.Zero, 0f);
            Assert.That(still.X, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(still.Y, Is.EqualTo(-1f).Within(Tolerance));

            var turned = DodgeMotion.ResolveDirection(new Vector2(0.001f, 0f), MathF.PI / 2f);
            Assert.That(turned.X, Is.EqualTo(-1f).Within(Tolerance), "a dead-zone input counts as none");
            Assert.That(turned.Y, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void DirectionRejectsNonFiniteInput()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => DodgeMotion.ResolveDirection(new Vector2(float.NaN, 0f), 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => DodgeMotion.ResolveDirection(Vector2.Zero, float.PositiveInfinity));
        }
    }
}
