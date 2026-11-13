using System;
using System.Text.RegularExpressions;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
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

        private static Fps.Core.WeaponSpec RecoilSpec(float pitch, float yaw, float recovery)
        {
            return new Fps.Core.WeaponSpec(
                "3f2b8c1e-0a4d-4c6e-9b1a-7d2e5f8c0b13", "Test", Fps.Core.WeaponCategory.AssaultRifle, Fps.Core.FireMode.Auto,
                30, 90, 0.1f, 2f, 10, 100f, Fps.Core.DamageFalloff.None,
                recoilPitchDegrees: pitch, recoilYawDegrees: yaw, recoilRecoveryPerSecond: recovery);
        }

        [Test]
        public void RecoilRaisesTheViewWithoutChangingTheLookAngles()
        {
            _look.Initialize(_pivot, _action);
            _look.ApplyLookDelta(new Vector2(30f, -10f));
            var recoil = new Fps.Core.RecoilState(0f);
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
            var recoil = new Fps.Core.RecoilState(0f);
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
            var recoil = new Fps.Core.RecoilState(0f);
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
            var recoil = new Fps.Core.RecoilState(0f);
            _look.Recoil = recoil;

            recoil.Kick(RecoilSpec(20f, 0f, 0f), 1, 0);
            _look.ApplyRecoil();

            Assert.That(Mathf.DeltaAngle(0f, _pivot.localEulerAngles.x), Is.EqualTo(-Fps.Core.LookAngles.DefaultMaxPitch).Within(1e-3f));
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
            var recoil = new Fps.Core.RecoilState(0f);
            _look.Recoil = recoil;
            recoil.Kick(RecoilSpec(4f, 0f, 0f), 1, 0);
            _look.ApplyRecoil();

            // 同じ軸で初期化し直すと transform から向きを読み直す。反動の分を向きに取り込まない。
            _look.Initialize(_pivot, _action);

            Assert.That(_look.Pitch, Is.EqualTo(0f).Within(1e-3f));
        }
    }
}
