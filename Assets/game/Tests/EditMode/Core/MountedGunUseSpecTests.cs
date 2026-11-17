using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class MountedGunUseSpecTests
    {
        [Test]
        public void DefaultsMatchTheConstants()
        {
            var spec = MountedGunUseSpec.Default;

            Assert.That(spec.SeekRadius, Is.EqualTo(8f));
            Assert.That(spec.GiveUpSeconds, Is.EqualTo(3f));
            Assert.That(spec.RemountDelaySeconds, Is.EqualTo(5f));
        }

        [Test]
        public void ZeroSeekRadiusAndZeroDelayAreAllowed()
        {
            var spec = new MountedGunUseSpec(0f, 0.5f, 0f);

            Assert.That(spec.SeekRadius, Is.EqualTo(0f));
            Assert.That(spec.RemountDelaySeconds, Is.EqualTo(0f));
        }

        [TestCase(-1f, 1f, 1f)]
        [TestCase(float.PositiveInfinity, 1f, 1f)]
        [TestCase(1f, 0f, 1f)]
        [TestCase(1f, float.NaN, 1f)]
        [TestCase(1f, 1f, -0.1f)]
        public void InvalidValuesAreRejected(float seekRadius, float giveUpSeconds, float remountDelaySeconds)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MountedGunUseSpec(seekRadius, giveUpSeconds, remountDelaySeconds));
        }
    }
}
