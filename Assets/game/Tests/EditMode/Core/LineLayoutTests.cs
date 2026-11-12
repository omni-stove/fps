using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class LineLayoutTests
    {
        [Test]
        public void CentersAnOddCountOnZero()
        {
            var offsets = LineLayout.CenteredOffsets(5, 2f);

            var expected = new[] { -4f, -2f, 0f, 2f, 4f };
            Assert.That(offsets, Is.EqualTo(expected));
        }

        [Test]
        public void CentersAnEvenCountBetweenTheMiddlePair()
        {
            var offsets = LineLayout.CenteredOffsets(2, 3f);

            var expected = new[] { -1.5f, 1.5f };
            Assert.That(offsets, Is.EqualTo(expected));
        }

        [Test]
        public void SingleItemSitsOnTheCenter()
        {
            var offsets = LineLayout.CenteredOffsets(1, 2f);

            var expected = new[] { 0f };
            Assert.That(offsets, Is.EqualTo(expected));
        }

        [Test]
        public void ZeroCountIsEmpty()
        {
            Assert.That(LineLayout.CenteredOffsets(0, 2f), Is.Empty);
        }

        [Test]
        public void RejectsNegativeCount()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => LineLayout.CenteredOffsets(-1, 2f));
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void RejectsBadSpacing(float spacing)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => LineLayout.CenteredOffsets(3, spacing));
        }
    }
}
