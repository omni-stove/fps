using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
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

        // 「狙うときだけ」のジャイロが、覗き込みの間だけ効くよう視点に伝わる
        [Test]
        public void AimingTellsTheLookForTheAimOnlyGyro()
        {
            Initialize();
            Assert.That(_look.Aiming, Is.False);

            _ads.Tick(true, true, 0.01f);
            Assert.That(_look.Aiming, Is.True, "as soon as aiming starts, before the zoom finishes");

            _ads.Tick(false, false, 0.01f);
            Assert.That(_look.Aiming, Is.False);

            _ads.Tick(true, true, 0.01f);
            _ads.ResetView();
            Assert.That(_look.Aiming, Is.False, "resetting the view stops aiming");
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
        public void WithoutAWeaponTheConfiguredFallbackFieldOfViewIsUsed()
        {
            Assert.That(_ads.FallbackFieldOfView, Is.EqualTo(AdsZoom.DefaultFieldOfView));
            Initialize();
            _ads.FallbackFieldOfView = 35f;

            _ads.Tick(true, true, 1f);

            Assert.That(_ads.TargetFieldOfView, Is.EqualTo(35f));
            Assert.That(_camera.fieldOfView, Is.EqualTo(35f).Within(1e-4f));

            _ads.WeaponSource = () => Rifle(40f);
            _ads.Tick(true, false, 0f);
            Assert.That(_camera.fieldOfView, Is.EqualTo(40f).Within(1e-4f), "a weapon's own value wins over the fallback");
        }

        [TestCase(0f)]
        [TestCase(180f)]
        [TestCase(float.NaN)]
        public void TheFallbackFieldOfViewRejectsUnusableValues(float value)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _ads.FallbackFieldOfView = value);
            Assert.That(_ads.FallbackFieldOfView, Is.EqualTo(AdsZoom.DefaultFieldOfView));
        }

        [Test]
        public void ALongerTransitionTakesLongerToZoomIn()
        {
            Initialize();
            _ads.TransitionSeconds = 0.6f;

            _ads.Tick(true, true, 0.3f);
            Assert.That(_ads.Progress, Is.EqualTo(0.5f).Within(1e-5f), "half of the configured time is half way");

            _ads.Tick(true, false, 0.3f);
            Assert.That(_ads.Progress, Is.EqualTo(1f));
            Assert.That(_camera.fieldOfView, Is.EqualTo(AdsZoom.DefaultFieldOfView).Within(1e-4f));
        }

        [Test]
        public void ChangingTheTransitionMidwayKeepsTheProgress()
        {
            Initialize();
            _ads.TransitionSeconds = 1f;
            _ads.Tick(true, true, 0.25f);

            _ads.TransitionSeconds = 0.5f;
            Assert.That(_ads.Progress, Is.EqualTo(0.25f).Within(1e-5f));

            _ads.Tick(true, false, 0.25f);
            Assert.That(_ads.Progress, Is.EqualTo(0.75f).Within(1e-5f));
        }

        [TestCase(-0.1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void TheTransitionRejectsUnusableValues(float value)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _ads.TransitionSeconds = value);
            Assert.That(_ads.TransitionSeconds, Is.EqualTo(AdsZoom.DefaultTransitionSeconds));
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
        public void AMeleeWeaponIsNeverAimed()
        {
            Initialize();
            var melee = new WeaponSpec(_rifleId, "weapon.crowbar", WeaponCategory.Melee, melee: MeleeWeaponSpec.Default);
            var gun = new WeaponSpec(_rifleId, "weapon.rifle", WeaponCategory.AssaultRifle);
            var held = melee;
            _ads.WeaponSource = () => held;

            _ads.Tick(true, true, _ads.TransitionSeconds);
            Assert.That(_ads.IsAiming, Is.False, "the aim button throws a melee weapon instead");
            Assert.That(_camera.fieldOfView, Is.EqualTo(_hip).Within(1e-4f));

            held = gun;
            LogAssert.Expect(LogType.Log, "[View] ads=true");
            _ads.Tick(true, false, 0f);
            Assert.That(_ads.IsAiming, Is.True, "a gun aims again while the button is held");
        }

        [Test]
        public void ABlockingWeaponReleasesAimingAndKeepsItFromStarting()
        {
            Initialize();
            var blocked = false;
            _ads.AdsBlocker = () => blocked;
            var changes = new List<bool>();
            _ads.AimingChanged += changes.Add;

            _ads.Tick(true, true, 1f);
            Assert.That(_ads.IsAiming, Is.True);

            blocked = true;
            LogAssert.Expect(LogType.Log, "[View] ads=false");
            _ads.Tick(true, false, 0f);
            Assert.That(_ads.IsAiming, Is.False);

            _ads.Tick(true, true, _ads.TransitionSeconds);
            Assert.That(_ads.IsAiming, Is.False, "pressing again while blocked does not aim");
            Assert.That(_camera.fieldOfView, Is.EqualTo(_hip).Within(1e-4f));
            Assert.That(changes, Is.EqualTo(new[] { true, false }));

            blocked = false;
            _ads.Tick(false, false, 0f);
            _ads.Tick(true, true, 0f);
            Assert.That(_ads.IsAiming, Is.True, "aiming works again once the weapon stops blocking");
        }

        [TestCase(HoldMode.Hold)]
        [TestCase(HoldMode.Toggle)]
        public void AimingComesBackWithoutAPressOnceTheWeaponStopsBlocking(HoldMode mode)
        {
            Initialize();
            _ads.ControlSettings = ControlSettings.Default.With(InputDeviceKind.KeyboardMouse, HoldAction.Aim, mode);
            var blocked = false;
            _ads.AdsBlocker = () => blocked;
            var changes = new List<bool>();
            _ads.AimingChanged += changes.Add;
            var held = mode == HoldMode.Hold;

            _ads.Tick(true, true, 1f);
            _ads.Tick(held, false, 0f);

            blocked = true;
            LogAssert.Expect(LogType.Log, "[View] ads=false");
            _ads.Tick(held, false, _ads.TransitionSeconds);
            Assert.That(_ads.IsAiming, Is.False);
            Assert.That(_camera.fieldOfView, Is.EqualTo(_hip).Within(1e-4f));

            blocked = false;
            LogAssert.Expect(LogType.Log, "[View] ads=true");
            _ads.Tick(held, false, 0f);
            Assert.That(_ads.IsAiming, Is.True, "the aim the player kept asking for returns");
            Assert.That(changes, Is.EqualTo(new[] { true, false, true }));
        }

        [Test]
        public void AReleaseWhileBlockedKeepsAimingFromComingBack()
        {
            Initialize();
            var blocked = false;
            _ads.AdsBlocker = () => blocked;

            _ads.Tick(true, true, 1f);
            blocked = true;
            LogAssert.Expect(LogType.Log, "[View] ads=false");
            _ads.Tick(true, false, 0f);

            Assert.That(_ads.Release(), Is.False, "nothing to report while already blocked");
            blocked = false;
            _ads.Tick(true, false, 0f);

            Assert.That(_ads.IsAiming, Is.False, "a reload or swap during the block still needs a fresh press");
        }

        [Test]
        public void ApplyingSettingsMovesTheHipFieldOfViewRightAway()
        {
            Initialize();

            _ads.ApplyControlSettings(ControlSettings.Default.With(ControlValue.FieldOfView, 80));

            Assert.That(_ads.HipFieldOfView, Is.EqualTo(80f));
            Assert.That(_camera.fieldOfView, Is.EqualTo(80f), "the camera follows without waiting for the next tick");
            Assert.That(_look.SensitivityMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void ApplyingSettingsWhileAimingKeepsTheZoom()
        {
            Initialize();
            _ads.Tick(true, true, 1f);

            _ads.ApplyControlSettings(ControlSettings.Default.With(ControlValue.FieldOfView, 80));

            Assert.That(_ads.IsAiming, Is.True);
            Assert.That(_camera.fieldOfView, Is.EqualTo(AdsZoom.DefaultFieldOfView).Within(1e-4f));
            Assert.That(
                _look.SensitivityMultiplier,
                Is.EqualTo(AdsZoom.SensitivityMultiplier(80f, AdsZoom.DefaultFieldOfView)).Within(1e-5f),
                "the zoom multiplier is measured from the new hip field of view");
        }

        [Test]
        public void AdsSensitivityScalesOnlyWhileAiming()
        {
            Initialize();
            _ads.ApplyControlSettings(ControlSettings.Default.With(ControlValue.AdsSensitivity, 50));
            Assert.That(_look.SensitivityMultiplier, Is.EqualTo(1f), "the hip sensitivity is untouched");

            _ads.Tick(true, true, 1f);

            var zoom = AdsZoom.SensitivityMultiplier(_hip, AdsZoom.DefaultFieldOfView);
            Assert.That(_look.SensitivityMultiplier, Is.EqualTo(zoom * 0.5f).Within(1e-5f));

            _ads.Tick(true, false, 0f);
            _ads.ApplyControlSettings(ControlSettings.Default.With(ControlValue.AdsSensitivity, 200));
            Assert.That(_look.SensitivityMultiplier, Is.EqualTo(zoom * 2f).Within(1e-5f), "a change applies while aiming");

            _ads.ResetView();
            Assert.That(_look.SensitivityMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void ApplyingSettingsBeforeInitializeOnlyKeepsThem()
        {
            var settings = ControlSettings.Default.With(ControlValue.FieldOfView, 80);

            _ads.ApplyControlSettings(settings);

            Assert.That(_ads.ControlSettings, Is.SameAs(settings));
            Assert.That(_camera.fieldOfView, Is.EqualTo(_hip));
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
            Assert.That(
                _ads.CycleMagnificationAction, Is.SameAs(InputSystem.actions.FindAction(AimDownSights.CycleMagnificationActionPath)));
            Assert.That(_ads.HoldBreathAction, Is.SameAs(InputSystem.actions.FindAction(AimDownSights.HoldBreathActionPath)));
            Assert.That(_ads.HipFieldOfView, Is.EqualTo(_hip));
        }

        [Test]
        public void CyclingMovesSmoothlyToTheNextZoomLevelAndWrapsToTheFirst()
        {
            Initialize();
            _ads.WeaponSource = () => Scoped(_rifleId);
            _ads.Tick(true, true, 1f);
            Assert.That(_camera.fieldOfView, Is.EqualTo(30f).Within(1e-4f));

            LogAssert.Expect(LogType.Log, "[View] zoom=2/3 fov=15");
            Assert.That(_ads.CycleZoomLevel(), Is.True);
            Assert.That(_ads.ZoomLevelIndex, Is.EqualTo(1));
            _ads.Tick(true, false, _ads.TransitionSeconds / 2f);
            Assert.That(_camera.fieldOfView, Is.LessThan(30f).And.GreaterThan(15f), "the step is not a cut");
            _ads.Tick(true, false, _ads.TransitionSeconds);
            Assert.That(_camera.fieldOfView, Is.EqualTo(15f).Within(1e-4f));

            _ads.CycleZoomLevel();
            _ads.Tick(true, false, _ads.TransitionSeconds);
            Assert.That(_camera.fieldOfView, Is.EqualTo(7.5f).Within(1e-4f));

            _ads.CycleZoomLevel();
            _ads.Tick(true, false, _ads.TransitionSeconds);
            Assert.That(_ads.ZoomLevelIndex, Is.EqualTo(0), "after the last level comes the first");
            Assert.That(_camera.fieldOfView, Is.EqualTo(30f).Within(1e-4f));
        }

        [Test]
        public void AHigherZoomLevelLowersTheSensitivityFurther()
        {
            Initialize();
            _ads.WeaponSource = () => Scoped(_rifleId);
            _ads.Tick(true, true, 1f);
            var first = _look.SensitivityMultiplier;

            _ads.CycleZoomLevel();
            _ads.Tick(true, false, 1f);

            Assert.That(_look.SensitivityMultiplier, Is.LessThan(first));
            Assert.That(_look.SensitivityMultiplier, Is.EqualTo(AdsZoom.SensitivityMultiplier(_hip, 15f)).Within(1e-5f));
        }

        [Test]
        public void StoppingAimingZoomsOutWithoutAJumpAndStartsAgainAtTheFirstLevel()
        {
            Initialize();
            _ads.WeaponSource = () => Scoped(_rifleId);
            _ads.Tick(true, true, 1f);
            _ads.CycleZoomLevel();
            _ads.Tick(true, false, 1f);
            Assert.That(_camera.fieldOfView, Is.EqualTo(15f).Within(1e-4f));

            _ads.Tick(false, false, 0f);
            Assert.That(_ads.ZoomLevelIndex, Is.EqualTo(0));
            Assert.That(_camera.fieldOfView, Is.EqualTo(15f).Within(1e-4f), "letting go does not snap the view");
            _ads.Tick(false, false, 1f);
            Assert.That(_camera.fieldOfView, Is.EqualTo(_hip).Within(1e-4f));

            _ads.Tick(true, true, 1f);
            Assert.That(_camera.fieldOfView, Is.EqualTo(30f).Within(1e-4f));
        }

        [Test]
        public void ResettingTheViewGoesBackToTheFirstLevel()
        {
            Initialize();
            _ads.WeaponSource = () => Scoped(_rifleId);
            _ads.Tick(true, true, 1f);
            _ads.CycleZoomLevel();

            _ads.ResetView();

            Assert.That(_ads.ZoomLevelIndex, Is.EqualTo(0));
            Assert.That(_camera.fieldOfView, Is.EqualTo(_hip));
        }

        [Test]
        public void CyclingDoesNothingWhenNotAimingOrWithASingleLevel()
        {
            Initialize();
            _ads.WeaponSource = () => Scoped(_rifleId);
            Assert.That(_ads.CycleZoomLevel(), Is.False, "not aiming");
            Assert.That(_ads.ZoomLevelIndex, Is.EqualTo(0));

            _ads.WeaponSource = () => Rifle(40f);
            _ads.Tick(true, true, 1f);
            Assert.That(_ads.ZoomLevelCount, Is.EqualTo(1));
            Assert.That(_ads.CycleZoomLevel(), Is.False, "a single level");
            _ads.Tick(true, false, 1f);
            Assert.That(_camera.fieldOfView, Is.EqualTo(40f).Within(1e-4f));
            Assert.That(_ads.Magnification, Is.Null);

            _ads.WeaponSource = null;
            Assert.That(_ads.CycleZoomLevel(), Is.False, "no weapon");
        }

        [Test]
        public void ChangingWeaponsStartsTheNewOneAtItsFirstLevel()
        {
            Initialize();
            var current = Scoped(_rifleId);
            _ads.WeaponSource = () => current;
            _ads.Tick(true, true, 1f);
            _ads.CycleZoomLevel();
            _ads.Tick(true, false, 1f);

            current = Scoped("00000000-0000-4000-8000-000000000002");
            _ads.Tick(true, false, 0f);

            Assert.That(_ads.ZoomLevelIndex, Is.EqualTo(0));
            Assert.That(_camera.fieldOfView, Is.EqualTo(30f).Within(1e-4f));
        }

        [Test]
        public void TheMagnificationIsShownOnlyWhileAimingAWeaponWithSeveralLevels()
        {
            Initialize();
            _ads.WeaponSource = () => Scoped(_rifleId);
            Assert.That(_ads.Magnification, Is.Null, "not aiming");

            _ads.Tick(true, true, 0f);
            var first = _ads.Magnification;
            Assert.That(first, Is.EqualTo(1f / AdsZoom.SensitivityMultiplier(_hip, 30f)).Within(1e-5f));

            _ads.CycleZoomLevel();
            Assert.That(_ads.Magnification, Is.EqualTo(1f / AdsZoom.SensitivityMultiplier(_hip, 15f)).Within(1e-5f));
            Assert.That(_ads.Magnification, Is.GreaterThan(first));

            _ads.Tick(false, false, 0f);
            Assert.That(_ads.Magnification, Is.Null);
        }

        [Test]
        public void InitializeTakesTheCycleMagnificationAction()
        {
            var cycle = new InputAction("CycleMagnification", InputActionType.Button);
            try
            {
                _ads.Initialize(_camera, _look, _aim, cycle);
                Assert.That(_ads.CycleMagnificationAction, Is.SameAs(cycle));
            }
            finally
            {
                cycle.Dispose();
            }
        }

        [TestCase(0, 3, 30f, "[View] zoom=1/3 fov=30")]
        [TestCase(1, 3, 16.43f, "[View] zoom=2/3 fov=16.43")]
        public void ZoomLogNamesTheLevelFromOne(int index, int count, float fieldOfView, string expected)
        {
            Assert.That(AimDownSights.FormatZoomLog(index, count, fieldOfView), Is.EqualTo(expected));
        }

        [Test]
        public void AimingAWeaponWithSwayTurnsTheCameraAndTheShotWithIt()
        {
            InitializeSwaying();

            _ads.Tick(false, false, 0.5f);
            Assert.That(_camera.transform.localRotation, Is.EqualTo(Quaternion.identity), "no sway from the hip");

            _ads.Tick(true, true, 0.5f);

            Assert.That(_ads.SwayYawDegrees, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(_ads.SwayPitchUpDegrees, Is.EqualTo(0f).Within(1e-4f));
            AssertCameraFollowsTheSway();
            Assert.That(Vector3.Angle(_camera.transform.forward, _camera.transform.parent.forward), Is.EqualTo(1f).Within(1e-2f));
            Assert.That(_look.Yaw, Is.Zero, "the sway is not written into the look angles");
        }

        [Test]
        public void HoldingBreathSteadiesTheScopeAndRunningOutWidensIt()
        {
            InitializeSwaying();
            _ads.Tick(false, false, 0.5f);
            _ads.Tick(true, true, 0.5f);

            _ads.HoldBreathInput = true;
            LogAssert.Expect(LogType.Log, "[View] breath=hold remaining=1.5");
            _ads.Tick(true, false, 0.5f);

            Assert.That(_ads.IsHoldingBreath, Is.True);
            AssertSway(1.5f, 0.1f);

            LogAssert.Expect(LogType.Log, "[View] breath=exhausted remaining=1");
            _ads.Tick(true, false, 2f);

            Assert.That(_ads.IsHoldingBreath, Is.False);
            Assert.That(_ads.Breath.IsExhausted, Is.True);
            AssertSway(3.5f, 1.5f);

            _ads.HoldBreathInput = false;
            LogAssert.Expect(LogType.Log, "[View] breath=ready remaining=2");
            _ads.Tick(true, false, 0.5f);
            Assert.That(_ads.Breath.IsExhausted, Is.False);
            AssertSway(0f, 1f);
        }

        [Test]
        public void HoldingBreathDoesNothingFromTheHip()
        {
            InitializeSwaying();
            _ads.HoldBreathInput = true;

            _ads.Tick(false, false, 0.5f);

            Assert.That(_ads.IsHoldingBreath, Is.False);
            Assert.That(_ads.Breath.RemainingSeconds, Is.EqualTo(2f));
            Assert.That(_camera.transform.localRotation, Is.EqualTo(Quaternion.identity));
        }

        [Test]
        public void HoldingBreathDoesNothingWhileTheWeaponBlocksAiming()
        {
            InitializeSwaying();
            var blocked = false;
            _ads.AdsBlocker = () => blocked;
            _ads.Tick(false, false, 0.5f);
            _ads.Tick(true, true, 0.5f);

            blocked = true;
            _ads.HoldBreathInput = true;
            _ads.Tick(true, false, 1f);

            Assert.That(_ads.IsHoldingBreath, Is.False);
            Assert.That(_ads.Breath.RemainingSeconds, Is.EqualTo(2f), "the breath is kept for when aiming comes back");
        }

        [Test]
        public void StoppingAimingSettlesTheCameraAndResetViewStraightensItAtOnce()
        {
            InitializeSwaying();
            _ads.Tick(false, false, 0.5f);
            _ads.Tick(true, true, 0.5f);

            _ads.Tick(false, false, 1f);
            Assert.That(_ads.Progress, Is.Zero);
            Assert.That(_camera.transform.localRotation, Is.EqualTo(Quaternion.identity));

            _ads.Tick(true, true, 1.5f);
            Assert.That(_camera.transform.localRotation, Is.Not.EqualTo(Quaternion.identity));
            _ads.HoldBreathInput = true;
            _ads.Tick(true, false, 1f);

            _ads.ResetView();

            Assert.That(_camera.transform.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(_ads.SwayYawDegrees, Is.Zero);
            Assert.That(_ads.Breath.RemainingSeconds, Is.EqualTo(2f), "the breath is refilled");
        }

        [Test]
        public void AWeaponWithoutSwayLeavesTheCameraAlone()
        {
            Initialize();
            _ads.WeaponSource = () => Rifle(40f);
            _ads.HoldBreathInput = true;

            _ads.Tick(true, true, 1f);

            Assert.That(_camera.transform.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(_ads.IsHoldingBreath, Is.False);
        }

        [Test]
        public void SwayStartsFromTheCamerasOwnRestingRotation()
        {
            var rest = Quaternion.Euler(0f, 0f, 5f);
            _camera.transform.localRotation = rest;
            InitializeSwaying();
            _ads.Tick(false, false, 0.5f);
            _ads.Tick(true, true, 0.5f);

            Assert.That(
                Quaternion.Angle(_camera.transform.localRotation, rest * Quaternion.Euler(0f, 1f, 0f)), Is.LessThan(1e-2f));

            _ads.ResetView();
            Assert.That(Quaternion.Angle(_camera.transform.localRotation, rest), Is.LessThan(1e-3f));
        }

        [Test]
        public void InitializeTakesTheHoldBreathAction()
        {
            var hold = new InputAction("HoldBreath", InputActionType.Button);
            try
            {
                _ads.Initialize(_camera, _look, _aim, null, hold);
                Assert.That(_ads.HoldBreathAction, Is.SameAs(hold));
            }
            finally
            {
                hold.Dispose();
            }
        }

        [Test]
        public void SwaySettingsRejectUnusableValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _ads.ExhaustedSwayMultiplier = 0.5f);
            Assert.Throws<ArgumentOutOfRangeException>(() => _ads.SwayBlendSeconds = -1f);
            Assert.Throws<ArgumentOutOfRangeException>(() => _ads.SwayCycleSeconds = 0f);
            Assert.That(_ads.ExhaustedSwayMultiplier, Is.EqualTo(BreathHold.DefaultExhaustedSwayMultiplier));
        }

        [TestCase(true, false, 1.5f, "[View] breath=hold remaining=1.5")]
        [TestCase(false, true, 0f, "[View] breath=exhausted remaining=0")]
        [TestCase(false, false, 4f, "[View] breath=ready remaining=4")]
        public void BreathLogNamesTheState(bool holding, bool exhausted, float remaining, string expected)
        {
            Assert.That(AimDownSights.FormatBreathLog(holding, exhausted, remaining), Is.EqualTo(expected));
        }

        private void InitializeSwaying()
        {
            Initialize();
            _ads.TransitionSeconds = 0.25f;
            _ads.SwayCycleSeconds = 4f;
            _ads.SwayBlendSeconds = 0f;
            _ads.WeaponSource = () => new WeaponSpec(
                _rifleId,
                "Swaying",
                WeaponCategory.MarksmanRifle,
                aim: new AimSpec(zoomLevels: new[] { 30f }, breathHold: new BreathHoldSpec(2f, 1f, 0.1f), swayDegrees: 1f));
        }

        private void AssertSway(float seconds, float amplitude)
        {
            BreathHold.SwayOffset(seconds, amplitude, 4f, out var pitchUp, out var yaw);
            Assert.That(_ads.SwayPitchUpDegrees, Is.EqualTo(pitchUp).Within(1e-4f));
            Assert.That(_ads.SwayYawDegrees, Is.EqualTo(yaw).Within(1e-4f));
            AssertCameraFollowsTheSway();
        }

        private void AssertCameraFollowsTheSway()
        {
            var expected = Quaternion.Euler(-_ads.SwayPitchUpDegrees, _ads.SwayYawDegrees, 0f);
            Assert.That(Quaternion.Angle(_camera.transform.localRotation, expected), Is.LessThan(1e-2f));
        }

        [Test]
        public void TheSightViewFollowsTheWeaponsSightAndTheProgress()
        {
            Initialize();
            var sight = new SightSpec("mildot", scopeOverlay: true, eyeViewInThirdPerson: true);
            _ads.WeaponSource = () => new WeaponSpec(_rifleId, "Scoped", WeaponCategory.MarksmanRifle, aim: new AimSpec(sight: sight));
            Assert.That(_ads.Sight, Is.SameAs(sight));

            _ads.Tick(true, true, 0.05f);
            Assert.That(_ads.SightView.ThroughSight, Is.True, "first person with no ViewRig");
            Assert.That(_ads.SightView.ScopeOverlay, Is.False, "a third of the way in");

            _ads.Tick(true, false, 1f);
            Assert.That(_ads.SightView.ScopeOverlay, Is.True);
            Assert.That(_ads.SightView.HidesViewmodel, Is.True);
            Assert.That(_ads.SightView.ReticleId, Is.EqualTo("mildot"));
            Assert.That(_ads.SightView.EyeBlend, Is.Zero);

            _ads.ViewModeSource = () => ViewMode.ThirdPerson;
            _ads.Tick(true, false, 0f);
            Assert.That(_ads.SightView.EyeBlend, Is.EqualTo(1f), "the scope takes the camera to the eye in third person");
            Assert.That(_ads.SightView.ScopeOverlay, Is.True);

            _ads.ResetView();
            Assert.That(_ads.SightView.ScopeOverlay, Is.False);
            Assert.That(_ads.SightView.EyeBlend, Is.Zero);
        }

        [Test]
        public void WithoutAWeaponTheSightIsTheDefault()
        {
            Initialize();
            _ads.Tick(true, true, 1f);

            Assert.That(_ads.Sight, Is.SameAs(SightSpec.Default));
            Assert.That(_ads.SightView.ShowsReticle, Is.False);
            Assert.That(_ads.SightView.ScopeOverlay, Is.False);
        }

        [Test]
        public void TheAdsEyeViewRuleDecidesWhetherAnIronSightTakesTheEyeInThirdPerson()
        {
            Initialize();
            _ads.WeaponSource = () => new WeaponSpec(_rifleId, "Iron", WeaponCategory.MarksmanRifle, aim: new AimSpec());
            _ads.ViewModeSource = () => ViewMode.ThirdPerson;

            _ads.Tick(true, true, 1f);
            Assert.That(_ads.SightView.EyeBlend, Is.Zero, "per weapon, a sight without eyeViewInThirdPerson stays over the shoulder");
            Assert.That(_ads.SightView.ThroughSight, Is.False);

            _ads.AdsEyeViewSource = () => AdsEyeView.Always;
            LogAssert.Expect(LogType.Log, "[View] sight aimedIn=true overlay=false reticle=- eye=true vision=None");
            _ads.Tick(true, false, 0f);
            Assert.That(_ads.SightView.EyeBlend, Is.EqualTo(1f));
            Assert.That(_ads.SightView.AimedIn, Is.True);
            Assert.That(_ads.SightView.HidesBody, Is.True);

            _ads.WeaponSource = () => new WeaponSpec(_rifleId, "Scoped", WeaponCategory.MarksmanRifle, aim: new AimSpec(sight: new SightSpec("mildot", scopeOverlay: true, eyeViewInThirdPerson: true)));
            _ads.AdsEyeViewSource = () => AdsEyeView.Never;
            LogAssert.Expect(LogType.Log, "[View] sight aimedIn=false overlay=false reticle=- eye=false vision=None");
            _ads.Tick(true, false, 0f);
            Assert.That(_ads.SightView.EyeBlend, Is.Zero, "Never keeps even a scope over the shoulder");
            Assert.That(_ads.SightView.ScopeOverlay, Is.False);
        }

        [Test]
        public void WithoutASourceTheAdsEyeViewComesFromTheViewRig()
        {
            Initialize();
            var rig = _ads.gameObject.AddComponent<ViewRig>();
            rig.Mode = ViewMode.ThirdPerson;
            rig.AdsEyeView = AdsEyeView.Always;
            _ads.WeaponSource = () => new WeaponSpec(_rifleId, "Iron", WeaponCategory.MarksmanRifle, aim: new AimSpec());

            _ads.Tick(true, true, 1f);

            Assert.That(_ads.SightView.EyeBlend, Is.EqualTo(1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => rig.AdsEyeView = (AdsEyeView)99);
            Assert.That(rig.AdsEyeView, Is.EqualTo(AdsEyeView.Always), "a rejected value leaves the rule as it was");
        }

        [Test]
        public void SightLogNamesTheShownReticle()
        {
            var scope = SightView.Of(new SightSpec("mildot", scopeOverlay: true, eyeViewInThirdPerson: true), ViewMode.ThirdPerson, 1f);
            Assert.That(SightLog.Format(scope), Is.EqualTo("[View] sight aimedIn=true overlay=true reticle=mildot eye=true vision=None"));

            var plainScope = SightView.Of(new SightSpec(scopeOverlay: true), ViewMode.FirstPerson, 1f);
            Assert.That(SightLog.Format(plainScope), Is.EqualTo("[View] sight aimedIn=true overlay=true reticle=default eye=false vision=None"));

            var thermal = SightView.Of(new SightSpec(scopeOverlay: true, visionMode: VisionMode.Thermal), ViewMode.FirstPerson, 1f);
            Assert.That(SightLog.Format(thermal), Is.EqualTo("[View] sight aimedIn=true overlay=true reticle=default eye=false vision=Thermal"));

            Assert.That(SightLog.Format(SightView.None), Is.EqualTo("[View] sight aimedIn=false overlay=false reticle=- eye=false vision=None"));
        }

        [Test]
        public void TheSightsVisionModeHoldsOnlyWhileAimedInAndIsLogged()
        {
            Initialize();
            var sight = new SightSpec("nv", visionMode: VisionMode.Night);
            _ads.WeaponSource = () => new WeaponSpec(_rifleId, "Night", WeaponCategory.MarksmanRifle, aim: new AimSpec(sight: sight));

            _ads.Tick(true, true, 0.05f);
            Assert.That(_ads.SightView.VisionMode, Is.EqualTo(VisionMode.None), "not while still raising the gun");

            LogAssert.Expect(LogType.Log, "[View] sight aimedIn=true overlay=false reticle=nv eye=false vision=Night");
            _ads.Tick(true, false, 1f);
            Assert.That(_ads.SightView.VisionMode, Is.EqualTo(VisionMode.Night));

            _ads.ViewModeSource = () => ViewMode.ThirdPerson;
            LogAssert.Expect(LogType.Log, "[View] sight aimedIn=false overlay=false reticle=- eye=false vision=None");
            _ads.Tick(true, false, 0f);
            Assert.That(_ads.SightView.VisionMode, Is.EqualTo(VisionMode.None), "over the shoulder the sight is not looked through");
        }

        private static WeaponSpec Scoped(string id)
        {
            return new WeaponSpec(id, "Scoped", WeaponCategory.MarksmanRifle, aim: new AimSpec(zoomLevels: new[] { 30f, 15f, 7.5f }));
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
                adsFieldOfView: adsFieldOfView);
        }
    }
}
