using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class PayloadRouteTests
    {
        private const float _tolerance = 1e-5f;

        // 3 m 東へ進み、4 m 北へ曲がる (長さ 7 m)。
        private static readonly Vector3[] _corner = { Vector3.Zero, new Vector3(3f, 0f, 0f), new Vector3(3f, 0f, 4f) };

        private static readonly Vector3[] _line =
        {
            Vector3.Zero, new Vector3(10f, 0f, 0f), new Vector3(20f, 0f, 0f), new Vector3(30f, 0f, 0f),
        };

        private static readonly Vector3[] _onePoint = { Vector3.Zero };
        private static readonly Vector3[] _repeated = { Vector3.Zero, Vector3.Zero, new Vector3(1f, 0f, 0f) };
        private static readonly Vector3[] _notFinite = { Vector3.Zero, new Vector3(float.NaN, 0f, 0f) };
        private static readonly Vector3[] _infinite = { Vector3.Zero, new Vector3(0f, float.PositiveInfinity, 0f) };

        private static readonly int[] _middleCheckpoints = { 1, 2 };
        private static readonly int[] _startCheckpoint = { 0 };
        private static readonly int[] _endCheckpoint = { 3 };
        private static readonly int[] _backwardCheckpoints = { 2, 1 };
        private static readonly int[] _repeatedCheckpoints = { 1, 1 };

        [Test]
        public void TheRouteRejectsTooFewOrBadPoints()
        {
            Assert.Throws<ArgumentNullException>(() => new PayloadRoute(null));
            Assert.Throws<ArgumentException>(() => new PayloadRoute(_onePoint));
            Assert.Throws<ArgumentException>(() => new PayloadRoute(_repeated), "a zero-length leg has no direction");
            Assert.Throws<ArgumentException>(() => new PayloadRoute(_notFinite));
            Assert.Throws<ArgumentException>(() => new PayloadRoute(_infinite));
        }

        [Test]
        public void CheckpointsMustBeInnerPointsInRouteOrder()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PayloadRoute(_line, _startCheckpoint), "the start is not a checkpoint");
            Assert.Throws<ArgumentOutOfRangeException>(() => new PayloadRoute(_line, _endCheckpoint), "the end finishes the match");
            Assert.Throws<ArgumentException>(() => new PayloadRoute(_line, _backwardCheckpoints));
            Assert.Throws<ArgumentException>(() => new PayloadRoute(_line, _repeatedCheckpoints));
            Assert.That(new PayloadRoute(_line).CheckpointCount, Is.Zero);
        }

        [Test]
        public void TheLengthAddsUpTheLegs()
        {
            var route = new PayloadRoute(_corner);

            Assert.That(route.PointCount, Is.EqualTo(3));
            Assert.That(route.Length, Is.EqualTo(7d).Within(_tolerance));
            Assert.That(route.DistanceAt(0), Is.Zero);
            Assert.That(route.DistanceAt(1), Is.EqualTo(3d).Within(_tolerance));
            Assert.That(route.DistanceAt(2), Is.EqualTo(7d).Within(_tolerance));
            Assert.That(route.Point(2), Is.EqualTo(new Vector3(3f, 0f, 4f)));
            Assert.Throws<ArgumentOutOfRangeException>(() => route.Point(3));
            Assert.Throws<ArgumentOutOfRangeException>(() => route.DistanceAt(-1));
        }

        [Test]
        public void ThePositionFollowsTheLegsAndStaysOnTheRoute()
        {
            var route = new PayloadRoute(_corner);

            AssertNear(route.PositionAt(1.5d), new Vector3(1.5f, 0f, 0f));
            AssertNear(route.PositionAt(3d), new Vector3(3f, 0f, 0f));
            AssertNear(route.PositionAt(5d), new Vector3(3f, 0f, 2f));
            AssertNear(route.PositionAt(-1d), Vector3.Zero);
            AssertNear(route.PositionAt(100d), new Vector3(3f, 0f, 4f));
            Assert.Throws<ArgumentOutOfRangeException>(() => route.PositionAt(double.NaN));
        }

        [Test]
        public void TheDirectionIsTheLegAhead()
        {
            var route = new PayloadRoute(_corner);

            AssertNear(route.DirectionAt(1d), Vector3.UnitX);
            AssertNear(route.DirectionAt(3d), Vector3.UnitZ, "at a corner the cart faces the next leg");
            AssertNear(route.DirectionAt(7d), Vector3.UnitZ, "at the end the cart faces along the last leg");
            AssertNear(route.DirectionAt(0d), Vector3.UnitX);
        }

        [Test]
        public void TheFractionIsTheShareOfTheLength()
        {
            var route = new PayloadRoute(_corner);

            Assert.That(route.FractionAt(0d), Is.Zero);
            Assert.That(route.FractionAt(3.5d), Is.EqualTo(0.5f).Within(_tolerance));
            Assert.That(route.FractionAt(7d), Is.EqualTo(1f));
            Assert.That(route.FractionAt(50d), Is.EqualTo(1f));
            Assert.That(route.Clamp(-3d), Is.Zero);
        }

        [Test]
        public void CheckpointsSitAtTheirPoints()
        {
            var route = new PayloadRoute(_line, _middleCheckpoints);

            Assert.That(route.CheckpointCount, Is.EqualTo(2));
            Assert.That(route.CheckpointPoint(0), Is.EqualTo(1));
            Assert.That(route.CheckpointPoint(1), Is.EqualTo(2));
            Assert.That(route.CheckpointDistance(0), Is.EqualTo(10d).Within(_tolerance));
            Assert.That(route.CheckpointDistance(1), Is.EqualTo(20d).Within(_tolerance));
            Assert.Throws<ArgumentOutOfRangeException>(() => route.CheckpointDistance(2));
            Assert.Throws<ArgumentOutOfRangeException>(() => route.CheckpointPoint(-1));

            Assert.That(route.CheckpointsReachedBy(9.99d), Is.Zero);
            Assert.That(route.CheckpointsReachedBy(10d), Is.EqualTo(1));
            Assert.That(route.CheckpointsReachedBy(25d), Is.EqualTo(2));
            Assert.That(route.CheckpointsReachedBy(30d), Is.EqualTo(2));
        }

        private static void AssertNear(Vector3 actual, Vector3 expected, string message = null)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(_tolerance), message ?? ("expected " + expected + " but was " + actual));
        }
    }
}
