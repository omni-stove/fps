using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class DeathCamViewFrameEyeTests
    {
        private static readonly Vector3 _cameraLocalPosition = new Vector3(0f, 1.6f, 0f);

        private readonly List<Object> _objects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (var i = _objects.Count - 1; i >= 0; i--)
            {
                if (_objects[i] != null)
                {
                    Object.DestroyImmediate(_objects[i]);
                }
            }

            _objects.Clear();
        }

        [Test]
        public void TheCameraSitsAtTheEyeFacingTheYawAndGoesBackOnRestore()
        {
            var camera = Player(out var player);
            var view = new DeathCamView(player.transform, "test");
            view.Begin(camera.transform.position, 0f);

            view.FrameEye(new Vector3(4f, 2f, -3f), 90f);

            Assert.That(Vector3.Distance(camera.transform.position, new Vector3(4f, 2f, -3f)), Is.LessThan(1e-4f));
            Assert.That(Vector3.Dot(camera.transform.forward, Vector3.right), Is.GreaterThan(0.999f), "yaw 90 faces +X");
            Assert.That(Mathf.Abs(camera.transform.forward.y), Is.LessThan(1e-4f), "level, without the ally's pitch");
            Assert.That(view.IsFreeCamera, Is.False);

            view.Restore(true);

            Assert.That(Vector3.Distance(camera.transform.localPosition, _cameraLocalPosition), Is.LessThan(1e-4f));
            Assert.That(Quaternion.Angle(camera.transform.localRotation, Quaternion.identity), Is.LessThan(1e-3f));
            Assert.That(DeathCamView.IsBorrowed(camera), Is.False);
        }

        [Test]
        public void WithoutBorrowingTheCameraItDoesNothing()
        {
            var camera = Player(out var player);
            var view = new DeathCamView(player.transform, "test");

            view.FrameEye(new Vector3(4f, 2f, -3f), 90f);

            Assert.That(Vector3.Distance(camera.transform.localPosition, _cameraLocalPosition), Is.LessThan(1e-4f));
            Assert.That(Quaternion.Angle(camera.transform.localRotation, Quaternion.identity), Is.LessThan(1e-3f));
        }

        private Camera Player(out GameObject player)
        {
            player = new GameObject("Player");
            _objects.Add(player);
            var cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(player.transform, false);
            cameraObject.transform.localPosition = _cameraLocalPosition;
            return cameraObject.AddComponent<Camera>();
        }
    }
}
