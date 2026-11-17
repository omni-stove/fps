using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class SprayConeTests
    {
        private const float _range = 8f;

        private static readonly SpraySpec _spray = new SpraySpec(0.1f, 20f);
        private static readonly Vector3 _origin = new Vector3(1f, 1.5f, -2f);

        [TestCase(0f, 0f, 2f)]
        [TestCase(0f, 0f, 8f)]
        [TestCase(0.5f, 0f, 3f)]
        [TestCase(0f, -0.6f, 2f)]
        [TestCase(0f, 0f, 0f)]
        public void APointInsideTheConeIsReached(float x, float y, float z)
        {
            Assert.That(SprayCone.Contains(_spray, _range, _origin, Vector3.UnitZ, _origin + new Vector3(x, y, z)), Is.True);
        }

        [TestCase(0f, 0f, 8.01f, TestName = "beyond the range")]
        [TestCase(0f, 0f, -2f, TestName = "behind the nozzle")]
        [TestCase(2f, 0f, 3f, TestName = "outside the cone")]
        [TestCase(0f, 3f, 3f, TestName = "above the cone")]
        public void APointOutsideTheConeIsNotReached(float x, float y, float z)
        {
            Assert.That(SprayCone.Contains(_spray, _range, _origin, Vector3.UnitZ, _origin + new Vector3(x, y, z)), Is.False);
        }

        [Test]
        public void TheEdgeOfTheConeCountsAsInsideAndTheAimNeedNotBeNormalized()
        {
            var radians = 20f * MathF.PI / 180f;
            var edge = _origin + (new Vector3(MathF.Sin(radians), 0f, MathF.Cos(radians)) * 5f);
            var aim = new Vector3(0f, 0f, 3f);

            Assert.That(SprayCone.Contains(_spray, _range, _origin, aim, edge), Is.True);
            Assert.That(SprayCone.Contains(new SpraySpec(0.1f, 19f), _range, _origin, aim, edge), Is.False);
        }

        [Test]
        public void TheConeFollowsTheAim()
        {
            var right = _origin + new Vector3(4f, 0f, 0f);

            Assert.That(SprayCone.Contains(_spray, _range, _origin, Vector3.UnitZ, right), Is.False);
            Assert.That(SprayCone.Contains(_spray, _range, _origin, Vector3.UnitX, right), Is.True);
        }

        [Test]
        public void TheNearestPointOfABodyIsOnTheAxisWhenTheAxisPassesThroughIt()
        {
            var bottom = new Vector3(0.2f, 0.5f, 4f);
            var top = new Vector3(0.2f, 2f, 4f);

            var point = SprayCone.NearestPoint(_range, new Vector3(0f, 1.5f, 0f), Vector3.UnitZ, bottom, top, 0.4f);

            Assert.That(Vector3.Distance(point, new Vector3(0f, 1.5f, 4f)), Is.LessThan(1e-4f));
        }

        [Test]
        public void TheEdgeOfABodyBesideTheAxisIsInsideTheConeWhileItsCenterIsNot()
        {
            var origin = new Vector3(0f, 1.5f, 0f);
            var bottom = new Vector3(1.6f, 0.5f, 4f);
            var top = new Vector3(1.6f, 2f, 4f);
            var spray = new SpraySpec(0.1f, 20f);

            var point = SprayCone.NearestPoint(_range, origin, Vector3.UnitZ, bottom, top, 0.4f);

            Assert.That(point.X, Is.EqualTo(1.2f).Within(1e-4f), "the side of the body facing the axis");
            Assert.That(point.Y, Is.EqualTo(1.5f).Within(1e-4f));
            Assert.That(SprayCone.Contains(spray, _range, origin, Vector3.UnitZ, point), Is.True);
            Assert.That(SprayCone.Contains(spray, _range, origin, Vector3.UnitZ, new Vector3(1.6f, 1.5f, 4f)), Is.False);
        }

        [Test]
        public void ABodyBeyondTheRangeIsMeasuredFromItsNearestSide()
        {
            var origin = Vector3.Zero;
            var bottom = new Vector3(0f, -1f, 9f);
            var top = new Vector3(0f, 1f, 9f);

            var point = SprayCone.NearestPoint(_range, origin, Vector3.UnitZ, bottom, top, 0.5f);

            Assert.That(point.Z, Is.EqualTo(8.5f).Within(1e-4f));
            Assert.That(SprayCone.Contains(_spray, _range, origin, Vector3.UnitZ, point), Is.False);
            Assert.That(SprayCone.Contains(_spray, 9f, origin, Vector3.UnitZ, SprayCone.NearestPoint(9f, origin, Vector3.UnitZ, bottom, top, 0.5f)), Is.True);
        }

        [Test]
        public void RejectsInvalidInput()
        {
            Assert.Throws<ArgumentNullException>(() => SprayCone.Contains(null, _range, _origin, Vector3.UnitZ, _origin));
            Assert.Throws<ArgumentOutOfRangeException>(() => SprayCone.Contains(_spray, 0f, _origin, Vector3.UnitZ, _origin));
            Assert.Throws<ArgumentOutOfRangeException>(() => SprayCone.Contains(_spray, _range, _origin, Vector3.Zero, _origin));
            Assert.Throws<ArgumentOutOfRangeException>(() => SprayCone.Contains(_spray, _range, _origin, Vector3.UnitZ, new Vector3(float.NaN, 0f, 0f)));
            Assert.Throws<ArgumentOutOfRangeException>(() => SprayCone.NearestPoint(_range, _origin, Vector3.UnitZ, Vector3.Zero, Vector3.UnitY, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => SprayCone.NearestPoint(_range, _origin, Vector3.UnitZ, new Vector3(float.PositiveInfinity, 0f, 0f), Vector3.UnitY, 0.5f));
        }
    }
}
