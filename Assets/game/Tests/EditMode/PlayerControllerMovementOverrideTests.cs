using System;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// 移動の横取り (<see cref="IMovementOverride"/>) の選び方と当て方、回避・乗り越えと同時に有効にならないこと、
    /// 操作を手放している間 (<see cref="PlayerController.Handoff"/>) は入力が移動に効かないこと。
    /// </summary>
    public sealed class PlayerControllerMovementOverrideTests
    {
        private const float StepSeconds = 0.02f;

        // 床に押し付けたまま止まる速度。0 だと Move が床に触れず、接地が外れる。
        private static readonly Vector3 _pressDown = new Vector3(0f, -2f, 0f);

        private GameObject _body;
        private CharacterController _characterController;
        private PlayerController _player;
        private InputAction _move;
        private InputAction _sprint;
        private InputAction _crouch;
        private InputAction _jump;
        private GameObject _floor;
        private GameObject _wall;

        [SetUp]
        public void SetUp()
        {
            _body = new GameObject("Body");
            _body.transform.position = new Vector3(0f, 1f, 0f);
            _characterController = _body.AddComponent<CharacterController>();
            _characterController.height = 2f;
            _characterController.radius = 0.5f;
            _characterController.center = Vector3.zero;
            _player = _body.AddComponent<PlayerController>();
            _move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            _sprint = new InputAction("Sprint", InputActionType.Button);
            _crouch = new InputAction("Crouch", InputActionType.Button);
            _jump = new InputAction("Jump", InputActionType.Button);
            var look = _body.AddComponent<FirstPersonLook>();
            _player.Initialize(_characterController, look, _move, _sprint, _crouch, _jump, new MotorSpec(5f, 8f, 1.2f, 20f, 0.1f, 2.5f));
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
        public void NothingIsOverriddenUntilAnActiveOverrideIsRegistered()
        {
            LandOnFloor();
            var idle = new FakeOverride(1) { IsActive = false };
            Assert.That(_player.RegisterMovementOverride(idle), Is.True);
            Assert.That(_player.RegisterMovementOverride(idle), Is.False, "registered once");

            _player.Tick(Vector2.up, false, false, false, StepSeconds);

            Assert.That(_player.IsMovementOverridden, Is.False);
            Assert.That(_player.ActiveMovementOverride, Is.Null);
            Assert.That(_player.MovementOverrideConditions, Is.EqualTo(PlayerActionConditions.None));
            Assert.That(idle.Begins, Is.Zero);
            Assert.That(idle.Steps, Is.Zero);
        }

        [Test]
        public void TheActiveOverrideMovesTheBodyInsteadOfTheMoveInput()
        {
            LandOnFloor();
            var slide = new FakeOverride(1) { IsActive = true, Velocity = new Vector3(2f, -2f, 0f), Conditions = PlayerActionConditions.Sprinting };
            _player.RegisterMovementOverride(slide);
            var start = _body.transform.position;

            for (var i = 0; i < 10; i++)
            {
                _player.Tick(Vector2.up, true, false, false, StepSeconds);
            }

            var moved = _body.transform.position - start;
            Assert.That(moved.x, Is.EqualTo(2f * 10 * StepSeconds).Within(0.01f), "the override's displacement");
            Assert.That(moved.z, Is.EqualTo(0f).Within(0.01f), "the forward input does not move the body");
            Assert.That(_player.Motor.Velocity.X, Is.EqualTo(2f).Within(1e-4f), "the override's velocity is kept for readers");
            Assert.That(_player.MovementOverrideConditions, Is.EqualTo(PlayerActionConditions.Sprinting));
            Assert.That(slide.Begins, Is.EqualTo(1));
            Assert.That(slide.Steps, Is.EqualTo(10));
            Assert.That(slide.LastInput.Move, Is.EqualTo(Vector2.up), "the input is handed to the override");
            Assert.That(slide.LastInput.Sprint, Is.True);
        }

        [Test]
        public void TheHigherPriorityWinsAndTheLowerResumesWhenItStops()
        {
            LandOnFloor();
            var low = new FakeOverride(1) { IsActive = true, Velocity = _pressDown };
            var high = new FakeOverride(5) { IsActive = false, Velocity = _pressDown };
            _player.RegisterMovementOverride(high);
            _player.RegisterMovementOverride(low);

            _player.Tick(Vector2.zero, false, false, false, StepSeconds);
            Assert.That(_player.ActiveMovementOverride, Is.SameAs(low));
            Assert.That(low.Begins, Is.EqualTo(1));

            high.IsActive = true;
            _player.Tick(Vector2.zero, false, false, false, StepSeconds);
            Assert.That(_player.ActiveMovementOverride, Is.SameAs(high));
            Assert.That(low.Ends, Is.EqualTo(1));
            Assert.That(high.Begins, Is.EqualTo(1));
            Assert.That(low.Steps, Is.EqualTo(1), "only the winner steps");
            Assert.That(high.Steps, Is.EqualTo(1));

            high.IsActive = false;
            _player.Tick(Vector2.zero, false, false, false, StepSeconds);
            Assert.That(_player.ActiveMovementOverride, Is.SameAs(low));
            Assert.That(high.Ends, Is.EqualTo(1));
            Assert.That(low.Begins, Is.EqualTo(2));

            low.IsActive = false;
            _player.Tick(Vector2.zero, false, false, false, StepSeconds);
            Assert.That(_player.IsMovementOverridden, Is.False);
            Assert.That(low.Ends, Is.EqualTo(2));
        }

        [Test]
        public void TheFirstRegisteredWinsATie()
        {
            LandOnFloor();
            var first = new FakeOverride(3) { IsActive = true, Velocity = _pressDown };
            var second = new FakeOverride(3) { IsActive = true, Velocity = _pressDown };
            _player.RegisterMovementOverride(first);
            _player.RegisterMovementOverride(second);

            _player.Tick(Vector2.zero, false, false, false, StepSeconds);

            Assert.That(_player.ActiveMovementOverride, Is.SameAs(first));
            Assert.That(second.Begins, Is.Zero);
        }

        [Test]
        public void UnregisteringTheActiveOverrideEndsItAtOnce()
        {
            LandOnFloor();
            var ladder = new FakeOverride(1) { IsActive = true, Velocity = _pressDown };
            _player.RegisterMovementOverride(ladder);
            _player.Tick(Vector2.zero, false, false, false, StepSeconds);

            Assert.That(_player.UnregisterMovementOverride(ladder), Is.True);
            Assert.That(_player.UnregisterMovementOverride(ladder), Is.False);

            Assert.That(ladder.Ends, Is.EqualTo(1));
            Assert.That(_player.IsMovementOverridden, Is.False);
            Assert.That(_player.MovementOverrides, Is.Empty);
        }

        [Test]
        public void ReinitializingEndsTheActiveOverride()
        {
            LandOnFloor();
            var seat = new FakeOverride(1) { IsActive = true, Velocity = _pressDown };
            _player.RegisterMovementOverride(seat);
            _player.Tick(Vector2.zero, false, false, false, StepSeconds);

            _player.Initialize(_characterController, _player.Look, _move, _sprint, _crouch, _jump, _player.Motor.Spec);

            Assert.That(seat.Ends, Is.EqualTo(1));
            Assert.That(_player.IsMovementOverridden, Is.False);
        }

        [Test]
        public void AnOverrideStopsTheDodgeAndNoDodgeStartsDuringIt()
        {
            LandOnFloor();
            _player.DodgeRule = Dodge.On;
            Assert.That(_player.TryStartDodge(new Vector2(1f, 0f)), Is.True);
            var knockback = new FakeOverride(1) { IsActive = true, Velocity = _pressDown };
            _player.RegisterMovementOverride(knockback);

            _player.Tick(Vector2.zero, false, false, false, StepSeconds);

            Assert.That(_player.IsDodging, Is.False, "the override wins over the roll");
            Assert.That(_player.IsGrounded, Is.True);
            Assert.That(_player.TryStartDodge(new Vector2(1f, 0f)), Is.False, "no roll while overridden");
        }

        [Test]
        public void NoVaultStartsDuringAnOverride()
        {
            LandOnFloor();
            _player.VaultRule = Vault.On;
            PlaceLowWallInFront();
            var seat = new FakeOverride(1) { IsActive = true, Velocity = _pressDown };
            _player.RegisterMovementOverride(seat);
            _player.Tick(Vector2.zero, false, false, true, StepSeconds);

            Assert.That(_player.IsVaulting, Is.False, "the jump goes to the override");
            Assert.That(_player.IsGrounded, Is.True);
            Assert.That(_player.TryStartVault(), Is.False);

            _player.UnregisterMovementOverride(seat);
            Assert.That(_player.TryStartVault(), Is.True, "the same wall can be vaulted without the override");
        }

        [Test]
        public void AnOverrideCanPlaceTurnAndCrouchTheBody()
        {
            LandOnFloor();
            var target = new Vector3(3f, 1f, -2f);
            var ladder = new FakeOverride(1)
            {
                IsActive = true,
                Velocity = _pressDown,
                Position = target,
                YawDegrees = 90f,
                Crouched = true,
            };
            _player.RegisterMovementOverride(ladder);

            _player.Tick(Vector2.zero, false, false, false, StepSeconds);

            Assert.That(_body.transform.position.x, Is.EqualTo(target.x).Within(0.01f));
            Assert.That(_body.transform.position.z, Is.EqualTo(target.z).Within(0.01f));
            Assert.That(Mathf.DeltaAngle(_body.transform.eulerAngles.y, 90f), Is.EqualTo(0f).Within(0.01f));
            Assert.That(_player.IsCrouched, Is.True);
        }

        [Test]
        public void AStepRejectsNonFiniteValues()
        {
            Assert.Throws<ArgumentException>(() => MovementOverrideStep.Move(new Vector3(float.NaN, 0f, 0f), Vector3.zero));
            Assert.Throws<ArgumentException>(() => MovementOverrideStep.Move(Vector3.zero, new Vector3(0f, float.PositiveInfinity, 0f)));
            Assert.Throws<ArgumentException>(() => new MovementOverrideStep(Vector3.zero, Vector3.zero, yawDegrees: float.NaN));
            Assert.Throws<ArgumentNullException>(() => _player.RegisterMovementOverride(null));
            Assert.Throws<ArgumentNullException>(() => _player.UnregisterMovementOverride(null));
        }

        [Test]
        public void OverrideLogNamesTheTypeAndState()
        {
            var fake = new FakeOverride(1);

            Assert.That(PlayerController.FormatMovementOverrideLog(fake, true), Is.EqualTo("[Player] override=FakeOverride active=true"));
            Assert.That(PlayerController.FormatMovementOverrideLog(fake, false), Is.EqualTo("[Player] override=FakeOverride active=false"));
        }

        [Test]
        public void InputMovesTheBodyUnlessControlIsHandedOff()
        {
            LandOnFloor();
            var seat = new object();
            var start = _body.transform.position;
            Assert.That(_player.Handoff.Claim(seat), Is.True);
            Assert.That(_player.IsControlHandedOff, Is.True);

            for (var i = 0; i < 10; i++)
            {
                _player.ApplyInput(Pressing(Vector2.up, jump: i == 0), StepSeconds);
            }

            var moved = _body.transform.position - start;
            Assert.That(moved.x, Is.EqualTo(0f).Within(0.01f));
            Assert.That(moved.z, Is.EqualTo(0f).Within(0.01f), "the forward input is ignored");
            Assert.That(_player.Motor.Velocity.Y, Is.LessThanOrEqualTo(0f), "the jump is ignored");
            Assert.That(_player.IsSprinting, Is.False, "the sprint button is ignored");
            Assert.That(_player.IsCrouched, Is.False, "the crouch button is ignored");

            Assert.That(_player.Handoff.Release(seat), Is.True);
            Assert.That(_player.IsControlHandedOff, Is.False);
            var walk = new PlayerMoveInput(
                Vector2.up, false, false, InputDeviceKind.KeyboardMouse, false, false, InputDeviceKind.KeyboardMouse, false, false);
            for (var i = 0; i < 10; i++)
            {
                _player.ApplyInput(walk, StepSeconds);
            }

            Assert.That(_body.transform.position.z - start.z, Is.GreaterThan(0.5f), "the forward input moves the body again");
        }

        [Test]
        public void ApplyInputBeforeInitializeThrows()
        {
            var other = new GameObject("Other");
            try
            {
                other.AddComponent<CharacterController>();
                var mover = other.AddComponent<PlayerController>();

                Assert.Throws<InvalidOperationException>(() => mover.ApplyInput(PlayerMoveInput.Idle(InputDeviceKind.KeyboardMouse, InputDeviceKind.KeyboardMouse), StepSeconds));
            }
            finally
            {
                Object.DestroyImmediate(other);
            }
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
                _player.Tick(Vector2.zero, false, false, false, StepSeconds);
            }

            Assert.That(_player.IsGrounded, Is.True, "landed on the floor");
        }

        // 正面 (+Z) の 0.8m 先に、厚さ 0.4m・高さ 1m の乗り越えられる壁を置く (PlayerControllerTests と同じ寸法)。
        private void PlaceLowWallInFront()
        {
            _wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wall.transform.position = new Vector3(0f, 0.5f, 1f);
            _wall.transform.localScale = new Vector3(3f, 1f, 0.4f);
            Physics.SyncTransforms();
        }

        // ダッシュとしゃがみも押したままにして、手放している間はどれも効かないことを見る。
        private static PlayerMoveInput Pressing(Vector2 move, bool jump)
        {
            return new PlayerMoveInput(
                move, true, true, InputDeviceKind.KeyboardMouse, true, true, InputDeviceKind.KeyboardMouse, jump, false);
        }

        private sealed class FakeOverride : IMovementOverride
        {
            public FakeOverride(int priority)
            {
                Priority = priority;
            }

            public int Priority { get; }
            public bool IsActive { get; set; }
            public PlayerActionConditions Conditions { get; set; }
            public Vector3 Velocity { get; set; }
            public Vector3? Position { get; set; }
            public float? YawDegrees { get; set; }
            public bool? Crouched { get; set; }
            public int Begins { get; private set; }
            public int Ends { get; private set; }
            public int Steps { get; private set; }
            public MovementOverrideInput LastInput { get; private set; }

            public void Begin(PlayerController mover)
            {
                Begins++;
            }

            public MovementOverrideStep Step(PlayerController mover, MovementOverrideInput input, float deltaSeconds)
            {
                Steps++;
                LastInput = input;
                return new MovementOverrideStep(Velocity * deltaSeconds, Velocity, Position, YawDegrees, Crouched);
            }

            public void End(PlayerController mover)
            {
                Ends++;
            }
        }
    }
}
