using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class StanceGeometryTests
    {
        private const float _tolerance = 1e-5f;

        // GameBootstrap のプレイヤー: 高さ 2、半径 0.5、center は原点、目は中心から 0.6 上 (足元から 1.6)。
        private static StanceGeometry Player(float crouchHeight = 1.2f)
        {
            return new StanceGeometry(
                standingHeight: 2f, standingCenterY: 0f, standingEyeY: 0.6f, crouchHeight: crouchHeight, radius: 0.5f);
        }

        [Test]
        public void StandingKeepsTheGivenShape()
        {
            var standing = Player().Standing;

            Assert.That(standing.Height, Is.EqualTo(2f));
            Assert.That(standing.CenterY, Is.EqualTo(0f));
            Assert.That(standing.EyeY, Is.EqualTo(0.6f));
            Assert.That(standing.BottomY, Is.EqualTo(-1f));
            Assert.That(standing.TopY, Is.EqualTo(1f));
        }

        [Test]
        public void CrouchShrinksHeightAndLowersCenterKeepingFeet()
        {
            var crouched = Player().Crouched;

            Assert.That(crouched.Height, Is.EqualTo(1.2f).Within(_tolerance));
            Assert.That(crouched.CenterY, Is.EqualTo(-0.4f).Within(_tolerance));
            Assert.That(crouched.BottomY, Is.EqualTo(-1f).Within(_tolerance));
            Assert.That(crouched.TopY, Is.EqualTo(0.2f).Within(_tolerance));
        }

        [Test]
        public void CrouchLowersEyeByTheHeightDifference()
        {
            var geometry = Player();

            Assert.That(geometry.Crouched.EyeY, Is.EqualTo(-0.2f).Within(_tolerance));
            Assert.That(
                geometry.Standing.TopY - geometry.Standing.EyeY,
                Is.EqualTo(geometry.Crouched.TopY - geometry.Crouched.EyeY).Within(_tolerance));
        }

        [Test]
        public void OffsetCenterKeepsFeet()
        {
            // 足元が本体の原点にあるカプセル (center が height / 2)
            var geometry = new StanceGeometry(1.8f, 0.9f, 1.6f, 1f, 0.3f);

            Assert.That(geometry.Crouched.BottomY, Is.EqualTo(0f).Within(_tolerance));
            Assert.That(geometry.Crouched.CenterY, Is.EqualTo(0.5f).Within(_tolerance));
            Assert.That(geometry.Crouched.EyeY, Is.EqualTo(0.8f).Within(_tolerance));
        }

        [Test]
        public void EyeDoesNotGoBelowFeet()
        {
            // 目が頭のてっぺんから遠いと、高さの差だけ下げると足元より下になる
            var geometry = new StanceGeometry(2f, 0f, -0.5f, 1f, 0.5f);

            Assert.That(geometry.Crouched.EyeY, Is.EqualTo(-1f).Within(_tolerance));
        }

        [Test]
        public void CrouchAtStandingHeightChangesNothing()
        {
            var geometry = Player(crouchHeight: 2f);

            Assert.That(geometry.Crouched.Height, Is.EqualTo(geometry.Standing.Height));
            Assert.That(geometry.Crouched.CenterY, Is.EqualTo(geometry.Standing.CenterY));
            Assert.That(geometry.Crouched.EyeY, Is.EqualTo(geometry.Standing.EyeY));
        }

        [Test]
        public void ShapeSelectsByStance()
        {
            var geometry = Player();

            Assert.That(geometry.Shape(false), Is.EqualTo(geometry.Standing));
            Assert.That(geometry.Shape(true), Is.EqualTo(geometry.Crouched));
        }

        [Test]
        public void SphereCentersAreRadiusInsideTheEnds()
        {
            var geometry = Player();

            geometry.SphereCenters(geometry.Standing, out var standingBottom, out var standingTop);
            geometry.SphereCenters(geometry.Crouched, out var crouchedBottom, out var crouchedTop);

            Assert.That(standingBottom, Is.EqualTo(-0.5f).Within(_tolerance));
            Assert.That(standingTop, Is.EqualTo(0.5f).Within(_tolerance));
            Assert.That(crouchedBottom, Is.EqualTo(-0.5f).Within(_tolerance));
            Assert.That(crouchedTop, Is.EqualTo(-0.3f).Within(_tolerance));
        }

        [Test]
        public void SphereCentersMeetAtCenterForASphere()
        {
            var geometry = Player(crouchHeight: 1f);

            geometry.SphereCenters(geometry.Crouched, out var bottom, out var top);

            Assert.That(bottom, Is.EqualTo(geometry.Crouched.CenterY).Within(_tolerance));
            Assert.That(top, Is.EqualTo(geometry.Crouched.CenterY).Within(_tolerance));
        }

        [TestCase(0f, 0f, 0.6f, 1.2f, 0.5f)]
        [TestCase(-2f, 0f, 0.6f, 1.2f, 0.5f)]
        [TestCase(float.PositiveInfinity, 0f, 0.6f, 1.2f, 0.5f)]
        [TestCase(2f, float.NaN, 0.6f, 1.2f, 0.5f)]
        [TestCase(2f, 0f, float.PositiveInfinity, 1.2f, 0.5f)]
        [TestCase(2f, 0f, 0.6f, 1.2f, -0.1f)]
        [TestCase(2f, 0f, 0.6f, 1.2f, float.NaN)]
        [TestCase(2f, 0f, 0.6f, 0f, 0.5f)]
        [TestCase(2f, 0f, 0.6f, 2.1f, 0.5f)]
        [TestCase(2f, 0f, 0.6f, 0.9f, 0.5f)]
        [TestCase(2f, 0f, 0.6f, float.NaN, 0.5f)]
        public void RejectsInvalidValues(float standingHeight, float centerY, float eyeY, float crouchHeight, float radius)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new StanceGeometry(standingHeight, centerY, eyeY, crouchHeight, radius));
        }
    }
}
