using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using Fps.Runtime.Traversal;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using LadderComponent = Fps.Runtime.Traversal.Ladder;
using LadderRule = Fps.Core.Ladder;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// はしご (<see cref="LadderMovement"/>) で、前からつかまって上り上の足場に降りる・上の端からつかまって下り地面に降りる・ジャンプで離れること、
    /// ルールが Off ならつかまらないこと、つかまっている間は武器をしまい <see cref="PlayerActionConditions.OnLadder"/> を立てること。
    /// 実際の <see cref="PlayerController"/> と CharacterController を、床と台の上で動かして確かめる。
    /// </summary>
    public sealed class LadderMovementTests
    {
        private const float StepSeconds = 0.02f;
        private const int MaxTicks = 600;
        private const float Tolerance = 0.15f;

        // はしごは原点に立ち、+Z を向いてつかまる。台は z = 0〜4、高さ 3m。
        private const float TowerHeight = 3f;

        private readonly List<LadderOutcome> _left = new List<LadderOutcome>();
        private readonly List<LadderEntry> _grabbed = new List<LadderEntry>();
        private GameObject _body;
        private CharacterController _characterController;
        private PlayerController _player;
        private InputAction _move;
        private InputAction _sprint;
        private InputAction _crouch;
        private InputAction _jump;
        private GameObject _floor;
        private GameObject _tower;
        private GameObject _ladderObject;
        private LadderComponent _ladder;
        private GameObject _weaponObject;
        private WeaponController _weapon;
        private LadderMovement _movement;

        [SetUp]
        public void SetUp()
        {
            _floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _floor.transform.position = new Vector3(0f, -0.5f, 0f);
            _floor.transform.localScale = new Vector3(40f, 1f, 40f);
            _tower = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _tower.transform.position = new Vector3(0f, TowerHeight / 2f, 2f);
            _tower.transform.localScale = new Vector3(4f, TowerHeight, 4f);

            _ladderObject = new GameObject("Ladder");
            _ladder = _ladderObject.AddComponent<LadderComponent>();
            _ladder.CreatesNavMeshLinks = false;
            _ladder.Height = TowerHeight;
            _ladder.Register();

            _body = new GameObject("Body");
            _body.transform.position = new Vector3(0f, 1f, -0.75f);
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

            _weaponObject = new GameObject("Weapon");
            _weapon = _weaponObject.AddComponent<WeaponController>();

            _movement = _body.AddComponent<LadderMovement>();
            _movement.Bind(_player, _weapon, null);
            _movement.Grabbed += (_, entry) => _grabbed.Add(entry);
            _movement.Left += (_, reason) => _left.Add(reason);
            Physics.SyncTransforms();
            Land();
        }

        [TearDown]
        public void TearDown()
        {
            _move.Dispose();
            _sprint.Dispose();
            _crouch.Dispose();
            _jump.Dispose();
            _ladder.Unregister();
            Object.DestroyImmediate(_body);
            Object.DestroyImmediate(_weaponObject);
            Object.DestroyImmediate(_ladderObject);
            Object.DestroyImmediate(_tower);
            Object.DestroyImmediate(_floor);
        }

        [Test]
        public void TheOverrideIsRegisteredOnceWithTheLadderCondition()
        {
            _movement.Bind(_player, _weapon, null);

            Assert.That(_player.MovementOverrides, Has.Exactly(1).SameAs(_movement));
            Assert.That(_movement.Conditions, Is.EqualTo(PlayerActionConditions.OnLadder));
        }

        [Test]
        public void WithTheRuleOffWalkingIntoTheLadderDoesNotGrabIt()
        {
            _movement.Rule = LadderRule.Off;

            Tick(Vector2.up, 50);

            Assert.That(_grabbed, Is.Empty);
            Assert.That(_movement.IsClimbing, Is.False);
            Assert.That(Feet().y, Is.EqualTo(0f).Within(Tolerance), "still on the ground");
        }

        [Test]
        public void ForwardGrabsClimbsAndStepsOntoThePlatform()
        {
            _movement.Rule = LadderRule.On;
            var sawClimbing = false;

            for (var i = 0; i < MaxTicks && _left.Count == 0; i++)
            {
                _player.Tick(Vector2.up, false, false, false, StepSeconds);
                if (_movement.IsClimbing)
                {
                    sawClimbing = true;
                    Assert.That(_player.MovementOverrideConditions, Is.EqualTo(PlayerActionConditions.OnLadder));
                    Assert.That(_weapon.IsHolstered, Is.True, "the weapon is put away on the ladder");
                    Assert.That(_weapon.IsFireBlocked, Is.True, "and cannot fire");
                    Assert.That(Feet().z, Is.LessThan(0f), "hanging in front of the ladder");
                }
            }

            Assert.That(sawClimbing, Is.True);
            Assert.That(_grabbed, Is.EqualTo(new[] { LadderEntry.Front }));
            Assert.That(_left, Is.EqualTo(new[] { LadderOutcome.ExitTop }));
            Assert.That(Feet().y, Is.EqualTo(TowerHeight).Within(Tolerance), "on the platform");
            Assert.That(Feet().z, Is.GreaterThan(0f));
            Assert.That(_weapon.IsHolstered, Is.False, "the weapon comes back");

            Tick(Vector2.zero, 10);
            Assert.That(_player.IsGrounded, Is.True);
            Assert.That(_player.IsMovementOverridden, Is.False);
        }

        [Test]
        public void BackingOffTheTopGrabsItAndClimbsDownToTheGround()
        {
            _movement.Rule = LadderRule.On;
            Teleport(new Vector3(0f, TowerHeight, 1f));
            Land();

            for (var i = 0; i < MaxTicks && _left.Count == 0; i++)
            {
                _player.Tick(Vector2.down, false, false, false, StepSeconds);
            }

            Assert.That(_grabbed, Is.EqualTo(new[] { LadderEntry.Top }));
            Assert.That(_left, Is.EqualTo(new[] { LadderOutcome.ExitBottom }));
            Assert.That(Feet().y, Is.EqualTo(0f).Within(Tolerance), "on the ground");
            Assert.That(Feet().z, Is.LessThan(0f), "in front of the ladder");
        }

        [Test]
        public void JumpLetsGoAndWaitsBeforeGrabbingAgain()
        {
            _movement.Rule = LadderRule.On;
            Tick(Vector2.up, 20);
            Assert.That(_movement.IsClimbing, Is.True);

            _player.Tick(Vector2.up, false, false, true, StepSeconds);

            Assert.That(_left, Is.EqualTo(new[] { LadderOutcome.JumpOff }));
            Assert.That(_movement.IsClimbing, Is.False);
            Assert.That(_player.Motor.Velocity.Z, Is.LessThan(0f), "pushed away from the ladder");
            Assert.That(_movement.RegrabRemaining, Is.GreaterThan(0f));
            Assert.That(_movement.IsActive, Is.False, "cannot grab again right away");

            _movement.AdvanceCooldown(_ladder.RegrabSeconds);
            Assert.That(_movement.RegrabRemaining, Is.Zero);
        }

        [Test]
        public void TurningTheRuleOffOrRemovingTheLadderLetsGo()
        {
            _movement.Rule = LadderRule.On;
            Tick(Vector2.up, 20);
            Assert.That(_movement.IsClimbing, Is.True);

            _movement.Rule = LadderRule.Off;
            Tick(Vector2.zero, 1);

            Assert.That(_left, Is.EqualTo(new[] { LadderOutcome.Released }));
            Assert.That(_player.IsMovementOverridden, Is.False);
            Assert.That(_weapon.IsHolstered, Is.False);

            _movement.Rule = LadderRule.On;
            _movement.AdvanceCooldown(_ladder.RegrabSeconds);
            Teleport(new Vector3(0f, 0f, -0.75f));
            Land();
            Tick(Vector2.up, 5);
            Assert.That(_movement.IsClimbing, Is.True);

            _ladder.Unregister();
            Tick(Vector2.up, 1);

            Assert.That(_left, Is.EqualTo(new[] { LadderOutcome.Released, LadderOutcome.Released }));
            Assert.That(_movement.IsClimbing, Is.False);
        }

        [Test]
        public void TheInstallerFollowsTheRuleAndAddsNothingWhileOff()
        {
            var other = new GameObject("Other");
            try
            {
                other.AddComponent<CharacterController>();
                var mover = other.AddComponent<PlayerController>();
                var installer = new LadderActionInstaller();

                installer.Install(other, GameRules.Default);
                Assert.That(other.GetComponent<LadderMovement>(), Is.Null, "Off adds nothing");

                installer.Install(other, GameRules.Default.WithActions(ActionRules.Default.WithLadder(LadderRule.On)));
                var movement = other.GetComponent<LadderMovement>();
                Assert.That(movement, Is.Not.Null);
                Assert.That(movement.Rule, Is.EqualTo(LadderRule.On));
                Assert.That(movement.Mover, Is.SameAs(mover));
                Assert.That(mover.MovementOverrides, Has.Exactly(1).SameAs(movement));

                installer.Install(other, GameRules.Default);
                Assert.That(other.GetComponents<LadderMovement>(), Has.Length.EqualTo(1));
                Assert.That(movement.Rule, Is.EqualTo(LadderRule.Off));
            }
            finally
            {
                Object.DestroyImmediate(other);
            }
        }

        [Test]
        public void TheInstallerIsRegisteredWithThePlayerActions()
        {
            Assert.That(PlayerActions.Installers, Has.Some.InstanceOf<LadderActionInstaller>());
        }

        private void Tick(Vector2 move, int count)
        {
            for (var i = 0; i < count; i++)
            {
                _player.Tick(move, false, false, false, StepSeconds);
            }
        }

        private Vector3 Feet()
        {
            return LadderMovement.FeetOf(_player);
        }

        // 足元を feet に移す。CharacterController は有効なままだと Transform の書き換えを次の移動で上書きするので、移す間だけ切る。
        private void Teleport(Vector3 feet)
        {
            var root = feet + (Vector3.up * 0.05f) + (_body.transform.position - Feet());
            _characterController.enabled = false;
            _body.transform.position = root;
            _characterController.enabled = true;
            Physics.SyncTransforms();
        }

        private void Land()
        {
            for (var i = 0; i < 20 && !_player.IsGrounded; i++)
            {
                _player.Tick(Vector2.zero, false, false, false, StepSeconds);
            }

            Assert.That(_player.IsGrounded, Is.True, "landed");
        }
    }
}
