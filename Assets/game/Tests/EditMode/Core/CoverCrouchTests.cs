using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    // 立った頭のてっぺんは 1.8 m、一番低い姿勢は 1.2 m、壁の上端から 0.05 m 下げる。
    public sealed class CoverCrouchTests
    {
        private const float _standingTop = 1.8f;
        private const float _lowestTop = 1.2f;
        private const float _clearance = 0.05f;
        private const float _tolerance = 1e-4f;

        [TestCase(3f, 0f)]
        [TestCase(1.85f, 0f)]
        [TestCase(1.55f, 0.5f)]
        [TestCase(1.25f, 1f)]
        [TestCase(0.8f, 1f)]
        public void TheAmountPutsTheHeadJustBelowTheWallTop(float wallHeight, float expected)
        {
            Assert.That(CoverCrouch.Amount(wallHeight, _standingTop, _lowestTop, _clearance), Is.EqualTo(expected).Within(_tolerance));
        }

        [Test]
        public void TheHeadTopFollowsTheWallBetweenTheTwoPoses()
        {
            var amount = CoverCrouch.Amount(1.45f, _standingTop, _lowestTop, _clearance);
            var headTop = _standingTop + ((_lowestTop - _standingTop) * amount);

            Assert.That(headTop, Is.EqualTo(1.4f).Within(_tolerance));
        }

        [Test]
        public void NoClearanceMeetsTheWallTop()
        {
            Assert.That(CoverCrouch.Amount(1.5f, _standingTop, _lowestTop, 0f), Is.EqualTo(0.5f).Within(_tolerance));
        }

        [TestCase(false, false)]
        [TestCase(true, true)]
        [TestCase(false, true)]
        public void OutOfCoverOrPeekingStandsUp(bool inCover, bool peeking)
        {
            Assert.That(CoverCrouch.Amount(inCover, peeking, 1.25f, _standingTop, _lowestTop, _clearance), Is.EqualTo(0f));
        }

        [Test]
        public void InCoverAndNotPeekingCrouches()
        {
            Assert.That(CoverCrouch.Amount(true, false, 1.55f, _standingTop, _lowestTop, _clearance), Is.EqualTo(0.5f).Within(_tolerance));
        }

        [TestCase(-0.1f, _standingTop, _lowestTop, _clearance, "wallHeight")]
        [TestCase(float.NaN, _standingTop, _lowestTop, _clearance, "wallHeight")]
        [TestCase(1.5f, _standingTop, -0.1f, _clearance, "lowestTop")]
        [TestCase(1.5f, _standingTop, _lowestTop, -0.1f, "headClearance")]
        [TestCase(1.5f, _lowestTop, _lowestTop, _clearance, "standingTop")]
        [TestCase(1.5f, 1f, _lowestTop, _clearance, "standingTop")]
        [TestCase(1.5f, float.PositiveInfinity, _lowestTop, _clearance, "standingTop")]
        public void OutOfRangeValuesNameTheParameter(float wallHeight, float standingTop, float lowestTop, float clearance, string name)
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(() => CoverCrouch.Amount(wallHeight, standingTop, lowestTop, clearance));

            Assert.That(e.ParamName, Is.EqualTo(name));
        }
    }
}
