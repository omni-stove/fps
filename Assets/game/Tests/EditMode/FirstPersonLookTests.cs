using System;
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
    public sealed class FirstPersonLookTests
    {
        private GameObject _body;
        private Transform _pivot;
        private FirstPersonLook _look;
        private InputAction _action;

        [SetUp]
        public void SetUp()
        {
            _body = new GameObject("Body");
            _pivot = new GameObject("Pivot").transform;
            _pivot.SetParent(_body.transform, false);
            _look = _body.AddComponent<FirstPersonLook>();
            _action = new InputAction("Look", InputActionType.Value, expectedControlType: "Vector2");
        }

        [TearDown]
        public void TearDown()
        {
            _action.Dispose();
            Object.DestroyImmediate(_body);
        }

        [Test]
        public void SensitivityHasCodeDefaults()
        {
            Assert.That(_look.MouseSensitivity, Is.EqualTo(FirstPersonLook.DefaultMouseSensitivity));
            Assert.That(_look.StickSensitivity, Is.EqualTo(FirstPersonLook.DefaultStickSensitivity));
        }

        [Test]
        public void SensitivityIsNeverNegative()
        {
            _look.MouseSensitivity = -1f;

            Assert.That(_look.MouseSensitivity, Is.Zero);
        }

        [Test]
        public void SensitivityMultiplierScalesBothSensitivities()
        {
            Assert.That(_look.SensitivityMultiplier, Is.EqualTo(1f));
            Assert.That(_look.EffectiveMouseSensitivity, Is.EqualTo(FirstPersonLook.DefaultMouseSensitivity));

            _look.SensitivityMultiplier = 0.5f;

            Assert.That(_look.EffectiveMouseSensitivity, Is.EqualTo(FirstPersonLook.DefaultMouseSensitivity * 0.5f));
            Assert.That(_look.EffectiveStickSensitivity, Is.EqualTo(FirstPersonLook.DefaultStickSensitivity * 0.5f));
            Assert.That(_look.MouseSensitivity, Is.EqualTo(FirstPersonLook.DefaultMouseSensitivity));
        }

        [Test]
        public void SettingsScaleEachDeviceSeparatelyAndStackWithTheMultiplier()
        {
            Assert.That(_look.ControlSettings, Is.EqualTo(ControlSettings.Default));

            _look.ControlSettings = ControlSettings.Default
                .With(ControlValue.MouseSensitivity, 200)
                .With(ControlValue.GamepadSensitivity, 50);
            _look.SensitivityMultiplier = 0.5f;

            Assert.That(_look.EffectiveMouseSensitivity, Is.EqualTo(FirstPersonLook.DefaultMouseSensitivity * 2f * 0.5f).Within(1e-6f));
            Assert.That(_look.EffectiveStickSensitivity, Is.EqualTo(FirstPersonLook.DefaultStickSensitivity * 0.5f * 0.5f).Within(1e-4f));
            Assert.That(_look.MouseSensitivity, Is.EqualTo(FirstPersonLook.DefaultMouseSensitivity), "the base value is untouched");
            Assert.Throws<ArgumentNullException>(() => _look.ControlSettings = null);
        }

        [TestCase(-0.1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void SensitivityMultiplierRejectsInvalidValues(float multiplier)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _look.SensitivityMultiplier = multiplier);
            Assert.That(_look.SensitivityMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void InitializeRejectsMissingReferences()
        {
            Assert.Throws<ArgumentNullException>(() => _look.Initialize(null, _action));
            Assert.Throws<ArgumentNullException>(() => _look.Initialize(_pivot, null));
            Assert.That(_look.IsInitialized, Is.False);
        }

        [Test]
        public void InitializeRejectsPivotOutsideBody()
        {
            var stranger = new GameObject("Stranger");
            try
            {
                Assert.Throws<ArgumentException>(() => _look.Initialize(stranger.transform, _action));
                Assert.Throws<ArgumentException>(() => _look.Initialize(_body.transform, _action));
            }
            finally
            {
                Object.DestroyImmediate(stranger);
            }
        }

        [Test]
        public void ApplyBeforeInitializeThrows()
        {
            Assert.Throws<InvalidOperationException>(() => _look.ApplyLookDelta(Vector2.one));
        }

        [Test]
        public void HorizontalInputTurnsOnlyTheBody()
        {
            _look.Initialize(_pivot, _action);

            _look.ApplyLookDelta(new Vector2(30f, 0f));

            Assert.That(_body.transform.localEulerAngles.y, Is.EqualTo(30f).Within(1e-3f));
            Assert.That(Quaternion.Angle(_pivot.localRotation, Quaternion.identity), Is.LessThan(1e-3f));
        }

        [Test]
        public void VerticalInputTiltsOnlyTheCamera()
        {
            _look.Initialize(_pivot, _action);

            _look.ApplyLookDelta(new Vector2(0f, 20f));

            // 上を向くと X 軸回転は負 (localEulerAngles では 340°)。
            Assert.That(_look.Pitch, Is.EqualTo(-20f).Within(1e-3f));
            Assert.That(_pivot.localEulerAngles.x, Is.EqualTo(340f).Within(1e-3f));
            Assert.That(Quaternion.Angle(_body.transform.localRotation, Quaternion.identity), Is.LessThan(1e-3f));
        }

        [Test]
        public void PitchIsClampedTo85Degrees()
        {
            _look.Initialize(_pivot, _action);

            _look.ApplyLookDelta(new Vector2(0f, 500f));
            Assert.That(_look.Pitch, Is.EqualTo(-85f).Within(1e-3f));

            _look.ApplyLookDelta(new Vector2(0f, -500f));
            Assert.That(_look.Pitch, Is.EqualTo(85f).Within(1e-3f));
            Assert.That(_pivot.localEulerAngles.x, Is.EqualTo(85f).Within(1e-3f));
        }

        [Test]
        public void InitializeKeepsTheCurrentOrientation()
        {
            _body.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            _pivot.localRotation = Quaternion.Euler(-10f, 0f, 0f);

            _look.Initialize(_pivot, _action);

            Assert.That(_look.Yaw, Is.EqualTo(90f).Within(1e-3f));
            Assert.That(_look.Pitch, Is.EqualTo(-10f).Within(1e-3f));
        }

        [Test]
        public void FindChildCameraIgnoresCameraOnTheBody()
        {
            _body.AddComponent<Camera>();

            Assert.That(FirstPersonLook.FindChildCamera(_body.transform), Is.Null);

            var child = _pivot.gameObject.AddComponent<Camera>();
            Assert.That(FirstPersonLook.FindChildCamera(_body.transform), Is.SameAs(child));
        }

        [Test]
        public void ResolvingWithoutCameraLogsAnError()
        {
            LogAssert.Expect(LogType.Error, new Regex(@"\[FirstPersonLook\] .*Camera"));

            Assert.That(_look.TryInitializeFromHierarchy(), Is.False);
            Assert.That(_look.IsInitialized, Is.False);
        }

        [Test]
        public void ResolvesCameraAndProjectWideLookAction()
        {
            var camera = _pivot.gameObject.AddComponent<Camera>();

            Assert.That(InputSystem.actions, Is.Not.Null, "Project-wide Input Actions must be assigned");
            Assert.That(_look.TryInitializeFromHierarchy(), Is.True);
            Assert.That(_look.PitchPivot, Is.SameAs(camera.transform));
            Assert.That(_look.LookAction, Is.SameAs(InputSystem.actions.FindAction(FirstPersonLook.LookActionPath)));
        }

        private static Shooter.Core.WeaponSpec RecoilSpec(float pitch, float yaw, float recovery)
        {
            return new Shooter.Core.WeaponSpec(
                "3f2b8c1e-0a4d-4c6e-9b1a-7d2e5f8c0b13", "Test", Shooter.Core.WeaponCategory.AssaultRifle, Shooter.Core.FireMode.Auto,
                30, 90, 0.1f, 2f, 10, 100f, Shooter.Core.DamageFalloff.None,
                recoilPitchDegrees: pitch, recoilYawDegrees: yaw, recoilRecoveryPerSecond: recovery, recoilRecoveryDelaySeconds: 0f);
        }

        [Test]
        public void RecoilRaisesTheViewWithoutChangingTheLookAngles()
        {
            _look.Initialize(_pivot, _action);
            _look.ApplyLookDelta(new Vector2(30f, -10f));
            var recoil = new Shooter.Core.RecoilState();
            _look.Recoil = recoil;

            recoil.Kick(RecoilSpec(4f, 0f, 0f), 1, 0);
            _look.ApplyRecoil();

            Assert.That(_look.Pitch, Is.EqualTo(10f).Within(1e-4f), "the look angles keep the player's own aim");
            Assert.That(Mathf.DeltaAngle(0f, _pivot.localEulerAngles.x), Is.EqualTo(6f).Within(1e-3f));
            Assert.That(_look.AppliedRecoilPitchUp, Is.EqualTo(4f).Within(1e-4f));
            Assert.That(_body.transform.localEulerAngles.y, Is.EqualTo(30f).Within(1e-3f), "the body yaw is not kicked");
        }

        [Test]
        public void RecoilYawTurnsOnlyThePitchPivot()
        {
            _look.Initialize(_pivot, _action);
            var recoil = new Shooter.Core.RecoilState();
            _look.Recoil = recoil;
            var spec = RecoilSpec(0f, 3f, 0f);

            recoil.Kick(spec, 1, 0);
            _look.ApplyRecoil();

            Assert.That(Mathf.DeltaAngle(0f, _pivot.localEulerAngles.y), Is.EqualTo(recoil.YawDegrees).Within(1e-3f));
            Assert.That(_look.AppliedRecoilYaw, Is.EqualTo(recoil.YawDegrees));
            Assert.That(_body.transform.localEulerAngles.y, Is.EqualTo(0f).Within(1e-3f));
        }

        [Test]
        public void ViewReturnsWhenTheRecoilRecovers()
        {
            _look.Initialize(_pivot, _action);
            _look.ApplyLookDelta(new Vector2(0f, 5f));
            var recoil = new Shooter.Core.RecoilState();
            _look.Recoil = recoil;
            recoil.Kick(RecoilSpec(4f, 2f, 100f), 1, 0);
            _look.ApplyRecoil();

            recoil.Tick(1f);
            _look.ApplyRecoil();

            Assert.That(Mathf.DeltaAngle(0f, _pivot.localEulerAngles.x), Is.EqualTo(-5f).Within(1e-3f));
            Assert.That(Mathf.DeltaAngle(0f, _pivot.localEulerAngles.y), Is.EqualTo(0f).Within(1e-3f));
            Assert.That(_look.AppliedRecoilPitchUp, Is.EqualTo(0f));
        }

        [Test]
        public void RecoilIsClampedAtTheMaximumPitch()
        {
            _look.Initialize(_pivot, _action);
            _look.ApplyLookDelta(new Vector2(0f, 80f));
            var recoil = new Shooter.Core.RecoilState();
            _look.Recoil = recoil;

            recoil.Kick(RecoilSpec(20f, 0f, 0f), 1, 0);
            _look.ApplyRecoil();

            Assert.That(Mathf.DeltaAngle(0f, _pivot.localEulerAngles.x), Is.EqualTo(-Shooter.Core.LookAngles.DefaultMaxPitch).Within(1e-3f));
            Assert.That(_look.AppliedRecoilPitchUp, Is.EqualTo(5f).Within(1e-3f));
        }

        [Test]
        public void WithoutRecoilTheRecoilStepLeavesThePivotAlone()
        {
            _look.Initialize(_pivot, _action);
            _pivot.localRotation = Quaternion.Euler(20f, 0f, 0f);

            _look.ApplyRecoil();

            Assert.That(Mathf.DeltaAngle(0f, _pivot.localEulerAngles.x), Is.EqualTo(20f).Within(1e-3f), "nothing to apply, nothing written");
        }

        [Test]
        public void ResyncingKeepsTheAimUnderTheRecoil()
        {
            _look.Initialize(_pivot, _action);
            var recoil = new Shooter.Core.RecoilState();
            _look.Recoil = recoil;
            recoil.Kick(RecoilSpec(4f, 0f, 0f), 1, 0);
            _look.ApplyRecoil();

            _look.Initialize(_pivot, _action);

            Assert.That(_look.Pitch, Is.EqualTo(0f).Within(1e-3f));
        }

        [Test]
        public void TheAssistScalesTheStickTurnAndAddsItsAngleOnce()
        {
            _look.Initialize(_pivot, _action);
            Assert.That(_look.AssistLookScale, Is.EqualTo(1f));
            Assert.That(_look.PendingAssistDegrees, Is.EqualTo(Vector2.zero));

            _look.AssistLookScale = 0.5f;
            _look.AddAssistDegrees(new Vector2(1f, 0.5f));
            _look.AddAssistDegrees(new Vector2(1f, 0.5f));
            Assert.That(_look.PendingAssistDegrees, Is.EqualTo(new Vector2(2f, 1f)), "angles handed over before the next turn add up");

            _look.Turn(new Vector2(10f, 0f), false);

            // 10 × 0.5 + 2 = 7 度右、1 度上。
            Assert.That(_look.Yaw, Is.EqualTo(7f).Within(1e-3f));
            Assert.That(_look.Pitch, Is.EqualTo(-1f).Within(1e-3f));
            Assert.That(_look.PendingAssistDegrees, Is.EqualTo(Vector2.zero));

            _look.Turn(Vector2.zero, false);
            Assert.That(_look.Yaw, Is.EqualTo(7f).Within(1e-3f), "the same angle is not turned twice");
            Assert.That(_look.Pitch, Is.EqualTo(-1f).Within(1e-3f));
        }

        [Test]
        public void TheAssistAngleTurnsTheViewWithoutStickInput()
        {
            _look.Initialize(_pivot, _action);
            _look.AddAssistDegrees(new Vector2(-1.5f, 0f));

            _look.Turn(Vector2.zero, false);

            Assert.That(_look.Yaw, Is.EqualTo(358.5f).Within(1e-3f));
        }

        [Test]
        public void TheAssistDoesNothingForTheMouse()
        {
            _look.Initialize(_pivot, _action);
            _look.AssistLookScale = 0.5f;
            _look.AddAssistDegrees(new Vector2(2f, 1f));

            _look.Turn(new Vector2(10f, 0f), true);

            Assert.That(_look.Yaw, Is.EqualTo(10f).Within(1e-3f));
            Assert.That(_look.Pitch, Is.EqualTo(0f).Within(1e-3f));
            Assert.That(_look.PendingAssistDegrees, Is.EqualTo(Vector2.zero), "the angle is dropped, not kept for the next stick turn");
        }

        [Test]
        public void TheTurnMultiplierAlsoSlowsTheAssistedTurn()
        {
            _look.Initialize(_pivot, _action);
            _look.TurnMultiplier = 0.5f;
            _look.AssistLookScale = 0.5f;
            _look.AddAssistDegrees(new Vector2(2f, 0f));

            _look.Turn(new Vector2(10f, 0f), false);

            // (10 × 0.5 + 2) × 0.5 = 3.5 度。
            Assert.That(_look.Yaw, Is.EqualTo(3.5f).Within(1e-3f));
        }

        [Test]
        public void TheAssistLeavesTheSpectatorAndPhotoModeReadingAlone()
        {
            _look.AssistLookScale = 0f;
            _look.AddAssistDegrees(new Vector2(5f, 5f));

            Assert.That(_look.ReadLookDegrees(1f), Is.EqualTo(Vector2.zero));
            Assert.That(_look.PendingAssistDegrees, Is.EqualTo(new Vector2(5f, 5f)), "reading the input does not use up the assist");
        }

        [Test]
        public void ClearingTheAssistRestoresTheScaleAndDropsTheAngle()
        {
            _look.AssistLookScale = 0.25f;
            _look.AddAssistDegrees(new Vector2(3f, 0f));

            _look.ClearAssist();

            Assert.That(_look.AssistLookScale, Is.EqualTo(1f));
            Assert.That(_look.PendingAssistDegrees, Is.EqualTo(Vector2.zero));
        }

        [TestCase(-0.1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void TheAssistScaleRejectsInvalidValues(float scale)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _look.AssistLookScale = scale);
            Assert.That(_look.AssistLookScale, Is.EqualTo(1f));
        }

        [Test]
        public void TheAssistAngleRejectsNonFiniteValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _look.AddAssistDegrees(new Vector2(float.NaN, 0f)));
            Assert.Throws<ArgumentOutOfRangeException>(() => _look.AddAssistDegrees(new Vector2(0f, float.PositiveInfinity)));
            Assert.That(_look.PendingAssistDegrees, Is.EqualTo(Vector2.zero));
        }
    }
}
