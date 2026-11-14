using System;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    public sealed class MotorSettingsTests
    {
        private MotorSettings _settings;
        private GameObject _body;
        private InputAction _move;
        private InputAction _sprint;
        private InputAction _crouch;
        private InputAction _jump;

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<MotorSettings>();
            _move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            _sprint = new InputAction("Sprint", InputActionType.Button);
            _crouch = new InputAction("Crouch", InputActionType.Button);
            _jump = new InputAction("Jump", InputActionType.Button);
        }

        [TearDown]
        public void TearDown()
        {
            _move.Dispose();
            _sprint.Dispose();
            _crouch.Dispose();
            _jump.Dispose();
            Object.DestroyImmediate(_settings);
            if (_body != null)
            {
                Object.DestroyImmediate(_body);
            }
        }

        [Test]
        public void NewSettingsHoldTheMotorSpecDefaults()
        {
            var spec = _settings.CreateSpec();

            Assert.That(spec.WalkSpeed, Is.EqualTo(MotorSpec.Default.WalkSpeed));
            Assert.That(spec.SprintSpeed, Is.EqualTo(MotorSpec.Default.SprintSpeed));
            Assert.That(spec.CrouchSpeed, Is.EqualTo(MotorSpec.Default.CrouchSpeed));
            Assert.That(spec.JumpHeight, Is.EqualTo(MotorSpec.Default.JumpHeight));
            Assert.That(spec.Gravity, Is.EqualTo(MotorSpec.Default.Gravity));
            Assert.That(spec.AirControl, Is.EqualTo(MotorSpec.Default.AirControl));
        }

        [Test]
        public void ResolveUsesTheAssignedSettings()
        {
            _settings.WalkSpeed = 4f;
            _settings.SprintSpeed = 9f;
            _settings.CrouchSpeed = 1.5f;

            var spec = MotorSettings.Resolve(_settings);

            Assert.That(spec.WalkSpeed, Is.EqualTo(4f));
            Assert.That(spec.SprintSpeed, Is.EqualTo(9f));
            Assert.That(spec.CrouchSpeed, Is.EqualTo(1.5f));
        }

        [Test]
        public void ResolveWithoutAnyAssetFallsBackToTheDefaultSpec()
        {
            Assume.That(MotorSettings.Load(), Is.Null, "a project asset at Resources/" + MotorSettings.ResourcePath + " overrides the default");

            Assert.That(MotorSettings.Resolve(null), Is.SameAs(MotorSpec.Default));
        }

        [Test]
        public void InvalidSettingsThrowOnResolveButFallBackOnResolveOrDefault()
        {
            _settings.WalkSpeed = -1f;

            Assert.Throws<ArgumentOutOfRangeException>(() => MotorSettings.Resolve(_settings));
            Assert.That(MotorSettings.ResolveOrDefault(_settings), Is.SameAs(MotorSpec.Default));
        }

        [Test]
        public void PlayerControllerReadsItsSettingsBeforeAndAfterInitializing()
        {
            _settings.WalkSpeed = 4f;
            _settings.CrouchSpeed = 1.5f;
            var player = CreatePlayer();
            player.MotorSettings = _settings;

            Assert.That(player.WalkSpeed, Is.EqualTo(4f));
            Assert.That(player.CrouchSpeed, Is.EqualTo(1.5f));

            Initialize(player, player.CreateSpec());

            Assert.That(player.Motor.Spec.WalkSpeed, Is.EqualTo(4f));
            Assert.That(player.WalkSpeed, Is.EqualTo(4f));
            Assert.That(player.CrouchSpeed, Is.EqualTo(1.5f));
        }

        [Test]
        public void PlayerControllerReportsTheSpecItWasInitializedWith()
        {
            var player = CreatePlayer();
            player.MotorSettings = _settings;

            Initialize(player, new MotorSpec(3f, 6f, 1f, 15f, 0.2f, 1.2f));

            Assert.That(player.Spec, Is.SameAs(player.Motor.Spec));
            Assert.That(player.WalkSpeed, Is.EqualTo(3f));
            Assert.That(player.SprintSpeed, Is.EqualTo(6f));
            Assert.That(player.CrouchSpeed, Is.EqualTo(1.2f));
            Assert.That(player.JumpHeight, Is.EqualTo(1f));
            Assert.That(player.Gravity, Is.EqualTo(15f));
            Assert.That(player.AirControl, Is.EqualTo(0.2f));
        }

        private PlayerController CreatePlayer()
        {
            _body = new GameObject("Body");
            _body.transform.position = new Vector3(0f, 1f, 0f);
            var controller = _body.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.5f;
            controller.center = Vector3.zero;
            _body.AddComponent<FirstPersonLook>();
            return _body.AddComponent<PlayerController>();
        }

        private void Initialize(PlayerController player, MotorSpec spec)
        {
            player.Initialize(
                _body.GetComponent<CharacterController>(), _body.GetComponent<FirstPersonLook>(), _move, _sprint, _crouch, _jump, spec);
        }
    }
}
