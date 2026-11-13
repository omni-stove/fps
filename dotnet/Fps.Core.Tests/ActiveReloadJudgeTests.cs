using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class ActiveReloadJudgeTests
    {
        // 幅は 2 進で割り切れる値にして、境界の float の誤差を判定から外す。
        [TestCase(0.5f, ActiveReloadResult.Success)]
        [TestCase(0.75f, ActiveReloadResult.Success)]
        [TestCase(0.625f, ActiveReloadResult.Success)]
        [TestCase(0.49999997f, ActiveReloadResult.Fail)]
        [TestCase(0.75000006f, ActiveReloadResult.Fail)]
        [TestCase(0f, ActiveReloadResult.Fail)]
        [TestCase(1f, ActiveReloadResult.Fail)]
        public void BothEndsOfTheWindowSucceed(float progress, ActiveReloadResult expected)
        {
            Assert.That(ActiveReloadJudge.Judge(progress, 0.5f, 0.25f), Is.EqualTo(expected));
        }

        [Test]
        public void AWindowAtTheStartOrTheEndOfTheReloadIncludesItsEdge()
        {
            Assert.That(ActiveReloadJudge.Judge(0f, 0f, 0.25f), Is.EqualTo(ActiveReloadResult.Success));
            Assert.That(ActiveReloadJudge.Judge(1f, 0.75f, 0.25f), Is.EqualTo(ActiveReloadResult.Success));
        }

        [Test]
        public void TheSpecDefaultWindowAcceptsItsOwnEnds()
        {
            var start = WeaponSpec.DefaultActiveReloadWindowStart;
            var length = WeaponSpec.DefaultActiveReloadWindowLength;

            Assert.That(ActiveReloadJudge.Judge(start, start, length), Is.EqualTo(ActiveReloadResult.Success));
            Assert.That(ActiveReloadJudge.Judge(start + length, start, length), Is.EqualTo(ActiveReloadResult.Success));
        }

        [TestCase(-0.5f)]
        [TestCase(1.5f)]
        [TestCase(float.NegativeInfinity)]
        [TestCase(float.PositiveInfinity)]
        public void ProgressOutsideTheReloadFails(float progress)
        {
            Assert.That(ActiveReloadJudge.Judge(progress, 0.5f, 0.25f), Is.EqualTo(ActiveReloadResult.Fail));
        }

        [Test]
        public void RejectsNaNProgress()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ActiveReloadJudge.Judge(float.NaN, 0.5f, 0.25f));
        }

        [TestCase(-0.1f, 0.25f)]
        [TestCase(1f, 0.25f)]
        [TestCase(float.NaN, 0.25f)]
        [TestCase(0.5f, 0f)]
        [TestCase(0.5f, -0.1f)]
        [TestCase(0.5f, 0.75f)]
        [TestCase(0.5f, float.NaN)]
        [TestCase(0.5f, float.PositiveInfinity)]
        public void RejectsAnInvalidWindow(float windowStart, float windowLength)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ActiveReloadJudge.Judge(0.5f, windowStart, windowLength));
        }
    }
}
