using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class CapsuleHitZoneTests
    {
        // 高さ 2、底が -1 (中心が 0) のカプセル
        // 頭は割合 0.85 (高さ 0.7) 以上、手足は 0.4 (高さ -0.2) 未満。境目そのものは float の丸めに左右されるので避ける
        [TestCase(0.9f, HitZone.Head)]
        [TestCase(0.71f, HitZone.Head)]
        [TestCase(0.69f, HitZone.Body)]
        [TestCase(0f, HitZone.Body)]
        [TestCase(-0.19f, HitZone.Body)]
        [TestCase(-0.21f, HitZone.Limb)]
        [TestCase(-0.9f, HitZone.Limb)]
        public void ClassifiesByHeightFraction(float hitHeight, HitZone expected)
        {
            Assert.That(CapsuleHitZone.Classify(hitHeight, -1f, 2f), Is.EqualTo(expected));
        }

        [Test]
        public void HeightsOutsideTheCapsuleUseTheNearestEnd()
        {
            Assert.That(CapsuleHitZone.Classify(5f, -1f, 2f), Is.EqualTo(HitZone.Head));
            Assert.That(CapsuleHitZone.Classify(-5f, -1f, 2f), Is.EqualTo(HitZone.Limb));
        }

        [Test]
        public void RejectsInvalidInput()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CapsuleHitZone.Classify(float.NaN, -1f, 2f));
            Assert.Throws<ArgumentOutOfRangeException>(() => CapsuleHitZone.Classify(0f, float.PositiveInfinity, 2f));
            Assert.Throws<ArgumentOutOfRangeException>(() => CapsuleHitZone.Classify(0f, -1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => CapsuleHitZone.Classify(0f, -1f, float.NaN));
        }
    }
}
