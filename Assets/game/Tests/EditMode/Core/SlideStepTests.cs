using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class SlideStepTests
    {
        [Test]
        public void OnlyFasterThanAWalkCanStartASlide()
        {
            Assert.That(SlideStep.TryMeasureEntry(new Vector2(0f, 5f), 5f, out _, out var walking), Is.False);
            Assert.That(walking, Is.EqualTo(0f));

            Assert.That(SlideStep.TryMeasureEntry(new Vector2(6f, 8f), 5f, out var direction, out var speed), Is.True);
            Assert.That(speed, Is.EqualTo(10f).Within(1e-5f));
            Assert.That(direction.X, Is.EqualTo(0.6f).Within(1e-5f));
            Assert.That(direction.Y, Is.EqualTo(0.8f).Within(1e-5f));

            Assert.That(SlideStep.TryMeasureEntry(new Vector2(float.NaN, 8f), 5f, out _, out _), Is.False);
        }

        [Test]
        public void TheSlopeSineIsPositiveDownhillAndZeroInTheAir()
        {
            // 前 (z) へ下る 30° の坂
            var angle = MathF.PI / 6f;
            var normal = new Vector3(0f, MathF.Cos(angle), MathF.Sin(angle));
            var forward = Vector3.UnitZ;

            var down = SlideStep.SlopeSine(forward, normal, true, out var along);
            Assert.That(down, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(along.Length(), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(along.Y, Is.EqualTo(-0.5f).Within(1e-5f));

            var up = SlideStep.SlopeSine(-forward, normal, true, out _);
            Assert.That(up, Is.EqualTo(-0.5f).Within(1e-5f));

            var air = SlideStep.SlopeSine(forward, normal, false, out var level);
            Assert.That(air, Is.Zero);
            Assert.That(level, Is.EqualTo(forward));
        }

        [Test]
        public void BeingHeldBackToUnderHalfTheStepLimitsTheSpeed()
        {
            Assert.That(SlideStep.TryLimitForBlocked(10f, 0.08f, 0.1f, out var free), Is.False);
            Assert.That(free, Is.EqualTo(10f));

            Assert.That(SlideStep.TryLimitForBlocked(10f, 0.02f, 0.1f, out var held), Is.True);
            Assert.That(held, Is.EqualTo(2f).Within(1e-5f));

            Assert.That(SlideStep.TryLimitForBlocked(10f, -0.05f, 0.1f, out var pushedBack), Is.True);
            Assert.That(pushedBack, Is.Zero);

            Assert.That(SlideStep.TryLimitForBlocked(10f, 0f, 0f, out _), Is.False, "nothing was asked to move");
        }

        [Test]
        public void TheJumpSpeedMatchesThePlayerMotorJump()
        {
            Assert.That(SlideStep.JumpSpeed(20f, 1.2f), Is.EqualTo(MathF.Sqrt(48f)).Within(1e-5f));
            Assert.That(SlideStep.JumpSpeed(20f, 0f), Is.Zero);
        }

        [Test]
        public void SlideMotionRestoresAndCapturesTheState()
        {
            var motion = new SlideMotion();
            var state = CharacterSlideState.Sliding(new Vector2(1f, 0f), 7f, 0.5f, 4f, 0.05f);

            motion.Restore(state);

            Assert.That(motion.IsSliding, Is.True);
            Assert.That(motion.Speed, Is.EqualTo(7f));
            Assert.That(motion.Elapsed, Is.EqualTo(0.5f));
            Assert.That(motion.Distance, Is.EqualTo(4f));
            Assert.That(motion.LastEnd, Is.EqualTo(SlideEnd.None));
            Assert.That(motion.Capture(0.05f), Is.EqualTo(state));

            motion.Restore(CharacterSlideState.None);
            Assert.That(motion.IsSliding, Is.False);
            Assert.That(motion.Capture(0f), Is.EqualTo(CharacterSlideState.None));
        }
    }
}
