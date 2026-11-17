using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class MinimapProjectionTests
    {
        private const float _tolerance = 1e-3f;

        private static readonly MinimapSpec _northUp = new MinimapSpec(rotateWithPlayer: false);

        private static readonly MinimapSpec _rotating = new MinimapSpec(rotateWithPlayer: true);

        [Test]
        public void NorthUpKeepsEastRightAndNorthUpWhateverTheHeading()
        {
            var self = new Vector3(10f, 0f, 20f);

            var facingNorth = MinimapProjection.ToMap(_northUp, self, 0f, new Vector3(13f, 0f, 24f), out var edgeNorth);
            var facingEast = MinimapProjection.ToMap(_northUp, self, 90f, new Vector3(13f, 0f, 24f), out var edgeEast);

            AssertPoint(facingNorth, 6f, 8f);
            AssertPoint(facingEast, 6f, 8f);
            Assert.That(edgeNorth, Is.False);
            Assert.That(edgeEast, Is.False);
        }

        [Test]
        public void RotatingPutsTheHeadingUp()
        {
            var self = Vector3.Zero;

            AssertPoint(MinimapProjection.ToMap(_rotating, self, 0f, new Vector3(0f, 0f, 10f), out _), 0f, 20f);
            AssertPoint(MinimapProjection.ToMap(_rotating, self, 90f, new Vector3(10f, 0f, 0f), out _), 0f, 20f);
            AssertPoint(MinimapProjection.ToMap(_rotating, self, 90f, new Vector3(0f, 0f, 10f), out _), -20f, 0f);
            AssertPoint(MinimapProjection.ToMap(_rotating, self, 180f, new Vector3(0f, 0f, 10f), out _), 0f, -20f);
            AssertPoint(MinimapProjection.ToMap(_rotating, self, -90f, new Vector3(-10f, 0f, 0f), out _), 0f, 20f);
            AssertPoint(MinimapProjection.ToMap(_rotating, self, 45f, new Vector3(3f, 0f, 3f), out _), 0f, 8.485281f);
        }

        [Test]
        public void BeyondTheRadiusIsPulledToTheEdgeInTheSameDirection()
        {
            var self = Vector3.Zero;

            var north = MinimapProjection.ToMap(_northUp, self, 0f, new Vector3(0f, 0f, 100f), out var northEdge);
            var diagonal = MinimapProjection.ToMap(_northUp, self, 0f, new Vector3(60f, 0f, 80f), out var diagonalEdge);
            var behind = MinimapProjection.ToMap(_rotating, self, 90f, new Vector3(-300f, 0f, 0f), out var behindEdge);
            var atRadius = MinimapProjection.ToMap(_northUp, self, 0f, new Vector3(0f, 0f, 50f), out var atRadiusEdge);

            AssertPoint(north, 0f, 100f);
            Assert.That(northEdge, Is.True);
            AssertPoint(diagonal, 60f, 80f);
            Assert.That(diagonalEdge, Is.True);
            AssertPoint(behind, 0f, -100f);
            Assert.That(behindEdge, Is.True);
            AssertPoint(atRadius, 0f, 100f);
            Assert.That(atRadiusEdge, Is.False, "exactly on the radius is still inside");
        }

        [Test]
        public void StraightAboveOrBelowIsTheCentre()
        {
            var self = new Vector3(5f, 0f, 5f);

            var above = MinimapProjection.ToMap(_rotating, self, 45f, new Vector3(5f, 30f, 5f), out var aboveEdge);
            var below = MinimapProjection.ToMap(_northUp, self, 45f, new Vector3(5f, -300f, 5f), out var belowEdge);

            AssertPoint(above, 0f, 0f);
            AssertPoint(below, 0f, 0f);
            Assert.That(aboveEdge, Is.False, "height does not count towards the radius");
            Assert.That(belowEdge, Is.False, "height does not count towards the radius");
        }

        [Test]
        public void ChangedJsonValuesAreUsedInTheProjection()
        {
            var lines = new List<string>();
            var changed = MinimapJson.Parse("{\"radius\": 20, \"scale\": 0.5, \"rotateWithPlayer\": false}", "minimap.json", lines.Add);
            var bundled = MinimapJson.Parse("{}", "minimap.json", lines.Add);
            var target = new Vector3(0f, 0f, 30f);

            var withChanged = MinimapProjection.ToMap(changed, Vector3.Zero, 90f, target, out var changedEdge);
            var withDefaults = MinimapProjection.ToMap(bundled, Vector3.Zero, 90f, target, out var defaultEdge);

            AssertPoint(withChanged, 0f, 10f);
            Assert.That(changedEdge, Is.True);
            AssertPoint(withDefaults, -60f, 0f);
            Assert.That(defaultEdge, Is.False);
            Assert.That(changed.MapRadius, Is.EqualTo(10f));
        }

        [Test]
        public void TheStatusOverloadReadsTheSelfPoseFromTheStatus()
        {
            var contact = new MinimapContact(MinimapContactKind.Enemy, new Vector3(10f, 0f, 0f), 0f, Team.Bravo);
            var status = new MinimapStatus(Vector3.Zero, 90f, new[] { contact });

            AssertPoint(MinimapProjection.ToMap(_rotating, status, contact, out var onEdge), 0f, 20f);
            Assert.That(onEdge, Is.False);
        }

        [Test]
        public void MarkersTurnWithTheMapOnlyWhenItRotates()
        {
            Assert.That(MinimapProjection.MapHeadingDegrees(_rotating, 90f, 0f), Is.EqualTo(270f).Within(_tolerance));
            Assert.That(MinimapProjection.MapHeadingDegrees(_rotating, 90f, 90f), Is.EqualTo(0f).Within(_tolerance));
            Assert.That(MinimapProjection.MapHeadingDegrees(_rotating, 350f, 10f), Is.EqualTo(20f).Within(_tolerance));
            Assert.That(MinimapProjection.MapHeadingDegrees(_northUp, 90f, 45f), Is.EqualTo(45f).Within(_tolerance));
        }

        [Test]
        public void HeadingIsMeasuredClockwiseFromNorth()
        {
            AssertHeading(new Vector3(0f, 0f, 1f), 0f);
            AssertHeading(new Vector3(1f, 0f, 0f), 90f);
            AssertHeading(new Vector3(0f, 0f, -1f), 180f);
            AssertHeading(new Vector3(-1f, 0f, 0f), 270f);
            AssertHeading(new Vector3(1f, 0f, 1f), 45f);
            AssertHeading(new Vector3(0f, -0.5f, 0.5f), 0f);
        }

        [Test]
        public void LookingStraightUpOrDownHasNoHeading()
        {
            Assert.That(MinimapProjection.TryHeadingDegrees(new Vector3(0f, 1f, 0f), out var up), Is.False);
            Assert.That(MinimapProjection.TryHeadingDegrees(new Vector3(0f, -1f, 0f), out var down), Is.False);
            Assert.That(MinimapProjection.TryHeadingDegrees(new Vector3(0f, 1f, 0.00001f), out _), Is.False);
            Assert.That(up, Is.EqualTo(0f));
            Assert.That(down, Is.EqualTo(0f));
        }

        [Test]
        public void BearingPointsFromOnePositionToAnother()
        {
            Assert.That(MinimapProjection.TryBearingDegrees(new Vector3(10f, 0f, 10f), new Vector3(10f, 4f, 0f), out var south), Is.True);
            Assert.That(south, Is.EqualTo(180f).Within(_tolerance));
            Assert.That(MinimapProjection.TryBearingDegrees(new Vector3(10f, 0f, 10f), new Vector3(10f, 5f, 10f), out _), Is.False);
        }

        [Test]
        public void RelativeDegreesAreSignedWithRightPositive()
        {
            Assert.That(MinimapProjection.RelativeDegrees(350f, 10f), Is.EqualTo(20f).Within(_tolerance));
            Assert.That(MinimapProjection.RelativeDegrees(10f, 350f), Is.EqualTo(-20f).Within(_tolerance));
            Assert.That(MinimapProjection.RelativeDegrees(0f, 90f), Is.EqualTo(90f).Within(_tolerance));
            Assert.That(MinimapProjection.RelativeDegrees(0f, 180f), Is.EqualTo(-180f).Within(_tolerance));
            Assert.That(MinimapProjection.RelativeDegrees(90f, 90f), Is.EqualTo(0f).Within(_tolerance));
        }

        [Test]
        public void BadArgumentsAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => MinimapProjection.ToMap(null, Vector3.Zero, 0f, Vector3.Zero, out _));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => MinimapProjection.ToMap(_northUp, Vector3.Zero, float.NaN, Vector3.Zero, out _));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => MinimapProjection.ToMap(_northUp, Vector3.Zero, 0f, new Vector3(float.PositiveInfinity, 0f, 0f), out _));
            Assert.Throws<ArgumentNullException>(() => MinimapProjection.MapHeadingDegrees(null, 0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => MinimapProjection.TryHeadingDegrees(new Vector3(float.NaN, 0f, 1f), out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => MinimapProjection.RelativeDegrees(0f, float.NegativeInfinity));
        }

        private static void AssertHeading(Vector3 direction, float expected)
        {
            Assert.That(MinimapProjection.TryHeadingDegrees(direction, out var degrees), Is.True, direction.ToString());
            Assert.That(degrees, Is.EqualTo(expected).Within(_tolerance), direction.ToString());
        }

        private static void AssertPoint(Vector2 actual, float x, float y)
        {
            Assert.That(actual.X, Is.EqualTo(x).Within(_tolerance), "x of " + actual);
            Assert.That(actual.Y, Is.EqualTo(y).Within(_tolerance), "y of " + actual);
        }
    }
}
