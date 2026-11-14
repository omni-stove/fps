using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class CameraShakeModelTests
    {
        private static CameraShakeModel CreateModel(int seed = 7)
        {
            return new CameraShakeModel(seed)
            {
                MaxAngles = new Vector3(4f, 4f, 3f),
                MaxOffset = new Vector3(0.04f, 0.04f, 0.02f),
            };
        }

        [Test]
        public void TraumaReturnsToZeroOverTime()
        {
            var model = CreateModel();
            model.AddTrauma(1f);

            var seconds = 0f;
            while (model.Trauma > 0f && seconds < 10f)
            {
                model.Tick(1f / 60f);
                seconds += 1f / 60f;
            }

            Assert.That(model.Trauma, Is.Zero);
            Assert.That(seconds, Is.EqualTo(1f / CameraShakeModel.DefaultDecayPerSecond).Within(1f / 30f));
            Assert.That(model.Sample(1f).IsZero, Is.True, "no trauma, no shake");
        }

        [Test]
        public void TraumaDecaysByTheRatePerSecond()
        {
            var model = CreateModel();
            model.DecayPerSecond = 0.5f;
            model.AddTrauma(0.8f);

            model.Tick(1f);

            Assert.That(model.Trauma, Is.EqualTo(0.3f).Within(1e-5f));
        }

        [Test]
        public void TraumaStopsAtOne()
        {
            var model = CreateModel();

            Assert.That(model.AddTrauma(0.7f), Is.EqualTo(0.7f).Within(1e-6f));
            Assert.That(model.AddTrauma(0.7f), Is.EqualTo(0.3f).Within(1e-6f), "only what fits below 1 is added");
            Assert.That(model.AddTrauma(0.2f), Is.Zero);
            Assert.That(model.Trauma, Is.EqualTo(1f));
        }

        [Test]
        public void ZeroMultiplierNeverShakes()
        {
            var model = CreateModel();
            for (var i = 0; i < 600; i++)
            {
                model.AddTrauma(0.3f);
                model.Tick(1f / 60f);

                Assert.That(model.Sample(0f).IsZero, Is.True, $"frame {i}");
                Assert.That(model.Intensity(0f), Is.Zero);
            }
        }

        [Test]
        public void ShakeGrowsWithTheSquareOfTrauma()
        {
            var model = CreateModel();
            model.AddTrauma(0.5f);

            Assert.That(model.Intensity(1f), Is.EqualTo(0.25f).Within(1e-6f));
            Assert.That(model.Intensity(2f), Is.EqualTo(0.5f).Within(1e-6f));
        }

        [Test]
        public void ShakeStaysWithinTheMaximum()
        {
            var model = CreateModel();
            var moved = false;
            for (var i = 0; i < 2000; i++)
            {
                model.AddTrauma(1f);
                model.Tick(1f / 60f);
                var sample = model.Sample(1f);

                Assert.That(Math.Abs(sample.Angles.X), Is.LessThanOrEqualTo(4f));
                Assert.That(Math.Abs(sample.Angles.Y), Is.LessThanOrEqualTo(4f));
                Assert.That(Math.Abs(sample.Angles.Z), Is.LessThanOrEqualTo(3f));
                Assert.That(Math.Abs(sample.Offset.X), Is.LessThanOrEqualTo(0.04f));
                Assert.That(Math.Abs(sample.Offset.Y), Is.LessThanOrEqualTo(0.04f));
                Assert.That(Math.Abs(sample.Offset.Z), Is.LessThanOrEqualTo(0.02f));
                moved |= !sample.IsZero;
            }

            Assert.That(moved, Is.True);
        }

        [Test]
        public void ShakeMovesSmoothlyBetweenFrames()
        {
            var model = CreateModel();
            model.DecayPerSecond = 0f;
            model.AddTrauma(1f);
            var previous = model.Sample(1f);
            for (var i = 0; i < 3000; i++)
            {
                model.Tick(1f / 600f);
                var sample = model.Sample(1f);

                // 15Hz で 1/600 秒なら格子の 0.025 しか進まない。2 倍に広げた後のノイズの傾きは 1 格子あたり 10 未満なので、最大 4 度の軸は 1 度未満しか動かない。
                Assert.That(Math.Abs(sample.Angles.X - previous.Angles.X), Is.LessThan(1f), $"frame {i}");
                previous = sample;
            }
        }

        [Test]
        public void TheSameSeedShakesTheSameWay()
        {
            var a = CreateModel(3);
            var b = CreateModel(3);
            var c = CreateModel(4);
            a.AddTrauma(1f);
            b.AddTrauma(1f);
            c.AddTrauma(1f);
            a.Tick(0.37f);
            b.Tick(0.37f);
            c.Tick(0.37f);

            Assert.That(a.Sample(1f).Angles, Is.EqualTo(b.Sample(1f).Angles));
            Assert.That(a.Sample(1f).Angles, Is.Not.EqualTo(c.Sample(1f).Angles));
        }

        [Test]
        public void ResetClearsTheTrauma()
        {
            var model = CreateModel();
            model.AddTrauma(0.9f);
            model.Tick(0.1f);

            model.Reset();

            Assert.That(model.Trauma, Is.Zero);
            Assert.That(model.Phase, Is.Zero);
        }

        [Test]
        public void NoiseStaysInRangeAndIsZeroOnTheLattice()
        {
            for (var i = -200; i <= 200; i++)
            {
                var x = i * 0.137f;
                var value = CameraShakeModel.Noise(11, x);
                Assert.That(value, Is.InRange(-1f, 1f), $"x={x}");
            }

            Assert.That(CameraShakeModel.Noise(11, 5f), Is.Zero);
            Assert.That(CameraShakeModel.Noise(11, -3f), Is.Zero);
        }

        [Test]
        public void NoiseRepeatsWithItsPeriod()
        {
            Assert.That(
                CameraShakeModel.Noise(5, 12.5f + CameraShakeModel.NoisePeriod),
                Is.EqualTo(CameraShakeModel.Noise(5, 12.5f)));
            Assert.That(CameraShakeModel.Noise(5, -0.5f), Is.EqualTo(CameraShakeModel.Noise(5, CameraShakeModel.NoisePeriod - 0.5f)));
        }

        [Test]
        public void PhaseWrapsAtTheNoisePeriod()
        {
            var model = CreateModel();
            model.Frequency = 1000f;

            for (var i = 0; i < 100; i++)
            {
                model.Tick(1f);
            }

            Assert.That(model.Phase, Is.InRange(0f, (float)CameraShakeModel.NoisePeriod));
        }

        [Test]
        public void RejectsInvalidValues()
        {
            var model = CreateModel();

            Assert.Throws<ArgumentOutOfRangeException>(() => model.AddTrauma(-0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => model.AddTrauma(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => model.Tick(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => model.Tick(float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => model.Sample(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => model.DecayPerSecond = -1f);
            Assert.Throws<ArgumentOutOfRangeException>(() => model.Frequency = float.NaN);
            Assert.Throws<ArgumentOutOfRangeException>(() => model.MaxAngles = new Vector3(1f, -1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => model.MaxOffset = new Vector3(float.PositiveInfinity, 0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => CameraShakeModel.Noise(0, float.NaN));
        }
    }
}
