using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class CoverPointSelectorTests
    {
        private static readonly Vector3 _forward = new Vector3(0f, 0f, 1f);
        private static readonly Vector3 _threat = new Vector3(0f, 0f, 10f);

        private static CoverPoint Point(float x, float z, Vector3 facing, CoverHeight height = CoverHeight.Low) =>
            new CoverPoint(new Vector3(x, 0f, z), facing, height);

        [Test]
        public void SelectsTheNearestPointThatBlocksTheThreat()
        {
            var selector = new CoverPointSelector(new[]
            {
                Point(0f, 5f, _forward),
                Point(1f, 1f, _forward),
                Point(-3f, 0f, _forward),
            });

            Assert.That(selector.Select(Vector3.Zero, _threat), Is.EqualTo(1));
        }

        [Test]
        public void SkipsPointsThatDoNotFaceTheThreat()
        {
            var selector = new CoverPointSelector(new[]
            {
                Point(1f, 0f, new Vector3(0f, 0f, -1f)),
                Point(0f, 1f, new Vector3(1f, 0f, 0f)),
                Point(3f, 3f, _forward),
            });

            Assert.That(selector.Select(Vector3.Zero, _threat), Is.EqualTo(2));
            Assert.That(selector.Protects(0, _threat), Is.False, "背を向けた点");
            Assert.That(selector.Protects(1, _threat), Is.False, "横を向いた点");
        }

        [Test]
        public void BlocksUpToTheMaxCoverAngle()
        {
            var selector = new CoverPointSelector(new[] { Point(0f, 0f, _forward) });

            Assert.That(selector.Protects(0, Direction(60f) * 10f), Is.True, "ちょうど 60°");
            Assert.That(selector.Protects(0, Direction(61f) * 10f), Is.False, "61°");
            Assert.That(selector.Protects(0, Direction(-59f) * 10f), Is.True, "反対側の 59°");
        }

        [Test]
        public void IgnoresHeightWhenJudgingTheDirection()
        {
            var selector = new CoverPointSelector(new[] { new CoverPoint(Vector3.Zero, new Vector3(0f, -5f, 1f), CoverHeight.High) });

            Assert.That(selector.Points[0].Facing, Is.EqualTo(_forward));
            Assert.That(selector.Protects(0, new Vector3(0f, 30f, 5f)), Is.True);
        }

        [Test]
        public void ThreatTooCloseToThePointIsNotBlocked()
        {
            var selector = new CoverPointSelector(new[] { Point(0f, 0f, _forward) });

            Assert.That(selector.Protects(0, new Vector3(0f, 0f, 1.9f)), Is.False);
            Assert.That(selector.Protects(0, new Vector3(0f, 0f, 2f)), Is.True);
            Assert.That(selector.Select(new Vector3(0f, 0f, -1f), new Vector3(0f, 5f, 0f)), Is.EqualTo(-1), "真上の脅威は遮れない");
        }

        [Test]
        public void SkipsPointsBeyondMaxDistance()
        {
            var selector = new CoverPointSelector(new[] { Point(0f, -6f, _forward), Point(0f, 4f, _forward) }, maxDistance: 5f);

            Assert.That(selector.Select(Vector3.Zero, _threat), Is.EqualTo(1));
            Assert.That(selector.Select(new Vector3(0f, 0f, -10f), _threat), Is.Zero);
            Assert.That(selector.Select(new Vector3(0f, 0f, -20f), _threat), Is.EqualTo(-1));
        }

        [Test]
        public void SkipsPointsUsedByOtherOwnersButNotItsOwn()
        {
            var selector = new CoverPointSelector(new[] { Point(1f, 1f, _forward), Point(2f, 2f, _forward) });
            var first = new object();
            var second = new object();

            Assert.That(selector.TryClaim(0, first), Is.True);

            Assert.That(selector.Select(Vector3.Zero, _threat, second), Is.EqualTo(1));
            Assert.That(selector.Select(Vector3.Zero, _threat), Is.EqualTo(1), "持ち主を渡さなければ使われている点はすべて外す");
            Assert.That(selector.Select(Vector3.Zero, _threat, first), Is.Zero);
        }

        [Test]
        public void SkipsExcludedPoints()
        {
            var selector = new CoverPointSelector(new[] { Point(1f, 1f, _forward), Point(2f, 2f, _forward) });

            Assert.That(selector.Select(Vector3.Zero, _threat, null, new HashSet<int> { 0 }), Is.EqualTo(1));
            Assert.That(selector.Select(Vector3.Zero, _threat, null, new HashSet<int> { 0, 1 }), Is.EqualTo(-1));
        }

        [Test]
        public void PrefersTheLowerIndexOnATie()
        {
            var selector = new CoverPointSelector(new[] { Point(-1f, 1f, _forward), Point(1f, 1f, _forward) });

            Assert.That(selector.Select(Vector3.Zero, _threat), Is.Zero);
        }

        [Test]
        public void ReturnsMinusOneWithoutPoints()
        {
            var selector = new CoverPointSelector(Array.Empty<CoverPoint>());

            Assert.That(selector.Select(Vector3.Zero, _threat), Is.EqualTo(-1));
        }

        [Test]
        public void ClaimingAnotherPointReleasesThePreviousOne()
        {
            var selector = new CoverPointSelector(new[] { Point(1f, 1f, _forward), Point(2f, 2f, _forward) });
            var owner = new object();

            selector.TryClaim(0, owner);
            selector.TryClaim(1, owner);

            Assert.That(selector.IsOccupied(0), Is.False);
            Assert.That(selector.IsOccupied(1), Is.True);
            Assert.That(selector.IndexClaimedBy(owner), Is.EqualTo(1));
        }

        [Test]
        public void ClaimFailsWhenAnotherOwnerUsesThePoint()
        {
            var selector = new CoverPointSelector(new[] { Point(1f, 1f, _forward), Point(2f, 2f, _forward) });
            var first = new object();
            var second = new object();
            selector.TryClaim(0, first);
            selector.TryClaim(1, second);

            Assert.That(selector.TryClaim(0, second), Is.False);
            Assert.That(selector.IndexClaimedBy(second), Is.EqualTo(1), "失敗したら今の点を放さない");
            Assert.That(selector.IndexClaimedBy(first), Is.Zero);
        }

        [Test]
        public void ReleaseFreesThePoint()
        {
            var selector = new CoverPointSelector(new[] { Point(1f, 1f, _forward) });
            var owner = new object();
            selector.TryClaim(0, owner);

            Assert.That(selector.Release(owner), Is.True);
            Assert.That(selector.Release(owner), Is.False);
            Assert.That(selector.Release(null), Is.False);
            Assert.That(selector.IsOccupied(0), Is.False);
            Assert.That(selector.IndexClaimedBy(owner), Is.EqualTo(-1));
        }

        [Test]
        public void CopiesThePointList()
        {
            var points = new List<CoverPoint> { Point(1f, 1f, _forward) };
            var selector = new CoverPointSelector(points);

            points[0] = Point(9f, 9f, _forward);

            Assert.That(selector.Points[0].Position, Is.EqualTo(new Vector3(1f, 0f, 1f)));
        }

        [Test]
        public void CoverPointKeepsHeightAndNormalizesFacing()
        {
            var point = new CoverPoint(new Vector3(1f, 2f, 3f), new Vector3(3f, 0f, 4f), CoverHeight.High);

            Assert.That(point.Position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(point.Facing.X, Is.EqualTo(0.6f).Within(1e-5f));
            Assert.That(point.Facing.Z, Is.EqualTo(0.8f).Within(1e-5f));
            Assert.That(point.Height, Is.EqualTo(CoverHeight.High));
        }

        [Test]
        public void CoverPointRejectsInvalidValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CoverPoint(new Vector3(float.NaN, 0f, 0f), _forward, CoverHeight.Low));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CoverPoint(Vector3.Zero, new Vector3(0f, 0f, float.PositiveInfinity), CoverHeight.Low));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CoverPoint(Vector3.Zero, new Vector3(0f, 1f, 0f), CoverHeight.Low));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CoverPoint(Vector3.Zero, Vector3.Zero, CoverHeight.Low));
        }

        [Test]
        public void RejectsInvalidArguments()
        {
            var points = new[] { Point(1f, 1f, _forward) };
            Assert.Throws<ArgumentNullException>(() => new CoverPointSelector(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CoverPointSelector(points, maxDistance: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CoverPointSelector(points, maxDistance: float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CoverPointSelector(points, maxCoverAngleDegrees: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CoverPointSelector(points, maxCoverAngleDegrees: 181f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CoverPointSelector(points, minThreatDistance: float.NaN));

            var selector = new CoverPointSelector(points);
            Assert.Throws<ArgumentOutOfRangeException>(() => selector.Protects(1, _threat));
            Assert.Throws<ArgumentOutOfRangeException>(() => selector.Protects(0, new Vector3(float.NaN, 0f, 0f)));
            Assert.Throws<ArgumentOutOfRangeException>(() => selector.Select(new Vector3(float.NaN, 0f, 0f), _threat));
            Assert.Throws<ArgumentOutOfRangeException>(() => selector.TryClaim(-1, new object()));
            Assert.Throws<ArgumentNullException>(() => selector.TryClaim(0, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => selector.IsOccupied(1));
        }

        private static Vector3 Direction(float degreesFromForward)
        {
            var radians = degreesFromForward * (MathF.PI / 180f);
            return new Vector3(MathF.Sin(radians), 0f, MathF.Cos(radians));
        }
    }
}
