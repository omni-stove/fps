using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class LadderSpanTests
    {
        private const float Radius = 0.5f;
        private const float Grab = 0.4f;

        // 原点に下端があり、+Z を向いてつかまる高さ 3m・幅 0.8m のはしご。上の足場は +Z 側に広がる。
        private static readonly LadderSpan _span = new LadderSpan(Vector3.Zero, new Vector3(0f, 0f, 2f), 3f, 0.8f);

        private static readonly Vector3 _towardLadder = new Vector3(0f, 0f, 3f);
        private static readonly Vector3 _awayFromLadder = new Vector3(0f, 0f, -3f);

        [Test]
        public void FacingIsTheNormalizedHorizontalPart()
        {
            var span = new LadderSpan(new Vector3(1f, 2f, 3f), new Vector3(3f, 5f, 0f), 2f, 1f);

            Assert.That(span.Facing.X, Is.EqualTo(1f).Within(1e-6f));
            Assert.That(span.Facing.Y, Is.EqualTo(0f));
            Assert.That(span.Facing.Z, Is.EqualTo(0f).Within(1e-6f));
            Assert.That(span.Top, Is.EqualTo(new Vector3(1f, 4f, 3f)));
        }

        [Test]
        public void RightIsToTheClimbersRight()
        {
            var right = _span.Right;

            Assert.That(right.X, Is.EqualTo(1f).Within(1e-6f), "facing +Z, the right is +X");
            Assert.That(right.Z, Is.EqualTo(0f).Within(1e-6f));
        }

        [Test]
        public void TheClimberHangsInFrontOfTheFaceAndStepsOntoThePlatformAtTheTop()
        {
            var feet = _span.FeetAt(1.5f, Radius);
            Assert.That(feet.Y, Is.EqualTo(1.5f));
            Assert.That(feet.Z, Is.EqualTo(-(Radius + LadderSpan.FaceGap)).Within(1e-6f));

            var exit = _span.TopExit(Radius);
            Assert.That(exit.Y, Is.EqualTo(3f));
            Assert.That(exit.Z, Is.EqualTo(Radius + LadderSpan.FaceGap).Within(1e-6f), "the whole body is on the platform");
        }

        [Test]
        public void TheClimbPathGoesUpTheLadderOrDownIt()
        {
            var up = _span.ClimbPath(true, Radius);
            var down = _span.ClimbPath(false, Radius);

            Assert.That(up, Is.EqualTo(new[] { _span.FeetAt(0f, Radius), _span.FeetAt(3f, Radius), _span.TopExit(Radius) }));
            Assert.That(down, Is.EqualTo(new[] { _span.TopExit(Radius), _span.FeetAt(3f, Radius), _span.FeetAt(0f, Radius) }));
        }

        [Test]
        public void TheLinkRunsFromTheGroundInFrontToThePlatform()
        {
            var bottom = _span.LinkBottom(0.8f);
            var top = _span.LinkTop(0.8f);

            Assert.That(bottom.Z, Is.EqualTo(-0.8f).Within(1e-6f));
            Assert.That(bottom.Y, Is.EqualTo(0f));
            Assert.That(top.Z, Is.EqualTo(0.8f).Within(1e-6f));
            Assert.That(top.Y, Is.EqualTo(3f));
        }

        [Test]
        public void WalkingIntoTheLadderFromTheFrontGrabsIt()
        {
            var feet = new Vector3(0.2f, 0f, -0.7f);

            Assert.That(_span.Probe(feet, _towardLadder, Radius, Grab), Is.EqualTo(LadderEntry.Front));
            Assert.That(_span.Probe(feet + new Vector3(0f, 1.5f, 0f), _towardLadder, Radius, Grab), Is.EqualTo(LadderEntry.Front), "in the air halfway up");
        }

        [Test]
        public void StandingStillWalkingAwayOrPassingByDoesNotGrab()
        {
            var feet = new Vector3(0f, 0f, -0.7f);

            Assert.That(_span.Probe(feet, Vector3.Zero, Radius, Grab), Is.EqualTo(LadderEntry.None), "not moving");
            Assert.That(_span.Probe(feet, _awayFromLadder, Radius, Grab), Is.EqualTo(LadderEntry.None), "walking away");
            Assert.That(_span.Probe(feet, new Vector3(3f, 0f, 0f), Radius, Grab), Is.EqualTo(LadderEntry.None), "walking along the wall");
            Assert.That(_span.Probe(feet, new Vector3(0f, 0f, 0.2f), Radius, Grab), Is.EqualTo(LadderEntry.None), "too slow");
        }

        [Test]
        public void TooFarAwayToTheSideOrAboveDoesNotGrab()
        {
            Assert.That(_span.Probe(new Vector3(0f, 0f, -1f), _towardLadder, Radius, Grab), Is.EqualTo(LadderEntry.None), "out of reach");
            Assert.That(_span.Probe(new Vector3(0.5f, 0f, -0.7f), _towardLadder, Radius, Grab), Is.EqualTo(LadderEntry.None), "beside the ladder");
            Assert.That(_span.Probe(new Vector3(0f, -0.5f, -0.7f), _towardLadder, Radius, Grab), Is.EqualTo(LadderEntry.None), "below the bottom");
            Assert.That(_span.Probe(new Vector3(0f, 2.9f, -0.7f), _towardLadder, Radius, Grab), Is.EqualTo(LadderEntry.None), "next to the top");
        }

        [Test]
        public void WalkingOffTheEdgeOfThePlatformGrabsTheTop()
        {
            var feet = new Vector3(0f, 3f, 0.6f);

            Assert.That(_span.Probe(feet, _awayFromLadder, Radius, Grab), Is.EqualTo(LadderEntry.Top));
            Assert.That(_span.Probe(feet, _towardLadder, Radius, Grab), Is.EqualTo(LadderEntry.None), "walking deeper into the platform");
            Assert.That(_span.Probe(new Vector3(0f, 3f, 1.5f), _awayFromLadder, Radius, Grab), Is.EqualTo(LadderEntry.None), "too far from the edge");
            Assert.That(_span.Probe(new Vector3(0f, 2f, 0.6f), _awayFromLadder, Radius, Grab), Is.EqualTo(LadderEntry.None), "not on the platform");
        }

        [Test]
        public void NonFiniteInputsDoNotGrab()
        {
            var feet = new Vector3(0f, 0f, -0.7f);

            Assert.That(_span.Probe(new Vector3(float.NaN, 0f, -0.7f), _towardLadder, Radius, Grab), Is.EqualTo(LadderEntry.None));
            Assert.That(_span.Probe(feet, new Vector3(0f, 0f, float.PositiveInfinity), Radius, Grab), Is.EqualTo(LadderEntry.None));
            Assert.That(_span.Probe(feet, _towardLadder, float.NaN, Grab), Is.EqualTo(LadderEntry.None));
            Assert.That(_span.Probe(feet, _towardLadder, Radius, -1f), Is.EqualTo(LadderEntry.None));
        }

        [Test]
        public void TheShapeIsChecked()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new LadderSpan(new Vector3(float.NaN, 0f, 0f), Vector3.UnitZ, 3f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LadderSpan(Vector3.Zero, Vector3.UnitY, 3f, 1f), "straight up has no horizontal part");
            Assert.Throws<ArgumentOutOfRangeException>(() => new LadderSpan(Vector3.Zero, Vector3.Zero, 3f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LadderSpan(Vector3.Zero, Vector3.UnitZ, 0f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LadderSpan(Vector3.Zero, Vector3.UnitZ, float.PositiveInfinity, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LadderSpan(Vector3.Zero, Vector3.UnitZ, 3f, -1f));
        }
    }
}
