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
    /// 伏せ (<see cref="PlayerController.ProneRule"/>): ルール・カプセルと当たり判定の高さ・頭上の検査・飛び込み・視点と射撃のばらつき。
    /// Edit Mode でも Physics の重なり判定はコライダーに当たるので、障害物を置いたら SyncTransforms してから Tick する。
    /// </summary>
    public sealed class PlayerControllerProneTests
    {
        private const float _tolerance = 1e-4f;
        private const float _step = 0.02f;

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
        private GameObject _floor;
        private GameObject _ceiling;

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

            if (_ceiling != null)
            {
                Object.DestroyImmediate(_ceiling);
            }
        }

        [Test]
        public void LogsHaveTagAndValues()
        {
            Assert.That(PlayerController.FormatStanceLog(PlayerStance.Prone, 1f), Is.EqualTo("[Player] stance=Prone hitboxHeight=1.00"));
            Assert.That(PlayerController.FormatStanceLog(PlayerStance.Standing, 2f), Is.EqualTo("[Player] stance=Standing hitboxHeight=2.00"));
            Assert.That(PlayerController.FormatDivingLog(true), Is.EqualTo("[Player] diving=true"));
            Assert.That(PlayerController.FormatDivingLog(false), Is.EqualTo("[Player] diving=false"));
            Assert.That(ProneInput.FormatLog(true, ProneInput.ReasonDive), Is.EqualTo("[Player] prone=on reason=dive"));
        }

        [Test]
        public void ProneIsOffByDefaultAndPressingDoesNothing()
        {
            InitializeWithLook();
            LandOnFloor();

            Assert.That(_player.ProneRule, Is.EqualTo(Prone.Off));
            Assert.That(_player.ProneSpec, Is.SameAs(ProneSpec.Default));
            Assert.That(_player.UpdateProne(true, false, false, false, Vector2.zero), Is.False);
            _player.Tick(Vector2.zero, false, false, false, _step);

            Assert.That(_player.IsProne, Is.False);
            Assert.That(_player.IsProneActive, Is.False);
            Assert.That(_characterController.height, Is.EqualTo(_height).Within(_tolerance));
        }

        [Test]
        public void ProneRuleRejectsUndefinedValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _player.ProneRule = (Prone)99);
            Assert.Throws<ArgumentNullException>(() => _player.ProneSpec = null);
        }

        [Test]
        public void InitializeBuildsTheProneShapeFromTheSpec()
        {
            InitializeWithLook();

            Assert.That(_player.Stance.Prone.Height, Is.EqualTo(ProneSpec.DefaultHeight).Within(_tolerance));
            Assert.That(_player.Stance.Prone.BottomY, Is.EqualTo(_player.Stance.Standing.BottomY).Within(_tolerance));
            Assert.That(_player.CurrentStance, Is.EqualTo(PlayerStance.Standing));
            Assert.That(_player.HitboxHeight, Is.EqualTo(_height).Within(_tolerance));
        }

        [Test]
        public void LyingDownLowersTheCapsuleTheHitboxAndTheEye()
        {
            var cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(_body.transform, false);
            cameraObject.transform.localPosition = _eyeOffset;
            cameraObject.AddComponent<Camera>();
            InitializeWithLook();
            LandOnFloor();
            _player.ProneRule = Prone.On;
            var feetBefore = FeetY();

            LogAssert.Expect(LogType.Log, "[Player] prone=on reason=button");
            Assert.That(_player.UpdateProne(true, false, false, false, Vector2.zero), Is.True);
            LogAssert.Expect(LogType.Log, "[Player] stance=Prone hitboxHeight=1.00");
            _player.Tick(Vector2.zero, false, false, false, _step);

            Assert.That(_player.IsProne, Is.True);
            Assert.That(_player.IsCrouched, Is.False);
            Assert.That(_player.CurrentStance, Is.EqualTo(PlayerStance.Prone));
            Assert.That(_characterController.height, Is.EqualTo(ProneSpec.DefaultHeight).Within(_tolerance));
            Assert.That(_player.HitboxHeight, Is.EqualTo(ProneSpec.DefaultHeight).Within(_tolerance));
            Assert.That(FeetY(), Is.EqualTo(feetBefore).Within(0.01f));

            // 頭のてっぺんが 1m 下がるので、目も 1m 下がる。
            Assert.That(cameraObject.transform.localPosition.y, Is.EqualTo(_eyeOffset.y - 1f).Within(_tolerance));
        }

        [Test]
        public void ProneNeedsGround()
        {
            InitializeWithLook();
            _player.ProneRule = Prone.On;

            Assert.That(_player.IsGrounded, Is.False);
            Assert.That(_player.UpdateProne(true, false, false, false, Vector2.zero), Is.False);
        }

        [Test]
        public void ALowCeilingKeepsThePlayerProneUntilThereIsRoom()
        {
            InitializeWithLook();
            LandOnFloor();
            LieDown();

            // 伏せた頭 (y = 1) と、しゃがんだ頭 (y = 1.2) の間に天井の下面を置く。
            PlaceCeiling(1.1f);
            Assert.That(_player.HasHeadroomToStand(), Is.False);
            Assert.That(_player.HasHeadroomFor(PlayerStance.Crouching), Is.False);
            Assert.That(_player.HasHeadroomFor(PlayerStance.Prone), Is.True);

            Assert.That(_player.UpdateProne(true, false, false, false, Vector2.zero), Is.False, "asks to get up");
            for (var i = 0; i < 3; i++)
            {
                _player.Tick(Vector2.zero, false, false, false, _step);
                Assert.That(_player.IsProne, Is.True, "the ceiling keeps the player down");
            }

            Assert.That(_characterController.height, Is.EqualTo(ProneSpec.DefaultHeight).Within(_tolerance));

            Object.DestroyImmediate(_ceiling);
            Physics.SyncTransforms();
            LogAssert.Expect(LogType.Log, "[Player] stance=Standing hitboxHeight=2.00");
            _player.Tick(Vector2.zero, false, false, false, _step);

            Assert.That(_player.CurrentStance, Is.EqualTo(PlayerStance.Standing));
            Assert.That(_player.HitboxHeight, Is.EqualTo(_height).Within(_tolerance));
        }

        [Test]
        public void ACeilingAboveTheCrouchGetsThePlayerUpOnlyToACrouch()
        {
            InitializeWithLook();
            LandOnFloor();
            LieDown();
            PlaceCeiling(1.5f);
            _player.UpdateProne(true, false, false, false, Vector2.zero);

            LogAssert.Expect(LogType.Log, "[Player] crouched=true");
            LogAssert.Expect(LogType.Log, "[Player] stance=Crouching hitboxHeight=1.20");
            _player.Tick(Vector2.zero, false, false, false, _step);

            Assert.That(_player.IsCrouched, Is.True);
            Assert.That(_player.IsProne, Is.False);
        }

        [Test]
        public void ProneMovesAtTheProneSpeedWithoutSprintingOrJumping()
        {
            InitializeWithLook();
            LandOnFloor();
            LieDown();

            _player.Tick(new Vector2(0f, 1f), true, false, true, _step);

            var velocity = _player.Motor.Velocity;
            Assert.That(new Vector2(velocity.X, velocity.Z).magnitude, Is.EqualTo(ProneSpec.DefaultSpeed).Within(_tolerance));
            Assert.That(velocity.Y, Is.LessThanOrEqualTo(0f), "does not jump");
            Assert.That(_player.IsProne, Is.True);
        }

        [Test]
        public void PressingProneWhileSprintingDivesForwardAndLiesDown()
        {
            _spec = new MotorSpec(5f, 8f, 1.2f, 20f, 1f, 2.5f);
            InitializeWithLook();
            LandOnFloor();
            _player.ProneRule = Prone.On;
            for (var i = 0; i < 5; i++)
            {
                _player.Tick(new Vector2(0f, 1f), true, false, false, _step);
            }

            Vector3? startedTowards = null;
            var ended = 0;
            _player.DiveStarted += direction => startedTowards = direction;
            _player.DiveEnded += () => ended++;
            var start = _body.transform.position;

            Assert.That(_player.UpdateSprint(new Vector2(0f, 1f), true, true, InputDeviceKind.KeyboardMouse), Is.True);
            LogAssert.Expect(LogType.Log, "[Player] prone=on reason=dive");
            LogAssert.Expect(LogType.Log, "[Player] diving=true");
            Assert.That(_player.UpdateProne(true, false, false, true, new Vector2(0f, 1f)), Is.True);

            Assert.That(_player.IsDiving, Is.True);
            Assert.That(_player.IsSprinting, Is.False, "lying down stops the sprint");
            Assert.That(startedTowards, Is.Not.Null);
            Assert.That(startedTowards.Value.z, Is.EqualTo(1f).Within(_tolerance));

            LogAssert.Expect(LogType.Log, "[Player] diving=false");
            for (var i = 0; i < 100 && _player.IsDiving; i++)
            {
                _player.Tick(Vector2.zero, false, false, false, _step);
            }

            Assert.That(_player.IsDiving, Is.False, "the dive ends");
            Assert.That(ended, Is.EqualTo(1));
            Assert.That(_player.IsProne, Is.True);
            var moved = _body.transform.position - start;
            Assert.That(moved.z, Is.EqualTo(ProneSpec.DefaultDiveDistance).Within(0.05f));
            Assert.That(moved.x, Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void TurningProneOffGetsUp()
        {
            InitializeWithLook();
            LandOnFloor();
            LieDown();

            LogAssert.Expect(LogType.Log, "[Player] prone=off reason=rule");
            _player.ProneRule = Prone.Off;
            Assert.That(_player.IsProneActive, Is.False);

            _player.Tick(Vector2.zero, false, false, false, _step);
            Assert.That(_player.CurrentStance, Is.EqualTo(PlayerStance.Standing));
            Assert.That(_player.UpdateProne(true, false, false, false, Vector2.zero), Is.False, "pressing again does nothing");
        }

        [Test]
        public void ThePressedProneButtonLiesDownThroughApplyInput()
        {
            InitializeWithLook();
            LandOnFloor();
            _player.ProneRule = Prone.On;

            _player.ApplyInput(Input(prone: true), _step);

            Assert.That(_player.IsProne, Is.True);
        }

        [Test]
        public void CrouchingWhileProneGetsUpToACrouch()
        {
            InitializeWithLook();
            LandOnFloor();
            _player.ProneRule = Prone.On;
            _player.ApplyInput(Input(prone: true), _step);

            LogAssert.Expect(LogType.Log, "[Player] prone=off reason=crouch");
            _player.ApplyInput(Input(crouchHeld: true, crouchPressed: true), _step);

            Assert.That(_player.IsProneActive, Is.False);
            Assert.That(_player.IsCrouched, Is.True);
        }

        [Test]
        public void JumpingWhileProneGetsUpWithoutJumping()
        {
            InitializeWithLook();
            LandOnFloor();
            _player.ProneRule = Prone.On;
            _player.ApplyInput(Input(prone: true), _step);

            LogAssert.Expect(LogType.Log, "[Player] prone=off reason=jump");
            _player.ApplyInput(Input(jump: true), _step);

            Assert.That(_player.CurrentStance, Is.EqualTo(PlayerStance.Standing));
            Assert.That(_player.Motor.Velocity.Y, Is.LessThanOrEqualTo(0f), "the jump that got up does not jump");
        }

        [Test]
        public void LyingDownReleasesAToggledCrouch()
        {
            InitializeWithLook();
            LandOnFloor();
            _player.ProneRule = Prone.On;
            var crouch = _player.UpdateCrouch(true, true, false, false, InputDeviceKind.Gamepad);
            _player.Tick(Vector2.zero, false, crouch, false, _step);
            Assert.That(_player.IsCrouched, Is.True);

            LogAssert.Expect(LogType.Log, "[Player] crouch=off mode=toggle reason=prone");
            _player.UpdateProne(true, false, false, false, Vector2.zero);

            Assert.That(_player.IsCrouchActive, Is.False);
            _player.Tick(Vector2.zero, false, false, false, _step);
            Assert.That(_player.IsProne, Is.True);
        }

        [Test]
        public void DodgeAndVaultAreNotStartedWhileProne()
        {
            InitializeWithLook();
            LandOnFloor();
            LieDown();
            _player.DodgeRule = Dodge.On;
            _player.VaultRule = Vault.On;

            Assert.That(_player.TryStartDodge(new Vector2(1f, 0f)), Is.False);
            Assert.That(_player.TryStartVault(), Is.False);
        }

        [Test]
        public void LyingDownNarrowsThePitchAndSteadiesTheAim()
        {
            InitializeWithLook();
            LandOnFloor();
            Assert.That(_player.SpreadScale, Is.EqualTo(1f));
            Assert.That(_look.PitchLimit, Is.EqualTo(_look.MaxPitch));

            LieDown();
            Assert.That(_look.PitchLimit, Is.EqualTo(ProneSpec.DefaultMaxPitchDegrees));
            Assert.That(_player.SpreadScale, Is.EqualTo(ProneSpec.DefaultSpreadScale));

            _player.UpdateProne(true, false, false, false, Vector2.zero);
            _player.Tick(Vector2.zero, false, false, false, _step);
            Assert.That(_look.PitchLimit, Is.EqualTo(_look.MaxPitch));
            Assert.That(_player.SpreadScale, Is.EqualTo(1f));
        }

        [Test]
        public void DeathCancelsTheDive()
        {
            var damageable = _body.AddComponent<Damageable>();
            damageable.Initialize("Player", 100);
            InitializeWithLook();
            LandOnFloor();
            _player.ProneRule = Prone.On;
            _player.BindSprintRelease(null, damageable);
            _player.UpdateSprint(new Vector2(0f, 1f), true, true, InputDeviceKind.KeyboardMouse);
            _player.UpdateProne(true, false, false, true, new Vector2(0f, 1f));
            Assert.That(_player.IsDiving, Is.True);

            LogAssert.Expect(LogType.Log, "[Player] diving=false");
            damageable.Kill();

            Assert.That(_player.IsDiving, Is.False);
            _player.BindSprintRelease(null, null);
        }

        [Test]
        public void AProneHeightThatDoesNotFitIsFittedWithAWarning()
        {
            _player.ProneSpec = new ProneSpec(height: 0.5f);

            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Player\] 伏せの高さ"));
            InitializeWithLook();

            Assert.That(_player.Stance.Prone.Height, Is.EqualTo(_radius * 2f).Within(_tolerance));
        }

        [Test]
        public void ChangingTheSpecWhileProneReappliesTheHeight()
        {
            InitializeWithLook();
            LandOnFloor();
            LieDown();

            _player.ProneSpec = new ProneSpec(height: 1.1f, maxPitchDegrees: 20f);

            Assert.That(_player.IsProne, Is.True);
            Assert.That(_characterController.height, Is.EqualTo(1.1f).Within(_tolerance));
            Assert.That(_look.PitchLimit, Is.EqualTo(20f));
        }

        [Test]
        public void PitchLimitClampsTheView()
        {
            var cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(_body.transform, false);
            cameraObject.AddComponent<Camera>();
            _look = _body.AddComponent<FirstPersonLook>();
            var lookAction = new InputAction("Look", InputActionType.Value, expectedControlType: "Vector2");
            try
            {
                _look.Initialize(cameraObject.transform, lookAction);
                _look.ApplyLookDelta(new Vector2(0f, 60f));
                Assert.That(_look.Pitch, Is.EqualTo(-60f).Within(_tolerance));

                _look.PitchLimit = 30f;
                Assert.That(_look.Pitch, Is.EqualTo(-30f).Within(_tolerance), "narrowing brings the view back in");

                _look.ApplyLookDelta(new Vector2(0f, -120f));
                Assert.That(_look.Pitch, Is.EqualTo(30f).Within(_tolerance));

                _look.PitchLimit = 200f;
                Assert.That(_look.PitchLimit, Is.EqualTo(_look.MaxPitch));
                Assert.Throws<ArgumentOutOfRangeException>(() => _look.PitchLimit = 0f);
                Assert.Throws<ArgumentOutOfRangeException>(() => _look.PitchLimit = float.NaN);
            }
            finally
            {
                lookAction.Dispose();
            }
        }

        [Test]
        public void WeaponSpreadIsScaledByTheStance()
        {
            var holder = new GameObject("Weapon");
            try
            {
                var weapon = holder.AddComponent<WeaponController>();
                var spec = WeaponAssetBuilder.RifleSpec();
                weapon.MoveFractionSource = () => 1f;
                var full = weapon.CurrentConeDegrees(spec);
                Assume.That(full, Is.GreaterThan(0f), "the rifle spreads while moving");
                Assert.That(weapon.SpreadScale, Is.EqualTo(1f));

                weapon.SpreadScaleSource = () => 0.5f;
                Assert.That(weapon.CurrentConeDegrees(spec), Is.EqualTo(full * 0.5f).Within(_tolerance));

                weapon.SpreadScaleSource = () => float.NaN;
                Assert.That(weapon.SpreadScale, Is.EqualTo(1f));

                weapon.SpreadScaleSource = () => 3f;
                Assert.That(weapon.SpreadScale, Is.EqualTo(1f));
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        private static PlayerMoveInput Input(bool prone = false, bool crouchHeld = false, bool crouchPressed = false, bool jump = false)
        {
            return new PlayerMoveInput(
                Vector2.zero,
                false,
                false,
                InputDeviceKind.KeyboardMouse,
                crouchHeld,
                crouchPressed,
                InputDeviceKind.KeyboardMouse,
                jump,
                false,
                prone);
        }

        private void LieDown()
        {
            _player.ProneRule = Prone.On;
            Assert.That(_player.UpdateProne(true, false, false, false, Vector2.zero), Is.True);
            _player.Tick(Vector2.zero, false, false, false, _step);
            Assert.That(_player.IsProne, Is.True, "lay down");
        }

        // 足元から bottom の高さに下面が来る、厚さ 0.4m の天井を体の真上に置く。
        private void PlaceCeiling(float bottom)
        {
            _ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ceiling.transform.position = new Vector3(0f, FeetY() + bottom + 0.2f, 0f);
            _ceiling.transform.localScale = new Vector3(3f, 0.4f, 3f);
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
                _player.Tick(Vector2.zero, false, false, false, _step);
            }

            Assert.That(_player.IsGrounded, Is.True, "landed on the floor");
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
