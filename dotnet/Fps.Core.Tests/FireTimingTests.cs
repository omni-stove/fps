using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class FireTimingTests
    {
        [TestCase(0d, 0)]
        [TestCase(1d, 60)]
        [TestCase(1.0166d, 60)]
        [TestCase(1.0167d, 61)]
        public void TickAtFloorsTheTimeToTicks(double seconds, int expected)
        {
            Assert.That(FireTiming.TickAt(seconds, 60), Is.EqualTo(expected));
        }

        [Test]
        public void TickAtRejectsInvalidInput()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => FireTiming.TickAt(-0.1d, 60));
            Assert.Throws<ArgumentOutOfRangeException>(() => FireTiming.TickAt(double.NaN, 60));
            Assert.Throws<ArgumentOutOfRangeException>(() => FireTiming.TickAt(double.PositiveInfinity, 60));
            Assert.Throws<ArgumentOutOfRangeException>(() => FireTiming.TickAt(1d, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => FireTiming.TickAt(1e12d, 60));
        }

        [Test]
        public void ViewTimeSubtractsTheInterpolationDelayAndSplitsTickAndAlpha()
        {
            // 1.5 秒 = 90 tick、2 tick 遅れて見ているので 88 tick ちょうど
            var exact = FireTiming.ViewTime(1.5d, 60, 2);
            Assert.That(exact.Tick, Is.EqualTo(88));
            Assert.That(exact.Alpha, Is.EqualTo(0f).Within(1e-4f));

            // 1.5125 秒 = 90.75 tick → 88.75
            var between = FireTiming.ViewTime(1.5125d, 60, 2);
            Assert.That(between.Tick, Is.EqualTo(88));
            Assert.That(between.Alpha, Is.EqualTo(0.75f).Within(1e-4f));
            Assert.That(between.ToTick(), Is.EqualTo(88.75f).Within(1e-4f));
            Assert.That(between.IsValid, Is.True);
        }

        [Test]
        public void ViewTimeWithoutDelayIsTheServerTime()
        {
            var view = FireTiming.ViewTime(2d, 60, 0);

            Assert.That(view.Tick, Is.EqualTo(120));
            Assert.That(view.Alpha, Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void ViewTimeDoesNotGoBeforeZero()
        {
            var view = FireTiming.ViewTime(0.01d, 60, 2);

            Assert.That(view.Tick, Is.EqualTo(0));
            Assert.That(view.Alpha, Is.EqualTo(0f));
        }

        [Test]
        public void ViewTimeAlphaStaysBelowOne()
        {
            // float に丸めると 1 になる端数でも 1 未満に収める
            var view = FireTiming.ViewTime((10d - 1e-12) / 60d, 60, 0);

            Assert.That(view.Alpha, Is.LessThan(1f));
            Assert.That(view.IsValid, Is.True);
        }

        [Test]
        public void ViewTimeRejectsInvalidInput()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => FireTiming.ViewTime(-1d, 60, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => FireTiming.ViewTime(double.NaN, 60, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => FireTiming.ViewTime(1d, 0, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => FireTiming.ViewTime(1d, 60, -1));
        }

        [TestCase(0, 0f, true)]
        [TestCase(5, 0.999f, true)]
        [TestCase(-1, 0f, false)]
        [TestCase(5, 1f, false)]
        [TestCase(5, -0.01f, false)]
        [TestCase(5, float.NaN, false)]
        [TestCase(5, float.PositiveInfinity, false)]
        public void ShotTimeValidity(int tick, float alpha, bool expected)
        {
            Assert.That(new ShotTime(tick, alpha).IsValid, Is.EqualTo(expected));
        }
    }
}
