using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class HostileSelectorTests
    {
        private static readonly Side _red = new Side(1);
        private static readonly Side _blue = new Side(2);

        [Test]
        public void PicksTheNearestCandidateOfAnotherSide()
        {
            var candidates = new[]
            {
                new HostileCandidate(Side.Alpha, new Vector3(0f, 0f, 20f), true),
                new HostileCandidate(_blue, new Vector3(0f, 0f, 8f), true),
                new HostileCandidate(Side.Alpha, new Vector3(0f, 0f, 5f), true),
            };

            Assert.That(HostileSelector.Nearest(_red, Vector3.Zero, candidates), Is.EqualTo(2));
        }

        [Test]
        public void SkipsOwnSideAndTheDead()
        {
            var candidates = new[]
            {
                new HostileCandidate(_red, new Vector3(1f, 0f, 0f), true),
                new HostileCandidate(Side.Alpha, new Vector3(2f, 0f, 0f), false),
                new HostileCandidate(_blue, new Vector3(30f, 0f, 0f), true),
            };

            Assert.That(HostileSelector.Nearest(_red, Vector3.Zero, candidates), Is.EqualTo(2));
        }

        [Test]
        public void ReturnsMinusOneWhenNoEnemyIsLeft()
        {
            var candidates = new[]
            {
                new HostileCandidate(_red, new Vector3(1f, 0f, 0f), true),
                new HostileCandidate(_blue, new Vector3(2f, 0f, 0f), false),
            };

            Assert.That(HostileSelector.Nearest(_red, Vector3.Zero, candidates), Is.EqualTo(-1));
            Assert.That(HostileSelector.Nearest(_red, Vector3.Zero, Array.Empty<HostileCandidate>()), Is.EqualTo(-1));
        }

        [Test]
        public void TiesGoToTheEarlierCandidate()
        {
            var candidates = new[]
            {
                new HostileCandidate(_blue, new Vector3(-4f, 0f, 0f), true),
                new HostileCandidate(Side.Alpha, new Vector3(4f, 0f, 0f), true),
            };

            Assert.That(HostileSelector.Nearest(_red, Vector3.Zero, candidates), Is.Zero);
        }

        [Test]
        public void IgnoresCandidatesWithBrokenPositions()
        {
            var candidates = new[]
            {
                new HostileCandidate(_blue, new Vector3(float.NaN, 0f, 0f), true),
                new HostileCandidate(Side.Alpha, new Vector3(9f, 0f, 0f), true),
            };

            Assert.That(HostileSelector.Nearest(_red, Vector3.Zero, candidates), Is.EqualTo(1));
        }

        [Test]
        public void RejectsBadArguments()
        {
            Assert.Throws<ArgumentNullException>(() => HostileSelector.Nearest(_red, Vector3.Zero, null));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => HostileSelector.Nearest(_red, new Vector3(float.PositiveInfinity, 0f, 0f), Array.Empty<HostileCandidate>()));
        }
    }
}
