using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class StanceGeometryProneTests
    {
        private const float _tolerance = 1e-5f;

        // GameBootstrap のプレイヤー: 高さ 2、半径 0.5、center は原点、目は中心から 0.6 上 (足元から 1.6)。しゃがみ 1.2、伏せ 1。
        private static StanceGeometry Player(float proneHeight = 1f)
        {
            return new StanceGeometry(
                standingHeight: 2f, standingCenterY: 0f, standingEyeY: 0.6f, crouchHeight: 1.2f, proneHeight: proneHeight, radius: 0.5f);
        }

        [Test]
        public void ProneShrinksToItsHeightKeepingFeet()
        {
            var prone = Player().Prone;

            Assert.That(prone.Height, Is.EqualTo(1f).Within(_tolerance));
            Assert.That(prone.BottomY, Is.EqualTo(-1f).Within(_tolerance));
            Assert.That(prone.CenterY, Is.EqualTo(-0.5f).Within(_tolerance));
            Assert.That(prone.TopY, Is.EqualTo(0f).Within(_tolerance));
        }

        [Test]
        public void ProneLowersEyeByTheHeightDifference()
        {
            var geometry = Player();

            Assert.That(geometry.Prone.EyeY, Is.EqualTo(-0.4f).Within(_tolerance));
            Assert.That(
                geometry.Prone.TopY - geometry.Prone.EyeY,
                Is.EqualTo(geometry.Standing.TopY - geometry.Standing.EyeY).Within(_tolerance));
        }

        [Test]
        public void ProneEyeDoesNotGoBelowFeet()
        {
            var geometry = new StanceGeometry(2f, 0f, -0.5f, 1.5f, 1f, 0.5f);

            Assert.That(geometry.Prone.EyeY, Is.EqualTo(-1f).Within(_tolerance));
        }

        [Test]
        public void WithoutAProneHeightProneIsTheCrouch()
        {
            var geometry = new StanceGeometry(2f, 0f, 0.6f, 1.2f, 0.5f);

            Assert.That(geometry.Prone, Is.EqualTo(geometry.Crouched));
        }

        [Test]
        public void ShapeSelectsEachStance()
        {
            var geometry = Player();

            Assert.That(geometry.Shape(PlayerStance.Standing), Is.EqualTo(geometry.Standing));
            Assert.That(geometry.Shape(PlayerStance.Crouching), Is.EqualTo(geometry.Crouched));
            Assert.That(geometry.Shape(PlayerStance.Prone), Is.EqualTo(geometry.Prone));
            Assert.Throws<ArgumentOutOfRangeException>(() => geometry.Shape((PlayerStance)99));
        }

        [Test]
        public void StancesGetLowerInOrder()
        {
            var geometry = Player();

            Assert.That(geometry.Crouched.TopY, Is.LessThan(geometry.Standing.TopY));
            Assert.That(geometry.Prone.TopY, Is.LessThan(geometry.Crouched.TopY));
        }

        [TestCase(1.3f)]
        [TestCase(0.9f)]
        [TestCase(0f)]
        [TestCase(float.NaN)]
        public void RejectsAProneHeightOutsideTheDiameterAndTheCrouch(float proneHeight)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Player(proneHeight));
        }

        [Test]
        public void ProneAtCrouchHeightMatchesTheCrouch()
        {
            var geometry = Player(proneHeight: 1.2f);

            Assert.That(geometry.Prone, Is.EqualTo(geometry.Crouched));
        }
    }
}
