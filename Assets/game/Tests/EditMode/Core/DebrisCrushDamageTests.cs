using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class DebrisCrushDamageTests
    {
        [Test]
        public void DefaultUsesTheDefaultValues()
        {
            var damage = DebrisCrushDamage.Default;

            Assert.That(damage.MinSpeed, Is.EqualTo(DebrisCrushDamage.DefaultMinSpeed));
            Assert.That(damage.DamagePerMomentum, Is.EqualTo(DebrisCrushDamage.DefaultDamagePerMomentum));
            Assert.That(damage.MaxDamage, Is.EqualTo(DebrisCrushDamage.DefaultMaxDamage));
        }

        [Test]
        public void NothingAtOrBelowTheMinimumSpeed()
        {
            var damage = new DebrisCrushDamage(2f, 1f, 500);

            Assert.That(damage.At(0f, 100f), Is.EqualTo(0));
            Assert.That(damage.At(1.9f, 100f), Is.EqualTo(0));
            Assert.That(damage.At(2f, 100f), Is.EqualTo(0));
        }

        [Test]
        public void GrowsWithTheSpeedAboveTheMinimumAndTheMass()
        {
            var damage = new DebrisCrushDamage(2f, 0.5f, 1000);

            // (6 - 2) m/s × 10 kg × 0.5
            Assert.That(damage.At(6f, 10f), Is.EqualTo(20));
            Assert.That(damage.At(10f, 10f), Is.EqualTo(40), "faster debris hurts more");
            Assert.That(damage.At(6f, 20f), Is.EqualTo(40), "heavier debris hurts more");
        }

        [Test]
        public void RoundsDown()
        {
            var damage = new DebrisCrushDamage(0f, 1f, 100);

            Assert.That(damage.At(1f, 2.9f), Is.EqualTo(2));
            Assert.That(damage.At(3f, 0.3f), Is.EqualTo(0), "a tiny chip at a low speed does nothing");
        }

        [Test]
        public void IsCappedAtTheMaximum()
        {
            var damage = new DebrisCrushDamage(2f, 0.25f, 150);

            Assert.That(damage.At(50f, 100f), Is.EqualTo(150));
            Assert.That(damage.At(float.PositiveInfinity, 1f), Is.EqualTo(150));
        }

        [Test]
        public void ZeroFactorOrZeroMaximumNeverHurts()
        {
            Assert.That(new DebrisCrushDamage(0f, 0f, 100).At(float.PositiveInfinity, 100f), Is.EqualTo(0));
            Assert.That(new DebrisCrushDamage(0f, 1f, 0).At(10f, 100f), Is.EqualTo(0));
        }

        [Test]
        public void TheDefaultsLetAFallenPieceKillButNotAThrownChip()
        {
            var damage = DebrisCrushDamage.Default;

            // 1m の部品 (100kg) が 3m 落ちた速さと、射撃で飛び散る 0.3m の欠片 (2.7kg) が 3m/s で当たった場合。
            var fallSpeed = (float)Math.Sqrt(2.0 * 9.81 * 3.0);
            Assert.That(damage.At(fallSpeed, 100f), Is.GreaterThanOrEqualTo(100));
            Assert.That(damage.At(3f, 2.7f), Is.EqualTo(0));
        }

        [Test]
        public void RejectsInvalidSettings()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DebrisCrushDamage(-1f, 1f, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DebrisCrushDamage(float.NaN, 1f, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DebrisCrushDamage(float.PositiveInfinity, 1f, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DebrisCrushDamage(0f, -0.1f, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DebrisCrushDamage(0f, float.NaN, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DebrisCrushDamage(0f, float.PositiveInfinity, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DebrisCrushDamage(0f, 1f, -1));
        }

        [Test]
        public void RejectsInvalidImpacts()
        {
            var damage = DebrisCrushDamage.Default;

            Assert.Throws<ArgumentOutOfRangeException>(() => damage.At(-1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => damage.At(float.NaN, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => damage.At(5f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => damage.At(5f, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => damage.At(5f, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => damage.At(5f, float.PositiveInfinity));
        }
    }
}
