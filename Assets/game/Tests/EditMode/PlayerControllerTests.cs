using System;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Editor;
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

        // 乗り越えの検査で正面に置く壁の、体の中心から手前の面までの距離と厚さ。
        private const float _wallNearZ = 0.8f;
        private const float _wallDepth = 0.4f;
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
        private GameObject _floor;
        private GameObject _wall;

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
            if (_floor != null)
            {
                Object.DestroyImmediate(_floor);
            }

            if (_wall != null)
            {
                Object.DestroyImmediate(_wall);
            }
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
        public void SprintUsesTheDefaultSettingsUntilLoaded()
        {
            Assert.That(_player.ControlSettings, Is.EqualTo(ControlSettings.Default));
            Assert.Throws<ArgumentNullException>(() => _player.ControlSettings = null);
        }

        [Test]
        public void ToggleSprintKeepsGoingAfterOnePressAndStopsOnTheNext()
        {
            _player.ControlSettings = ControlSettings.Default.With(InputDeviceKind.KeyboardMouse, HoldAction.Sprint, HoldMode.Toggle);
            LogAssert.Expect(LogType.Log, "[Player] sprint=off mode=toggle reason=mode");
            LogAssert.Expect(LogType.Log, "[Player] sprint=on mode=toggle reason=button");

            Assert.That(_player.UpdateSprint(Vector2.up, true, true, InputDeviceKind.KeyboardMouse), Is.True);
            Assert.That(_player.UpdateSprint(Vector2.up, false, false, InputDeviceKind.KeyboardMouse), Is.True);
            Assert.That(_player.IsSprinting, Is.True);
            Assert.That(_player.SprintMode, Is.EqualTo(HoldMode.Toggle));

            LogAssert.Expect(LogType.Log, "[Player] sprint=off mode=toggle reason=button");
            Assert.That(_player.UpdateSprint(Vector2.up, true, true, InputDeviceKind.KeyboardMouse), Is.False);
            Assert.That(_player.UpdateSprint(Vector2.up, false, false, InputDeviceKind.KeyboardMouse), Is.False);
        }

        [Test]
        public void ToggleSprintIsReleasedWhenTheMoveInputStops()
        {
            _player.ControlSettings = ControlSettings.Default.With(InputDeviceKind.KeyboardMouse, HoldAction.Sprint, HoldMode.Toggle);
            _player.UpdateSprint(Vector2.up, true, true, InputDeviceKind.KeyboardMouse);

            LogAssert.Expect(LogType.Log, "[Player] sprint=off mode=toggle reason=stopped");
            Assert.That(_player.UpdateSprint(Vector2.zero, false, false, InputDeviceKind.KeyboardMouse), Is.False);
            Assert.That(_player.UpdateSprint(Vector2.up, false, false, InputDeviceKind.KeyboardMouse), Is.False);
        }

        [Test]
        public void HoldSprintsOnlyWhilePressedAsBefore()
        {
            Assert.That(_player.ControlSettings.Get(InputDeviceKind.KeyboardMouse, HoldAction.Sprint), Is.EqualTo(HoldMode.Hold));

            Assert.That(_player.UpdateSprint(Vector2.up, false, false, InputDeviceKind.KeyboardMouse), Is.False);
            Assert.That(_player.UpdateSprint(Vector2.up, true, true, InputDeviceKind.KeyboardMouse), Is.True);
            Assert.That(_player.UpdateSprint(Vector2.zero, true, false, InputDeviceKind.KeyboardMouse), Is.True, "stopping while held keeps sprinting");
            Assert.That(_player.UpdateSprint(Vector2.up, true, false, InputDeviceKind.KeyboardMouse), Is.True);
            Assert.That(_player.UpdateSprint(Vector2.up, false, false, InputDeviceKind.KeyboardMouse), Is.False);
        }

        [Test]
        public void SprintModeFollowsTheDevice()
        {
            // 初期値ではキーボード／マウスが Hold、ゲームパッドが Toggle。
            _player.UpdateSprint(Vector2.up, false, false, InputDeviceKind.Gamepad);
            Assert.That(_player.ActiveDevice, Is.EqualTo(InputDeviceKind.Gamepad));
            Assert.That(_player.SprintMode, Is.EqualTo(HoldMode.Toggle));

            LogAssert.Expect(LogType.Log, "[Player] sprint=off mode=hold reason=mode");
            _player.UpdateSprint(Vector2.up, false, false, InputDeviceKind.KeyboardMouse);
            Assert.That(_player.SprintMode, Is.EqualTo(HoldMode.Hold));
        }

        [Test]
        public void ReleaseSprintStopsUntilPressedAgain()
        {
            _player.UpdateSprint(Vector2.up, true, true, InputDeviceKind.KeyboardMouse);

            LogAssert.Expect(LogType.Log, "[Player] sprint=off mode=hold reason=aim");
            Assert.That(_player.ReleaseSprint("aim"), Is.True);
            Assert.That(_player.UpdateSprint(Vector2.up, true, false, InputDeviceKind.KeyboardMouse), Is.False);
            Assert.That(_player.ReleaseSprint("aim"), Is.False);
        }

        [Test]
        public void DeathAndRespawnReleaseTheBoundSprint()
        {
            var damageable = _body.AddComponent<Damageable>();
            damageable.Initialize("Player", 100);
            _player.BindSprintRelease(null, damageable);

            _player.UpdateSprint(Vector2.up, true, true, InputDeviceKind.Gamepad);
            LogAssert.Expect(LogType.Log, "[Player] sprint=off mode=toggle reason=death");
            damageable.Kill();
            Assert.That(_player.IsSprinting, Is.False);

            _player.UpdateSprint(Vector2.up, true, true, InputDeviceKind.Gamepad);
            LogAssert.Expect(LogType.Log, "[Player] sprint=off mode=toggle reason=respawn");
            damageable.ResetHealth();
            Assert.That(_player.IsSprinting, Is.False);

            _player.BindSprintRelease(null, null);
            _player.UpdateSprint(Vector2.up, true, true, InputDeviceKind.Gamepad);
            damageable.Kill();
            Assert.That(_player.IsSprinting, Is.True, "unbound damageable no longer releases");
        }

        [Test]
        public void FiringReleasesTheBoundSprint()
        {
            var camera = new GameObject("Camera", typeof(Camera));
            var rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            var attack = new InputAction("Attack", InputActionType.Button);
            var reload = new InputAction("Reload", InputActionType.Button);
            try
            {
                camera.transform.SetParent(_body.transform, false);
                WeaponAssetBuilder.Configure(rifle, WeaponAssetBuilder.RifleSpec());
                var weapon = camera.AddComponent<WeaponController>();
                weapon.Initialize(camera.transform, attack, reload, rifle);
                _player.BindSprintRelease(weapon, null);

                _player.UpdateSprint(Vector2.up, true, true, InputDeviceKind.KeyboardMouse);
                LogAssert.Expect(LogType.Log, "[Player] sprint=off mode=hold reason=fire");
                Assert.That(weapon.Fire(), Is.True);
                Assert.That(_player.IsSprinting, Is.False);
            }
            finally
            {
                _player.BindSprintRelease(null, null);
                attack.Dispose();
                reload.Dispose();
                Object.DestroyImmediate(camera);
                Object.DestroyImmediate(rifle);
            }
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
        public void MoveFilterIsNullByDefault()
        {
            Assert.That(_player.MoveFilter, Is.Null);
        }

        [Test]
        public void MoveFilterSeesTheInputPassedToTick()
        {
            InitializeWithLook();
            var filter = new RewritingMoveFilter();
            _player.MoveFilter = filter;

            _player.Tick(new Vector2(0.5f, 1f), true, false, true, 0f);

            Assert.That(filter.Calls, Is.EqualTo(1));
            Assert.That(filter.SeenMove, Is.EqualTo(new Vector2(0.5f, 1f)));
            Assert.That(filter.SeenSprint, Is.True);
            Assert.That(filter.SeenCrouch, Is.False);
            Assert.That(filter.SeenJump, Is.True);
        }

        [Test]
        public void MoveFilterRewritesMoveSprintAndCrouchBeforeTheMotor()
        {
            // 床の無い宙で Tick するので、空中制御で速度が薄まらないよう 1 にする
            _spec = new MotorSpec(5f, 8f, 1.2f, 20f, 1f, 2.5f);
            InitializeWithLook();
            _player.MoveFilter = new RewritingMoveFilter { Move = new Vector2(1f, 0f), Sprint = false, Crouch = true };

            LogAssert.Expect(LogType.Log, "[Player] crouched=true");
            _player.Tick(new Vector2(0f, 1f), true, false, false, 0.01f);

            Assert.That(_player.IsCrouched, Is.True);
            var velocity = _player.Motor.Velocity;
            Assert.That(velocity.X, Is.EqualTo(_spec.CrouchSpeed).Within(_tolerance), "moves along the rewritten input (right)");
            Assert.That(velocity.Z, Is.EqualTo(0f).Within(_tolerance));
        }

        [Test]
        public void MoveFilterCanStartASprint()
        {
            _spec = new MotorSpec(5f, 8f, 1.2f, 20f, 1f, 2.5f);
            InitializeWithLook();
            _player.MoveFilter = new RewritingMoveFilter { Sprint = true };

            _player.Tick(new Vector2(0f, 1f), false, false, false, 0.01f);

            var velocity = _player.Motor.Velocity;
            Assert.That(new Vector2(velocity.X, velocity.Z).magnitude, Is.EqualTo(_spec.SprintSpeed).Within(_tolerance));
        }

        [Test]
        public void MoveFilterCannotStandUpUnderACeiling()
        {
            InitializeWithLook();
            LogAssert.Expect(LogType.Log, "[Player] crouched=true");
            _player.Tick(Vector2.zero, false, true, false, 0f);

            var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                ceiling.transform.position = new Vector3(0f, 1.7f, 0f);
                ceiling.transform.localScale = new Vector3(3f, 0.4f, 3f);
                Physics.SyncTransforms();
                _player.MoveFilter = new RewritingMoveFilter { Crouch = false };

                _player.Tick(Vector2.zero, false, true, false, 0f);

                Assert.That(_player.IsCrouched, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(ceiling);
            }
        }

        [Test]
        public void WithoutMoveFilterTheInputReachesTheMotorAsBefore()
        {
            _spec = new MotorSpec(5f, 8f, 1.2f, 20f, 1f, 2.5f);
            InitializeWithLook();
            _player.MoveFilter = null;

            _player.Tick(new Vector2(0f, 1f), true, false, false, 0.01f);

            Assert.That(_player.IsCrouched, Is.False);
            var velocity = _player.Motor.Velocity;
            Assert.That(velocity.X, Is.EqualTo(0f).Within(_tolerance));
            Assert.That(velocity.Z, Is.EqualTo(_spec.SprintSpeed).Within(_tolerance));
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

        [Test]
        public void DodgeIsOffByDefaultAndDoesNothing()
        {
            InitializeWithLook();
            LandOnFloor();

            Assert.That(_player.DodgeRule, Is.EqualTo(Dodge.Off));
            Assert.That(_player.TryStartDodge(new Vector2(1f, 0f)), Is.False);
            Assert.That(_player.IsDodging, Is.False);
        }

        [Test]
        public void DodgeRollsTheSpecDistanceTowardsTheInput()
        {
            InitializeWithLook();
            LandOnFloor();
            _player.DodgeRule = Dodge.On;
            var start = _body.transform.position;
            Vector3? startedTowards = null;
            var ended = 0;
            _player.DodgeStarted += direction => startedTowards = direction;
            _player.DodgeEnded += () => ended++;

            LogAssert.Expect(LogType.Log, "[Player] dodging=true");
            Assert.That(_player.TryStartDodge(new Vector2(1f, 0f)), Is.True);
            Assert.That(_player.IsDodging, Is.True);
            Assert.That(startedTowards, Is.Not.Null);
            Assert.That(startedTowards.Value.x, Is.EqualTo(1f).Within(_tolerance));

            LogAssert.Expect(LogType.Log, "[Player] dodging=false");
            TickUntilDodgeEnds();

            var moved = _body.transform.position - start;
            Assert.That(moved.x, Is.EqualTo(MotorSpec.DefaultDodgeDistance).Within(0.01f));
            Assert.That(moved.z, Is.EqualTo(0f).Within(0.01f));
            Assert.That(ended, Is.EqualTo(1));
        }

        [Test]
        public void DodgeWithoutInputRollsBackwards()
        {
            InitializeWithLook();
            LandOnFloor();
            _player.DodgeRule = Dodge.On;
            var start = _body.transform.position;

            Assert.That(_player.TryStartDodge(Vector2.zero), Is.True);
            TickUntilDodgeEnds();

            var moved = _body.transform.position - start;
            Assert.That(moved.z, Is.EqualTo(-MotorSpec.DefaultDodgeDistance).Within(0.01f));
            Assert.That(moved.x, Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void DodgeNeedsGround()
        {
            InitializeWithLook();
            _player.DodgeRule = Dodge.On;

            Assert.That(_player.IsGrounded, Is.False);
            Assert.That(_player.TryStartDodge(new Vector2(1f, 0f)), Is.False);
        }

        [Test]
        public void DodgeIsNotRestartedUntilTheCooldownPasses()
        {
            InitializeWithLook();
            LandOnFloor();
            _player.DodgeRule = Dodge.On;

            Assert.That(_player.TryStartDodge(new Vector2(1f, 0f)), Is.True);
            Assert.That(_player.TryStartDodge(new Vector2(-1f, 0f)), Is.False, "not while rolling");
            TickUntilDodgeEnds();
            Assert.That(_player.TryStartDodge(new Vector2(-1f, 0f)), Is.False, "not right after the roll");

            for (var i = 0; i < 26; i++)
            {
                _player.Tick(Vector2.zero, false, false, false, 0.02f);
            }

            Assert.That(_player.TryStartDodge(new Vector2(-1f, 0f)), Is.True, "after the cooldown");
        }

        [Test]
        public void DodgeIsNotStartedWhileBlocked()
        {
            InitializeWithLook();
            LandOnFloor();
            _player.DodgeRule = Dodge.On;
            var blocked = true;
            _player.DodgeBlocker = () => blocked;

            Assert.That(_player.TryStartDodge(new Vector2(1f, 0f)), Is.False);

            blocked = false;
            Assert.That(_player.TryStartDodge(new Vector2(1f, 0f)), Is.True);
        }

        [Test]
        public void DodgeReleasesCrouchAndSprintAndKeepsThemOffWhileRolling()
        {
            InitializeWithLook();
            LandOnFloor();
            _player.DodgeRule = Dodge.On;
            // 0 秒の Tick では動かず接地が外れるので、時間を進めて床に押し付けたまましゃがむ。
            var crouch = _player.UpdateCrouch(true, true, false, false, InputDeviceKind.KeyboardMouse);
            _player.Tick(Vector2.zero, false, crouch, false, 0.02f);
            _player.UpdateSprint(new Vector2(0f, 1f), true, true, InputDeviceKind.KeyboardMouse);
            Assert.That(_player.IsCrouched, Is.True);
            Assert.That(_player.IsSprinting, Is.True);

            LogAssert.Expect(LogType.Log, "[Player] crouch=off mode=hold reason=dodge");
            Assert.That(_player.TryStartDodge(new Vector2(1f, 0f)), Is.True);
            Assert.That(_player.IsCrouchActive, Is.False);
            Assert.That(_player.IsSprinting, Is.False);

            // しゃがみとジャンプを押し続けても、転がっている間は立ったまま跳ばない。
            Physics.SyncTransforms();
            _player.Tick(Vector2.zero, true, true, true, 0.02f);

            Assert.That(_player.IsCrouched, Is.False);
            Assert.That(_player.Motor.Velocity.Y, Is.LessThanOrEqualTo(0f));
        }

        [Test]
        public void TurningDodgeOffStopsTheRoll()
        {
            InitializeWithLook();
            LandOnFloor();
            _player.DodgeRule = Dodge.On;
            _player.TryStartDodge(new Vector2(1f, 0f));

            LogAssert.Expect(LogType.Log, "[Player] dodging=false");
            _player.DodgeRule = Dodge.Off;

            Assert.That(_player.IsDodging, Is.False);
        }

        [Test]
        public void DodgingLogHasTagAndLowercaseValue()
        {
            Assert.That(PlayerController.FormatDodgingLog(true), Is.EqualTo("[Player] dodging=true"));
            Assert.That(PlayerController.FormatDodgingLog(false), Is.EqualTo("[Player] dodging=false"));
        }

        [Test]
        public void VaultIsOffByDefaultAndJumpStaysAJump()
        {
            InitializeWithLook();
            LandOnFloor();
            PlaceWallInFront(1f);

            Assert.That(_player.VaultRule, Is.EqualTo(Vault.Off));
            _player.Tick(Vector2.zero, false, false, true, 0.02f);

            Assert.That(_player.IsVaulting, Is.False);
            Assert.That(_player.Motor.Velocity.Y, Is.GreaterThan(0f), "jumped");
        }

        [Test]
        public void JumpInFrontOfALowWallVaultsOverIt()
        {
            InitializeWithLook();
            LandOnFloor();
            _player.VaultRule = Vault.On;
            PlaceWallInFront(1f);
            var start = _body.transform.position;
            Vector3? startedTowards = null;
            var ended = 0;
            _player.VaultStarted += direction => startedTowards = direction;
            _player.VaultEnded += () => ended++;

            LogAssert.Expect(LogType.Log, new Regex(@"^\[Player\] vault=Vault height=\d\.\d\d depth=0\.40$"));
            LogAssert.Expect(LogType.Log, "[Player] vaulting=true");
            _player.Tick(Vector2.zero, false, false, true, 0.02f);

            Assert.That(_player.IsVaulting, Is.True);
            Assert.That(_player.LastVaultProbe.CanVault, Is.True);
            Assert.That(startedTowards, Is.Not.Null);
            Assert.That(startedTowards.Value.z, Is.EqualTo(1f).Within(_tolerance));

            var highest = float.MinValue;
            LogAssert.Expect(LogType.Log, "[Player] vaulting=false");
            for (var i = 0; i < 100 && _player.IsVaulting; i++)
            {
                _player.Tick(Vector2.zero, true, true, true, 0.02f);
                highest = Mathf.Max(highest, FeetY());
                Assert.That(_player.IsCrouched, Is.False, "crouch is held off while vaulting");
            }

            Assert.That(_player.IsVaulting, Is.False, "the vault ends");
            Assert.That(ended, Is.EqualTo(1));
            Assert.That(highest, Is.GreaterThan(1f), "the feet went over the top of the wall");

            var moved = _body.transform.position - start;
            var farSide = _wallNearZ + _wallDepth + _radius;
            Assert.That(_body.transform.position.z, Is.GreaterThan(farSide), "landed beyond the wall");
            Assert.That(moved.x, Is.EqualTo(0f).Within(0.01f));
            Assert.That(moved.y, Is.EqualTo(0f).Within(0.1f), "back down at the floor");
        }

        [Test]
        public void JumpInFrontOfAHighWallJumpsInstead()
        {
            InitializeWithLook();
            LandOnFloor();
            _player.VaultRule = Vault.On;
            PlaceWallInFront(2f);

            LogAssert.Expect(LogType.Log, new Regex(@"^\[Player\] vault=TooHigh "));
            _player.Tick(Vector2.zero, false, false, true, 0.02f);

            Assert.That(_player.IsVaulting, Is.False);
            Assert.That(_player.LastVaultProbe.Verdict, Is.EqualTo(VaultVerdict.TooHigh));
            Assert.That(_player.Motor.Velocity.Y, Is.GreaterThan(0f), "jumped");
        }

        [Test]
        public void JumpWithNothingInFrontJumps()
        {
            InitializeWithLook();
            LandOnFloor();
            _player.VaultRule = Vault.On;

            _player.Tick(Vector2.zero, false, false, true, 0.02f);

            Assert.That(_player.IsVaulting, Is.False);
            Assert.That(_player.LastVaultProbe.Verdict, Is.EqualTo(VaultVerdict.NoObstacle));
            Assert.That(_player.Motor.Velocity.Y, Is.GreaterThan(0f));
        }

        [Test]
        public void VaultNeedsGround()
        {
            InitializeWithLook();
            _player.VaultRule = Vault.On;

            Assert.That(_player.IsGrounded, Is.False);
            Assert.That(_player.TryStartVault(), Is.False);
        }

        [Test]
        public void VaultIsNotStartedWhileDodging()
        {
            InitializeWithLook();
            LandOnFloor();
            _player.VaultRule = Vault.On;
            _player.DodgeRule = Dodge.On;
            PlaceWallInFront(1f);

            Assert.That(_player.TryStartDodge(new Vector2(1f, 0f)), Is.True);
            Assert.That(_player.TryStartVault(), Is.False);
        }

        [Test]
        public void TurningVaultOffStopsTheVault()
        {
            InitializeWithLook();
            LandOnFloor();
            _player.VaultRule = Vault.On;
            PlaceWallInFront(1f);
            Assert.That(_player.TryStartVault(), Is.True);

            LogAssert.Expect(LogType.Log, "[Player] vaulting=false");
            _player.VaultRule = Vault.Off;

            Assert.That(_player.IsVaulting, Is.False);
        }

        [Test]
        public void VaultLogsHaveTagAndValues()
        {
            Assert.That(PlayerController.FormatVaultingLog(true), Is.EqualTo("[Player] vaulting=true"));
            Assert.That(PlayerController.FormatVaultingLog(false), Is.EqualTo("[Player] vaulting=false"));

            var result = new VaultProbeResult(VaultVerdict.TooDeep, 1f, float.PositiveInfinity, System.Numerics.Vector3.UnitZ, null);
            Assert.That(PlayerController.FormatVaultProbeLog(result), Is.EqualTo("[Player] vault=TooDeep height=1.00 depth=inf"));
        }

        // 正面 (+Z) の、体の中心から _wallNearZ 先に、厚さ _wallDepth・高さ height の壁を床の上に置く。
        private void PlaceWallInFront(float height)
        {
            _wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wall.transform.position = new Vector3(0f, height / 2f, _wallNearZ + (_wallDepth / 2f));
            _wall.transform.localScale = new Vector3(3f, height, _wallDepth);
            Physics.SyncTransforms();
        }

        // 足元 (y = 0) の下に床を置き、接地するまで落とす。
        private void LandOnFloor()
        {
            _floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _floor.transform.position = new Vector3(0f, -0.5f, 0f);
            _floor.transform.localScale = new Vector3(40f, 1f, 40f);
            Physics.SyncTransforms();

            for (var i = 0; i < 10 && !_player.IsGrounded; i++)
            {
                _player.Tick(Vector2.zero, false, false, false, 0.02f);
            }

            Assert.That(_player.IsGrounded, Is.True, "landed on the floor");
        }

        private void TickUntilDodgeEnds()
        {
            for (var i = 0; i < 100 && _player.IsDodging; i++)
            {
                _player.Tick(Vector2.zero, false, false, false, 0.02f);
            }

            Assert.That(_player.IsDodging, Is.False, "the roll ends");
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

        // 受け取った値を覚え、値を決めた項目だけ書き換える。
        private sealed class RewritingMoveFilter : IPlayerMoveFilter
        {
            public Vector2? Move { get; set; }
            public bool? Sprint { get; set; }
            public bool? Crouch { get; set; }
            public bool? Jump { get; set; }

            public int Calls { get; private set; }
            public Vector2 SeenMove { get; private set; }
            public bool SeenSprint { get; private set; }
            public bool SeenCrouch { get; private set; }
            public bool SeenJump { get; private set; }

            public void Filter(ref Vector2 move, ref bool sprint, ref bool crouch, ref bool jumpPressed)
            {
                Calls++;
                SeenMove = move;
                SeenSprint = sprint;
                SeenCrouch = crouch;
                SeenJump = jumpPressed;
                move = Move ?? move;
                sprint = Sprint ?? sprint;
                crouch = Crouch ?? crouch;
                jumpPressed = Jump ?? jumpPressed;
            }
        }
    }
}
