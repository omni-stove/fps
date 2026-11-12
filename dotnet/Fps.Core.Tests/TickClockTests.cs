using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class TickClockTests
    {
        [Test]
        public void DefaultsTo60HzWithLimitOf8()
        {
            var clock = new TickClock();

            Assert.That(clock.TickRate, Is.EqualTo(60));
            Assert.That(clock.TickSeconds, Is.EqualTo(1f / 60f));
            Assert.That(clock.MaxTicksPerAdvance, Is.EqualTo(8));
            Assert.That(clock.CurrentTick, Is.Zero);
            Assert.That(clock.Alpha, Is.Zero);
        }

        [Test]
        public void ExactlyOneTickAdvancesOneTick()
        {
            var clock = new TickClock();

            var ticks = clock.Advance(1f / 60f);

            Assert.That(ticks, Is.EqualTo(1));
            Assert.That(clock.CurrentTick, Is.EqualTo(1));
        }

        [Test]
        public void TwoHalfTicksAdvanceOneTick()
        {
            var clock = new TickClock();

            var first = clock.Advance(1f / 120f);
            var second = clock.Advance(1f / 120f);

            Assert.That(first, Is.Zero);
            Assert.That(second, Is.EqualTo(1));
            Assert.That(clock.CurrentTick, Is.EqualTo(1));
        }

        [Test]
        public void LargeDeltaIsCappedAndExcessDiscarded()
        {
            var clock = new TickClock();

            var ticks = clock.Advance(1f);

            Assert.That(ticks, Is.EqualTo(8));
            Assert.That(clock.CurrentTick, Is.EqualTo(8));

            // 捨てた分は次のフレームに持ち越さない。
            Assert.That(clock.Advance(0f), Is.Zero);
            Assert.That(clock.CurrentTick, Is.EqualTo(8));
        }

        [Test]
        public void LargeDeltaKeepsFractionalRemainder()
        {
            var clock = new TickClock();

            clock.Advance(20.5f / 60f);

            Assert.That(clock.CurrentTick, Is.EqualTo(8));
            Assert.That(clock.Alpha, Is.EqualTo(0.5f).Within(1e-4f));
        }

        [Test]
        public void CustomLimitIsHonored()
        {
            var clock = new TickClock(30, 2);

            Assert.That(clock.Advance(1f), Is.EqualTo(2));
        }

        [TestCase(9)]
        [TestCase(60)]
        [TestCase(144)]
        [TestCase(1000)]
        [TestCase(4410)]
        public void SplittingOneSecondYieldsSixtyTicks(int steps)
        {
            var clock = new TickClock();
            var delta = 1f / steps;
            var total = 0;

            for (var i = 0; i < steps; i++)
            {
                total += clock.Advance(delta);
            }

            Assert.That(total, Is.EqualTo(60));
            Assert.That(clock.CurrentTick, Is.EqualTo(60));
        }

        [Test]
        public void ErrorDoesNotAccumulateOverLongRuns()
        {
            var clock = new TickClock();
            const int seconds = 600;

            // 2 の累乗にして入力の delta 自体を誤差なく表せるようにし、累積側の誤差だけを見る。
            const int framesPerSecond = 128;

            for (var i = 0; i < seconds * framesPerSecond; i++)
            {
                clock.Advance(1f / framesPerSecond);
            }

            Assert.That(clock.CurrentTick, Is.EqualTo(seconds * 60));
        }

        [Test]
        public void AlphaIsFractionTowardNextTick()
        {
            var clock = new TickClock();

            clock.Advance(1f / 120f);

            Assert.That(clock.Alpha, Is.EqualTo(0.5f).Within(1e-4f));
        }

        [Test]
        public void AlphaStaysWithinZeroToOne()
        {
            var clock = new TickClock();
            var deltas = new[] { 0f, 0.001f, 0.007f, 1f / 60f, 0.0166f, 0.033f, 0.05f, 0.25f, 1f / 144f };

            for (var i = 0; i < 1000; i++)
            {
                clock.Advance(deltas[i % deltas.Length]);

                Assert.That(clock.Alpha, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
            }
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void RejectsNonPositiveTickRate(int tickRate)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TickClock(tickRate));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void RejectsNonPositiveMaxTicksPerAdvance(int maxTicksPerAdvance)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TickClock(60, maxTicksPerAdvance));
        }

        [TestCase(-0.001f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void RejectsInvalidDelta(float deltaSeconds)
        {
            var clock = new TickClock();

            Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(deltaSeconds));
            Assert.That(clock.CurrentTick, Is.Zero);
        }
    }
}
