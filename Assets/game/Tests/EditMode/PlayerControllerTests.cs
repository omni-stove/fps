using System;
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
    /// <summary>
    /// 参照の解決と、しゃがみ (カプセルと目の寸法・頭上の検査)。
    /// Edit Mode でも Physics の重なり判定はコライダーに当たるので、障害物を置いたら SyncTransforms してから Tick する。
    /// </summary>
    public sealed class PlayerControllerTests
    {
        private const float _tolerance = 1e-4f;

        // GameBootstrap のプレイヤーと同じ寸法。足元が y = 0 に来るよう本体を y = 1 に置く。
        private const float _height = 2f;
        private const float _radius = 0.5f;
        private static readonly Vector3 _bodyPosition = new Vector3(0f, 1f, 0f);
        private static readonly Vector3 _eyeOffset = new Vector3(0f, 0.6f, 0f);

        private GameObject _body;
        private CharacterController _characterController;
        private FirstPersonLook _look;
        private PlayerController _player;
        private InputAction _move;
        private InputAction _sprint;
        private InputAction _crouch;
        private InputAction _jump;
        private MotorSpec _spec;

        [SetUp]
        public void SetUp()
        {
            _body = new GameObject("Body");
            _body.transform.position = _bodyPosition;
            _characterController = _body.AddComponent<CharacterController>();
            _characterController.height = _height;
            _characterController.radius = _radius;
            _characterController.center = Vector3.zero;
            _player = _body.AddComponent<PlayerController>();
            _move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            _sprint = new InputAction("Sprint", InputActionType.Button);
            _crouch = new InputAction("Crouch", InputActionType.Button);
            _jump = new InputAction("Jump", InputActionType.Button);
            _spec = new MotorSpec(5f, 8f, 1.2f, 20f, 0.1f, 2.5f);
        }

        [TearDown]
        public void TearDown()
        {
            _move.Dispose();
            _sprint.Dispose();
            _crouch.Dispose();
            _jump.Dispose();
            Object.DestroyImmediate(_body);
        }

        [Test]
        public void SettingsHaveCodeDefaults()
        {
            var spec = _player.CreateSpec();

            Assert.That(spec.WalkSpeed, Is.EqualTo(PlayerController.DefaultWalkSpeed));
            Assert.That(spec.SprintSpeed, Is.EqualTo(PlayerController.DefaultSprintSpeed));
            Assert.That(spec.CrouchSpeed, Is.EqualTo(PlayerController.DefaultCrouchSpeed));
            Assert.That(spec.JumpHeight, Is.EqualTo(PlayerController.DefaultJumpHeight));
            Assert.That(spec.Gravity, Is.EqualTo(PlayerController.DefaultGravity));
            Assert.That(spec.AirControl, Is.EqualTo(PlayerController.DefaultAirControl));
            Assert.That(_player.CrouchHeight, Is.EqualTo(PlayerController.DefaultCrouchHeight));
        }

        [Test]
        public void GroundedLogHasTagAndLowercaseValue()
        {
            Assert.That(PlayerController.FormatGroundedLog(true), Is.EqualTo("[Player] grounded=true"));
            Assert.That(PlayerController.FormatGroundedLog(false), Is.EqualTo("[Player] grounded=false"));
        }

        [Test]
        public void SprintIsHeldAndReleaseSprintNeedsAFreshPress()
        {
            Assert.That(_player.IsSprintActive, Is.False);
            Assert.That(_player.UpdateSprint(true, true), Is.True);
            Assert.That(_player.UpdateSprint(true, false), Is.True);

            _player.ReleaseSprint();

            Assert.That(_player.IsSprintActive, Is.False);
            Assert.That(_player.UpdateSprint(true, false), Is.False, "holding Sprint through a release does not resume it");
            Assert.That(_player.UpdateSprint(false, false), Is.False);
            Assert.That(_player.UpdateSprint(true, true), Is.True);
        }

        [Test]
        public void CrouchedLogHasTagAndLowercaseValue()
        {
            Assert.That(PlayerController.FormatCrouchedLog(true), Is.EqualTo("[Player] crouched=true"));
            Assert.That(PlayerController.FormatCrouchedLog(false), Is.EqualTo("[Player] crouched=false"));
        }

        [Test]
        public void TickBeforeInitializeThrows()
        {
            Assert.That(_player.IsInitialized, Is.False);
            Assert.Throws<InvalidOperationException>(() => _player.Tick(Vector2.zero, false, false, false, 0.016f));
        }

        [Test]
        public void InitializeRejectsMissingReferences()
        {
            _look = _body.AddComponent<FirstPersonLook>();

            Assert.Throws<ArgumentNullException>(() => _player.Initialize(null, _look, _move, _sprint, _crouch, _jump, _spec));
            Assert.Throws<ArgumentNullException>(
                () => _player.Initialize(_characterController, null, _move, _sprint, _crouch, _jump, _spec));
            Assert.Throws<ArgumentNullException>(
                () => _player.Initialize(_characterController, _look, null, _sprint, _crouch, _jump, _spec));
            Assert.Throws<ArgumentNullException>(
                () => _player.Initialize(_characterController, _look, _move, null, _crouch, _jump, _spec));
            Assert.Throws<ArgumentNullException>(
                () => _player.Initialize(_characterController, _look, _move, _sprint, null, _jump, _spec));
            Assert.Throws<ArgumentNullException>(
                () => _player.Initialize(_characterController, _look, _move, _sprint, _crouch, null, _spec));
            Assert.Throws<ArgumentNullException>(
                () => _player.Initialize(_characterController, _look, _move, _sprint, _crouch, _jump, null));
            Assert.That(_player.IsInitialized, Is.False);
        }

        [Test]
        public void InitializeRejectsComponentsOnOtherObjects()
        {
            _look = _body.AddComponent<FirstPersonLook>();
            var stranger = new GameObject("Stranger");
            try
            {
                var otherController = stranger.AddComponent<CharacterController>();
                var otherLook = stranger.AddComponent<FirstPersonLook>();

                Assert.Throws<ArgumentException>(
                    () => _player.Initialize(otherController, _look, _move, _sprint, _crouch, _jump, _spec));
                Assert.Throws<ArgumentException>(
                    () => _player.Initialize(_characterController, otherLook, _move, _sprint, _crouch, _jump, _spec));
                Assert.That(_player.IsInitialized, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(stranger);
            }
        }

        [Test]
        public void InitializeRejectsCrouchHeightBelowTheCapsuleDiameter()
        {
            _look = _body.AddComponent<FirstPersonLook>();
            _characterController.radius = 1f;

            Assert.Throws<ArgumentOutOfRangeException>(
                () => _player.Initialize(_characterController, _look, _move, _sprint, _crouch, _jump, _spec));
            Assert.That(_player.IsInitialized, Is.False);
        }

        [Test]
        public void InitializeStoresReferencesAndBuildsMotor()
        {
            _look = _body.AddComponent<FirstPersonLook>();

            _player.Initialize(_characterController, _look, _move, _sprint, _crouch, _jump, _spec);

            Assert.That(_player.IsInitialized, Is.True);
            Assert.That(_player.Controller, Is.SameAs(_characterController));
            Assert.That(_player.Look, Is.SameAs(_look));
            Assert.That(_player.MoveAction, Is.SameAs(_move));
            Assert.That(_player.SprintAction, Is.SameAs(_sprint));
            Assert.That(_player.CrouchAction, Is.SameAs(_crouch));
            Assert.That(_player.JumpAction, Is.SameAs(_jump));
            Assert.That(_player.Motor.Spec, Is.SameAs(_spec));
            Assert.That(_player.Stance.Standing.Height, Is.EqualTo(_height));
            Assert.That(_player.Stance.Crouched.Height, Is.EqualTo(PlayerController.DefaultCrouchHeight));
            Assert.That(_player.IsCrouched, Is.False);
        }

        [Test]
        public void ResolvingWithoutLookLogsAnError()
        {
            LogAssert.Expect(LogType.Error, new Regex(@"\[Player\] .*FirstPersonLook"));

            Assert.That(_player.TryInitializeFromHierarchy(), Is.False);
            Assert.That(_player.IsInitialized, Is.False);
        }

        [Test]
        public void ResolvingWithCrouchHeightThatDoesNotFitLogsAnError()
        {
            _look = _body.AddComponent<FirstPersonLook>();
            _characterController.radius = 1f;
            LogAssert.Expect(LogType.Error, new Regex(@"\[Player\] .*crouchHeight"));

            Assert.That(_player.TryInitializeFromHierarchy(), Is.False);
            Assert.That(_player.IsInitialized, Is.False);
        }

        [Test]
        public void ResolvesProjectWideMoveSprintCrouchJumpActions()
        {
            _look = _body.AddComponent<FirstPersonLook>();
            var actions = InputSystem.actions;

            Assert.That(actions, Is.Not.Null, "Project-wide Input Actions must be assigned");
            Assert.That(_player.TryInitializeFromHierarchy(), Is.True);
            Assert.That(_player.Controller, Is.SameAs(_characterController));
            Assert.That(_player.Look, Is.SameAs(_look));
            Assert.That(_player.MoveAction, Is.SameAs(actions.FindAction(PlayerController.MoveActionPath)));
            Assert.That(_player.SprintAction, Is.SameAs(actions.FindAction(PlayerController.SprintActionPath)));
            Assert.That(_player.CrouchAction, Is.SameAs(actions.FindAction(PlayerController.CrouchActionPath)));
            Assert.That(_player.JumpAction, Is.SameAs(actions.FindAction(PlayerController.JumpActionPath)));
        }

        [Test]
        public void CrouchShrinksCapsuleWithoutMovingFeet()
        {
            InitializeWithLook();
            var feetBefore = FeetY();

            LogAssert.Expect(LogType.Log, "[Player] crouched=true");
            _player.Tick(Vector2.zero, false, true, false, 0f);

            Assert.That(_player.IsCrouched, Is.True);
            Assert.That(_characterController.height, Is.EqualTo(PlayerController.DefaultCrouchHeight).Within(_tolerance));
            Assert.That(_characterController.center.y, Is.EqualTo(-0.4f).Within(_tolerance));
            Assert.That(FeetY(), Is.EqualTo(feetBefore).Within(_tolerance));
            Assert.That(_body.transform.position.y, Is.EqualTo(_bodyPosition.y).Within(_tolerance));
        }

        [Test]
        public void ReleasingCrouchWithClearHeadroomStandsUp()
        {
            InitializeWithLook();
            var feetBefore = FeetY();
            LogAssert.Expect(LogType.Log, "[Player] crouched=true");
            _player.Tick(Vector2.zero, false, true, false, 0f);

            LogAssert.Expect(LogType.Log, "[Player] crouched=false");
            Physics.SyncTransforms();
            _player.Tick(Vector2.zero, false, false, false, 0f);

            Assert.That(_player.IsCrouched, Is.False);
            Assert.That(_characterController.height, Is.EqualTo(_height).Within(_tolerance));
            Assert.That(_characterController.center.y, Is.EqualTo(0f).Within(_tolerance));
            Assert.That(FeetY(), Is.EqualTo(feetBefore).Within(_tolerance));
        }

        [Test]
        public void CeilingKeepsThePlayerCrouched()
        {
            InitializeWithLook();
            LogAssert.Expect(LogType.Log, "[Player] crouched=true");
            _player.Tick(Vector2.zero, false, true, false, 0f);

            // 立った頭 (y = 2) と、しゃがんだ頭 (y = 1.2) の間に天井を置く。
            var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                ceiling.transform.position = new Vector3(0f, 1.7f, 0f);
                ceiling.transform.localScale = new Vector3(3f, 0.4f, 3f);
                Physics.SyncTransforms();

                Assert.That(_player.HasHeadroomToStand(), Is.False);
                _player.Tick(Vector2.zero, false, false, false, 0f);

                Assert.That(_player.IsCrouched, Is.True);
                Assert.That(_characterController.height, Is.EqualTo(PlayerController.DefaultCrouchHeight).Within(_tolerance));
            }
            finally
            {
                Object.DestroyImmediate(ceiling);
            }

            Physics.SyncTransforms();
            LogAssert.Expect(LogType.Log, "[Player] crouched=false");
            _player.Tick(Vector2.zero, false, false, false, 0f);

            Assert.That(_player.IsCrouched, Is.False);
        }

        [Test]
        public void TriggerAboveDoesNotBlockStanding()
        {
            InitializeWithLook();
            LogAssert.Expect(LogType.Log, "[Player] crouched=true");
            _player.Tick(Vector2.zero, false, true, false, 0f);

            var trigger = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                trigger.transform.position = new Vector3(0f, 1.7f, 0f);
                trigger.transform.localScale = new Vector3(3f, 0.4f, 3f);
                trigger.GetComponent<Collider>().isTrigger = true;
                Physics.SyncTransforms();

                LogAssert.Expect(LogType.Log, "[Player] crouched=false");
                _player.Tick(Vector2.zero, false, false, false, 0f);

                Assert.That(_player.IsCrouched, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(trigger);
            }
        }

        [Test]
        public void CrouchLowersTheViewRigEyeAndStandingRestoresIt()
        {
            var eye = new GameObject("Eye").transform;
            eye.SetParent(_body.transform, false);
            eye.localPosition = _eyeOffset;
            var cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(eye, false);
            cameraObject.AddComponent<Camera>();
            _body.AddComponent<ViewRig>();
            InitializeWithLook();

            Assert.That(_player.Eye, Is.SameAs(eye));

            LogAssert.Expect(LogType.Log, "[Player] crouched=true");
            _player.Tick(Vector2.zero, false, true, false, 0f);

            // 頭のてっぺんが 0.8m 下がるので、目も 0.8m 下がる。
            Assert.That(eye.localPosition.y, Is.EqualTo(_eyeOffset.y - 0.8f).Within(_tolerance));

            LogAssert.Expect(LogType.Log, "[Player] crouched=false");
            Physics.SyncTransforms();
            _player.Tick(Vector2.zero, false, false, false, 0f);

            Assert.That(eye.localPosition.y, Is.EqualTo(_eyeOffset.y).Within(_tolerance));
        }

        [Test]
        public void CrouchLowersTheCameraWithoutViewRig()
        {
            var cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(_body.transform, false);
            cameraObject.transform.localPosition = _eyeOffset;
            cameraObject.AddComponent<Camera>();
            InitializeWithLook();

            Assert.That(_player.Eye, Is.SameAs(cameraObject.transform));

            LogAssert.Expect(LogType.Log, "[Player] crouched=true");
            _player.Tick(Vector2.zero, false, true, false, 0f);

            Assert.That(cameraObject.transform.localPosition.y, Is.EqualTo(_eyeOffset.y - 0.8f).Within(_tolerance));
        }

        [Test]
        public void CrouchMovesAtCrouchSpeedEvenWhileSprinting()
        {
            // 床の無い宙で Tick するので、空中制御で速度が薄まらないよう 1 にする
            _spec = new MotorSpec(5f, 8f, 1.2f, 20f, 1f, 2.5f);
            InitializeWithLook();

            LogAssert.Expect(LogType.Log, "[Player] crouched=true");
            _player.Tick(new Vector2(0f, 1f), true, true, false, 0.01f);

            var velocity = _player.Motor.Velocity;
            Assert.That(new Vector2(velocity.X, velocity.Z).magnitude, Is.EqualTo(_spec.CrouchSpeed).Within(_tolerance));
        }

        [Test]
        public void CrouchUsesTheDefaultSettingsUntilLoaded()
        {
            Assert.That(_player.ControlSettings, Is.EqualTo(ControlSettings.Default));
            Assert.Throws<ArgumentNullException>(() => _player.ControlSettings = null);
        }

        [Test]
        public void CrouchModeFollowsTheDevice()
        {
            // 初期値ではキーボード／マウスが Hold、ゲームパッドが Toggle。
            _player.UpdateCrouch(false, false, false, false, InputDeviceKind.Gamepad);
            Assert.That(_player.CrouchDevice, Is.EqualTo(InputDeviceKind.Gamepad));
            Assert.That(_player.CrouchMode, Is.EqualTo(HoldMode.Toggle));

            LogAssert.Expect(LogType.Log, "[Player] crouch=off mode=hold reason=mode");
            _player.UpdateCrouch(false, false, false, false, InputDeviceKind.KeyboardMouse);
            Assert.That(_player.CrouchMode, Is.EqualTo(HoldMode.Hold));
        }

        [Test]
        public void HoldCrouchesOnlyWhilePressedAsBefore()
        {
            InitializeWithLook();

            LogAssert.Expect(LogType.Log, "[Player] crouched=true");
            TickCrouch(true, true, false, false, InputDeviceKind.KeyboardMouse);
            Assert.That(_player.IsCrouched, Is.True);

            TickCrouch(true, false, false, false, InputDeviceKind.KeyboardMouse);
            Assert.That(_player.IsCrouched, Is.True);

            LogAssert.Expect(LogType.Log, "[Player] crouched=false");
            Physics.SyncTransforms();
            TickCrouch(false, false, false, false, InputDeviceKind.KeyboardMouse);
            Assert.That(_player.IsCrouched, Is.False);
        }

        [Test]
        public void ToggleCrouchStaysDownAfterOnePressAndJumpingStandsUp()
        {
            InitializeWithLook();
            _player.ControlSettings = ControlSettings.Default.With(InputDeviceKind.KeyboardMouse, HoldAction.Crouch, HoldMode.Toggle);

            LogAssert.Expect(LogType.Log, "[Player] crouch=on mode=toggle reason=button");
            LogAssert.Expect(LogType.Log, "[Player] crouched=true");
            TickCrouch(true, true, false, false, InputDeviceKind.KeyboardMouse);
            Physics.SyncTransforms();
            TickCrouch(false, false, false, false, InputDeviceKind.KeyboardMouse);
            TickCrouch(false, false, false, false, InputDeviceKind.KeyboardMouse);

            Assert.That(_player.IsCrouched, Is.True, "letting go keeps crouching");
            Assert.That(_player.IsCrouchActive, Is.True);

            LogAssert.Expect(LogType.Log, "[Player] crouch=off mode=toggle reason=jump");
            LogAssert.Expect(LogType.Log, "[Player] crouched=false");
            TickCrouch(false, false, true, false, InputDeviceKind.KeyboardMouse);

            Assert.That(_player.IsCrouchActive, Is.False);
            Assert.That(_player.IsCrouched, Is.False);
        }

        [Test]
        public void ToggleCrouchIsReleasedWhenTheSprintStarts()
        {
            InitializeWithLook();
            TickCrouch(true, true, false, false, InputDeviceKind.Gamepad);
            Assert.That(_player.IsCrouched, Is.True);

            LogAssert.Expect(LogType.Log, "[Player] crouch=off mode=toggle reason=sprint");
            Physics.SyncTransforms();
            TickCrouch(false, false, false, true, InputDeviceKind.Gamepad);

            Assert.That(_player.IsCrouched, Is.False);
        }

        [Test]
        public void ToggleCrouchUnderACeilingIsNotReleasedByJumping()
        {
            InitializeWithLook();
            _player.ControlSettings = ControlSettings.Default.With(InputDeviceKind.KeyboardMouse, HoldAction.Crouch, HoldMode.Toggle);
            TickCrouch(true, true, false, false, InputDeviceKind.KeyboardMouse);

            // 立った頭 (y = 2) と、しゃがんだ頭 (y = 1.2) の間に天井を置く。
            var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                ceiling.transform.position = new Vector3(0f, 1.7f, 0f);
                ceiling.transform.localScale = new Vector3(3f, 0.4f, 3f);
                Physics.SyncTransforms();

                TickCrouch(false, false, true, false, InputDeviceKind.KeyboardMouse);
                TickCrouch(false, false, false, true, InputDeviceKind.KeyboardMouse);

                Assert.That(_player.IsCrouchActive, Is.True, "a blocked headroom keeps the toggle on");
                Assert.That(_player.IsCrouched, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(ceiling);
            }

            // 天井が無くなっても、切り替えたしゃがみは勝手には解けない。
            Physics.SyncTransforms();
            TickCrouch(false, false, false, false, InputDeviceKind.KeyboardMouse);
            Assert.That(_player.IsCrouched, Is.True);

            TickCrouch(false, false, true, false, InputDeviceKind.KeyboardMouse);
            Assert.That(_player.IsCrouched, Is.False);
        }

        [Test]
        public void ReleaseCrouchStandsUpOnTheNextTickAndNeedsAFreshPress()
        {
            InitializeWithLook();
            TickCrouch(true, true, false, false, InputDeviceKind.KeyboardMouse);

            LogAssert.Expect(LogType.Log, "[Player] crouch=off mode=hold reason=death");
            Assert.That(_player.ReleaseCrouch(CrouchInput.ReasonDeath), Is.True);
            Assert.That(_player.ReleaseCrouch(CrouchInput.ReasonDeath), Is.False);

            Physics.SyncTransforms();
            TickCrouch(true, false, false, false, InputDeviceKind.KeyboardMouse);
            Assert.That(_player.IsCrouched, Is.False, "holding Crouch through a release does not crouch again");
        }

        // UpdateCrouch で解釈した結果を Tick に渡す (Update と同じ流れ)。
        private void TickCrouch(bool isPressed, bool wasPressed, bool jump, bool sprint, InputDeviceKind device)
        {
            var crouch = _player.UpdateCrouch(isPressed, wasPressed, jump, sprint, device);
            _player.Tick(Vector2.zero, sprint, crouch, jump, 0f);
        }

        private void InitializeWithLook()
        {
            _look = _body.AddComponent<FirstPersonLook>();
            _player.Initialize(_characterController, _look, _move, _sprint, _crouch, _jump, _spec);
        }

        private float FeetY()
        {
            return _body.transform.position.y + _characterController.center.y - (_characterController.height * 0.5f);
        }
    }
}
