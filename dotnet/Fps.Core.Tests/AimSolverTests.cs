using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class AimSolverTests
    {
        [Test]
        public void FirstPersonStartsAtTheCameraAndAimsAlongItsForward()
        {
            var eye = new Vector3(1f, 1.6f, 2f);
            var forward = Vector3.Normalize(new Vector3(0f, 0.3f, 1f));

            Assert.That(AimSolver.RayStart(eye, forward, eye), Is.Zero);

            var direction = AimSolver.DirectionFromEye(eye, eye + (forward * 40f), forward);
            Assert.That(Vector3.Distance(direction, forward), Is.LessThan(1e-5f));
        }

        [Test]
        public void ThirdPersonSkipsTheStretchBetweenCameraAndEye()
        {
            // 肩越し: 目の右 0.5m・後ろ 2.5m にカメラ
            var eye = new Vector3(0f, 1.6f, 0f);
            var camera = new Vector3(0.5f, 1.6f, -2.5f);

            Assert.That(AimSolver.RayStart(camera, Vector3.UnitZ, eye), Is.EqualTo(2.5f).Within(1e-5f));
        }

        [Test]
        public void EyeBehindTheCameraStartsAtTheCamera()
        {
            Assert.That(AimSolver.RayStart(Vector3.Zero, Vector3.UnitZ, new Vector3(0f, 0f, -1f)), Is.Zero);
        }

        [Test]
        public void ThirdPersonAimsFromTheEyeToThePointUnderTheCrosshair()
        {
            var eye = new Vector3(0f, 1.6f, 0f);
            var aimPoint = new Vector3(0.5f, 1.6f, 20f);

            var direction = AimSolver.DirectionFromEye(eye, aimPoint, Vector3.UnitZ);

            Assert.That(direction.Length(), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(Vector3.Distance(eye + (direction * Vector3.Distance(eye, aimPoint)), aimPoint), Is.LessThan(1e-4f));
        }

        [Test]
        public void FallsBackWhenTheAimPointIsOnTheEye()
        {
            var eye = new Vector3(3f, 2f, 1f);

            var direction = AimSolver.DirectionFromEye(eye, eye, new Vector3(0f, 0f, 5f));

            Assert.That(direction, Is.EqualTo(Vector3.UnitZ));
        }

        [Test]
        public void FallsBackWhenTheAimPointIsNotFinite()
        {
            var direction = AimSolver.DirectionFromEye(Vector3.Zero, new Vector3(float.NaN, 0f, 0f), Vector3.UnitX);

            Assert.That(direction, Is.EqualTo(Vector3.UnitX));
        }

        [Test]
        public void RejectsZeroOrNonFiniteForward()
        {
            Assert.Throws<ArgumentException>(() => AimSolver.RayStart(Vector3.Zero, Vector3.Zero, Vector3.One));
            Assert.Throws<ArgumentException>(() => AimSolver.RayStart(Vector3.Zero, new Vector3(float.NaN, 0f, 1f), Vector3.One));
            Assert.Throws<ArgumentException>(() => AimSolver.DirectionFromEye(Vector3.Zero, Vector3.One, Vector3.Zero));
        }
    }
}
