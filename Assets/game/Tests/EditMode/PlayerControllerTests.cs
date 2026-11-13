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
    public sealed class PlayerControllerTests
    {
        private GameObject _body;
        private CharacterController _characterController;
        private FirstPersonLook _look;
        private PlayerController _player;
        private InputAction _move;
        private InputAction _sprint;
        private InputAction _jump;
        private MotorSpec _spec;

        [SetUp]
        public void SetUp()
        {
            _body = new GameObject("Body");
            _characterController = _body.AddComponent<CharacterController>();
            _player = _body.AddComponent<PlayerController>();
            _move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            _sprint = new InputAction("Sprint", InputActionType.Button);
            _jump = new InputAction("Jump", InputActionType.Button);
            _spec = new MotorSpec(5f, 8f, 1.2f, 20f, 0.1f);
        }

        [TearDown]
        public void TearDown()
        {
            _move.Dispose();
            _sprint.Dispose();
            _jump.Dispose();
            Object.DestroyImmediate(_body);
        }

        [Test]
        public void SettingsHaveCodeDefaults()
        {
            var spec = _player.CreateSpec();

            Assert.That(spec.WalkSpeed, Is.EqualTo(PlayerController.DefaultWalkSpeed));
            Assert.That(spec.SprintSpeed, Is.EqualTo(PlayerController.DefaultSprintSpeed));
            Assert.That(spec.JumpHeight, Is.EqualTo(PlayerController.DefaultJumpHeight));
            Assert.That(spec.Gravity, Is.EqualTo(PlayerController.DefaultGravity));
            Assert.That(spec.AirControl, Is.EqualTo(PlayerController.DefaultAirControl));
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
        public void TickBeforeInitializeThrows()
        {
            Assert.That(_player.IsInitialized, Is.False);
            Assert.Throws<InvalidOperationException>(() => _player.Tick(Vector2.zero, false, false, 0.016f));
        }

        [Test]
        public void InitializeRejectsMissingReferences()
        {
            _look = _body.AddComponent<FirstPersonLook>();

            Assert.Throws<ArgumentNullException>(() => _player.Initialize(null, _look, _move, _sprint, _jump, _spec));
            Assert.Throws<ArgumentNullException>(() => _player.Initialize(_characterController, null, _move, _sprint, _jump, _spec));
            Assert.Throws<ArgumentNullException>(() => _player.Initialize(_characterController, _look, null, _sprint, _jump, _spec));
            Assert.Throws<ArgumentNullException>(() => _player.Initialize(_characterController, _look, _move, null, _jump, _spec));
            Assert.Throws<ArgumentNullException>(() => _player.Initialize(_characterController, _look, _move, _sprint, null, _spec));
            Assert.Throws<ArgumentNullException>(() => _player.Initialize(_characterController, _look, _move, _sprint, _jump, null));
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

                Assert.Throws<ArgumentException>(() => _player.Initialize(otherController, _look, _move, _sprint, _jump, _spec));
                Assert.Throws<ArgumentException>(() => _player.Initialize(_characterController, otherLook, _move, _sprint, _jump, _spec));
                Assert.That(_player.IsInitialized, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(stranger);
            }
        }

        [Test]
        public void InitializeStoresReferencesAndBuildsMotor()
        {
            _look = _body.AddComponent<FirstPersonLook>();

            _player.Initialize(_characterController, _look, _move, _sprint, _jump, _spec);

            Assert.That(_player.IsInitialized, Is.True);
            Assert.That(_player.Controller, Is.SameAs(_characterController));
            Assert.That(_player.Look, Is.SameAs(_look));
            Assert.That(_player.MoveAction, Is.SameAs(_move));
            Assert.That(_player.SprintAction, Is.SameAs(_sprint));
            Assert.That(_player.JumpAction, Is.SameAs(_jump));
            Assert.That(_player.Motor.Spec, Is.SameAs(_spec));
        }

        [Test]
        public void ResolvingWithoutLookLogsAnError()
        {
            LogAssert.Expect(LogType.Error, new Regex(@"\[Player\] .*FirstPersonLook"));

            Assert.That(_player.TryInitializeFromHierarchy(), Is.False);
            Assert.That(_player.IsInitialized, Is.False);
        }

        [Test]
        public void ResolvesProjectWideMoveSprintJumpActions()
        {
            _look = _body.AddComponent<FirstPersonLook>();
            var actions = InputSystem.actions;

            Assert.That(actions, Is.Not.Null, "Project-wide Input Actions must be assigned");
            Assert.That(_player.TryInitializeFromHierarchy(), Is.True);
            Assert.That(_player.Controller, Is.SameAs(_characterController));
            Assert.That(_player.Look, Is.SameAs(_look));
            Assert.That(_player.MoveAction, Is.SameAs(actions.FindAction(PlayerController.MoveActionPath)));
            Assert.That(_player.SprintAction, Is.SameAs(actions.FindAction(PlayerController.SprintActionPath)));
            Assert.That(_player.JumpAction, Is.SameAs(actions.FindAction(PlayerController.JumpActionPath)));
        }
    }
}
