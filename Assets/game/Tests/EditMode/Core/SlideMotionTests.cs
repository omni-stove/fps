using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class SlideMotionTests
    {
        private static readonly Vector2 _forward = new Vector2(0f, 1f);

        [Test]
        public void DefaultsMatchTheConstants()
        {
            var motion = new SlideMotion();

            Assert.That(motion.StartSpeed, Is.EqualTo(SlideMotion.DefaultStartSpeed));
            Assert.That(motion.Deceleration, Is.EqualTo(SlideMotion.DefaultDeceleration));
            Assert.That(motion.EndSpeed, Is.EqualTo(SlideMotion.DefaultEndSpeed));
            Assert.That(motion.MinSeconds, Is.EqualTo(SlideMotion.DefaultMinSeconds));
            Assert.That(motion.MaxSeconds, Is.EqualTo(SlideMotion.DefaultMaxSeconds));
            Assert.That(motion.SlopeAcceleration, Is.EqualTo(SlideMotion.DefaultSlopeAcceleration));
            Assert.That(motion.IsSliding, Is.False);
            Assert.That(motion.LastEnd, Is.EqualTo(SlideEnd.None));
        }

        [Test]
        public void RejectsTuningThatCannotSlide()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SlideMotion(3f, 8f, 3f, 0.4f, 3f, 20f), "start must beat the end speed");
            Assert.Throws<ArgumentOutOfRangeException>(() => new SlideMotion(float.PositiveInfinity, 8f, 3f, 0.4f, 3f, 20f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SlideMotion(11f, -1f, 3f, 0.4f, 3f, 20f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SlideMotion(11f, 8f, -1f, 0.4f, 3f, 20f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SlideMotion(11f, 8f, 3f, 4f, 3f, 20f), "min beyond max");
            Assert.Throws<ArgumentOutOfRangeException>(() => new SlideMotion(11f, 8f, 3f, 0f, 0f, 20f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SlideMotion(11f, 8f, 3f, 0.4f, 3f, float.NaN));
        }

        [Test]
        public void StartsAtTheStartSpeedOrFasterIfAlreadyFaster()
        {
            var motion = new SlideMotion();

            Assert.That(motion.TryStart(new Vector2(3f, 4f), 8f), Is.True);
            Assert.That(motion.Speed, Is.EqualTo(SlideMotion.DefaultStartSpeed));
            Assert.That(motion.Direction.X, Is.EqualTo(0.6f).Within(1e-5f));
            Assert.That(motion.Direction.Y, Is.EqualTo(0.8f).Within(1e-5f));
            Assert.That(motion.TryStart(_forward, 20f), Is.False, "already sliding");

            motion.Finish(SlideEnd.Cancelled);
            Assert.That(motion.TryStart(_forward, 14f), Is.True);
            Assert.That(motion.Speed, Is.EqualTo(14f), "keeps a faster run");
            Assert.Throws<ArgumentOutOfRangeException>(() => motion.TryStart(Vector2.Zero, 8f));
        }

        [Test]
        public void OnFlatGroundItSlowsDownToTheEndSpeedAndStops()
        {
            var motion = new SlideMotion();
            motion.TryStart(_forward, 8f);

            var steps = Run(motion, 0.02f, 0f);

            // 11m/s から毎秒 8 ずつ落ち、3m/s で止まる: 1 秒で (11 + 3) / 2 = 7m。
            Assert.That(motion.IsSliding, Is.False);
            Assert.That(motion.LastEnd, Is.EqualTo(SlideEnd.Stopped));
            Assert.That(motion.Elapsed, Is.EqualTo(1f).Within(0.021f));
            Assert.That(motion.Distance, Is.EqualTo(7f).Within(0.1f));
            Assert.That(motion.Speed, Is.LessThanOrEqualTo(SlideMotion.DefaultEndSpeed));
            Assert.That(steps, Is.EqualTo(50).Within(1));
        }

        [Test]
        public void TheDistanceBarelyDependsOnTheFrameRate()
        {
            var fine = new SlideMotion();
            var coarse = new SlideMotion();
            fine.TryStart(_forward, 0f);
            coarse.TryStart(_forward, 0f);

            Run(fine, 1f / 240f, 0.1f);
            Run(coarse, 1f / 30f, 0.1f);

            Assert.That(coarse.Distance, Is.EqualTo(fine.Distance).Within(0.3f));
        }

        [Test]
        public void DownhillSlidesFurtherAndUphillShorter()
        {
            var flat = new SlideMotion();
            var down = new SlideMotion();
            var up = new SlideMotion();
            flat.TryStart(_forward, 0f);
            down.TryStart(_forward, 0f);
            up.TryStart(_forward, 0f);

            var sine = (float)Math.Sin(12.0 * Math.PI / 180.0);
            Run(flat, 0.02f, 0f);
            Run(down, 0.02f, sine);
            Run(up, 0.02f, -sine);

            Assert.That(down.Distance, Is.GreaterThan(flat.Distance * 1.5f));
            Assert.That(down.Elapsed, Is.GreaterThan(flat.Elapsed));
            Assert.That(up.Distance, Is.LessThan(flat.Distance));
        }

        [Test]
        public void ASteepSlopeEndsAtTheLongestTime()
        {
            var motion = new SlideMotion();
            motion.TryStart(_forward, 0f);

            Run(motion, 0.02f, 0.7f);

            Assert.That(motion.LastEnd, Is.EqualTo(SlideEnd.TimedOut));
            Assert.That(motion.Elapsed, Is.EqualTo(SlideMotion.DefaultMaxSeconds).Within(1e-4f), "no time past the longest");
            Assert.That(motion.Speed, Is.GreaterThan(SlideMotion.DefaultStartSpeed), "it sped up on the way down");
        }

        [Test]
        public void ItKeepsSlidingForTheShortestTimeEvenIfSlowAndStopsAtZero()
        {
            // 0.25 秒で 3m/s まで落ちるが、最短の 0.4 秒までは続ける。
            var slow = new SlideMotion(5f, 8f, 3f, 0.4f, 3f, 20f);
            slow.TryStart(_forward, 0f);
            slow.Step(0.3f, 0f);
            Assert.That(slow.IsSliding, Is.True);
            slow.Step(0.1f, 0f);
            Assert.That(slow.IsSliding, Is.False);
            Assert.That(slow.LastEnd, Is.EqualTo(SlideEnd.Stopped));

            // 止まり切ったら最短の秒数を待たずに終え、後ろへは戻らない。
            var hard = new SlideMotion(4f, 40f, 0f, 1f, 3f, 0f);
            hard.TryStart(_forward, 0f);
            var travelled = hard.Step(0.5f, 0f);
            Assert.That(hard.LastEnd, Is.EqualTo(SlideEnd.Stopped));
            Assert.That(hard.Speed, Is.Zero);
            Assert.That(travelled, Is.EqualTo(4f * 0.5f * 0.1f).Within(1e-5f), "only up to the stop");
        }

        [Test]
        public void LimitingTheSpeedAndFinishingFromOutside()
        {
            var motion = new SlideMotion();
            motion.TryStart(_forward, 0f);

            motion.LimitSpeed(20f);
            Assert.That(motion.Speed, Is.EqualTo(SlideMotion.DefaultStartSpeed), "a higher limit changes nothing");
            motion.LimitSpeed(1f);
            Assert.That(motion.Speed, Is.EqualTo(1f));
            motion.Step(SlideMotion.DefaultMinSeconds, 0f);
            Assert.That(motion.LastEnd, Is.EqualTo(SlideEnd.Stopped), "blocked by a wall");

            motion.TryStart(_forward, 0f);
            Assert.That(motion.Finish(SlideEnd.Jumped), Is.True);
            Assert.That(motion.LastEnd, Is.EqualTo(SlideEnd.Jumped));
            Assert.That(motion.Finish(SlideEnd.Cancelled), Is.False, "already over");
            Assert.That(motion.Step(0.1f, 0f), Is.Zero, "nothing moves after the end");
            Assert.Throws<ArgumentOutOfRangeException>(() => motion.Finish(SlideEnd.None));
            Assert.Throws<ArgumentOutOfRangeException>(() => motion.Step(-0.1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => motion.Step(0.1f, 1.5f));
        }

        [Test]
        public void LogsCarryTheTagAndValues()
        {
            Assert.That(
                SlideMotion.FormatStartLog(11f, new Vector2(0f, -1f)),
                Is.EqualTo("[Slide] start speed=11.00 direction=(0.00, -1.00)"));
            Assert.That(
                SlideMotion.FormatEndLog(SlideEnd.Stopped, 2.9f, 1.02f, 7.4f),
                Is.EqualTo("[Slide] end reason=Stopped speed=2.90 seconds=1.02 distance=7.40"));
        }

        private static int Run(SlideMotion motion, float deltaSeconds, float slopeSine)
        {
            var steps = 0;
            while (motion.IsSliding && steps < 10000)
            {
                motion.Step(deltaSeconds, slopeSine);
                steps++;
            }

            return steps;
        }
    }
}
