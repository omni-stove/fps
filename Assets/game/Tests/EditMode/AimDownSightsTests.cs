using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    public sealed class AimDownSightsTests
    {
        private const float _hip = 60f;
        private const string _rifleId = "00000000-0000-4000-8000-000000000001";

        private GameObject _body;
        private Camera _camera;
        private FirstPersonLook _look;
        private AimDownSights _ads;
        private InputAction _aim;

        [SetUp]
        public void SetUp()
        {
            _body = new GameObject("Body");
            var eye = new GameObject("Eye").transform;
            eye.SetParent(_body.transform, false);
            var cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(eye, false);
            _camera = cameraObject.AddComponent<Camera>();
            _camera.fieldOfView = _hip;
            _look = _body.AddComponent<FirstPersonLook>();
            _ads = _body.AddComponent<AimDownSights>();
            _aim = new InputAction("Aim", InputActionType.Button);
        }

        [TearDown]
        public void TearDown()
        {
            _aim.Dispose();
            Object.DestroyImmediate(_body);
        }

        [Test]
        public void AdsLogHasTagAndLowercaseValue()
        {
            Assert.That(AimDownSights.FormatAdsLog(true), Is.EqualTo("[View] ads=true"));
            Assert.That(AimDownSights.FormatAdsLog(false), Is.EqualTo("[View] ads=false"));
        }

        [Test]
        public void TickBeforeInitializeThrows()
        {
            Assert.That(_ads.IsInitialized, Is.False);
            Assert.Throws<InvalidOperationException>(() => _ads.Tick(true, true, 0.1f));
        }

        [Test]
        public void InitializeRejectsMissingOrMisplacedReferences()
        {
            Assert.Throws<ArgumentNullException>(() => _ads.Initialize(null, _look, _aim));
            Assert.Throws<ArgumentNullException>(() => _ads.Initialize(_camera, null, _aim));
            Assert.Throws<ArgumentNullException>(() => _ads.Initialize(_camera, _look, null));

            var stranger = new GameObject("Stranger");
            try
            {
                var strangerCamera = stranger.AddComponent<Camera>();
                var strangerLook = stranger.AddComponent<FirstPersonLook>();
                Assert.Throws<ArgumentException>(() => _ads.Initialize(strangerCamera, _look, _aim));
                Assert.Throws<ArgumentException>(() => _ads.Initialize(_camera, strangerLook, _aim));
            }
            finally
            {
                Object.DestroyImmediate(stranger);
            }

            Assert.That(_ads.IsInitialized, Is.False);
        }

        [Test]
        public void HipFieldOfViewComesFromTheCameraAndRejectsUnusableValues()
        {
            _camera.fieldOfView = 70f;
            Initialize();
            Assert.That(_ads.HipFieldOfView, Is.EqualTo(70f));

            _ads.HipFieldOfView = _hip;
            Assert.That(_ads.HipFieldOfView, Is.EqualTo(_hip));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ads.HipFieldOfView = 0f);
            Assert.That(_ads.HipFieldOfView, Is.EqualTo(_hip));
        }

        [Test]
        public void HoldingAimNarrowsTheFieldOfViewAndReleasingRestoresIt()
        {
            Initialize();
            var transition = _ads.TransitionSeconds;

            _ads.Tick(true, true, transition / 2f);
            var halfWay = _camera.fieldOfView;
            Assert.That(halfWay, Is.LessThan(_hip).And.GreaterThan(AdsZoom.DefaultFieldOfView), "the zoom is interpolated");

            _ads.Tick(true, false, transition);
            Assert.That(_camera.fieldOfView, Is.EqualTo(AdsZoom.DefaultFieldOfView).Within(1e-4f));
            Assert.That(_ads.IsAiming, Is.True);
            Assert.That(_ads.Progress, Is.EqualTo(1f));

            _ads.Tick(false, false, transition / 2f);
            Assert.That(_camera.fieldOfView, Is.GreaterThan(AdsZoom.DefaultFieldOfView).And.LessThan(_hip));
            Assert.That(_ads.IsAiming, Is.False);

            _ads.Tick(false, false, transition);
            Assert.That(_camera.fieldOfView, Is.EqualTo(_hip).Within(1e-4f));
            Assert.That(_ads.Progress, Is.Zero);
        }

        [Test]
        public void AimingLowersTheLookSensitivityToMatchTheZoom()
        {
            Initialize();
            var mouse = _look.EffectiveMouseSensitivity;
            var stick = _look.EffectiveStickSensitivity;

            _ads.Tick(true, true, 1f);

            var expected = AdsZoom.SensitivityMultiplier(_hip, AdsZoom.DefaultFieldOfView);
            Assert.That(_look.SensitivityMultiplier, Is.EqualTo(expected).Within(1e-5f));
            Assert.That(_look.EffectiveMouseSensitivity, Is.LessThan(mouse));
            Assert.That(_look.EffectiveStickSensitivity, Is.LessThan(stick));
            Assert.That(_look.MouseSensitivity, Is.EqualTo(FirstPersonLook.DefaultMouseSensitivity), "the saved sensitivity is untouched");

            _ads.Tick(false, false, 1f);

            Assert.That(_look.SensitivityMultiplier, Is.EqualTo(1f));
            Assert.That(_look.EffectiveMouseSensitivity, Is.EqualTo(mouse));
        }

        [Test]
        public void ZoomsToTheCurrentWeaponsFieldOfView()
        {
            Initialize();
            WeaponSpec current = Rifle(40f);
            _ads.WeaponSource = () => current;

            _ads.Tick(true, true, 1f);
            Assert.That(_camera.fieldOfView, Is.EqualTo(40f).Within(1e-4f));

            current = Rifle(null);
            _ads.Tick(true, false, 0f);
            Assert.That(_camera.fieldOfView, Is.EqualTo(AdsZoom.DefaultFieldOfView).Within(1e-4f), "an unset value uses the common default");

            current = null;
            _ads.Tick(true, false, 0f);
            Assert.That(_camera.fieldOfView, Is.EqualTo(AdsZoom.DefaultFieldOfView).Within(1e-4f), "no weapon uses the common default");
        }

        [Test]
        public void ReportsOnlyWhenTheStateChanges()
        {
            Initialize();
            var changes = new List<bool>();
            _ads.AimingChanged += changes.Add;

            LogAssert.Expect(LogType.Log, "[View] ads=true");
            _ads.Tick(true, true, 0.01f);
            _ads.Tick(true, false, 0.01f);
            LogAssert.Expect(LogType.Log, "[View] ads=false");
            _ads.Tick(false, false, 0.01f);
            _ads.Tick(false, false, 0.01f);

            Assert.That(changes, Is.EqualTo(new[] { true, false }));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ResetViewReturnsToTheHipAndNeedsAFreshPress()
        {
            Initialize();
            _ads.Tick(true, true, 1f);

            LogAssert.Expect(LogType.Log, "[View] ads=false");
            _ads.ResetView();

            Assert.That(_camera.fieldOfView, Is.EqualTo(_hip));
            Assert.That(_look.SensitivityMultiplier, Is.EqualTo(1f));
            Assert.That(_ads.IsAiming, Is.False);

            _ads.Tick(true, false, 1f);
            Assert.That(_ads.IsAiming, Is.False, "holding the button through a reset must not zoom in again");
            Assert.That(_camera.fieldOfView, Is.EqualTo(_hip));
        }

        [Test]
        public void UsesTheDefaultSettingsUntilLoaded()
        {
            Assert.That(_ads.ControlSettings, Is.EqualTo(ControlSettings.Default));
            Assert.Throws<ArgumentNullException>(() => _ads.ControlSettings = null);
        }

        [Test]
        public void ToggleKeepsTheZoomAfterOnePressAndRestoresOnTheNext()
        {
            Initialize();
            _ads.ControlSettings = ControlSettings.Default.With(InputDeviceKind.KeyboardMouse, HoldAction.Aim, HoldMode.Toggle);

            LogAssert.Expect(LogType.Log, "[View] ads=true");
            _ads.Tick(true, true, InputDeviceKind.KeyboardMouse, 1f);
            _ads.Tick(false, false, InputDeviceKind.KeyboardMouse, 1f);

            Assert.That(_ads.Mode, Is.EqualTo(HoldMode.Toggle));
            Assert.That(_ads.IsAiming, Is.True, "letting go keeps aiming");
            Assert.That(_camera.fieldOfView, Is.EqualTo(AdsZoom.DefaultFieldOfView).Within(1e-4f));

            LogAssert.Expect(LogType.Log, "[View] ads=false");
            _ads.Tick(true, true, InputDeviceKind.KeyboardMouse, 1f);
            _ads.Tick(false, false, InputDeviceKind.KeyboardMouse, 1f);

            Assert.That(_ads.IsAiming, Is.False);
            Assert.That(_camera.fieldOfView, Is.EqualTo(_hip).Within(1e-4f));
        }

        [Test]
        public void ModeFollowsTheDeviceAndSwitchingItStopsAiming()
        {
            Initialize();
            _ads.ControlSettings = ControlSettings.Default.With(InputDeviceKind.Gamepad, HoldAction.Aim, HoldMode.Toggle);

            _ads.Tick(true, true, InputDeviceKind.Gamepad, 1f);
            _ads.Tick(false, false, 0f);
            Assert.That(_ads.ActiveDevice, Is.EqualTo(InputDeviceKind.Gamepad));
            Assert.That(_ads.IsAiming, Is.True, "the last device's mode stays in use while nothing is pressed");

            LogAssert.Expect(LogType.Log, "[View] ads=false");
            _ads.Tick(false, false, InputDeviceKind.KeyboardMouse, 0f);
            Assert.That(_ads.Mode, Is.EqualTo(HoldMode.Hold));
            Assert.That(_ads.IsAiming, Is.False);
        }

        [TestCase(HoldMode.Hold)]
        [TestCase(HoldMode.Toggle)]
        public void ReleaseStopsAimingAndZoomsOutGradually(HoldMode mode)
        {
            Initialize();
            _ads.ControlSettings = ControlSettings.Default.With(InputDeviceKind.KeyboardMouse, HoldAction.Aim, mode);
            var changes = new List<bool>();
            _ads.Tick(true, true, InputDeviceKind.KeyboardMouse, 1f);
            _ads.AimingChanged += changes.Add;

            LogAssert.Expect(LogType.Log, "[View] ads=false");
            Assert.That(_ads.Release(), Is.True);
            Assert.That(_ads.Release(), Is.False, "already released");
            Assert.That(changes, Is.EqualTo(new[] { false }));
            Assert.That(_camera.fieldOfView, Is.EqualTo(AdsZoom.DefaultFieldOfView).Within(1e-4f), "the zoom is not snapped back");

            _ads.Tick(true, false, InputDeviceKind.KeyboardMouse, _ads.TransitionSeconds / 2f);
            Assert.That(_ads.IsAiming, Is.False, "holding the button through a release does not aim again");
            Assert.That(_camera.fieldOfView, Is.GreaterThan(AdsZoom.DefaultFieldOfView).And.LessThan(_hip));
        }

        [Test]
        public void ReleaseBeforeInitializeDoesNothing()
        {
            Assert.That(_ads.Release(), Is.False);
        }

        [Test]
        public void ResolvingWithoutCameraLogsAnError()
        {
            var bare = new GameObject("Bare");
            try
            {
                bare.AddComponent<FirstPersonLook>();
                var ads = bare.AddComponent<AimDownSights>();
                LogAssert.Expect(LogType.Error, new Regex(@"\[View\] .*Camera"));

                Assert.That(ads.TryInitializeFromHierarchy(), Is.False);
                Assert.That(ads.IsInitialized, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(bare);
            }
        }

        [Test]
        public void ResolvesCameraLookAndProjectWideAimAction()
        {
            Assert.That(InputSystem.actions, Is.Not.Null, "Project-wide Input Actions must be assigned");
            Assert.That(_ads.TryInitializeFromHierarchy(), Is.True);
            Assert.That(_ads.Camera, Is.SameAs(_camera));
            Assert.That(_ads.Look, Is.SameAs(_look));
            Assert.That(_ads.AimAction, Is.SameAs(InputSystem.actions.FindAction(AimDownSights.AimActionPath)));
            Assert.That(_ads.HipFieldOfView, Is.EqualTo(_hip));
        }

        private void Initialize()
        {
            _ads.Initialize(_camera, _look, _aim);
        }

        private static WeaponSpec Rifle(float? adsFieldOfView)
        {
            return new WeaponSpec(
                _rifleId,
                "Rifle",
                WeaponCategory.AssaultRifle,
                FireMode.Auto,
                30,
                90,
                0.1f,
                2f,
                25,
                100f,
                DamageFalloff.None,
                adsFieldOfView);
        }
    }
}
