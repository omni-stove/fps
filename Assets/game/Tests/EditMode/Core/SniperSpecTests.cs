using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class SniperSpecTests
    {
        [Test]
        public void KeepsTheGivenValues()
        {
            var spec = new SniperSpec(2f, 4f, 0.5f, 6f);

            Assert.That(spec.AimSeconds, Is.EqualTo(2f));
            Assert.That(spec.ShotDamageMultiplier, Is.EqualTo(4f));
            Assert.That(spec.ReaimDelaySeconds, Is.EqualTo(0.5f));
            Assert.That(spec.RelocateGiveUpSeconds, Is.EqualTo(6f));
        }

        [Test]
        public void TheDefaultShowsTheBeamBeforeAStrongerShot()
        {
            Assert.That(SniperSpec.Default.AimSeconds, Is.GreaterThan(0f));
            Assert.That(SniperSpec.Default.ShotDamageMultiplier, Is.GreaterThan(1f));
        }

        [Test]
        public void InvalidValuesAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new SniperSpec(0f, 3f, 1f, 8f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new SniperSpec(float.PositiveInfinity, 3f, 1f, 8f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new SniperSpec(2f, 0f, 1f, 8f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new SniperSpec(2f, float.NaN, 1f, 8f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new SniperSpec(2f, 3f, -1f, 8f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new SniperSpec(2f, 3f, 1f, 0f));
        }
    }
}
