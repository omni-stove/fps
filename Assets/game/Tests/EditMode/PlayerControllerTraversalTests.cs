using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;
using NVector3 = System.Numerics.Vector3;
using WaterVolumes = Shooter.Runtime.Traversal.WaterVolumes;

namespace Shooter.Tests.EditMode
{
    // 床の上面は y = 0。体は高さ 2・半径 0.5・中心 0 で、目は中心から 0.6 上なので、胸は本体から 0.3 上になる (SwimStep.ChestY)。
    public sealed class PlayerControllerTraversalTests
    {
        private const float _tick = 0.02f;
        private const int _maxTicks = 1000;
        private const int _maxHealth = 100;
        private const float _deepSurfaceY = 2.5f;

        // 水面 2.5 から胸の 0.3 を引いた本体の高さに浮く
        private const float _floatY = _deepSurfaceY - 0.3f;

        private readonly List<Object> _created = new List<Object>();
        private readonly List<bool> _swimChanges = new List<bool>();
        private GameObject _body;
        private CharacterController _controller;
        private PlayerController _player;
        private Damageable _damageable;
        private InputAction _move;
        private InputAction _sprint;
        private InputAction _crouch;
        private InputAction _jump;

        [SetUp]
        public void SetUp()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(40f, 1f, 40f);
            _created.Add(floor);

            _body = new GameObject("Swimmer");
            _created.Add(_body);
            _body.transform.position = new Vector3(0f, 1f, 0f);
            _controller = _body.AddComponent<CharacterController>();
            _controller.height = 2f;
            _controller.radius = 0.5f;
            _controller.center = Vector3.zero;

            var eye = new GameObject(GameBootstrap.EyeName).transform;
            eye.SetParent(_body.transform, false);
            eye.localPosition = new Vector3(0f, 0.6f, 0f);

            _player = _body.AddComponent<PlayerController>();
            _damageable = _body.AddComponent<Damageable>();
            _damageable.Initialize("Swimmer", _maxHealth);
            _move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            _sprint = new InputAction("Sprint", InputActionType.Button);
            _crouch = new InputAction("Crouch", InputActionType.Button);
            _jump = new InputAction("Jump", InputActionType.Button);
            var look = _body.AddComponent<FirstPersonLook>();
            _player.Initialize(_controller, look, _move, _sprint, _crouch, _jump, Spec());
            _player.Water = NoWaterVolumes.Instance;
            _player.SwimTuning = SwimTuning.Default;
            _player.SwimmingChanged += _swimChanges.Add;
        }

        [TearDown]
        public void TearDown()
        {
            _move.Dispose();
            _sprint.Dispose();
            _crouch.Dispose();
            _jump.Dispose();
            for (var i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                {
                    Object.DestroyImmediate(_created[i]);
                }
            }

            _created.Clear();
            _swimChanges.Clear();
        }

        [Test]
        public void WaterAboveTheChestStartsASwimAndTheBuoyancyFloatsTheChestAtTheSurface()
        {
            _player.Water = Pool(_deepSurfaceY);

            _player.Tick(Vector2.zero, false, false, false, _tick);

            Assert.That(_player.IsSwimming, Is.True);
            Assert.That(_player.Swim.SurfaceY, Is.EqualTo(_deepSurfaceY));
            Assert.That(_swimChanges, Is.EqualTo(new[] { true }));
            Assert.That(_player.TryGetMotorState(out var state), Is.True);
            Assert.That(state.IsSwimming, Is.True);
            Assert.That(state.CanAimDownSights, Is.False);

            TickFor(4f);

            Assert.That(_player.IsSwimming, Is.True, "floating keeps the swim");
            Assert.That(_body.transform.position.y, Is.EqualTo(_floatY).Within(0.01f));
        }

        [Test]
        public void WithoutWaterNobodySwims()
        {
            TickFor(0.5f);

            Assert.That(_player.IsSwimming, Is.False);
            Assert.That(_swimChanges, Is.Empty);
            Assert.That(_player.TryGetMotorState(out var state), Is.True);
            Assert.That(state.CanAimDownSights, Is.True);
        }

        [Test]
        public void CrouchDivesAndJumpRisesAtTheSwimSpeed()
        {
            _player.Water = Pool(_deepSurfaceY);
            TickFor(4f);
            var floating = _body.transform.position.y;

            _player.Tick(Vector2.zero, false, true, false, _tick);
            var dived = _body.transform.position.y;
            _player.Tick(Vector2.zero, false, false, true, _tick);
            var risen = _body.transform.position.y;

            Assert.That(dived, Is.EqualTo(floating - (SwimTuning.DefaultSpeed * _tick)).Within(1e-3f));
            Assert.That(risen, Is.EqualTo(dived + (SwimTuning.DefaultSpeed * _tick)).Within(1e-3f));
        }

        [Test]
        public void LeavingTheWaterEndsTheSwimAndTheFeetCountAsGroundedAgain()
        {
            var motion = (ICharacterMotionSource)_player;
            _player.Water = Pool(_deepSurfaceY);
            TickFor(0.2f);
            Assert.That(motion.IsGrounded, Is.False, "a swimmer is not standing for the footsteps");

            _player.Water = NoWaterVolumes.Instance;
            TickFor(2f);

            Assert.That(_player.IsSwimming, Is.False);
            Assert.That(_swimChanges, Is.EqualTo(new[] { true, false }));
            Assert.That(motion.IsGrounded, Is.True, "fell back to the floor");
        }

        [Test]
        public void EnteringTheWaterReleasesTheCrouchAndTheSprintAndStandsUp()
        {
            Assert.That(_player.UpdateCrouch(true, true, false, false, InputDeviceKind.KeyboardMouse), Is.True);
            _player.Tick(Vector2.zero, false, true, false, _tick);
            Assert.That(_player.IsCrouched, Is.True);

            _player.Water = Pool(_deepSurfaceY);
            _player.Tick(Vector2.zero, false, false, false, _tick);

            Assert.That(_player.IsCrouchActive, Is.False);
            Assert.That(_player.IsSprinting, Is.False);
            Assert.That(_player.CurrentStance, Is.EqualTo(PlayerStance.Standing));
        }

        [Test]
        public void ApplyInputTurnsTheCrouchButtonIntoADiveWhileSwimming()
        {
            _player.Water = Pool(_deepSurfaceY);
            TickFor(4f);
            var floating = _body.transform.position.y;

            var dive = new PlayerMoveInput(
                Vector2.zero, false, false, InputDeviceKind.KeyboardMouse, true, true, InputDeviceKind.KeyboardMouse, false, false);
            _player.ApplyInput(dive, _tick);

            Assert.That(_body.transform.position.y, Is.LessThan(floating));
            Assert.That(_player.IsCrouchActive, Is.False, "the crouch toggle is not used under water");
        }

        [Test]
        public void HoldingJumpKeepsRisingAtTheSwimSpeedWhileSwimming()
        {
            _player.Water = Pool(_deepSurfaceY);
            TickFor(4f);
            for (var i = 0; i < 10; i++)
            {
                _player.Tick(Vector2.zero, false, true, false, _tick);
            }

            var dived = _body.transform.position.y;
            var held = new PlayerMoveInput(
                Vector2.zero, false, false, InputDeviceKind.KeyboardMouse, false, false, InputDeviceKind.KeyboardMouse, false, false, false, 0, true);
            _player.ApplyInput(held, _tick);
            var first = _body.transform.position.y;
            _player.ApplyInput(held, _tick);

            Assert.That(first, Is.EqualTo(dived + (SwimTuning.DefaultSpeed * _tick)).Within(1e-3f));
            Assert.That(_body.transform.position.y, Is.EqualTo(first + (SwimTuning.DefaultSpeed * _tick)).Within(1e-3f), "not only on the press");
        }

        [Test]
        public void ALandingInShallowWaterDealsNoFallDamage()
        {
            _player.FallDamage = FallDamage.On;

            // 水面 0.5 は胸 (立って 1.3) より低いので泳がず、足元だけが水に入る
            _player.Water = Pool(0.5f);
            DropFrom(12f);

            Assert.That(_player.IsSwimming, Is.False);
            Assert.That(_damageable.Health.Current, Is.EqualTo(_maxHealth));
        }

        [Test]
        public void TheSameLandingOnDryGroundHurts()
        {
            _player.FallDamage = FallDamage.On;

            DropFrom(12f);

            Assert.That(_damageable.Health.Current, Is.LessThan(_maxHealth));
        }

        [Test]
        public void HoldingALeanTiltsTowardItAndReleasingItComesBack()
        {
            Settle();

            _player.Tick(Vector2.zero, false, false, false, 1, _tick);
            var first = _player.Lean.Amount;
            TickFor(1f, lean: 1);
            var held = _player.Lean.Amount;
            TickFor(1f, lean: 0);

            Assert.That(first, Is.EqualTo(MotorSpec.DefaultLeanSpeed * _tick).Within(1e-4f), "approaches at the lean speed");
            Assert.That(held, Is.EqualTo(1f));
            Assert.That(_player.Lean.Amount, Is.EqualTo(0f));

            TickFor(1f, lean: -1);
            Assert.That(_player.Lean.Amount, Is.EqualTo(-1f));
            Assert.That(_player.TryGetMotorState(out var state), Is.True);
            Assert.That(state.Lean.Amount, Is.EqualTo(-1f), "the view rig reads the lean from the motor state");

            var source = _body.AddComponent<PlayerMotorStateSource>();
            Assert.That(source.TryGetMotorState(out var published), Is.True);
            Assert.That(published.Lean.Amount, Is.EqualTo(-1f));
            Assert.That(source.Spec, Is.SameAs(_player.Spec));
        }

        [Test]
        public void AWallBesideStopsTheLeanShortOfIt()
        {
            Settle();

            // 体の右 (+x) の面から 0.2 m 先に壁を置く。リーンで横へ出る 0.4 m より近い
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _created.Add(wall);
            wall.transform.position = new Vector3(0.8f, 1.5f, 0f);
            wall.transform.localScale = new Vector3(0.2f, 3f, 3f);
            Physics.SyncTransforms();

            TickFor(1f, lean: 1);

            Assert.That(_player.Lean.Amount, Is.GreaterThan(0f));
            Assert.That(_player.Lean.Amount, Is.LessThan(1f));
        }

        [Test]
        public void TheLeanIsIgnoredWhileSwimming()
        {
            _player.Water = Pool(_deepSurfaceY);

            TickFor(1f, lean: 1);

            Assert.That(_player.IsSwimming, Is.True);
            Assert.That(_player.Lean.Amount, Is.EqualTo(0f));
        }

        [Test]
        public void TickRejectsALeanOutsideMinusOneToOne()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _player.Tick(Vector2.zero, false, false, false, 2, _tick));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerMoveInput(
                Vector2.zero, false, false, InputDeviceKind.KeyboardMouse, false, false, InputDeviceKind.KeyboardMouse, false, false, false, -2));
        }

        [Test]
        public void ADoubleJumpUsesTheAirJumpsAndLandingGivesThemBack()
        {
            var look = _body.GetComponent<FirstPersonLook>();
            _player.Initialize(_controller, look, _move, _sprint, _crouch, _jump, Spec(airJumps: 1));
            Settle();

            _player.Tick(Vector2.zero, false, false, true, _tick);
            TickFor(0.1f);
            Assert.That(_player.IsGrounded, Is.False, "in the air after the ground jump");
            Assert.That(_player.AirJumpsUsed, Is.EqualTo(0));

            _player.Tick(Vector2.zero, false, false, true, _tick);
            Assert.That(_player.AirJumpsUsed, Is.EqualTo(1));
            Assert.That(_player.Motor.Velocity.Y, Is.EqualTo(MotorSpec.DefaultAirJumpSpeed - (20f * _tick)).Within(1e-3f));

            _player.Tick(Vector2.zero, false, false, true, _tick);
            Assert.That(_player.AirJumpsUsed, Is.EqualTo(1), "no third jump");

            Settle();
            Assert.That(_player.AirJumpsUsed, Is.EqualTo(0));
        }

        [Test]
        public void WithoutAirJumpsAJumpInTheAirDoesNothing()
        {
            Settle();

            _player.Tick(Vector2.zero, false, false, true, _tick);
            TickFor(0.1f);
            var before = _player.Motor.Velocity.Y;
            _player.Tick(Vector2.zero, false, false, true, _tick);

            Assert.That(_player.AirJumpsUsed, Is.EqualTo(0));
            Assert.That(_player.Motor.Velocity.Y, Is.EqualTo(before - (20f * _tick)).Within(1e-3f));
        }

        [Test]
        public void TheSwimmingLogHasTheTag()
        {
            Assert.That(PlayerController.FormatSwimmingLog(true, 2.5f), Is.EqualTo("[Player] swimming=true surface=2.50"));
            Assert.That(PlayerController.FormatSwimmingLog(false, 2.5f), Is.EqualTo("[Player] swimming=false"));
        }

        private static MotorSpec Spec(int airJumps = MotorSpec.DefaultAirJumps)
        {
            return new MotorSpec(5f, 8f, 1.2f, 20f, 0.1f, 2.5f, 10f, 20f, airJumps: airJumps);
        }

        private static WaterVolumes Pool(float surfaceY)
        {
            return new WaterVolumes(new[] { new WaterBox(new NVector3(-20f, -1f, -20f), new NVector3(20f, 4f, 20f), surfaceY) });
        }

        private void TickFor(float seconds, int lean = 0)
        {
            var ticks = Mathf.RoundToInt(seconds / _tick);
            for (var i = 0; i < ticks; i++)
            {
                _player.Tick(Vector2.zero, false, false, false, lean, _tick);
            }
        }

        private void Settle()
        {
            for (var i = 0; i < _maxTicks; i++)
            {
                _player.Tick(Vector2.zero, false, false, false, _tick);
                if (_player.IsGrounded && _player.Motor.Velocity.Y <= 0f)
                {
                    return;
                }
            }

            Assert.Fail("the player never landed");
        }

        private void DropFrom(float height)
        {
            _body.transform.position = new Vector3(0f, 1f + height, 0f);
            Physics.SyncTransforms();

            for (var i = 0; i < _maxTicks; i++)
            {
                _player.Tick(Vector2.zero, false, false, false, _tick);
                if (i > 0 && _player.IsGrounded)
                {
                    return;
                }
            }

            Assert.Fail("the player never landed");
        }
    }
}
