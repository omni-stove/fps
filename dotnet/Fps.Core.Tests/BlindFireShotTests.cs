using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class BlindFireShotTests
    {
        private const float _tolerance = 1e-5f;
        private const int _samples = 10000;

        // +Z を向いて壁に張り付いたプレイヤー。法線はこちら向き (-Z)、壁に向いて右は +X。
        private static readonly Vector3 _normal = new Vector3(0f, 0f, -1f);
        private static readonly Vector3 _eye = new Vector3(2f, 0.8f, 3f);
        private const float _radius = 0.5f;
        private const float _topY = 1f;

        private static bool Resolve(CoverHeight height, CoverEdge edges, out Vector3 origin, float preferredSide = 1f)
        {
            return BlindFireShot.TryResolveOrigin(height, edges, preferredSide, _normal, _topY, _radius, _eye, out origin);
        }

        private static float AngleDegrees(Vector3 a, Vector3 b)
        {
            var cos = Math.Clamp(Vector3.Dot(Vector3.Normalize(a), Vector3.Normalize(b)), -1f, 1f);
            return (float)(Math.Acos(cos) * 180.0 / Math.PI);
        }

        [Test]
        public void LowWallRaisesTheOriginAboveTheTopAndOutAlongTheNormal()
        {
            Assert.That(Resolve(CoverHeight.Low, CoverEdge.None, out var origin), Is.True);

            Assert.That(origin.X, Is.EqualTo(_eye.X).Within(_tolerance));
            Assert.That(origin.Y, Is.EqualTo(_topY + BlindFireShot.LowWallClearance).Within(_tolerance));
            Assert.That(origin.Z, Is.EqualTo(_eye.Z - BlindFireShot.LowWallNormalOffset).Within(_tolerance));
        }

        [Test]
        public void LowWallIgnoresTheEdgesAndTheVerticalPartOfTheNormal()
        {
            Assert.That(
                BlindFireShot.TryResolveOrigin(
                    CoverHeight.Low, CoverEdge.Left | CoverEdge.Right, -1f, new Vector3(0f, 0.2f, -2f), _topY, _radius, _eye, out var origin),
                Is.True);

            Assert.That(origin.Y, Is.EqualTo(1.15f).Within(_tolerance));
            Assert.That(origin.Z, Is.EqualTo(_eye.Z - 0.1f).Within(_tolerance));
        }

        [Test]
        public void HighWallAtTheRightEdgeStepsRightByTheRadiusAndMargin()
        {
            Assert.That(Resolve(CoverHeight.High, CoverEdge.Right, out var origin), Is.True);

            Assert.That(origin.X, Is.EqualTo(_eye.X + _radius + BlindFireShot.HighWallSideMargin).Within(_tolerance));
            Assert.That(origin.Y, Is.EqualTo(_eye.Y).Within(_tolerance), "stays at eye height");
            Assert.That(origin.Z, Is.EqualTo(_eye.Z).Within(_tolerance));
        }

        [Test]
        public void HighWallAtTheLeftEdgeStepsLeft()
        {
            Assert.That(Resolve(CoverHeight.High, CoverEdge.Left, out var origin), Is.True);

            Assert.That(origin.X, Is.EqualTo(_eye.X - 0.65f).Within(_tolerance));
        }

        [TestCase(1f, 1)]
        [TestCase(0f, 1)]
        [TestCase(-1f, -1)]
        public void HighWallOpenOnBothSidesUsesThePreferredSide(float preferredSide, int expectedSide)
        {
            Assert.That(Resolve(CoverHeight.High, CoverEdge.Left | CoverEdge.Right, out var origin, preferredSide), Is.True);

            Assert.That(origin.X, Is.EqualTo(_eye.X + (expectedSide * 0.65f)).Within(_tolerance));
        }

        [Test]
        public void HighWallAwayFromTheEdgesCannotFire()
        {
            Assert.That(Resolve(CoverHeight.High, CoverEdge.None, out var origin), Is.False);
            Assert.That(origin, Is.EqualTo(_eye));
            Assert.That(BlindFireShot.EdgeSide(CoverEdge.None, -1f), Is.Zero);
        }

        [Test]
        public void HighWallSideFollowsTheWallNotTheWorld()
        {
            // +X を向いて張り付いた壁 (法線 -X)。壁に向いて右は -Z。
            Assert.That(
                BlindFireShot.TryResolveOrigin(
                    CoverHeight.High, CoverEdge.Right, 1f, new Vector3(-1f, 0f, 0f), _topY, _radius, _eye, out var origin),
                Is.True);

            Assert.That(origin.X, Is.EqualTo(_eye.X).Within(_tolerance));
            Assert.That(origin.Z, Is.EqualTo(_eye.Z - 0.65f).Within(_tolerance));
        }

        [Test]
        public void ResolveOriginRejectsBadValues()
        {
            Assert.Throws<ArgumentException>(
                () => BlindFireShot.TryResolveOrigin(CoverHeight.Low, CoverEdge.None, 1f, Vector3.UnitY, _topY, _radius, _eye, out _));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => BlindFireShot.TryResolveOrigin(CoverHeight.Low, CoverEdge.None, 1f, _normal, float.NaN, _radius, _eye, out _));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => BlindFireShot.TryResolveOrigin(CoverHeight.Low, CoverEdge.None, 1f, _normal, _topY, -0.1f, _eye, out _));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => BlindFireShot.TryResolveOrigin(
                    CoverHeight.Low, CoverEdge.None, 1f, _normal, _topY, _radius, new Vector3(float.PositiveInfinity, 0f, 0f), out _));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => BlindFireShot.TryResolveOrigin((CoverHeight)9, CoverEdge.None, 1f, _normal, _topY, _radius, _eye, out _));
        }

        [TestCase(8f)]
        [TestCase(12f)]
        [TestCase(90f)]
        public void SpreadNeverExceedsTheHalfAngle(float halfAngle)
        {
            var random = new Random(1234);
            var aim = new Vector3(0.3f, -0.2f, 1f);
            var widest = 0f;

            for (var i = 0; i < _samples; i++)
            {
                var direction = BlindFireShot.Spread(aim, halfAngle, random);

                Assert.That(direction.Length(), Is.EqualTo(1f).Within(1e-4f));
                widest = Math.Max(widest, AngleDegrees(aim, direction));
            }

            Assert.That(widest, Is.LessThanOrEqualTo(halfAngle + 1e-2f));
            Assert.That(widest, Is.GreaterThan(halfAngle * 0.95f), "samples reach near the rim of the cone");
        }

        [Test]
        public void SpreadIsUniformOverTheCone()
        {
            // 半角 α の円錐の中で一様なら、半角 α/2 の内側に入る割合は (1 - cos(α/2)) / (1 - cos α) (極角で一様なら 1/2)。
            const float halfAngle = 60f;
            var random = new Random(42);
            var inner = 0;
            for (var i = 0; i < _samples; i++)
            {
                if (AngleDegrees(Vector3.UnitZ, BlindFireShot.Spread(Vector3.UnitZ, halfAngle, random)) <= halfAngle * 0.5f)
                {
                    inner++;
                }
            }

            var expected = (1.0 - Math.Cos(Math.PI / 6.0)) / (1.0 - Math.Cos(Math.PI / 3.0));
            Assert.That(inner / (double)_samples, Is.EqualTo(expected).Within(0.02));
        }

        [Test]
        public void SpreadReachesEverySideOfTheAim()
        {
            var random = new Random(7);
            var up = 0;
            var down = 0;
            var left = 0;
            var right = 0;
            for (var i = 0; i < 1000; i++)
            {
                var direction = BlindFireShot.Spread(Vector3.UnitZ, 8f, random);
                up += direction.Y > 0f ? 1 : 0;
                down += direction.Y < 0f ? 1 : 0;
                right += direction.X > 0f ? 1 : 0;
                left += direction.X < 0f ? 1 : 0;
            }

            Assert.That(new[] { up, down, left, right }, Has.All.GreaterThan(400));
        }

        [Test]
        public void ZeroSpreadKeepsTheAimAndDrawsNoRandomNumbers()
        {
            var aim = new Vector3(0.3f, -0.2f, 1f);
            var random = new Random(99);

            var direction = BlindFireShot.Spread(aim, 0f, random);

            Assert.That(Vector3.Distance(direction, Vector3.Normalize(aim)), Is.LessThan(_tolerance));
            Assert.That(random.Next(), Is.EqualTo(new Random(99).Next()), "the random sequence is untouched");
        }

        [Test]
        public void SpreadWorksStraightUpAndDown()
        {
            var random = new Random(3);
            foreach (var aim in new[] { Vector3.UnitY, -Vector3.UnitY })
            {
                for (var i = 0; i < 100; i++)
                {
                    Assert.That(AngleDegrees(aim, BlindFireShot.Spread(aim, 10f, random)), Is.LessThanOrEqualTo(10.01f));
                }
            }
        }

        [Test]
        public void SpreadIsRepeatableWithTheSameSeed()
        {
            var a = BlindFireShot.Spread(Vector3.UnitZ, 8f, new Random(5));
            var b = BlindFireShot.Spread(Vector3.UnitZ, 8f, new Random(5));

            Assert.That(a, Is.EqualTo(b));
        }

        [Test]
        public void SpreadRejectsBadArguments()
        {
            var random = new Random(0);
            Assert.Throws<ArgumentNullException>(() => BlindFireShot.Spread(Vector3.UnitZ, 8f, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => BlindFireShot.Spread(Vector3.Zero, 8f, random));
            Assert.Throws<ArgumentOutOfRangeException>(() => BlindFireShot.Spread(new Vector3(float.NaN, 0f, 1f), 8f, random));
            Assert.Throws<ArgumentOutOfRangeException>(() => BlindFireShot.Spread(Vector3.UnitZ, -1f, random));
            Assert.Throws<ArgumentOutOfRangeException>(() => BlindFireShot.Spread(Vector3.UnitZ, 90.5f, random));
            Assert.Throws<ArgumentOutOfRangeException>(() => BlindFireShot.Spread(Vector3.UnitZ, float.NaN, random));
        }

        [TestCase(8f, "[Combat] blind-fire spread=8")]
        [TestCase(12.5f, "[Combat] blind-fire spread=12.5")]
        [TestCase(0f, "[Combat] blind-fire spread=0")]
        public void FormatsTheLog(float spread, string expected)
        {
            Assert.That(BlindFireShot.FormatLog(spread), Is.EqualTo(expected));
        }
    }
}
