using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class DrowningClockTests
    {
        private static readonly SwimTuning _short = new SwimTuning(breathSeconds: 2f, drownDamage: 10, drownIntervalSeconds: 1f);

        [Test]
        public void NoDamageWhileTheBreathLasts()
        {
            var clock = new DrowningClock(_short);

            Assert.That(clock.Tick(true, 1.5f), Is.EqualTo(0));
            Assert.That(clock.BreathLeft, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(clock.BreathFraction, Is.EqualTo(0.25f).Within(1e-5f));
            Assert.That(clock.IsOutOfBreath, Is.False);
        }

        [Test]
        public void RunningOutOfBreathDamagesAtOnceAndThenEveryInterval()
        {
            var clock = new DrowningClock(_short);

            Assert.That(clock.Tick(true, 2f), Is.EqualTo(10), "the breath runs out");
            Assert.That(clock.IsOutOfBreath, Is.True);
            Assert.That(clock.BreathLeft, Is.EqualTo(0f));
            Assert.That(clock.Tick(true, 0.5f), Is.EqualTo(0));
            Assert.That(clock.Tick(true, 0.5f), Is.EqualTo(10), "one interval later");
            Assert.That(clock.Tick(true, 2f), Is.EqualTo(20), "a long frame deals every interval it passed");
        }

        [Test]
        public void SurfacingRefillsTheBreath()
        {
            var clock = new DrowningClock(_short);
            clock.Tick(true, 3f);

            Assert.That(clock.Tick(false, 0.1f), Is.EqualTo(0));
            Assert.That(clock.BreathLeft, Is.EqualTo(2f));
            Assert.That(clock.Tick(true, 1.5f), Is.EqualTo(0), "the breath starts again in full");
            Assert.That(clock.Tick(true, 0.5f), Is.EqualTo(10));
        }

        [Test]
        public void ZeroDrownDamageNeverDamages()
        {
            var clock = new DrowningClock(new SwimTuning(breathSeconds: 1f, drownDamage: 0));

            Assert.That(clock.Tick(true, 10f), Is.EqualTo(0));
            Assert.That(clock.IsOutOfBreath, Is.True);
        }

        [Test]
        public void ChangingTheTuningUsesTheNewBreath()
        {
            var clock = new DrowningClock(_short);
            clock.Tick(true, 1f);

            clock.Tuning = new SwimTuning(breathSeconds: 1f, drownDamage: 7);

            Assert.That(clock.Tick(true, 0f), Is.EqualTo(7));
        }

        [Test]
        public void ChangingTheTuningNeverReturnsNegativeDamage()
        {
            var clock = new DrowningClock(_short);
            clock.Tick(true, 2f);
            clock.Tick(true, 3f);

            clock.Tuning = new SwimTuning(breathSeconds: 10f, drownDamage: 10, drownIntervalSeconds: 1f);

            Assert.That(clock.Tick(true, 6f), Is.EqualTo(20), "11 seconds under, one second past the new breath");
        }

        [Test]
        public void BadArgumentsAreRejected()
        {
            var clock = new DrowningClock(_short);

            Assert.Throws<ArgumentNullException>(() => new DrowningClock(null));
            Assert.Throws<ArgumentNullException>(() => clock.Tuning = null);
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.Tick(true, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.Tick(true, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.Tick(true, float.PositiveInfinity));
        }

        [Test]
        public void SwimTuningRejectsBadDrownValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SwimTuning(drownDamage: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SwimTuning(drownIntervalSeconds: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SwimTuning(drownIntervalSeconds: float.NaN));
            Assert.That(SwimTuning.Default.DrownDamage, Is.EqualTo(10));
            Assert.That(SwimTuning.Default.DrownIntervalSeconds, Is.EqualTo(1f));
        }

        [Test]
        public void AWaterBoxContainsItsFacesAndKeepsItsSurfaceInside()
        {
            var box = new WaterBox(new Vector3(0f, 0f, 0f), new Vector3(2f, 3f, 2f), 2f);

            Assert.That(box.Contains(new Vector3(1f, 1f, 1f)), Is.True);
            Assert.That(box.Contains(new Vector3(0f, 0f, 0f)), Is.True, "the faces count as inside");
            Assert.That(box.Contains(new Vector3(2f, 3f, 2f)), Is.True);
            Assert.That(box.Contains(new Vector3(2.01f, 1f, 1f)), Is.False);
            Assert.That(box.Contains(new Vector3(1f, -0.01f, 1f)), Is.False);
            Assert.Throws<ArgumentException>(() => new WaterBox(new Vector3(1f, 0f, 0f), new Vector3(0f, 1f, 1f), 0.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WaterBox(Vector3.Zero, Vector3.One, 1.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WaterBox(Vector3.Zero, Vector3.One, float.NaN));
            Assert.Throws<ArgumentException>(() => new WaterBox(new Vector3(float.NaN, 0f, 0f), Vector3.One, 0.5f));
        }
    }
}
