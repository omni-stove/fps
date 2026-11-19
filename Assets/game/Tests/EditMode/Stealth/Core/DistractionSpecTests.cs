using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class DistractionSpecTests
    {
        [Test]
        public void KeepsTheValues()
        {
            var spec = new DistractionSpec(10f, 2f, 8f, 3f, 0f);

            Assert.That(spec.ThrowSpeed, Is.EqualTo(10f));
            Assert.That(spec.ThrowLiftSpeed, Is.EqualTo(2f));
            Assert.That(spec.NoiseRadius, Is.EqualTo(8f));
            Assert.That(spec.MaxFlightSeconds, Is.EqualTo(3f));
            Assert.That(spec.LingerSeconds, Is.EqualTo(0f));
            Assert.That(DistractionSpec.Default.NoiseRadius, Is.EqualTo(DistractionSpec.DefaultNoiseRadius));
        }

        [TestCase(0f, 3f, 15f, 6f, 4f, "throwSpeed")]
        [TestCase(16f, -1f, 15f, 6f, 4f, "throwLiftSpeed")]
        [TestCase(16f, 3f, 0f, 6f, 4f, "noiseRadius")]
        [TestCase(16f, 3f, float.PositiveInfinity, 6f, 4f, "noiseRadius")]
        [TestCase(16f, 3f, 15f, float.NaN, 4f, "maxFlightSeconds")]
        [TestCase(16f, 3f, 15f, 6f, -0.1f, "lingerSeconds")]
        public void RejectsInvalidValues(float speed, float lift, float radius, float flight, float linger, string name)
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(() => new DistractionSpec(speed, lift, radius, flight, linger));

            Assert.That(e.ParamName, Is.EqualTo(name));
        }
    }
}
