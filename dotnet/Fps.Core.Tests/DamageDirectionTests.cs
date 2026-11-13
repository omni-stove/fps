using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class DamageDirectionTests
    {
        [TestCase(0f, 10f, 0f)]
        [TestCase(10f, 0f, 90f)]
        [TestCase(0f, -10f, 180f)]
        [TestCase(-10f, 0f, 270f)]
        [TestCase(5f, 5f, 45f)]
        [TestCase(-5f, 5f, 315f)]
        public void FacingPlusZTheAngleIsClockwiseFromTheTop(float x, float z, float expected)
        {
            Assert.That(DamageDirection.TryScreenAngle(Vector3.Zero, 0f, new Vector3(x, 0f, z), out var degrees), Is.True);

            Assert.That(degrees, Is.EqualTo(expected).Within(1e-3f));
        }

        [Test]
        public void TheAngleFollowsTheViewerYaw()
        {
            var source = new Vector3(10f, 0f, 0f);

            DamageDirection.TryScreenAngle(Vector3.Zero, 90f, source, out var ahead);
            DamageDirection.TryScreenAngle(Vector3.Zero, 180f, source, out var left);
            DamageDirection.TryScreenAngle(Vector3.Zero, -90f, source, out var behind);

            // +X を向けば正面、+X が左手になる -Z を向けば左、+X に背を向ければ後ろ
            Assert.That(ahead, Is.EqualTo(0f).Within(1e-3f));
            Assert.That(left, Is.EqualTo(270f).Within(1e-3f));
            Assert.That(behind, Is.EqualTo(180f).Within(1e-3f));
        }

        [Test]
        public void YawOutsideTheUsualRangeGivesTheSameAngle()
        {
            var source = new Vector3(3f, 0f, 4f);

            DamageDirection.TryScreenAngle(Vector3.Zero, 30f, source, out var plain);
            DamageDirection.TryScreenAngle(Vector3.Zero, 30f + 720f, source, out var wrapped);
            DamageDirection.TryScreenAngle(Vector3.Zero, 30f - 360f, source, out var negative);

            Assert.That(wrapped, Is.EqualTo(plain).Within(1e-3f));
            Assert.That(negative, Is.EqualTo(plain).Within(1e-3f));
        }

        [Test]
        public void HeightIsIgnoredAndTheViewerPositionIsTheOrigin()
        {
            var viewer = new Vector3(5f, 1.6f, 5f);

            Assert.That(DamageDirection.TryScreenAngle(viewer, 0f, new Vector3(5f, 30f, -20f), out var degrees), Is.True);

            Assert.That(degrees, Is.EqualTo(180f).Within(1e-3f), "far above but behind is still behind");
        }

        [Test]
        public void TheAngleStaysBelow360()
        {
            Assert.That(DamageDirection.TryScreenAngle(Vector3.Zero, 1e-5f, new Vector3(0f, 0f, 1f), out var degrees), Is.True);

            Assert.That(degrees, Is.GreaterThanOrEqualTo(0f).And.LessThan(360f));
        }

        [Test]
        public void NoDirectionWhenTheSourceIsAtTheViewerOrStraightAbove()
        {
            var viewer = new Vector3(1f, 1f, 1f);

            Assert.That(DamageDirection.TryScreenAngle(viewer, 0f, viewer, out var same), Is.False);
            Assert.That(DamageDirection.TryScreenAngle(viewer, 0f, new Vector3(1f, 10f, 1f), out var above), Is.False);
            Assert.That(same, Is.EqualTo(0f));
            Assert.That(above, Is.EqualTo(0f));
        }

        [TestCase(float.NaN, 0f)]
        [TestCase(0f, float.NaN)]
        [TestCase(float.PositiveInfinity, 0f)]
        public void NoDirectionForNonFiniteValues(float sourceX, float yaw)
        {
            Assert.That(DamageDirection.TryScreenAngle(Vector3.Zero, yaw, new Vector3(sourceX, 0f, 1f), out _), Is.False);
        }

        [TestCase(0f, 1f, 1f)]
        [TestCase(0.25f, 1f, 0.75f)]
        [TestCase(0.5f, 2f, 0.75f)]
        [TestCase(1f, 1f, 0f)]
        [TestCase(3f, 1f, 0f)]
        [TestCase(-1f, 1f, 1f)]
        public void OpacityFadesLinearlyToZero(float elapsed, float duration, float expected)
        {
            Assert.That(DamageDirection.Opacity(elapsed, duration), Is.EqualTo(expected).Within(1e-5f));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        public void OpacityIsZeroWithoutAPositiveDuration(float duration)
        {
            Assert.That(DamageDirection.Opacity(0f, duration), Is.EqualTo(0f));
        }

        [Test]
        public void TheDefaultDurationIsOneSecond()
        {
            Assert.That(DamageDirection.DefaultSeconds, Is.EqualTo(1f));
        }
    }
}
