using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class AirstrikeLineTests
    {
        private const float _tolerance = 1e-4f;

        [Test]
        public void TheHeadingIsMeasuredFromNorthTowardEast()
        {
            AssertClose(AirstrikeLine.Direction(0f), new Vector3(0f, 0f, 1f));
            AssertClose(AirstrikeLine.Direction(90f), new Vector3(1f, 0f, 0f));
            AssertClose(AirstrikeLine.Direction(180f), new Vector3(0f, 0f, -1f));
            AssertClose(AirstrikeLine.Direction(-90f), new Vector3(-1f, 0f, 0f));
            AssertClose(AirstrikeLine.Direction(450f), new Vector3(1f, 0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => AirstrikeLine.Direction(float.NaN));
        }

        [Test]
        public void TheShellsAreSpreadEvenlyFromTheBackEndToTheFrontEnd()
        {
            var center = new Vector3(10f, 2f, -4f);

            AssertClose(AirstrikeLine.PointOnLine(center, 90f, 24f, 0, 5), new Vector3(-2f, 2f, -4f));
            AssertClose(AirstrikeLine.PointOnLine(center, 90f, 24f, 1, 5), new Vector3(4f, 2f, -4f));
            AssertClose(AirstrikeLine.PointOnLine(center, 90f, 24f, 2, 5), center);
            AssertClose(AirstrikeLine.PointOnLine(center, 90f, 24f, 3, 5), new Vector3(16f, 2f, -4f));
            AssertClose(AirstrikeLine.PointOnLine(center, 90f, 24f, 4, 5), new Vector3(22f, 2f, -4f));
            AssertClose(AirstrikeLine.PointOnLine(center, 0f, 10f, 0, 2), new Vector3(10f, 2f, -9f));
            AssertClose(AirstrikeLine.PointOnLine(center, 0f, 10f, 1, 2), new Vector3(10f, 2f, 1f));
        }

        [Test]
        public void ASingleShellOrAZeroLineLandsOnTheCentre()
        {
            var center = new Vector3(3f, 0f, 3f);

            Assert.That(AirstrikeLine.PointOnLine(center, 45f, 30f, 0, 1), Is.EqualTo(center));
            for (var i = 0; i < 4; i++)
            {
                AssertClose(AirstrikeLine.PointOnLine(center, 45f, 0f, i, 4), center);
            }
        }

        [Test]
        public void PointOnLineRejectsInvalidArguments()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => AirstrikeLine.PointOnLine(Vector3.Zero, 0f, -1f, 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => AirstrikeLine.PointOnLine(Vector3.Zero, 0f, float.PositiveInfinity, 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => AirstrikeLine.PointOnLine(Vector3.Zero, 0f, 1f, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => AirstrikeLine.PointOnLine(Vector3.Zero, 0f, 1f, 3, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => AirstrikeLine.PointOnLine(Vector3.Zero, 0f, 1f, -1, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => AirstrikeLine.PointOnLine(new Vector3(float.NaN, 0f, 0f), 0f, 1f, 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => AirstrikeLine.PointOnLine(Vector3.Zero, float.NaN, 1f, 0, 1));
        }

        [Test]
        public void ScatterStaysInTheCircleAndFollowsTheSeed()
        {
            var first = new Random(11);
            var second = new Random(11);
            var center = new Vector3(3f, 2f, -4f);

            for (var i = 0; i < 100; i++)
            {
                var point = AirstrikeLine.Scatter(center, 5f, first);
                Assert.That(point, Is.EqualTo(AirstrikeLine.Scatter(center, 5f, second)));
                Assert.That(point.Y, Is.EqualTo(center.Y));
                Assert.That(new Vector2(point.X - center.X, point.Z - center.Z).Length(), Is.LessThanOrEqualTo(5f + _tolerance));
            }

            Assert.That(AirstrikeLine.Scatter(center, 0f, first), Is.EqualTo(center));
            Assert.Throws<ArgumentNullException>(() => AirstrikeLine.Scatter(center, 1f, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => AirstrikeLine.Scatter(center, -1f, first));
        }

        [Test]
        public void AShellPointIsTheLinePointMovedByTheSpread()
        {
            var spec = new StreakAirstrikeSpec(shellCount: 3, spread: 2f, lineLength: 20f);
            var center = new Vector3(0f, 1f, 0f);
            var expected = AirstrikeLine.Scatter(AirstrikeLine.PointOnLine(center, 0f, 20f, 2, 3), 2f, new Random(5));

            var point = AirstrikeLine.ShellPoint(center, 0f, spec, 2, new Random(5));

            Assert.That(point, Is.EqualTo(expected));
            Assert.That(Vector3.Distance(point, new Vector3(0f, 1f, 10f)), Is.LessThanOrEqualTo(2f + _tolerance));
        }

        [Test]
        public void WithoutSpreadAShellPointDrawsNoRandomNumber()
        {
            var spec = new StreakAirstrikeSpec(shellCount: 2, spread: 0f, lineLength: 8f);
            var random = new Random(9);
            var untouched = new Random(9);

            var point = AirstrikeLine.ShellPoint(Vector3.Zero, 180f, spec, 0, random);

            AssertClose(point, new Vector3(0f, 0f, 4f));
            Assert.That(random.Next(), Is.EqualTo(untouched.Next()), "the random sequence is left for the next shell");
            Assert.Throws<ArgumentNullException>(() => AirstrikeLine.ShellPoint(Vector3.Zero, 0f, null, 0, random));
        }

        [Test]
        public void TheRangeIsMeasuredOnTheGround()
        {
            var origin = new Vector3(0f, 5f, 0f);

            Assert.That(AirstrikeLine.IsWithinRange(origin, new Vector3(30f, -40f, 40f), 50f), Is.True, "height is not counted");
            Assert.That(AirstrikeLine.IsWithinRange(origin, new Vector3(30f, 5f, 40.1f), 50f), Is.False);
        }

        private static void AssertClose(Vector3 actual, Vector3 expected)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(_tolerance), actual + " should be " + expected);
        }
    }
}
