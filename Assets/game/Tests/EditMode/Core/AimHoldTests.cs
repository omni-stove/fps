using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class AimHoldTests
    {
        private const float _tolerance = 1e-3f;

        private static readonly Vector3 _forward = new Vector3(0f, 0f, 1f);

        [Test]
        public void APointToTheRightTurnsRight()
        {
            var delta = AimHold.LookDelta(_forward, Vector3.Zero, new Vector3(1f, 0f, 1f));

            Assert.That(delta.X, Is.EqualTo(45f).Within(_tolerance));
            Assert.That(delta.Y, Is.EqualTo(0f).Within(_tolerance));
        }

        [Test]
        public void APointAboveLooksUp()
        {
            var delta = AimHold.LookDelta(_forward, Vector3.Zero, new Vector3(0f, 1f, 1f));

            Assert.That(delta.X, Is.EqualTo(0f).Within(_tolerance));
            Assert.That(delta.Y, Is.EqualTo(45f).Within(_tolerance));
        }

        [Test]
        public void MovingTheCameraFromTheShoulderToTheEyeTurnsTowardTheShoulderSide()
        {
            // 右肩 0.5 m のカメラが中央に捉えていた 12.5 m 先の点を、目から見て中央に戻す
            var held = new Vector3(0.5f, 0f, 10f);

            var delta = AimHold.LookDelta(_forward, Vector3.Zero, held);

            Assert.That(delta.X, Is.EqualTo(2.8624f).Within(_tolerance), "atan(0.5 / 10)");
            Assert.That(delta.Y, Is.EqualTo(0f).Within(_tolerance));
        }

        [Test]
        public void TheYawTakesTheShortWayAcrossBehind()
        {
            var forward = Direction(170f);
            var target = Direction(-170f);

            var delta = AimHold.LookDelta(forward, Vector3.Zero, target * 10f);

            Assert.That(delta.X, Is.EqualTo(20f).Within(_tolerance));
        }

        [Test]
        public void APointAtTheCameraDoesNotTurn()
        {
            var at = new Vector3(1f, 2f, 3f);

            Assert.That(AimHold.LookDelta(_forward, at, at), Is.EqualTo(Vector2.Zero));
        }

        [Test]
        public void AFarPointIsHeldWhereItIs()
        {
            var aim = new Vector3(0.5f, 0f, 10f);

            var held = AimHold.HeldPoint(new Vector3(0.5f, 0f, -2.5f), _forward, Vector3.Zero, aim, 2f);

            Assert.That(held, Is.EqualTo(aim));
        }

        [Test]
        public void APointCloserToTheEyeThanTheMinimumIsPushedOutAlongTheRay()
        {
            var camera = new Vector3(0f, 0f, -2.5f);

            var held = AimHold.HeldPoint(camera, _forward, Vector3.Zero, new Vector3(0f, 0f, 0.5f), 2f);

            Assert.That(Vector3.Distance(held, new Vector3(0f, 0f, 2f)), Is.LessThan(_tolerance), "2 m ahead of the eye, not of the camera");
        }

        [Test]
        public void RejectsABadMinimumAndForward()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => AimHold.HeldPoint(Vector3.Zero, _forward, Vector3.Zero, Vector3.One, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => AimHold.HeldPoint(Vector3.Zero, _forward, Vector3.Zero, Vector3.One, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => AimHold.HeldPoint(Vector3.Zero, _forward, Vector3.Zero, Vector3.One, float.PositiveInfinity));
            Assert.Throws<ArgumentException>(() => AimHold.HeldPoint(Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.One, 1f));
            Assert.Throws<ArgumentException>(() => AimHold.LookDelta(Vector3.Zero, Vector3.Zero, Vector3.One));
        }

        private static Vector3 Direction(float yawDegrees)
        {
            var radians = yawDegrees * (float)(Math.PI / 180.0);
            return new Vector3((float)Math.Sin(radians), 0f, (float)Math.Cos(radians));
        }
    }
}
