using System;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class MotorSettingsTraversalTests
    {
        private const float Tolerance = 1e-5f;

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
        public void NewSettingsHoldTheTraversalDefaults()
        {
            var spec = _settings.CreateSpec();

            Assert.That(spec.SafeFallSpeed, Is.EqualTo(MotorSpec.DefaultSafeFallSpeed));
            Assert.That(spec.LethalFallSpeed, Is.EqualTo(MotorSpec.DefaultLethalFallSpeed));
            Assert.That(spec.DodgeDistance, Is.EqualTo(MotorSpec.DefaultDodgeDistance));
            Assert.That(spec.DodgeSeconds, Is.EqualTo(MotorSpec.DefaultDodgeSeconds));
            Assert.That(spec.VaultMaxHeight, Is.EqualTo(MotorSpec.DefaultVaultMaxHeight));
            Assert.That(spec.MantleMaxHeight, Is.EqualTo(MotorSpec.DefaultMantleMaxHeight));
            Assert.That(spec.MantleSpeed, Is.EqualTo(MotorSpec.DefaultMantleSpeed));
        }

        [Test]
        public void NewSettingsHoldTheProneDefaults()
        {
            var prone = _settings.CreateProneSpec();

            Assert.That(prone.Height, Is.EqualTo(ProneSpec.DefaultHeight));
            Assert.That(prone.Speed, Is.EqualTo(ProneSpec.DefaultSpeed));
            Assert.That(prone.MaxPitchDegrees, Is.EqualTo(ProneSpec.DefaultMaxPitchDegrees));
            Assert.That(prone.SpreadScale, Is.EqualTo(ProneSpec.DefaultSpreadScale));
            Assert.That(prone.DiveDistance, Is.EqualTo(ProneSpec.DefaultDiveDistance));
            Assert.That(prone.DiveSeconds, Is.EqualTo(ProneSpec.DefaultDiveSeconds));
        }

        [Test]
        public void ResolveCarriesTheFallDodgeVaultAndMantleValues()
        {
            SetTraversal();

            var spec = MotorSettings.Resolve(_settings);

            Assert.That(spec.SafeFallSpeed, Is.EqualTo(10f));
            Assert.That(spec.LethalFallSpeed, Is.EqualTo(30f));
            Assert.That(spec.DodgeDistance, Is.EqualTo(6f));
            Assert.That(spec.DodgeSeconds, Is.EqualTo(0.5f));
            Assert.That(spec.VaultMaxHeight, Is.EqualTo(1f));
            Assert.That(spec.MantleMaxHeight, Is.EqualTo(2.5f));
            Assert.That(spec.MantleSpeed, Is.EqualTo(4f));
        }

        [Test]
        public void ResolveProneCarriesTheProneValues()
        {
            _settings.Prone = new ProneSpec(1.1f, 2.4f, 20f, 0.25f, 4f, 0.8f);

            var prone = MotorSettings.ResolveProne(_settings);

            Assert.That(prone.Height, Is.EqualTo(1.1f));
            Assert.That(prone.Speed, Is.EqualTo(2.4f));
            Assert.That(prone.MaxPitchDegrees, Is.EqualTo(20f));
            Assert.That(prone.SpreadScale, Is.EqualTo(0.25f));
            Assert.That(prone.DiveDistance, Is.EqualTo(4f));
            Assert.That(prone.DiveSeconds, Is.EqualTo(0.8f));
        }

        [Test]
        public void ResetRestoresTheTraversalAndProneDefaults()
        {
            SetTraversal();
            _settings.Prone = new ProneSpec(1.1f, 2.4f, 20f, 0.25f, 4f, 0.8f);

            _settings.ResetToDefaults();

            Assert.That(_settings.CreateSpec().DodgeDistance, Is.EqualTo(MotorSpec.DefaultDodgeDistance));
            Assert.That(_settings.CreateSpec().LethalFallSpeed, Is.EqualTo(MotorSpec.DefaultLethalFallSpeed));
            Assert.That(_settings.CreateSpec().MantleSpeed, Is.EqualTo(MotorSpec.DefaultMantleSpeed));
            Assert.That(_settings.CreateProneSpec().Speed, Is.EqualTo(ProneSpec.DefaultSpeed));
            Assert.That(_settings.CreateProneSpec().DiveSeconds, Is.EqualTo(ProneSpec.DefaultDiveSeconds));
        }

        [Test]
        public void InvalidTraversalOrProneValuesThrow()
        {
            _settings.LethalFallSpeed = _settings.SafeFallSpeed;
            Assert.Throws<ArgumentOutOfRangeException>(() => MotorSettings.Resolve(_settings));

            _settings.ResetToDefaults();
            var serialized = new SerializedObject(_settings);
            serialized.FindProperty("_proneSpeed").floatValue = -1f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.Throws<ArgumentOutOfRangeException>(() => MotorSettings.ResolveProne(_settings));
        }

        [Test]
        public void ResolveProneWithoutAnyAssetFallsBackToTheDefault()
        {
            Assume.That(MotorSettings.Load(), Is.Null, "a project asset at Resources/" + MotorSettings.ResourcePath + " overrides the default");

            Assert.That(MotorSettings.ResolveProne(null), Is.SameAs(ProneSpec.Default));
        }

        [Test]
        public void NetMatchToMotorCarriesTheMotorAndProneSettings()
        {
            SetTraversal();
            _settings.Prone = new ProneSpec(1.1f, 2.4f, 20f, 0.25f, 4f, 0.8f);
            _match = ScriptableObject.CreateInstance<NetMatchDefinition>();
            _match.MotorSettings = _settings;

            var motor = _match.ToMotor();

            Assert.That(motor.Motor.DodgeDistance, Is.EqualTo(6f));
            Assert.That(motor.Motor.SafeFallSpeed, Is.EqualTo(10f));
            Assert.That(motor.Motor.MantleMaxHeight, Is.EqualTo(2.5f));
            Assert.That(motor.Prone.Speed, Is.EqualTo(2.4f));
            Assert.That(motor.Prone.DiveDistance, Is.EqualTo(4f));
            Assert.That(motor.Prone.MaxPitchDegrees, Is.EqualTo(20f));
            Assert.That(motor.Stance.Prone.Height, Is.EqualTo(1.1f).Within(Tolerance));
        }

        [Test]
        public void NetMatchToMotorFitsAProneHeightBelowTheCapsule()
        {
            // 既定の半径 0.5 m のカプセルは 1 m より低くできない。
            _settings.Prone = new ProneSpec(height: 0.6f);
            _match = ScriptableObject.CreateInstance<NetMatchDefinition>();
            _match.MotorSettings = _settings;

            var motor = _match.ToMotor();

            Assert.That(motor.Stance.Prone.Height, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(motor.Prone.Height, Is.EqualTo(0.6f));
        }

        [Test]
        public void NetMatchToMotorCarriesTheSameParachuteTuningAsOffline()
        {
            _match = ScriptableObject.CreateInstance<NetMatchDefinition>();
            _match.MotorSettings = _settings;

            var motor = _match.ToMotor();
            var offline = TraversalTuning.Current.Parachute;

            Assert.That(motor.Parachute.MinOpenHeight, Is.EqualTo(offline.MinOpenHeight));
            Assert.That(motor.Parachute.DescentSpeed, Is.EqualTo(offline.DescentSpeed));
            Assert.That(motor.Parachute.HorizontalSpeed, Is.EqualTo(offline.HorizontalSpeed));
            Assert.That(motor.Parachute.SteerAcceleration, Is.EqualTo(offline.SteerAcceleration));
        }

        [Test]
        public void NetMatchToMotorCarriesTheSameSlideTuningAsOffline()
        {
            _match = ScriptableObject.CreateInstance<NetMatchDefinition>();
            _match.MotorSettings = _settings;

            var motor = _match.ToMotor();

            var offline = TraversalTuning.Current.Slide;

            Assert.That(motor.Slide.StartSpeed, Is.EqualTo(offline.StartSpeed));
            Assert.That(motor.Slide.Deceleration, Is.EqualTo(offline.Deceleration));
            Assert.That(motor.Slide.EndSpeed, Is.EqualTo(offline.EndSpeed));
            Assert.That(motor.Slide.MinSeconds, Is.EqualTo(offline.MinSeconds));
            Assert.That(motor.Slide.MaxSeconds, Is.EqualTo(offline.MaxSeconds));
            Assert.That(motor.Slide.SlopeAcceleration, Is.EqualTo(offline.SlopeAcceleration));
        }

        [Test]
        public void PlayerControllerReadsTheProneFromItsSettings()
        {
            _settings.Prone = new ProneSpec(1.1f, 2.4f, 20f, 0.25f, 4f, 0.8f);
            var player = CreatePlayer();
            player.MotorSettings = _settings;

            Assert.That(player.TryInitializeFromHierarchy(), Is.True);

            Assert.That(player.ProneSpec.Speed, Is.EqualTo(2.4f));
            Assert.That(player.ProneSpec.DiveDistance, Is.EqualTo(4f));
            Assert.That(player.Stance.Prone.Height, Is.EqualTo(1.1f).Within(Tolerance));
        }

        [Test]
        public void PlayerControllerKeepsAProneAssignedInCode()
        {
            _settings.Prone = new ProneSpec(1.1f, 2.4f, 20f, 0.25f, 4f, 0.8f);
            var player = CreatePlayer();
            player.MotorSettings = _settings;
            var assigned = new ProneSpec(speed: 0.7f);
            player.ProneSpec = assigned;

            Assert.That(player.TryInitializeFromHierarchy(), Is.True);

            Assert.That(player.ProneSpec, Is.SameAs(assigned));
        }

        [Test]
        public void MockPoseShrinksToThePlayersStanceHeights()
        {
            var stance = new StanceGeometry(2f, 0f, 0.6f, 1.6f, 1.2f, 0.5f);
            var crouching = new PlayerActionState(
                PlayerActionConditions.Crouching, 0f, null, ShoulderSide.Right, ViewMode.ThirdPerson, "Rifle", 0f);
            var prone = new PlayerActionState(
                PlayerActionConditions.Prone, 0f, null, ShoulderSide.Right, ViewMode.ThirdPerson, "Rifle", 0f);

            Assert.That(PlayerMockPose.StanceScale(crouching, stance), Is.EqualTo(0.8f).Within(Tolerance));
            Assert.That(PlayerMockPose.StanceScale(prone, stance), Is.EqualTo(0.6f).Within(Tolerance));
            Assert.That(PlayerMockPose.StanceScale(crouching, null), Is.EqualTo(PlayerMockPose.CrouchScale));
            Assert.That(PlayerMockPose.StanceScale(prone, null), Is.EqualTo(PlayerMockPose.ProneScale));
            Assert.That(
                PlayerMockPose.BodyGun(crouching, Quaternion.identity, stance).Position.y,
                Is.EqualTo(PlayerMockPose.BodyHipGunPosition.y * 0.8f).Within(Tolerance));
        }

        private void SetTraversal()
        {
            _settings.SafeFallSpeed = 10f;
            _settings.LethalFallSpeed = 30f;
            _settings.DodgeDistance = 6f;
            _settings.DodgeSeconds = 0.5f;
            _settings.VaultMaxHeight = 1f;
            _settings.MantleMaxHeight = 2.5f;
            _settings.MantleSpeed = 4f;
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
