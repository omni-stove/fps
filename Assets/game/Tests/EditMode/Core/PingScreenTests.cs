using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class PingScreenTests
    {
        private const float Aspect = 16f / 9f;
        private const float Margin = 0.05f;
        private const float Tolerance = 1e-4f;

        [Test]
        public void APointInsideTheScreenStaysWhereItIs()
        {
            var placement = PingScreen.Place(new Vector3(0.25f, 0.75f, 5f), Aspect, Margin);

            Assert.That(placement.IsOnScreen, Is.True);
            Assert.That(placement.X, Is.EqualTo(0.25f).Within(Tolerance));
            Assert.That(placement.Y, Is.EqualTo(0.25f).Within(Tolerance), "y is flipped to the UI's top-down axis");
            Assert.That(placement.Edge, Is.EqualTo(PingScreenEdge.None));
            Assert.That(placement.AngleDegrees, Is.Zero);
        }

        [Test]
        public void APointToTheRightGoesToTheRightEdgePointingRight()
        {
            var placement = PingScreen.Place(new Vector3(1.5f, 0.5f, 5f), Aspect, Margin);

            Assert.That(placement.IsOnScreen, Is.False);
            Assert.That(placement.Edge, Is.EqualTo(PingScreenEdge.Right));
            Assert.That(placement.X, Is.EqualTo(1f - (Margin / Aspect)).Within(Tolerance));
            Assert.That(placement.Y, Is.EqualTo(0.5f).Within(Tolerance));
            Assert.That(placement.AngleDegrees, Is.EqualTo(90f).Within(Tolerance));
        }

        [Test]
        public void APointToTheLeftGoesToTheLeftEdgePointingLeft()
        {
            var placement = PingScreen.Place(new Vector3(-0.5f, 0.5f, 5f), Aspect, Margin);

            Assert.That(placement.Edge, Is.EqualTo(PingScreenEdge.Left));
            Assert.That(placement.X, Is.EqualTo(Margin / Aspect).Within(Tolerance));
            Assert.That(placement.AngleDegrees, Is.EqualTo(-90f).Within(Tolerance));
        }

        [Test]
        public void APointAboveGoesToTheTopEdgePointingUp()
        {
            var placement = PingScreen.Place(new Vector3(0.5f, 2f, 5f), Aspect, Margin);

            Assert.That(placement.Edge, Is.EqualTo(PingScreenEdge.Top));
            Assert.That(placement.X, Is.EqualTo(0.5f).Within(Tolerance));
            Assert.That(placement.Y, Is.EqualTo(Margin).Within(Tolerance));
            Assert.That(placement.AngleDegrees, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void APointInsideTheMarginCountsAsOffScreen()
        {
            var placement = PingScreen.Place(new Vector3(0.99f, 0.5f, 5f), Aspect, Margin);

            Assert.That(placement.IsOnScreen, Is.False);
            Assert.That(placement.Edge, Is.EqualTo(PingScreenEdge.Right));
        }

        [Test]
        public void APointStraightBehindGoesToTheBottomEdge()
        {
            var placement = PingScreen.Place(new Vector3(0.5f, 0.5f, -3f), Aspect, Margin);

            Assert.That(placement.IsOnScreen, Is.False);
            Assert.That(placement.Edge, Is.EqualTo(PingScreenEdge.Bottom));
            Assert.That(placement.Y, Is.EqualTo(1f - Margin).Within(Tolerance));
            Assert.That(Math.Abs(placement.AngleDegrees), Is.EqualTo(180f).Within(Tolerance));
        }

        [Test]
        public void APointBehindIsMirroredBackToItsRealSide()
        {
            // カメラの後ろ左にある点は、投影すると画面の右に写る。向きは左に戻す。
            var placement = PingScreen.Place(new Vector3(0.8f, 0.5f, -3f), Aspect, Margin);

            Assert.That(placement.IsOnScreen, Is.False);
            Assert.That(placement.Edge, Is.EqualTo(PingScreenEdge.Left));
        }

        [Test]
        public void TheAngleFollowsTheScreenShape()
        {
            var placement = PingScreen.Place(new Vector3(2f, 2f, 5f), 1f, 0f);

            Assert.That(placement.X, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(placement.Y, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(placement.AngleDegrees, Is.EqualTo(45f).Within(Tolerance));
        }

        [Test]
        public void RejectsInvalidArguments()
        {
            var inside = new Vector3(0.5f, 0.5f, 1f);

            Assert.Throws<ArgumentOutOfRangeException>(() => PingScreen.Place(new Vector3(float.NaN, 0.5f, 1f), Aspect, Margin));
            Assert.Throws<ArgumentOutOfRangeException>(() => PingScreen.Place(inside, 0f, Margin));
            Assert.Throws<ArgumentOutOfRangeException>(() => PingScreen.Place(inside, float.PositiveInfinity, Margin));
            Assert.Throws<ArgumentOutOfRangeException>(() => PingScreen.Place(inside, Aspect, -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => PingScreen.Place(inside, Aspect, 0.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => PingScreen.Place(inside, 0.5f, 0.3f));
        }
    }
}
