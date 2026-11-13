using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class HorizontalSpeedMeterTests
    {
        private const float Tick = 1f / 60f;
        private const float Tolerance = 1e-3f;

        [Test]
        public void FirstSampleOnlyRemembersThePosition()
        {
            var meter = new HorizontalSpeedMeter();

            Assert.That(meter.Sample(10f, 10f, 1f), Is.EqualTo(0f));
            Assert.That(meter.HasPosition, Is.True);
        }

        [Test]
        public void SpeedIsTheHorizontalDistanceOverTheWindow()
        {
            var meter = new HorizontalSpeedMeter(0.1f);
            meter.Sample(0f, 0f, 0f);

            // 3-4-5 の向きに 5 m/s で 6 tick (0.1 秒) 進む
            for (var i = 1; i <= 6; i++)
            {
                meter.Sample(0.3f * Tick * 10f * i, 0.4f * Tick * 10f * i, Tick);
            }

            Assert.That(meter.Speed, Is.EqualTo(5f).Within(Tolerance));
        }

        [Test]
        public void SpeedHoldsUntilTheWindowFills()
        {
            var meter = new HorizontalSpeedMeter(0.1f);
            meter.Sample(0f, 0f, 0f);

            meter.Sample(0.05f, 0f, 0.05f);

            Assert.That(meter.Speed, Is.EqualTo(0f), "half a window is not enough to tell the speed");

            meter.Sample(0.11f, 0f, 0.06f);

            Assert.That(meter.Speed, Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void MovingOnlyOnTicksDoesNotMakeTheSpeedJump()
        {
            // 144fps の画面で、60Hz の tick のときだけ位置が動くキャラ
            var meter = new HorizontalSpeedMeter(0.1f);
            const float frame = 1f / 144f;
            var x = 0f;
            var nextTick = Tick;
            var time = 0f;
            meter.Sample(x, 0f, 0f);
            var min = float.MaxValue;
            var max = 0f;
            for (var i = 0; i < 144; i++)
            {
                time += frame;
                while (time >= nextTick)
                {
                    x += 5f * Tick;
                    nextTick += Tick;
                }

                var speed = meter.Sample(x, 0f, frame);
                if (time > 0.2f)
                {
                    min = Math.Min(min, speed);
                    max = Math.Max(max, speed);
                }
            }

            Assert.That(min, Is.GreaterThan(4f));
            Assert.That(max, Is.LessThan(6f));
        }

        [Test]
        public void StandingStillMeasuresZero()
        {
            var meter = new HorizontalSpeedMeter(0.1f);
            meter.Sample(1f, 1f, 0f);

            meter.Sample(1f, 1f, 0.1f);

            Assert.That(meter.Speed, Is.EqualTo(0f));
        }

        [Test]
        public void ATeleportResetsTheSpeedInsteadOfCountingIt()
        {
            var meter = new HorizontalSpeedMeter(0.1f, 30f);
            meter.Sample(0f, 0f, 0f);
            meter.Sample(0.5f, 0f, 0.1f);
            Assert.That(meter.Speed, Is.EqualTo(5f).Within(Tolerance));

            meter.Sample(40f, 0f, Tick);

            Assert.That(meter.Speed, Is.EqualTo(0f));

            // 移った先から測り直す
            meter.Sample(40.2f, 0f, 0.1f);
            Assert.That(meter.Speed, Is.EqualTo(2f).Within(Tolerance));
        }

        [Test]
        public void ResetForgetsThePosition()
        {
            var meter = new HorizontalSpeedMeter(0.1f);
            meter.Sample(0f, 0f, 0f);
            meter.Sample(0.5f, 0f, 0.1f);

            meter.Reset();

            Assert.That(meter.HasPosition, Is.False);
            Assert.That(meter.Speed, Is.EqualTo(0f));
            Assert.That(meter.Sample(100f, 0f, 0.1f), Is.EqualTo(0f));
        }

        [Test]
        public void RejectsInvalidSettingsAndSamples()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new HorizontalSpeedMeter(0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HorizontalSpeedMeter(float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HorizontalSpeedMeter(0.1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HorizontalSpeedMeter(0.1f, float.NaN));

            var meter = new HorizontalSpeedMeter();
            Assert.Throws<ArgumentOutOfRangeException>(() => meter.Sample(float.NaN, 0f, Tick));
            Assert.Throws<ArgumentOutOfRangeException>(() => meter.Sample(0f, float.PositiveInfinity, Tick));
            Assert.Throws<ArgumentOutOfRangeException>(() => meter.Sample(0f, 0f, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => meter.Sample(0f, 0f, float.NaN));
        }
    }
}
