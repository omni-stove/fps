using System;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class MotorSettingsLeanAirJumpTests
    {
        private MotorSettings _settings;
        private NetMatchDefinition _match;

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
        }

        [Test]
        public void NewSettingsHoldTheLeanAndAirJumpDefaults()
        {
            var spec = _settings.CreateSpec();

            Assert.That(spec.LeanSideOffset, Is.EqualTo(MotorSpec.DefaultLeanSideOffset));
            Assert.That(spec.LeanAngleDegrees, Is.EqualTo(MotorSpec.DefaultLeanAngleDegrees));
            Assert.That(spec.LeanSpeed, Is.EqualTo(MotorSpec.DefaultLeanSpeed));
            Assert.That(spec.AirJumps, Is.EqualTo(MotorSpec.DefaultAirJumps));
            Assert.That(spec.AirJumpSpeed, Is.EqualTo(MotorSpec.DefaultAirJumpSpeed));
            Assert.That(spec.JumpGraceSeconds, Is.EqualTo(MotorSpec.DefaultJumpGraceSeconds));
        }

        [Test]
        public void ResolveCarriesTheLeanAndAirJumpValues()
        {
            Set();

            var spec = MotorSettings.Resolve(_settings);

            Assert.That(spec.LeanSideOffset, Is.EqualTo(0.5f));
            Assert.That(spec.LeanAngleDegrees, Is.EqualTo(20f));
            Assert.That(spec.LeanSpeed, Is.EqualTo(4f));
            Assert.That(spec.AirJumps, Is.EqualTo(1));
            Assert.That(spec.AirJumpSpeed, Is.EqualTo(5.5f));
            Assert.That(spec.JumpGraceSeconds, Is.EqualTo(0.12f));
        }

        [Test]
        public void ResetRestoresTheLeanAndAirJumpDefaults()
        {
            Set();

            _settings.ResetToDefaults();

            Assert.That(_settings.LeanSideOffset, Is.EqualTo(MotorSpec.DefaultLeanSideOffset));
            Assert.That(_settings.LeanAngleDegrees, Is.EqualTo(MotorSpec.DefaultLeanAngleDegrees));
            Assert.That(_settings.LeanSpeed, Is.EqualTo(MotorSpec.DefaultLeanSpeed));
            Assert.That(_settings.AirJumps, Is.EqualTo(MotorSpec.DefaultAirJumps));
            Assert.That(_settings.AirJumpSpeed, Is.EqualTo(MotorSpec.DefaultAirJumpSpeed));
            Assert.That(_settings.JumpGraceSeconds, Is.EqualTo(MotorSpec.DefaultJumpGraceSeconds));
        }

        [Test]
        public void ANegativeJumpGraceThrows()
        {
            var serialized = new SerializedObject(_settings);
            serialized.FindProperty("_jumpGraceSeconds").floatValue = -0.1f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var e = Assert.Throws<ArgumentOutOfRangeException>(() => MotorSettings.Resolve(_settings));
            Assert.That(e.ParamName, Is.EqualTo("jumpGraceSeconds"));
        }

        [Test]
        public void ANegativeAirJumpCountThrows()
        {
            var serialized = new SerializedObject(_settings);
            serialized.FindProperty("_airJumps").intValue = -1;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var e = Assert.Throws<ArgumentOutOfRangeException>(() => MotorSettings.Resolve(_settings));
            Assert.That(e.ParamName, Is.EqualTo("airJumps"));
        }

        [Test]
        public void NetMatchToMotorCarriesTheLeanAirJumpSwimAndWallRunValues()
        {
            Set();
            _match = ScriptableObject.CreateInstance<NetMatchDefinition>();
            _match.MotorSettings = _settings;
            var traversal = TraversalTuning.Current;

            var motor = _match.ToMotor();

            Assert.That(motor.Motor.LeanSideOffset, Is.EqualTo(0.5f));
            Assert.That(motor.Motor.AirJumps, Is.EqualTo(1));
            Assert.That(motor.Motor.AirJumpSpeed, Is.EqualTo(5.5f));
            Assert.That(motor.Motor.JumpGraceSeconds, Is.EqualTo(0.12f));
            Assert.That(motor.Swim.Speed, Is.EqualTo(traversal.Swim.Speed));
            Assert.That(motor.Swim.Buoyancy, Is.EqualTo(traversal.Swim.Buoyancy));
            Assert.That(motor.Swim.BreathSeconds, Is.EqualTo(traversal.Swim.BreathSeconds));
            Assert.That(motor.WallRun.Enabled, Is.EqualTo(traversal.WallRun.Enabled));
            Assert.That(motor.WallRun.MaxSeconds, Is.EqualTo(traversal.WallRun.MaxSeconds));
            Assert.That(motor.WallRun.JumpOffSpeed, Is.EqualTo(traversal.WallRun.JumpOffSpeed));
        }

        private void Set()
        {
            _settings.LeanSideOffset = 0.5f;
            _settings.LeanAngleDegrees = 20f;
            _settings.LeanSpeed = 4f;
            _settings.AirJumps = 1;
            _settings.AirJumpSpeed = 5.5f;
            _settings.JumpGraceSeconds = 0.12f;
        }
    }
}
