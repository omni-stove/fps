using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class MotorSettingsDodgeVaultTests
    {
        private MotorSettings _settings;
        private NetMatchDefinition _match;
        private GameObject _body;

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<MotorSettings>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_settings);
            if (_match != null)
            {
                Object.DestroyImmediate(_match);
            }

            if (_body != null)
            {
                Object.DestroyImmediate(_body);
            }
        }

        [Test]
        public void NewSettingsHoldTheDefaults()
        {
            var spec = _settings.CreateSpec();

            Assert.That(spec.DodgeCooldownSeconds, Is.EqualTo(0.5f));
            Assert.That(spec.VaultSpeed, Is.EqualTo(6f));
            Assert.That(spec.VaultReach, Is.EqualTo(1f));
            Assert.That(spec.VaultMaxAngleDegrees, Is.EqualTo(45f));
            Assert.That(spec.VaultMinHeight, Is.EqualTo(0.3f));
            Assert.That(spec.VaultMaxDepth, Is.EqualTo(1f));
        }

        [Test]
        public void ResolveCarriesTheValues()
        {
            SetValues();

            var spec = MotorSettings.Resolve(_settings);

            Assert.That(spec.DodgeCooldownSeconds, Is.EqualTo(2f));
            Assert.That(spec.VaultSpeed, Is.EqualTo(3f));
            Assert.That(spec.VaultReach, Is.EqualTo(1.4f));
            Assert.That(spec.VaultMaxAngleDegrees, Is.EqualTo(30f));
            Assert.That(spec.VaultMinHeight, Is.EqualTo(0.5f));
            Assert.That(spec.VaultMaxDepth, Is.EqualTo(0.6f));
        }

        [Test]
        public void ResetRestoresTheDefaults()
        {
            SetValues();

            _settings.ResetToDefaults();

            var spec = _settings.CreateSpec();
            Assert.That(spec.DodgeCooldownSeconds, Is.EqualTo(MotorSpec.DefaultDodgeCooldownSeconds));
            Assert.That(spec.VaultSpeed, Is.EqualTo(MotorSpec.DefaultVaultSpeed));
            Assert.That(spec.VaultReach, Is.EqualTo(MotorSpec.DefaultVaultReach));
            Assert.That(spec.VaultMaxAngleDegrees, Is.EqualTo(MotorSpec.DefaultVaultMaxAngleDegrees));
            Assert.That(spec.VaultMinHeight, Is.EqualTo(MotorSpec.DefaultVaultMinHeight));
            Assert.That(spec.VaultMaxDepth, Is.EqualTo(MotorSpec.DefaultVaultMaxDepth));
        }

        [Test]
        public void InvalidValuesThrow()
        {
            _settings.VaultSpeed = 0f;
            Assert.Throws<System.ArgumentOutOfRangeException>(() => MotorSettings.Resolve(_settings));

            _settings.ResetToDefaults();
            _settings.DodgeCooldownSeconds = -1f;
            Assert.Throws<System.ArgumentOutOfRangeException>(() => MotorSettings.Resolve(_settings));
        }

        [Test]
        public void NetMatchToMotorCarriesTheValues()
        {
            SetValues();
            _match = ScriptableObject.CreateInstance<NetMatchDefinition>();
            _match.MotorSettings = _settings;

            var motor = _match.ToMotor().Motor;

            Assert.That(motor.DodgeCooldownSeconds, Is.EqualTo(2f));
            Assert.That(motor.VaultSpeed, Is.EqualTo(3f));
            Assert.That(motor.VaultMaxDepth, Is.EqualTo(0.6f));
        }

        [Test]
        public void PlayerControllerWaitsForTheConfiguredDodgeCooldown()
        {
            SetValues();
            var player = CreatePlayer();
            player.MotorSettings = _settings;

            Assert.That(player.TryInitializeFromHierarchy(), Is.True);

            Assert.That(player.DodgeMotion.CooldownSeconds, Is.EqualTo(2f));
            Assert.That(player.Spec.VaultSpeed, Is.EqualTo(3f));
        }

        private void SetValues()
        {
            _settings.DodgeCooldownSeconds = 2f;
            _settings.VaultSpeed = 3f;
            _settings.VaultReach = 1.4f;
            _settings.VaultMaxAngleDegrees = 30f;
            _settings.VaultMinHeight = 0.5f;
            _settings.VaultMaxDepth = 0.6f;
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
    }
}
