using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class NearMissTests
    {
        private const float _epsilon = 1e-5f;

        [Test]
        public void MeasuresTheDistanceFromTheLineBesideIt()
        {
            var found = NearMiss.TryDistance(Vector3.Zero, new Vector3(0f, 0f, 2f), 10f, new Vector3(0.6f, 0f, 5f), out var distance, out var along);

            Assert.That(found, Is.True);
            Assert.That(distance, Is.EqualTo(0.6f).Within(_epsilon));
            Assert.That(along, Is.EqualTo(5f).Within(_epsilon));
        }

        [Test]
        public void PointsBehindTheShooterAreNotNearMisses()
        {
            var found = NearMiss.TryDistance(Vector3.Zero, Vector3.UnitZ, 10f, new Vector3(0.2f, 0f, -1f), out var distance, out _);

            Assert.That(found, Is.False);
            Assert.That(distance, Is.EqualTo(0f));
        }

        [Test]
        public void PastWhereTheBulletStoppedTheDistanceIsFromTheStop()
        {
            var found = NearMiss.TryDistance(Vector3.Zero, Vector3.UnitZ, 4f, new Vector3(0f, 0f, 5f), out var distance, out var along);

            Assert.That(found, Is.True);
            Assert.That(along, Is.EqualTo(4f).Within(_epsilon));
            Assert.That(distance, Is.EqualTo(1f).Within(_epsilon));
        }

        [Test]
        public void RejectsAZeroDirectionAndABadLength()
        {
            Assert.Throws<ArgumentException>(() => NearMiss.TryDistance(Vector3.Zero, Vector3.Zero, 1f, Vector3.One, out _, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => NearMiss.TryDistance(Vector3.Zero, Vector3.UnitZ, -1f, Vector3.One, out _, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => NearMiss.TryDistance(Vector3.Zero, Vector3.UnitZ, float.NaN, Vector3.One, out _, out _));
        }
    }
}
