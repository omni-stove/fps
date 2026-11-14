using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class GrappleSpecTests
    {
        private const float _radius = 0.5f;
        private const float _halfHeight = 1f;

        [Test]
        public void TheDefaultKeepsTheKitValues()
        {
            var spec = GrappleSpec.Default;

            Assert.That(spec.MinRange, Is.EqualTo(GrappleSpec.DefaultMinRange));
            Assert.That(spec.MaxRange, Is.EqualTo(GrappleSpec.DefaultMaxRange));
            Assert.That(spec.PullSpeed, Is.EqualTo(GrappleSpec.DefaultPullSpeed));
            Assert.That(spec.PullAcceleration, Is.EqualTo(GrappleSpec.DefaultPullAcceleration));
            Assert.That(spec.ArriveDistance, Is.EqualTo(GrappleSpec.DefaultArriveDistance));
            Assert.That(spec.MaxPullSeconds, Is.EqualTo(GrappleSpec.DefaultMaxPullSeconds));
            Assert.That(spec.HangHeight, Is.EqualTo(GrappleSpec.DefaultHangHeight));
            Assert.That(spec.EdgeSnapDistance, Is.EqualTo(GrappleSpec.DefaultEdgeSnapDistance));
            Assert.That(spec.HangSeconds, Is.EqualTo(GrappleSpec.DefaultHangSeconds));
        }

        [Test]
        public void InvalidValuesAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GrappleSpec(minRange: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GrappleSpec(maxRange: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GrappleSpec(maxRange: float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GrappleSpec(minRange: 10f, maxRange: 10f), "the range must not be empty");
            Assert.Throws<ArgumentOutOfRangeException>(() => new GrappleSpec(pullSpeed: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GrappleSpec(pullAcceleration: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GrappleSpec(arriveDistance: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GrappleSpec(maxPullSeconds: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GrappleSpec(hangHeight: -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GrappleSpec(edgeSnapDistance: -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GrappleSpec(hangSeconds: float.NaN));
            Assert.DoesNotThrow(() => new GrappleSpec(minRange: 0f, edgeSnapDistance: 0f, hangSeconds: 0f, hangHeight: 0f));
        }

        [Test]
        public void TheRangeIncludesBothEnds()
        {
            var spec = new GrappleSpec(minRange: 2f, maxRange: 20f);

            Assert.That(spec.InRange(1.99f), Is.False);
            Assert.That(spec.InRange(2f), Is.True);
            Assert.That(spec.InRange(20f), Is.True);
            Assert.That(spec.InRange(20.01f), Is.False);
            Assert.That(spec.InRange(float.NaN), Is.False);
        }

        [Test]
        public void OnAWallTheHandIsAtTheAnchorInFrontOfTheFace()
        {
            var spec = new GrappleSpec(hangHeight: 0.8f);
            var anchor = new Vector3(10f, 5f, 3f);

            var point = spec.HangPoint(anchor, -Vector3.UnitX, _radius, _halfHeight);

            Assert.That(point.X, Is.EqualTo(10f - _radius - GrappleSpec.SurfaceGap).Within(1e-5f));
            Assert.That(point.Y, Is.EqualTo(4.2f).Within(1e-5f));
            Assert.That(point.Z, Is.EqualTo(3f).Within(1e-5f));
        }

        [Test]
        public void ASlantedWallOnlyUsesItsHorizontalNormal()
        {
            var spec = new GrappleSpec(hangHeight: 0.8f);
            var normal = Vector3.Normalize(new Vector3(0f, 0.5f, -1f));

            var point = spec.HangPoint(Vector3.Zero, normal, _radius, _halfHeight);

            Assert.That(point.Z, Is.EqualTo(-(_radius + GrappleSpec.SurfaceGap)).Within(1e-5f));
            Assert.That(point.Y, Is.EqualTo(-0.8f).Within(1e-5f));
        }

        [Test]
        public void OnAFloorTheBodyStandsOnTopAndUnderACeilingItHangsBelow()
        {
            var spec = GrappleSpec.Default;
            var anchor = new Vector3(1f, 3f, 2f);

            var onTop = spec.HangPoint(anchor, Vector3.UnitY, _radius, _halfHeight);
            var below = spec.HangPoint(anchor, -Vector3.UnitY, _radius, _halfHeight);

            Assert.That(onTop, Is.EqualTo(new Vector3(1f, 3f + _halfHeight + GrappleSpec.SurfaceGap, 2f)));
            Assert.That(below, Is.EqualTo(new Vector3(1f, 3f - _halfHeight - GrappleSpec.SurfaceGap, 2f)));
        }

        [Test]
        public void HangPointRejectsBadInput()
        {
            var spec = GrappleSpec.Default;

            Assert.Throws<ArgumentException>(() => spec.HangPoint(Vector3.Zero, Vector3.Zero, _radius, _halfHeight));
            Assert.Throws<ArgumentException>(() => spec.HangPoint(new Vector3(float.NaN, 0f, 0f), Vector3.UnitY, _radius, _halfHeight));
            Assert.Throws<ArgumentOutOfRangeException>(() => spec.HangPoint(Vector3.Zero, Vector3.UnitY, -1f, _halfHeight));
        }

        [Test]
        public void OnlySteepSurfacesAreWalls()
        {
            Assert.That(GrappleSpec.IsWall(Vector3.UnitX), Is.True);
            Assert.That(GrappleSpec.IsWall(Vector3.Normalize(new Vector3(1f, 0.5f, 0f))), Is.True);
            Assert.That(GrappleSpec.IsWall(Vector3.UnitY), Is.False);
            Assert.That(GrappleSpec.IsWall(-Vector3.UnitY), Is.False);
            Assert.That(GrappleSpec.IsWall(Vector3.Zero), Is.False);
        }
    }
}
