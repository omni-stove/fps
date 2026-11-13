using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class VaultPathTests
    {
        private const float _tolerance = 1e-4f;

        // 1 m 上がって 2 m 進む、長さ 3 m の折れ線。
        private static VaultPath UpThenForward(float speed = 3f)
        {
            return new VaultPath(new[] { Vector3.Zero, new Vector3(0f, 1f, 0f), new Vector3(0f, 1f, 2f) }, speed);
        }

        [Test]
        public void MeasuresLengthAndDuration()
        {
            var path = UpThenForward();

            Assert.That(path.Length, Is.EqualTo(3f).Within(_tolerance));
            Assert.That(path.Speed, Is.EqualTo(3f));
            Assert.That(path.Duration, Is.EqualTo(1f).Within(_tolerance));
            Assert.That(path.End, Is.EqualTo(new Vector3(0f, 1f, 2f)));
            Assert.That(path.Points, Has.Count.EqualTo(3));
        }

        [Test]
        public void FollowsTheSegmentsAtConstantSpeed()
        {
            var path = UpThenForward();

            AssertNear(path.PositionAt(0f), Vector3.Zero);
            AssertNear(path.PositionAt(1f / 6f), new Vector3(0f, 0.5f, 0f));
            AssertNear(path.PositionAt(1f / 3f), new Vector3(0f, 1f, 0f));
            AssertNear(path.PositionAt(2f / 3f), new Vector3(0f, 1f, 1f));
        }

        [Test]
        public void StaysAtTheEndAfterTheDuration()
        {
            var path = UpThenForward();

            AssertNear(path.PositionAt(5f), path.End);
            AssertNear(path.PositionAt(float.PositiveInfinity), path.End);
        }

        [TestCase(0.016f)]
        [TestCase(0.05f)]
        [TestCase(0.3f)]
        public void DisplacementsAddUpToTheEndForAnyFrameStep(float step)
        {
            var path = UpThenForward();
            var total = Vector3.Zero;
            var elapsed = 0f;
            while (elapsed < path.Duration)
            {
                var next = Math.Min(path.Duration, elapsed + step);
                total += path.Displacement(elapsed, next);
                elapsed = next;
            }

            AssertNear(total, path.End);
        }

        [Test]
        public void SkipsZeroLengthSegments()
        {
            var path = new VaultPath(new[] { Vector3.Zero, Vector3.Zero, new Vector3(1f, 0f, 0f) }, 1f);

            Assert.That(path.Length, Is.EqualTo(1f).Within(_tolerance));
            AssertNear(path.PositionAt(0.5f), new Vector3(0.5f, 0f, 0f));
        }

        [Test]
        public void AZeroLengthPathEndsAtOnce()
        {
            var path = new VaultPath(new[] { Vector3.Zero, Vector3.Zero }, 1f);

            Assert.That(path.Duration, Is.EqualTo(0f));
            AssertNear(path.PositionAt(0f), Vector3.Zero);
        }

        [Test]
        public void RejectsInvalidArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new VaultPath(null, 1f));
            Assert.Throws<ArgumentException>(() => new VaultPath(new[] { Vector3.Zero }, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VaultPath(new[] { Vector3.Zero, Vector3.UnitX }, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VaultPath(new[] { Vector3.Zero, Vector3.UnitX }, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VaultPath(new[] { Vector3.Zero, Vector3.UnitX }, float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VaultPath(new[] { Vector3.Zero, new Vector3(float.NaN, 0f, 0f) }, 1f));

            var path = UpThenForward();
            Assert.Throws<ArgumentOutOfRangeException>(() => path.PositionAt(-0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => path.PositionAt(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => path.Displacement(0.5f, 0.2f));
        }

        private static void AssertNear(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.X, Is.EqualTo(expected.X).Within(_tolerance), "X");
            Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(_tolerance), "Y");
            Assert.That(actual.Z, Is.EqualTo(expected.Z).Within(_tolerance), "Z");
        }
    }
}
