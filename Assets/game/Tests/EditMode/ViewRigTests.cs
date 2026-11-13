using System;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// 一人称と三人称で、カメラの置き場所・体の描き方・射撃の始点と向きがどう変わるか (変わらないか)。
    /// </summary>
    public sealed class ViewRigTests
    {
        private static readonly Vector3 _eyeHeight = new Vector3(0f, 0.6f, 0f);

        private GameObject _body;
        private Transform _eye;
        private Camera _camera;
        private ViewRig _rig;
        private GameObject _wall;
        private InputAction _attack;
        private InputAction _reload;

        [SetUp]
        public void SetUp()
        {
            _body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _body.name = "Body";
            _eye = new GameObject("Eye").transform;
            _eye.SetParent(_body.transform, false);
            _eye.localPosition = _eyeHeight;
            _camera = new GameObject("Camera").AddComponent<Camera>();
            _camera.transform.SetParent(_eye, false);
            _rig = _body.AddComponent<ViewRig>();
            _attack = new InputAction("Attack", InputActionType.Button);
            _reload = new InputAction("Reload", InputActionType.Button);
        }

        [TearDown]
        public void TearDown()
        {
            _attack.Dispose();
            _reload.Dispose();
            Object.DestroyImmediate(_body);
            if (_wall != null)
            {
                Object.DestroyImmediate(_wall);
            }
        }

        [Test]
        public void ResolvesEyeAsTheCameraParent()
        {
            Assert.That(_rig.TryInitializeFromHierarchy(), Is.True);
            Assert.That(_rig.Eye, Is.SameAs(_eye));
            Assert.That(_rig.Camera, Is.SameAs(_camera));
            Assert.That(ViewRig.FindEye(_body.transform), Is.SameAs(_eye));
        }

        [Test]
        public void CameraDirectlyUnderTheBodyIsRejected()
        {
            _camera.transform.SetParent(_body.transform, false);
            LogAssert.Expect(LogType.Error, new Regex(@"\[ViewRig\] .*本体 > 目 > カメラ"));

            Assert.That(_rig.TryInitializeFromHierarchy(), Is.False);
            Assert.That(ViewRig.FindEye(_body.transform), Is.Null);
            Assert.Throws<ArgumentException>(() => _rig.Initialize(_eye, _camera));
        }

        [Test]
        public void DefaultsToFirstPerson()
        {
            Assert.That(_rig.Mode, Is.EqualTo(ViewMode.FirstPerson));
            Assert.That(_rig.ThirdPersonOffset, Is.EqualTo(ViewRig.DefaultThirdPersonOffset));
        }

        [Test]
        public void FirstPersonPutsTheCameraOnTheEyeAndHidesTheBody()
        {
            _rig.Initialize(_eye, _camera);
            _camera.transform.localPosition = Vector3.one;

            _rig.UpdateCameraPosition();
            _rig.ApplyBodyVisibility(true);

            Assert.That(_camera.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(_body.GetComponent<Renderer>().shadowCastingMode, Is.EqualTo(ShadowCastingMode.ShadowsOnly));
        }

        [Test]
        public void ThirdPersonPutsTheCameraBehindTheShoulderAndShowsTheBody()
        {
            _rig.Initialize(_eye, _camera);
            _rig.Mode = ViewMode.ThirdPerson;

            _rig.UpdateCameraPosition();
            _rig.ApplyBodyVisibility(true);

            Assert.That(Vector3.Distance(_camera.transform.localPosition, ViewRig.DefaultThirdPersonOffset), Is.LessThan(1e-4f),
                "own capsule must not pull the camera in");
            Assert.That(_body.GetComponent<Renderer>().shadowCastingMode, Is.EqualTo(ShadowCastingMode.On));
        }

        [Test]
        public void OtherPlayersBodyIsDrawnInFirstPerson()
        {
            _rig.Initialize(_eye, _camera);

            _rig.ApplyBodyVisibility(false);

            Assert.That(_body.GetComponent<Renderer>().shadowCastingMode, Is.EqualTo(ShadowCastingMode.On));
        }

        [Test]
        public void ThirdPersonCameraStopsShortOfAWall()
        {
            _rig.Initialize(_eye, _camera);
            _rig.Mode = ViewMode.ThirdPerson;
            _rig.ThirdPersonOffset = new Vector3(0f, 0f, -3f);
            _wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wall.transform.position = _eye.position + new Vector3(0f, 0f, -1.5f);
            _wall.transform.localScale = new Vector3(4f, 4f, 0.2f);
            Physics.SyncTransforms();

            _rig.UpdateCameraPosition();

            var z = _camera.transform.localPosition.z;
            Assert.That(z, Is.LessThan(-CameraBoom.DefaultMinLength + 1e-4f));
            Assert.That(z, Is.GreaterThan(-1.4f), "the camera must stay in front of the wall (1.5m behind minus its 0.1m half thickness)");
        }

        [Test]
        public void FirstPersonAimsAlongTheCameraForwardFromTheEye()
        {
            _rig.Initialize(_eye, _camera);
            _body.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            _eye.localRotation = Quaternion.Euler(-20f, 0f, 0f);

            _rig.ResolveAim(100f, out var origin, out var direction);

            Assert.That(Vector3.Distance(origin, _eye.position), Is.LessThan(1e-5f));
            Assert.That(Vector3.Distance(direction, _camera.transform.forward), Is.LessThan(1e-5f));
        }

        [Test]
        public void ThirdPersonAimsFromTheEyeAtWhatTheCrosshairCovers()
        {
            _rig.Initialize(_eye, _camera);
            _rig.Mode = ViewMode.ThirdPerson;
            _rig.UpdateCameraPosition();

            // 画面中央 (カメラの正面) の 20m 先に標的
            var cameraTransform = _camera.transform;
            _wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wall.transform.position = cameraTransform.position + (cameraTransform.forward * 20f);
            Physics.SyncTransforms();

            _rig.ResolveAim(100f, out var origin, out var direction);

            Assert.That(Vector3.Distance(origin, _eye.position), Is.LessThan(1e-5f), "third person still fires from the eye");
            Assert.That(Physics.Raycast(origin, direction, out var hit, 100f), Is.True);
            Assert.That(hit.collider.gameObject, Is.SameAs(_wall), "the shot from the eye must land on what the crosshair covers");
        }

        [Test]
        public void ThirdPersonIgnoresWhatIsBetweenTheCameraAndTheEye()
        {
            _rig.Initialize(_eye, _camera);
            _rig.Mode = ViewMode.ThirdPerson;
            _rig.ThirdPersonOffset = new Vector3(0f, 0f, -3f);
            _rig.UpdateCameraPosition();

            // カメラと目の間 (目の 1m 後ろ) にだけある物は狙わない
            _wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wall.transform.position = _eye.position + new Vector3(0f, 0f, -1f);
            _wall.transform.localScale = new Vector3(0.5f, 0.5f, 0.2f);
            Physics.SyncTransforms();

            _rig.ResolveAim(100f, out _, out var direction);

            Assert.That(direction.z, Is.GreaterThan(0.99f));
        }

        [Test]
        public void WeaponFiresFromTheEyeInThirdPerson()
        {
            _rig.Initialize(_eye, _camera);
            _rig.Mode = ViewMode.ThirdPerson;
            _rig.UpdateCameraPosition();
            var weapon = _camera.gameObject.AddComponent<WeaponController>();
            var rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            try
            {
                weapon.Initialize(_camera.transform, _attack, _reload, rifle);

                weapon.ResolveShot(out var origin, out var direction);

                Assert.That(Vector3.Distance(origin, _eye.position), Is.LessThan(1e-5f));
                Assert.That(Vector3.Distance(origin, _camera.transform.position), Is.GreaterThan(1f));
                Assert.That(direction.z, Is.GreaterThan(0.99f));
            }
            finally
            {
                Object.DestroyImmediate(rifle);
            }
        }

        [Test]
        public void LookPitchesTheEyeSoTheThirdPersonCameraOrbitsWithIt()
        {
            // ViewRig の Awake より先でも、FirstPersonLook は目を pitch の軸に選ぶ
            var look = _body.AddComponent<FirstPersonLook>();

            Assert.That(InputSystem.actions, Is.Not.Null, "Project-wide Input Actions must be assigned");
            Assert.That(look.TryInitializeFromHierarchy(), Is.True);
            Assert.That(look.PitchPivot, Is.SameAs(_eye));
        }

        [Test]
        public void RejectsUnknownMode()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _rig.Mode = (ViewMode)99);
        }
    }
}
