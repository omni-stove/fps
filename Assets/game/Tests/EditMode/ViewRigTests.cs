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
        public void SettingsSetTheCameraFieldOfView()
        {
            _rig.Initialize(_eye, _camera);
            _camera.fieldOfView = 40f;

            _rig.ApplyControlSettings(ControlSettings.Default.With(ControlValue.FieldOfView, 85));

            Assert.That(_camera.fieldOfView, Is.EqualTo(85f));
            Assert.Throws<ArgumentNullException>(() => _rig.ApplyControlSettings(null));
        }

        [Test]
        public void SettingsLeaveTheFieldOfViewToAnInitializedAds()
        {
            _rig.Initialize(_eye, _camera);
            _camera.fieldOfView = 40f;
            var look = _body.AddComponent<FirstPersonLook>();
            var ads = _body.AddComponent<AimDownSights>();
            using (var aim = new InputAction("Aim", InputActionType.Button))
            {
                ads.Initialize(_camera, look, aim);

                _rig.ApplyControlSettings(ControlSettings.Default.With(ControlValue.FieldOfView, 85));

                Assert.That(_camera.fieldOfView, Is.EqualTo(40f), "AimDownSights applies the same settings itself");
            }
        }

        [Test]
        public void SettingsBeforeInitializeDoNothing()
        {
            _camera.fieldOfView = 40f;

            _rig.ApplyControlSettings(ControlSettings.Default.With(ControlValue.FieldOfView, 85));

            Assert.That(_camera.fieldOfView, Is.EqualTo(40f));
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
        public void DefaultsToTheRightShoulder()
        {
            Assert.That(_rig.Shoulder, Is.EqualTo(ShoulderSide.Right));
        }

        [Test]
        public void LeftShoulderMirrorsTheThirdPersonCameraToTheLeft()
        {
            _rig.Initialize(_eye, _camera);
            _rig.Mode = ViewMode.ThirdPerson;
            LogAssert.Expect(LogType.Log, "[View] shoulder=Left");

            _rig.Shoulder = ShoulderSide.Left;
            _rig.Tick(false, ShoulderBlend.DefaultSeconds);

            Assert.That(_camera.transform.localPosition.x, Is.LessThan(0f));
            Assert.That(Vector3.Distance(_camera.transform.localPosition, LeftShoulderOffset()), Is.LessThan(1e-4f));
            Assert.That(_rig.ThirdPersonOffset, Is.EqualTo(ViewRig.DefaultThirdPersonOffset), "the stored offset stays on the right");
        }

        [Test]
        public void SettingTheShoulderDoesNotJumpTheCamera()
        {
            _rig.Initialize(_eye, _camera);
            _rig.Mode = ViewMode.ThirdPerson;

            _rig.Shoulder = ShoulderSide.Left;
            _rig.UpdateCameraPosition();

            Assert.That(_rig.IsSwappingShoulder, Is.True);
            Assert.That(Vector3.Distance(_camera.transform.localPosition, ViewRig.DefaultThirdPersonOffset), Is.LessThan(1e-4f),
                "the camera only moves as the blend advances");
        }

        [Test]
        public void SettingTheSameShoulderDoesNothing()
        {
            _rig.Initialize(_eye, _camera);

            _rig.Shoulder = ShoulderSide.Right;

            Assert.That(_rig.IsSwappingShoulder, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void SwappingBackToTheRightShoulderRestoresTheCamera()
        {
            _rig.Initialize(_eye, _camera);
            _rig.Mode = ViewMode.ThirdPerson;
            LogAssert.Expect(LogType.Log, "[View] shoulder=Left");
            LogAssert.Expect(LogType.Log, "[View] shoulder=Right");
            _rig.Shoulder = ShoulderSide.Left;
            _rig.Tick(false, ShoulderBlend.DefaultSeconds);

            _rig.Shoulder = ShoulderSide.Right;
            _rig.Tick(false, ShoulderBlend.DefaultSeconds);

            Assert.That(Vector3.Distance(_camera.transform.localPosition, ViewRig.DefaultThirdPersonOffset), Is.LessThan(1e-4f));
        }

        [Test]
        public void ShoulderDoesNotMoveTheFirstPersonCamera()
        {
            _rig.Initialize(_eye, _camera);
            LogAssert.Expect(LogType.Log, "[View] shoulder=Left");
            _rig.Shoulder = ShoulderSide.Left;

            _rig.Tick(false, ShoulderBlend.DefaultSeconds);

            Assert.That(_camera.transform.localPosition, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void ShoulderLogNamesTheSide()
        {
            Assert.That(ViewRig.FormatShoulderLog(ShoulderSide.Left), Is.EqualTo("[View] shoulder=Left"));
            Assert.That(ViewRig.FormatShoulderLog(ShoulderSide.Right), Is.EqualTo("[View] shoulder=Right"));
        }

        [Test]
        public void SwapInThirdPersonBlendsTheCameraAcrossToTheOtherShoulder()
        {
            InitializeLocalThirdPerson();
            var right = ViewRig.DefaultThirdPersonOffset.x;
            LogAssert.Expect(LogType.Log, "[View] shoulder=Left");

            _rig.Tick(true, ShoulderBlend.DefaultSeconds / 3f);

            Assert.That(_rig.Shoulder, Is.EqualTo(ShoulderSide.Left));
            var first = _camera.transform.localPosition.x;
            Assert.That(first, Is.LessThan(right).And.GreaterThan(-right), "the camera is between the shoulders, not jumped");

            _rig.Tick(false, ShoulderBlend.DefaultSeconds / 3f);
            var second = _camera.transform.localPosition.x;
            Assert.That(second, Is.LessThan(first).And.GreaterThan(-right), "it keeps moving to the left");

            _rig.Tick(false, ShoulderBlend.DefaultSeconds);
            Assert.That(Vector3.Distance(_camera.transform.localPosition, LeftShoulderOffset()), Is.LessThan(1e-4f));
            Assert.That(_rig.IsSwappingShoulder, Is.False);
        }

        [Test]
        public void SwapMidwayTurnsTheCameraBack()
        {
            InitializeLocalThirdPerson();
            LogAssert.Expect(LogType.Log, "[View] shoulder=Left");
            LogAssert.Expect(LogType.Log, "[View] shoulder=Right");
            _rig.Tick(true, ShoulderBlend.DefaultSeconds * 0.3f);
            var midway = _camera.transform.localPosition.x;

            _rig.Tick(true, 0f);

            Assert.That(_rig.Shoulder, Is.EqualTo(ShoulderSide.Right));
            Assert.That(_camera.transform.localPosition.x, Is.EqualTo(midway).Within(1e-4f), "turning back must not jump");
            _rig.Tick(false, ShoulderBlend.DefaultSeconds);
            Assert.That(Vector3.Distance(_camera.transform.localPosition, ViewRig.DefaultThirdPersonOffset), Is.LessThan(1e-4f));
        }

        [Test]
        public void SwapIsIgnoredInFirstPerson()
        {
            _rig.Initialize(_eye, _camera);
            _rig.ApplyBodyVisibility(true);

            _rig.Tick(true, ShoulderBlend.DefaultSeconds);

            Assert.That(_rig.Shoulder, Is.EqualTo(ShoulderSide.Right));
            Assert.That(_rig.IsSwappingShoulder, Is.False);
            Assert.That(_camera.transform.localPosition, Is.EqualTo(Vector3.zero));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void OtherPlayersViewDoesNotReadTheSwap()
        {
            InitializeLocalThirdPerson();
            _rig.ApplyBodyVisibility(false);

            _rig.Tick(true, ShoulderBlend.DefaultSeconds);

            Assert.That(_rig.IsLocalView, Is.False);
            Assert.That(_rig.Shoulder, Is.EqualTo(ShoulderSide.Right));
            Assert.That(Vector3.Distance(_camera.transform.localPosition, ViewRig.DefaultThirdPersonOffset), Is.LessThan(1e-4f));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void OtherPlayersViewDoesNotEnableTheSwapAction()
        {
            var swap = new InputAction("SwapShoulder", InputActionType.Button);
            try
            {
                _rig.Initialize(_eye, _camera);
                _rig.SetSwapShoulderAction(swap);
                _rig.ApplyBodyVisibility(false);

                Assert.That(_rig.SwapShoulderAction, Is.SameAs(swap));
                Assert.That(swap.enabled, Is.False);
            }
            finally
            {
                swap.Dispose();
            }
        }

        [Test]
        public void FindsTheSwapShoulderActionInTheProjectWideActions()
        {
            Assert.That(InputSystem.actions, Is.Not.Null, "Project-wide Input Actions must be assigned");

            Assert.That(_rig.TryInitializeFromHierarchy(), Is.True);

            Assert.That(_rig.SwapShoulderAction, Is.SameAs(InputSystem.actions.FindAction(InputActionPaths.SwapShoulder)));
        }

        [Test]
        public void ThirdPersonCameraStopsShortOfAWallWhileSwapping()
        {
            InitializeLocalThirdPerson();
            _rig.ThirdPersonOffset = new Vector3(0.5f, 0f, -3f);
            _wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wall.transform.position = _eye.position + new Vector3(0f, 0f, -1.5f);
            _wall.transform.localScale = new Vector3(4f, 4f, 0.2f);
            Physics.SyncTransforms();
            LogAssert.Expect(LogType.Log, "[View] shoulder=Left");

            _rig.Tick(true, ShoulderBlend.DefaultSeconds * 0.3f);

            Assert.That(_rig.IsSwappingShoulder, Is.True);
            Assert.That(_camera.transform.localPosition.z, Is.GreaterThan(-1.4f), "the blended camera must stay in front of the wall");
        }

        [Test]
        public void TickRejectsInvalidDeltaSecondsWithoutSwapping()
        {
            InitializeLocalThirdPerson();

            Assert.Throws<ArgumentOutOfRangeException>(() => _rig.Tick(true, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _rig.Tick(true, float.NaN));

            Assert.That(_rig.Shoulder, Is.EqualTo(ShoulderSide.Right));
        }

        [Test]
        public void TickBeforeInitializeThrows()
        {
            Assert.Throws<InvalidOperationException>(() => _rig.Tick(false, 0f));
        }

        [Test]
        public void LeftShoulderStillHitsWhatTheCrosshairCovers()
        {
            InitializeLocalThirdPerson();
            LogAssert.Expect(LogType.Log, "[View] shoulder=Left");
            _rig.Tick(true, ShoulderBlend.DefaultSeconds);

            var cameraTransform = _camera.transform;
            _wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wall.transform.position = cameraTransform.position + (cameraTransform.forward * 20f);
            Physics.SyncTransforms();

            _rig.ResolveAim(100f, out var origin, out var direction);

            Assert.That(Vector3.Distance(origin, _eye.position), Is.LessThan(1e-5f), "the shot still starts at the eye");
            Assert.That(Physics.Raycast(origin, direction, out var hit, 100f), Is.True);
            Assert.That(hit.collider.gameObject, Is.SameAs(_wall));
        }

        [Test]
        public void RejectsUnknownShoulder()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _rig.Shoulder = (ShoulderSide)99);
            Assert.That(_rig.Shoulder, Is.EqualTo(ShoulderSide.Right));
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
        public void BoomPositionReachesTheOffsetInTheOpen()
        {
            var origin = _eye.position;

            var position = _rig.ResolveBoomPosition(origin, new Vector3(0f, 1.5f, -2f));

            Assert.That(Vector3.Distance(position, origin + new Vector3(0f, 1.5f, -2f)), Is.LessThan(1e-4f));
            Assert.That(_rig.ResolveBoomPosition(origin, Vector3.zero), Is.EqualTo(origin));
        }

        [Test]
        public void BoomPositionStopsShortOfACeiling()
        {
            var origin = _eye.position;
            _wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wall.transform.position = origin + new Vector3(0f, 1f, 0f);
            _wall.transform.localScale = new Vector3(4f, 0.2f, 4f);
            Physics.SyncTransforms();

            var position = _rig.ResolveBoomPosition(origin, new Vector3(0f, 3f, 0f));

            Assert.That(position.y - origin.y, Is.LessThan(0.9f), "the camera must stay under the ceiling (1m up minus its 0.1m half thickness)");
            Assert.That(position.y - origin.y, Is.GreaterThanOrEqualTo(CameraBoom.DefaultMinLength - 1e-4f));
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
            rifle.AssignIdIfEmpty();
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

        private static Vector3 LeftShoulderOffset()
        {
            var offset = ViewRig.DefaultThirdPersonOffset;
            offset.x = -offset.x;
            return offset;
        }

        // この端末のプレイヤーの三人称で、右肩に落ち着いた状態にする。
        private void InitializeLocalThirdPerson()
        {
            _rig.Initialize(_eye, _camera);
            _rig.Mode = ViewMode.ThirdPerson;
            _rig.ApplyBodyVisibility(true);
            _rig.Tick(false, 0f);
        }
    }
}
