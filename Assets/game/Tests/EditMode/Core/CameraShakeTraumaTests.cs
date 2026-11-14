using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class CameraShakeTraumaTests
    {
        [Test]
        public void ExplosionFadesWithDistance()
        {
            Assert.That(CameraShakeTrauma.ForExplosion(0f, 0.8f, 20f), Is.EqualTo(0.8f).Within(1e-6f));
            Assert.That(CameraShakeTrauma.ForExplosion(10f, 0.8f, 20f), Is.EqualTo(0.4f).Within(1e-6f));
            Assert.That(CameraShakeTrauma.ForExplosion(20f, 0.8f, 20f), Is.Zero);
            Assert.That(CameraShakeTrauma.ForExplosion(100f, 0.8f, 20f), Is.Zero);
        }

        [Test]
        public void ExplosionRejectsInvalidValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CameraShakeTrauma.ForExplosion(-1f, 1f, 20f));
            Assert.Throws<ArgumentOutOfRangeException>(() => CameraShakeTrauma.ForExplosion(float.NaN, 1f, 20f));
            Assert.Throws<ArgumentOutOfRangeException>(() => CameraShakeTrauma.ForExplosion(1f, -1f, 20f));
            Assert.Throws<ArgumentOutOfRangeException>(() => CameraShakeTrauma.ForExplosion(1f, 1f, 0f));
        }

        [Test]
        public void DamageGrowsWithTheAmountUpToTheCap()
        {
            Assert.That(CameraShakeTrauma.ForDamage(0, 0.01f, 0.5f), Is.Zero);
            Assert.That(CameraShakeTrauma.ForDamage(-5, 0.01f, 0.5f), Is.Zero);
            Assert.That(CameraShakeTrauma.ForDamage(20, 0.01f, 0.5f), Is.EqualTo(0.2f).Within(1e-6f));
            Assert.That(CameraShakeTrauma.ForDamage(200, 0.01f, 0.5f), Is.EqualTo(0.5f));
        }

        [Test]
        public void LandingShakesOnlyAboveTheMinimumSpeed()
        {
            Assert.That(CameraShakeTrauma.ForLanding(5f, 7f, 17f, 0.6f), Is.Zero, "a jump landing does not shake");
            Assert.That(CameraShakeTrauma.ForLanding(7f, 7f, 17f, 0.6f), Is.Zero);
            Assert.That(CameraShakeTrauma.ForLanding(12f, 7f, 17f, 0.6f), Is.EqualTo(0.3f).Within(1e-6f));
            Assert.That(CameraShakeTrauma.ForLanding(30f, 7f, 17f, 0.6f), Is.EqualTo(0.6f).Within(1e-6f));
        }

        [Test]
        public void LandingRejectsInvalidValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CameraShakeTrauma.ForLanding(-1f, 7f, 17f, 0.6f));
            Assert.Throws<ArgumentOutOfRangeException>(() => CameraShakeTrauma.ForLanding(10f, 7f, 7f, 0.6f));
            Assert.Throws<ArgumentOutOfRangeException>(() => CameraShakeTrauma.ForLanding(10f, 7f, float.PositiveInfinity, 0.6f));
        }
    }
}
