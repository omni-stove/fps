using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class DeathCamFramingTests
    {
        [TestCase(0f, 0f, 10f, 0f)]
        [TestCase(10f, 0f, 0f, 90f)]
        [TestCase(0f, 0f, -10f, 180f)]
        [TestCase(-10f, 0f, 0f, 270f)]
        [TestCase(5f, 0f, 5f, 45f)]
        public void LooksTowardTheTargetOnTheLevel(float x, float y, float z, float expectedYaw)
        {
            DeathCamFraming.LookAt(Vector3.Zero, new Vector3(x, y, z), 123f, LookAngles.DefaultMaxPitch, out var yaw, out var pitch);

            Assert.That(yaw, Is.EqualTo(expectedYaw).Within(1e-3f));
            Assert.That(pitch, Is.EqualTo(0f).Within(1e-3f));
        }

        [Test]
        public void TargetBelowPitchesDownAndAboveUp()
        {
            DeathCamFraming.LookAt(new Vector3(0f, 2f, 0f), new Vector3(0f, 0f, 2f), 0f, LookAngles.DefaultMaxPitch, out _, out var down);
            DeathCamFraming.LookAt(Vector3.Zero, new Vector3(0f, 2f, 2f), 0f, LookAngles.DefaultMaxPitch, out _, out var up);

            // Unity の X 軸回転と同じく正が下
            Assert.That(down, Is.EqualTo(45f).Within(1e-3f));
            Assert.That(up, Is.EqualTo(-45f).Within(1e-3f));
        }

        [Test]
        public void AnglesPointTheForwardAtTheTarget()
        {
            var from = new Vector3(1f, 3f, -2f);
            var target = new Vector3(-4f, 1.5f, 6f);

            DeathCamFraming.LookAt(from, target, 0f, LookAngles.DefaultMaxPitch, out var yaw, out var pitch);

            var expected = Vector3.Normalize(target - from);
            Assert.That(Vector3.Distance(DeathCamFraming.Forward(yaw, pitch), expected), Is.LessThan(1e-4f));
        }

        [Test]
        public void SteepTargetIsClampedToMaxPitch()
        {
            DeathCamFraming.LookAt(new Vector3(0f, 10f, 0f), new Vector3(0f, 0f, 0.1f), 0f, 60f, out _, out var pitch);

            Assert.That(pitch, Is.EqualTo(60f).Within(1e-3f));
        }

        [Test]
        public void SamePositionLooksStraightDownKeepingTheFallbackYaw()
        {
            var at = new Vector3(3f, 1f, 3f);

            DeathCamFraming.LookAt(at, at, 200f, LookAngles.DefaultMaxPitch, out var yaw, out var pitch);

            Assert.That(yaw, Is.EqualTo(200f).Within(1e-3f));
            Assert.That(pitch, Is.EqualTo(LookAngles.DefaultMaxPitch).Within(1e-3f));
        }

        [Test]
        public void TargetStraightAboveLooksUpKeepingTheFallbackYaw()
        {
            DeathCamFraming.LookAt(Vector3.Zero, new Vector3(0f, 5f, 0f), -90f, LookAngles.DefaultMaxPitch, out var yaw, out var pitch);

            Assert.That(yaw, Is.EqualTo(270f).Within(1e-3f));
            Assert.That(pitch, Is.EqualTo(-LookAngles.DefaultMaxPitch).Within(1e-3f));
        }

        [Test]
        public void NonFiniteTargetLooksDown()
        {
            DeathCamFraming.LookAt(
                Vector3.Zero, new Vector3(float.NaN, 0f, 1f), 30f, LookAngles.DefaultMaxPitch, out var yaw, out var pitch);

            Assert.That(yaw, Is.EqualTo(30f).Within(1e-3f));
            Assert.That(pitch, Is.EqualTo(LookAngles.DefaultMaxPitch).Within(1e-3f));
        }

        [TestCase(0f)]
        [TestCase(91f)]
        [TestCase(float.NaN)]
        public void RejectsInvalidMaxPitch(float maxPitch)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => DeathCamFraming.LookAt(Vector3.Zero, Vector3.UnitZ, 0f, maxPitch, out _, out _));
        }

        [Test]
        public void BoomRisesAndBacksAwayFromTheTarget()
        {
            var eye = new Vector3(1f, 1.6f, 1f);
            var killer = new Vector3(11f, 5f, 1f);

            var offset = DeathCamFraming.BoomOffset(eye, killer, 0f, 1.5f, 2f);

            // 相手は +X にいるので、上へ 1.5m・-X へ 2m (相手の高さは下げる向きに入れない)
            Assert.That(Vector3.Distance(offset, new Vector3(-2f, 1.5f, 0f)), Is.LessThan(1e-5f));
        }

        [Test]
        public void BoomWithoutTargetBacksAwayFromTheFallbackYaw()
        {
            var eye = new Vector3(0f, 1.6f, 0f);

            var offset = DeathCamFraming.BoomOffset(eye, eye, 90f, 1.5f, 2f);

            // 倒れたとき +X を向いていたので、その後ろ (-X) の上から倒れた位置を見る
            Assert.That(Vector3.Distance(offset, new Vector3(-2f, 1.5f, 0f)), Is.LessThan(1e-5f));
        }

        [Test]
        public void CameraOnTheBoomLooksDownAtTheDeathSpotWithoutTarget()
        {
            var eye = new Vector3(0f, 1.6f, 0f);
            var camera = eye + DeathCamFraming.BoomOffset(eye, eye, 0f, 2f, 2f);

            DeathCamFraming.LookAt(camera, eye, 0f, LookAngles.DefaultMaxPitch, out var yaw, out var pitch);

            Assert.That(yaw, Is.EqualTo(0f).Within(1e-3f), "the camera stays behind and faces the way the body faced");
            Assert.That(pitch, Is.EqualTo(45f).Within(1e-3f), "looking down at where the player fell");
        }

        [Test]
        public void CameraOnTheBoomFacesTheKiller()
        {
            var eye = new Vector3(0f, 1.6f, 0f);
            var killer = new Vector3(0f, 1.6f, -20f);
            var camera = eye + DeathCamFraming.BoomOffset(eye, killer, 0f, DeathCamFraming.DefaultHeight, DeathCamFraming.DefaultBackDistance);

            DeathCamFraming.LookAt(camera, killer, 0f, LookAngles.DefaultMaxPitch, out var yaw, out var pitch);

            Assert.That(camera.Z, Is.GreaterThan(eye.Z), "backs away from the killer so the death spot stays in front");
            Assert.That(yaw, Is.EqualTo(180f).Within(1e-3f));
            Assert.That(pitch, Is.GreaterThan(0f), "slightly above, so it looks a little down");
        }

        [TestCase(-1f, 0f)]
        [TestCase(0f, -1f)]
        [TestCase(float.NaN, 0f)]
        [TestCase(0f, float.PositiveInfinity)]
        public void BoomRejectsInvalidLengths(float height, float backDistance)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => DeathCamFraming.BoomOffset(Vector3.Zero, Vector3.UnitZ, 0f, height, backDistance));
        }

        [TestCase(0f, 0f, 0f, 0f, 1f)]
        [TestCase(90f, 0f, 1f, 0f, 0f)]
        [TestCase(0f, 90f, 0f, -1f, 0f)]
        public void ForwardMatchesUnityEuler(float yaw, float pitch, float x, float y, float z)
        {
            Assert.That(Vector3.Distance(DeathCamFraming.Forward(yaw, pitch), new Vector3(x, y, z)), Is.LessThan(1e-5f));
        }
    }
}
