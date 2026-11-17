using System;
using System.Text.RegularExpressions;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
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
        public void InitializeCreatesSeparateShakeAndViewmodelRootsUnderTheCamera()
        {
            Assert.That(_rig.ShakeRoot, Is.Null, "nothing before initializing");
            Assert.That(_rig.ViewmodelRoot, Is.Null);
            _camera.gameObject.layer = 5;

            _rig.Initialize(_eye, _camera);

            Assert.That(_rig.ShakeRoot, Is.Not.Null);
            Assert.That(_rig.ViewmodelRoot, Is.Not.Null);
            Assert.That(_rig.ShakeRoot, Is.Not.SameAs(_rig.ViewmodelRoot));
            Assert.That(_rig.ShakeRoot.name, Is.EqualTo(ViewRig.ShakeRootName));
            Assert.That(_rig.ViewmodelRoot.name, Is.EqualTo(ViewRig.ViewmodelRootName));
            Assert.That(_rig.ShakeRoot.parent, Is.SameAs(_camera.transform));
            Assert.That(_rig.ViewmodelRoot.parent, Is.SameAs(_camera.transform));
            Assert.That(_rig.ShakeRoot.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(_rig.ViewmodelRoot.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(_rig.ViewmodelRoot.gameObject.layer, Is.EqualTo(5), "the roots share the camera's layer");
        }

        [Test]
        public void InitializingAgainReusesTheRoots()
        {
            _rig.Initialize(_eye, _camera);
            var shake = _rig.ShakeRoot;
            var viewmodel = _rig.ViewmodelRoot;

            Assert.That(_rig.TryInitializeFromHierarchy(), Is.True);

            Assert.That(_rig.ShakeRoot, Is.SameAs(shake));
            Assert.That(_rig.ViewmodelRoot, Is.SameAs(viewmodel));
            Assert.That(_camera.transform.childCount, Is.EqualTo(2), "no duplicate roots");
        }

        [Test]
        public void RootsAlreadyUnderTheCameraAreUsed()
        {
            var placed = new GameObject(ViewRig.ViewmodelRootName).transform;
            placed.SetParent(_camera.transform, false);
            placed.localPosition = new Vector3(0.1f, -0.2f, 0.3f);

            _rig.Initialize(_eye, _camera);

            Assert.That(_rig.ViewmodelRoot, Is.SameAs(placed));
            Assert.That(placed.localPosition, Is.EqualTo(new Vector3(0.1f, -0.2f, 0.3f)), "an existing root is not moved");
        }

        [Test]
        public void CameraPlacementDoesNotMoveTheRoots()
        {
            _rig.Initialize(_eye, _camera);
            _rig.Mode = ViewMode.ThirdPerson;

            _rig.UpdateCameraPosition();

            Assert.That(_camera.transform.localPosition, Is.Not.EqualTo(Vector3.zero), "the camera sits behind the shoulder");
            Assert.That(_rig.ShakeRoot.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(_rig.ViewmodelRoot.localPosition, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void EnsureCameraChildRejectsMissingArguments()
        {
            Assert.Throws<ArgumentNullException>(() => ViewRig.EnsureCameraChild(null, ViewRig.ShakeRootName));
            Assert.Throws<ArgumentException>(() => ViewRig.EnsureCameraChild(_camera, string.Empty));
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
        public void AScopeInThirdPersonPullsTheCameraToTheEyeAndHidesTheBody()
        {
            var sight = new SightSpec("mildot", scopeOverlay: true, eyeViewInThirdPerson: true);
            var progress = 0f;
            _rig.SightSource = () => SightView.Of(sight, _rig.Mode, progress);
            InitializeLocalThirdPerson();
            Assert.That(Vector3.Distance(_camera.transform.localPosition, ViewRig.DefaultThirdPersonOffset), Is.LessThan(1e-4f));
            Assert.That(_rig.IsBodyHiddenBySight, Is.False);

            progress = 0.5f;
            _rig.UpdateCameraPosition();

            // 半分では smoothstep でも半分まで寄る
            Assert.That(Vector3.Distance(_camera.transform.localPosition, ViewRig.DefaultThirdPersonOffset * 0.5f), Is.LessThan(1e-4f));
            Assert.That(_rig.IsBodyHiddenBySight, Is.True);
            Assert.That(_body.GetComponent<Renderer>().shadowCastingMode, Is.EqualTo(ShadowCastingMode.ShadowsOnly));

            progress = 1f;
            _rig.UpdateCameraPosition();
            Assert.That(_camera.transform.localPosition, Is.EqualTo(Vector3.zero));

            progress = 0f;
            _rig.UpdateCameraPosition();
            Assert.That(Vector3.Distance(_camera.transform.localPosition, ViewRig.DefaultThirdPersonOffset), Is.LessThan(1e-4f));
            Assert.That(_rig.IsBodyHiddenBySight, Is.False);
            Assert.That(_body.GetComponent<Renderer>().shadowCastingMode, Is.EqualTo(ShadowCastingMode.On));
        }

        [Test]
        public void ASightThatStaysOnTheShoulderKeepsTheThirdPersonCamera()
        {
            var sight = new SightSpec("redDot");
            _rig.SightSource = () => SightView.Of(sight, _rig.Mode, 1f);
            InitializeLocalThirdPerson();

            Assert.That(Vector3.Distance(_camera.transform.localPosition, ViewRig.DefaultThirdPersonOffset), Is.LessThan(1e-4f));
            Assert.That(_rig.IsBodyHiddenBySight, Is.False);
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
        public void TheConfiguredMinBoomLengthKeepsTheCameraAwayFromTheEye()
        {
            Assert.That(_rig.MinBoomLength, Is.EqualTo(CameraBoom.DefaultMinLength));
            _rig.Initialize(_eye, _camera);
            _rig.Mode = ViewMode.ThirdPerson;
            _rig.ThirdPersonOffset = new Vector3(0f, 0f, -3f);
            _wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wall.transform.position = _eye.position + new Vector3(0f, 0f, -0.5f);
            _wall.transform.localScale = new Vector3(4f, 4f, 0.2f);
            Physics.SyncTransforms();

            _rig.MinBoomLength = 0.6f;
            _rig.UpdateCameraPosition();

            Assert.That(_rig.MinBoomLength, Is.EqualTo(0.6f));
            Assert.That(_camera.transform.localPosition.z, Is.EqualTo(-0.6f).Within(1e-3f));
        }

        [TestCase(-0.1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void TheMinBoomLengthRejectsUnusableValues(float value)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _rig.MinBoomLength = value);
            Assert.That(_rig.MinBoomLength, Is.EqualTo(CameraBoom.DefaultMinLength));
        }

        [Test]
        public void AFartherThirdPersonOffsetPutsTheCameraFartherBehind()
        {
            _rig.Initialize(_eye, _camera);
            _rig.Mode = ViewMode.ThirdPerson;
            _rig.ThirdPersonOffset = new Vector3(0f, 0f, -5f);

            _rig.UpdateCameraPosition();

            Assert.That(_camera.transform.localPosition.z, Is.EqualTo(-5f).Within(1e-3f));
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
        public void BoomPositionCanIgnoreAnotherBody()
        {
            // 他のプレイヤーの目から肩越しへ置くときは、その体の中から球を飛ばすことになる
            _wall = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _wall.transform.position = new Vector3(10f, 1f, 0f);
            Physics.SyncTransforms();
            var origin = new Vector3(10f, 1.6f, 0f);
            var offset = new Vector3(0.5f, 0.2f, -2.5f);

            var blocked = _rig.ResolveBoomPosition(origin, offset);
            var ignored = _rig.ResolveBoomPosition(origin, offset, _wall.transform);

            Assert.That(Vector3.Distance(blocked, origin), Is.LessThan(offset.magnitude - 1f), "the other body shortens the boom");
            Assert.That(Vector3.Distance(ignored, origin + offset), Is.LessThan(1e-4f));
        }

        [Test]
        public void FreeMoveGoesTheWholeWayInTheOpen()
        {
            var from = new Vector3(0f, 5f, 0f);

            Assert.That(Vector3.Distance(_rig.ResolveFreeMove(from, new Vector3(0f, 0f, 3f)), new Vector3(0f, 5f, 3f)), Is.LessThan(1e-4f));
            Assert.That(_rig.ResolveFreeMove(from, Vector3.zero), Is.EqualTo(from));
        }

        [Test]
        public void FreeMoveStopsShortOfAWallAndStaysOut()
        {
            // 手前の面は z = 2.9
            _wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wall.transform.position = new Vector3(0f, 5f, 3f);
            _wall.transform.localScale = new Vector3(4f, 4f, 0.2f);
            Physics.SyncTransforms();
            var limit = 2.9f - _rig.CollisionRadius;

            var first = _rig.ResolveFreeMove(new Vector3(0f, 5f, 0f), new Vector3(0f, 0f, 10f));
            var second = _rig.ResolveFreeMove(first, new Vector3(0f, 0f, 1f));

            Assert.That(first.z, Is.LessThanOrEqualTo(limit + 1e-3f));
            Assert.That(first.z, Is.GreaterThan(limit - ViewRig.FreeMoveSkin - 0.01f));
            Assert.That(second.z, Is.LessThanOrEqualTo(limit + 1e-3f), "pushing again does not go through");
        }

        [Test]
        public void FreeMoveSlidesAlongAWall()
        {
            _wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wall.transform.position = new Vector3(0f, 5f, 3f);
            _wall.transform.localScale = new Vector3(4f, 4f, 0.2f);
            Physics.SyncTransforms();

            var position = _rig.ResolveFreeMove(new Vector3(0f, 5f, 0f), new Vector3(1f, 0f, 10f));

            Assert.That(position.x, Is.EqualTo(1f).Within(1e-2f), "the sideways part of the move is kept");
            Assert.That(position.z, Is.LessThanOrEqualTo(2.9f - _rig.CollisionRadius + 1e-3f));
        }

        [Test]
        public void FreeMoveCanLeaveSomethingItStartedInside()
        {
            _wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wall.transform.position = new Vector3(0f, 5f, 0f);
            Physics.SyncTransforms();

            var position = _rig.ResolveFreeMove(new Vector3(0f, 5f, 0f), new Vector3(0f, 0f, 3f));

            Assert.That(Vector3.Distance(position, new Vector3(0f, 5f, 3f)), Is.LessThan(1e-4f));
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
        public void ThirdPersonFindsShoulderHighCoverBetweenTheEyeAndTheAimPoint()
        {
            _rig.Initialize(_eye, _camera);
            _rig.Mode = ViewMode.ThirdPerson;
            _rig.UpdateCameraPosition();

            // カメラからは 20m 先の標的が見えているが、目の 1m 先に目の線だけを遮る小さな箱がある
            var cameraTransform = _camera.transform;
            _wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wall.transform.position = cameraTransform.position + (cameraTransform.forward * 20f);
            var cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cover.transform.SetParent(_wall.transform, true);
            cover.transform.position = _eye.position + new Vector3(0f, 0f, 1f);
            cover.transform.localScale = new Vector3(0.3f, 0.3f, 0.1f);
            Physics.SyncTransforms();

            var blocked = _rig.TryFindAimBlock(100f, AimBlockSettings.DefaultMargin, out var blockPoint);

            Assert.That(blocked, Is.True);
            Assert.That(blockPoint.z - _eye.position.z, Is.EqualTo(0.95f).Within(0.01f), "the mark sits on the near face of the cover");
        }

        [Test]
        public void ThirdPersonIsNotBlockedWhenTheEyeSeesTheAimPoint()
        {
            _rig.Initialize(_eye, _camera);
            _rig.Mode = ViewMode.ThirdPerson;
            _rig.UpdateCameraPosition();
            var cameraTransform = _camera.transform;
            _wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wall.transform.position = cameraTransform.position + (cameraTransform.forward * 20f);
            Physics.SyncTransforms();

            Assert.That(_rig.TryFindAimBlock(100f, AimBlockSettings.DefaultMargin, out var blockPoint), Is.False);
            Assert.That(blockPoint, Is.EqualTo(_eye.position));
        }

        [Test]
        public void NothingToAimAtIsNotBlocked()
        {
            _rig.Initialize(_eye, _camera);
            _rig.Mode = ViewMode.ThirdPerson;
            _rig.UpdateCameraPosition();

            Assert.That(_rig.TryFindAimBlock(100f, AimBlockSettings.DefaultMargin, out _), Is.False);
        }

        [Test]
        public void FirstPersonIsNotBlockedByWhatTheCrosshairCovers()
        {
            _rig.Initialize(_eye, _camera);
            _rig.Mode = ViewMode.FirstPerson;
            _rig.UpdateCameraPosition();
            _wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wall.transform.position = _eye.position + new Vector3(0f, 0f, 5f);
            Physics.SyncTransforms();

            Assert.That(_rig.TryFindAimBlock(100f, AimBlockSettings.DefaultMargin, out _), Is.False);
        }

        [Test]
        public void AimBlockRejectsANegativeMargin()
        {
            _rig.Initialize(_eye, _camera);

            Assert.Throws<ArgumentOutOfRangeException>(() => _rig.TryFindAimBlock(100f, -0.1f, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => _rig.TryFindAimBlock(0f, 0.3f, out _));
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

        [Test]
        public void FirstPersonLeanShiftsTheCameraSidewaysAndTiltsIt()
        {
            _rig.Initialize(_eye, _camera);
            Lean(1f);

            _rig.UpdateCameraPosition();

            // 既定の MotorSpec は 0.4m・15 度
            Assert.That(Vector3.Distance(_camera.transform.localPosition, new Vector3(0.4f, 0f, 0f)), Is.LessThan(1e-4f));
            Assert.That(_rig.LeanAmount, Is.EqualTo(1f));
            Assert.That(_rig.LeanRollDegrees, Is.EqualTo(-15f).Within(1e-4f));
            Assert.That(Quaternion.Angle(_camera.transform.localRotation, Quaternion.Euler(0f, 0f, -15f)), Is.LessThan(0.01f));
            Assert.That(_camera.transform.up.x, Is.GreaterThan(0f), "leaning right tilts the top of the view to the right");
        }

        [Test]
        public void HalfALeanToTheLeftShiftsAndTiltsHalfAsFar()
        {
            _rig.Initialize(_eye, _camera);
            Lean(-0.5f);

            _rig.UpdateCameraPosition();

            Assert.That(Vector3.Distance(_camera.transform.localPosition, new Vector3(-0.2f, 0f, 0f)), Is.LessThan(1e-4f));
            Assert.That(_rig.LeanRollDegrees, Is.EqualTo(7.5f).Within(1e-4f));
            Assert.That(_camera.transform.up.x, Is.LessThan(0f));
        }

        [Test]
        public void LeanUsesTheSourcesMotorSpec()
        {
            _rig.Initialize(_eye, _camera);
            var source = Lean(1f);
            source.Spec = new MotorSpec(
                MotorSpec.DefaultWalkSpeed,
                MotorSpec.DefaultSprintSpeed,
                MotorSpec.DefaultJumpHeight,
                MotorSpec.DefaultGravity,
                MotorSpec.DefaultAirControl,
                MotorSpec.DefaultCrouchSpeed,
                leanSideOffset: 0.25f,
                leanAngleDegrees: 10f);

            _rig.UpdateCameraPosition();

            Assert.That(Vector3.Distance(_camera.transform.localPosition, new Vector3(0.25f, 0f, 0f)), Is.LessThan(1e-4f));
            Assert.That(_rig.LeanRollDegrees, Is.EqualTo(-10f).Within(1e-4f));
        }

        [Test]
        public void LeanFollowsTheBodyYawAndTheEyePitch()
        {
            _rig.Initialize(_eye, _camera);
            _body.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            _eye.localRotation = Quaternion.Euler(-30f, 0f, 0f);
            Lean(1f);

            _rig.UpdateCameraPosition();

            // 東を向いた体の右は南 (-z)。見上げても横へずれるだけで、高さは変わらない
            Assert.That(Vector3.Distance(_camera.transform.position, _eye.position + new Vector3(0f, 0f, -0.4f)), Is.LessThan(1e-4f));
            Assert.That(Vector3.Distance(_camera.transform.localPosition, new Vector3(0.4f, 0f, 0f)), Is.LessThan(1e-4f));
        }

        [Test]
        public void FirstPersonLeanStopsShortOfAWall()
        {
            _rig.Initialize(_eye, _camera);
            // 壁の手前の面は目の 0.4m 右。カメラの球 (半径 0.2m) はその手前で止まる
            _wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wall.transform.position = _eye.position + new Vector3(0.5f, 0f, 0f);
            _wall.transform.localScale = new Vector3(0.2f, 4f, 4f);
            Physics.SyncTransforms();
            Lean(1f);

            _rig.UpdateCameraPosition();

            var x = _camera.transform.localPosition.x;
            Assert.That(x, Is.LessThanOrEqualTo(0.4f - _rig.CollisionRadius + 1e-3f), "the camera must not enter the wall");
            Assert.That(x, Is.GreaterThan(0.1f), "it still leans as far as the wall allows");
            Assert.That(_rig.LeanRollDegrees, Is.EqualTo(-15f).Within(1e-4f));
        }

        [Test]
        public void FirstPersonLeanAgainstAWallTouchingTheEyeStaysOnTheEye()
        {
            _rig.Initialize(_eye, _camera);
            _wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            // 手前の面は目の 0.15m 右で、カメラの球 (半径 0.2m) は目にいるうちから壁に掛かっている
            _wall.transform.position = _eye.position + new Vector3(0.25f, 0f, 0f);
            _wall.transform.localScale = new Vector3(0.2f, 4f, 4f);
            Physics.SyncTransforms();
            Lean(1f);

            _rig.UpdateCameraPosition();

            Assert.That(_camera.transform.localPosition.x, Is.LessThan(0.01f), "the third-person minimum length does not push the camera into the wall");
            Assert.That(_camera.transform.localPosition.x, Is.GreaterThanOrEqualTo(0f));
        }

        [Test]
        public void StandingUpPutsTheCameraBackOnTheEye()
        {
            _rig.Initialize(_eye, _camera);
            var source = Lean(1f);
            _rig.UpdateCameraPosition();

            source.State = FakeCharacterMotorStateSource.Motor();
            _rig.UpdateCameraPosition();

            Assert.That(_camera.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(_rig.LeanRollDegrees, Is.EqualTo(0f));
            Assert.That(Quaternion.Angle(_camera.transform.localRotation, Quaternion.identity), Is.LessThan(0.01f));
        }

        [Test]
        public void LeanTiltIsAddedOnTopOfWhatOthersWriteToTheCamera()
        {
            _rig.Initialize(_eye, _camera);
            var source = Lean(1f);
            _rig.UpdateCameraPosition();

            // 照準器の揺れ (AimDownSights) がカメラの向きを書き直した
            var sway = Quaternion.Euler(-2f, 1f, 0f);
            _camera.transform.localRotation = sway;
            _rig.UpdateCameraPosition();
            Assert.That(Quaternion.Angle(_camera.transform.localRotation, sway * Quaternion.Euler(0f, 0f, -15f)), Is.LessThan(0.01f));

            _rig.UpdateCameraPosition();
            Assert.That(Quaternion.Angle(_camera.transform.localRotation, sway * Quaternion.Euler(0f, 0f, -15f)), Is.LessThan(0.01f),
                "the tilt is not stacked frame after frame");

            source.State = FakeCharacterMotorStateSource.Motor();
            _rig.UpdateCameraPosition();
            Assert.That(Quaternion.Angle(_camera.transform.localRotation, sway), Is.LessThan(0.01f), "only the tilt is taken off");
        }

        [Test]
        public void WithoutAMotorSourceTheCameraDoesNotLean()
        {
            _rig.Initialize(_eye, _camera);

            _rig.UpdateCameraPosition();

            Assert.That(_rig.MotorSource, Is.Null);
            Assert.That(_rig.LeanAmount, Is.EqualTo(0f));
            Assert.That(_camera.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(_camera.transform.localRotation, Is.EqualTo(Quaternion.identity));
        }

        [Test]
        public void AnInvalidOrMissingLeanStateDoesNotLean()
        {
            _rig.Initialize(_eye, _camera);
            var source = Lean(float.NaN);

            _rig.UpdateCameraPosition();
            Assert.That(_rig.LeanAmount, Is.EqualTo(0f));
            Assert.That(_camera.transform.localPosition, Is.EqualTo(Vector3.zero));

            source.State = FakeCharacterMotorStateSource.Motor(lean: 1f);
            source.HasState = false;
            _rig.UpdateCameraPosition();
            Assert.That(_rig.LeanAmount, Is.EqualTo(0f));
            Assert.That(_camera.transform.localPosition, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void AnAssignedMotorSourceIsUsedInsteadOfTheBodys()
        {
            _rig.Initialize(_eye, _camera);
            Lean(1f);
            var other = new GameObject("Other").AddComponent<FakeCharacterMotorStateSource>();
            try
            {
                other.State = FakeCharacterMotorStateSource.Motor(lean: -1f);
                _rig.MotorSource = other;

                _rig.UpdateCameraPosition();

                Assert.That(_rig.MotorSource, Is.SameAs(other));
                Assert.That(_rig.LeanAmount, Is.EqualTo(-1f));
            }
            finally
            {
                Object.DestroyImmediate(other.gameObject);
            }
        }

        [Test]
        public void ThirdPersonLeanMovesTheCameraToTheLeaningShoulderAndBack()
        {
            InitializeLocalThirdPerson();
            LogAssert.Expect(LogType.Log, "[View] shoulder=Left");
            _rig.Shoulder = ShoulderSide.Left;
            _rig.Tick(false, ShoulderBlend.DefaultSeconds);
            var source = Lean(1f);

            _rig.Tick(false, ShoulderBlend.DefaultSeconds);

            Assert.That(_rig.CameraShoulder, Is.EqualTo(ShoulderSide.Right));
            Assert.That(_rig.Shoulder, Is.EqualTo(ShoulderSide.Left), "the chosen shoulder is kept");
            Assert.That(Vector3.Distance(_camera.transform.localPosition, ViewRig.DefaultThirdPersonOffset), Is.LessThan(1e-4f),
                "third person moves to the shoulder instead of shifting the camera");
            Assert.That(_rig.LeanRollDegrees, Is.EqualTo(0f), "third person does not tilt the camera");

            source.State = FakeCharacterMotorStateSource.Motor();
            _rig.Tick(false, ShoulderBlend.DefaultSeconds);

            Assert.That(_rig.CameraShoulder, Is.EqualTo(ShoulderSide.Left));
            Assert.That(Vector3.Distance(_camera.transform.localPosition, LeftShoulderOffset()), Is.LessThan(1e-4f));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ThirdPersonLeanBlendsAcrossInsteadOfJumping()
        {
            InitializeLocalThirdPerson();
            Lean(-1f);
            var right = ViewRig.DefaultThirdPersonOffset.x;

            _rig.Tick(false, ShoulderBlend.DefaultSeconds / 3f);

            Assert.That(_rig.CameraShoulder, Is.EqualTo(ShoulderSide.Left));
            Assert.That(_rig.IsSwappingShoulder, Is.True);
            Assert.That(_camera.transform.localPosition.x, Is.LessThan(right).And.GreaterThan(-right));
        }

        [Test]
        public void SwappingWhileLeaningChangesTheShoulderToReturnTo()
        {
            InitializeLocalThirdPerson();
            var source = Lean(1f);
            _rig.Tick(false, ShoulderBlend.DefaultSeconds);
            LogAssert.Expect(LogType.Log, "[View] shoulder=Left");

            _rig.Tick(true, ShoulderBlend.DefaultSeconds);

            Assert.That(_rig.Shoulder, Is.EqualTo(ShoulderSide.Left));
            Assert.That(_rig.CameraShoulder, Is.EqualTo(ShoulderSide.Right), "the lean still holds the camera on its side");

            source.State = FakeCharacterMotorStateSource.Motor();
            _rig.Tick(false, ShoulderBlend.DefaultSeconds);
            Assert.That(_rig.CameraShoulder, Is.EqualTo(ShoulderSide.Left));
        }

        [TestCase(1f, ShoulderSide.Right)]
        [TestCase(0.01f, ShoulderSide.Right)]
        [TestCase(-0.5f, ShoulderSide.Left)]
        [TestCase(0f, ShoulderSide.Left)]
        public void CameraShoulderFollowsTheLeanSide(float lean, ShoulderSide expected)
        {
            LogAssert.Expect(LogType.Log, "[View] shoulder=Left");
            _rig.Shoulder = ShoulderSide.Left;

            Assert.That(_rig.CameraShoulderFor(lean), Is.EqualTo(expected));
        }

        [Test]
        public void LeanEyeOffsetRejectsAMissingSpec()
        {
            _rig.Initialize(_eye, _camera);

            Assert.Throws<ArgumentNullException>(() => _rig.LeanEyeOffset(1f, null));
            var bare = new GameObject("Bare");
            try
            {
                var uninitialized = bare.AddComponent<ViewRig>();
                Assert.Throws<InvalidOperationException>(() => uninitialized.LeanEyeOffset(1f, MotorSpec.Default));
            }
            finally
            {
                Object.DestroyImmediate(bare);
            }
        }

        // 体に移動の状態の読み元を付け、リーンの傾きだけを決めた状態にする。
        private FakeCharacterMotorStateSource Lean(float amount)
        {
            var source = _body.GetComponent<FakeCharacterMotorStateSource>();
            if (source == null)
            {
                source = _body.AddComponent<FakeCharacterMotorStateSource>();
            }

            source.State = FakeCharacterMotorStateSource.Motor(lean: amount);
            return source;
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
