using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class AircraftDefinitionTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (var i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                {
                    Object.DestroyImmediate(_created[i]);
                }
            }

            _created.Clear();
        }

        [Test]
        public void ANewDefinitionStartsFromTheSpecDefaults()
        {
            var definition = NewDefinition("Fresh");

            var spec = definition.ToSpec();
            var aim = definition.ToAimSpec();

            Assert.That(spec.Mass, Is.EqualTo(AircraftSpec.DefaultMass));
            Assert.That(spec.MaxThrust, Is.EqualTo(AircraftSpec.DefaultMaxThrust));
            Assert.That(spec.Aero.MaxSpeed, Is.EqualTo(AircraftAeroSpec.DefaultMaxSpeed));
            Assert.That(spec.Aero.LiftSpeed, Is.EqualTo(AircraftAeroSpec.DefaultLiftSpeed));
            Assert.That(spec.Aero.ControlSpeed, Is.EqualTo(AircraftAeroSpec.DefaultControlSpeed));
            Assert.That(spec.Aero.MinControl, Is.EqualTo(AircraftAeroSpec.DefaultMinControl));
            Assert.That(spec.Aero.PitchRate, Is.EqualTo(AircraftAeroSpec.DefaultPitchRate));
            Assert.That(spec.Aero.RollRate, Is.EqualTo(AircraftAeroSpec.DefaultRollRate));
            Assert.That(spec.Aero.YawRate, Is.EqualTo(AircraftAeroSpec.DefaultYawRate));
            Assert.That(spec.Aero.Responsiveness, Is.EqualTo(AircraftAeroSpec.DefaultResponsiveness));
            Assert.That(spec.Aero.Alignment, Is.EqualTo(AircraftAeroSpec.DefaultAlignment));
            Assert.That(spec.Aero.AirBrakeDrag, Is.EqualTo(AircraftAeroSpec.DefaultAirBrakeDrag));
            Assert.That(spec.Aero.LiftSlope, Is.EqualTo(AircraftAeroSpec.DefaultLiftSlope));
            Assert.That(spec.Aero.CriticalAngle, Is.EqualTo(AircraftAeroSpec.DefaultCriticalAngle));
            Assert.That(spec.Aero.PostStallLift, Is.EqualTo(AircraftAeroSpec.DefaultPostStallLift));
            Assert.That(spec.Aero.InducedDrag, Is.EqualTo(AircraftAeroSpec.DefaultInducedDrag));
            Assert.That(spec.Aero.Weathervane, Is.EqualTo(AircraftAeroSpec.DefaultWeathervane));
            Assert.That(spec.Aero.Dihedral, Is.EqualTo(AircraftAeroSpec.DefaultDihedral));
            Assert.That(spec.Aero.WingRoll, Is.EqualTo(AircraftAeroSpec.DefaultWingRoll));
            Assert.That(aim.PitchGain, Is.EqualTo(AircraftAimSpec.DefaultPitchGain));
            Assert.That(aim.YawGain, Is.EqualTo(AircraftAimSpec.DefaultYawGain));
            Assert.That(aim.RollGain, Is.EqualTo(AircraftAimSpec.DefaultRollGain));
            Assert.That(aim.LevelGain, Is.EqualTo(AircraftAimSpec.DefaultLevelGain));
            Assert.That(aim.ThrottleRate, Is.EqualTo(AircraftAimSpec.DefaultThrottleRate));
            Assert.That(aim.MaxLoadFactor, Is.EqualTo(AircraftAimSpec.DefaultMaxLoadFactor));
            Assert.That(aim.LoadFactorMargin, Is.EqualTo(AircraftAimSpec.DefaultLoadFactorMargin));
            Assert.That(aim.CriticalAngleOfAttack, Is.EqualTo(AircraftAimSpec.DefaultCriticalAngleOfAttack));
            Assert.That(aim.AngleOfAttackMargin, Is.EqualTo(AircraftAimSpec.DefaultAngleOfAttackMargin));
            Assert.That(aim.GroundWarningSeconds, Is.EqualTo(AircraftAimSpec.DefaultGroundWarningSeconds));
            Assert.That(aim.MinSinkRate, Is.EqualTo(AircraftAimSpec.DefaultMinSinkRate));

            var ground = definition.ToGroundSpec();
            Assert.That(ground.MaxSteerAngle, Is.EqualTo(AircraftGroundSpec.DefaultMaxSteerAngle));
            Assert.That(ground.HighSpeedSteerAngle, Is.EqualTo(AircraftGroundSpec.DefaultHighSpeedSteerAngle));
            Assert.That(ground.SteerFadeSpeed, Is.EqualTo(AircraftGroundSpec.DefaultSteerFadeSpeed));
            Assert.That(ground.BrakeTorque, Is.EqualTo(AircraftGroundSpec.DefaultBrakeTorque));
            Assert.That(ground.IdleBrakeTorque, Is.EqualTo(AircraftGroundSpec.DefaultIdleBrakeTorque));
            Assert.That(ground.HeightProbeDistance, Is.EqualTo(AircraftGroundSpec.DefaultHeightProbeDistance));
        }

        [Test]
        public void TheFlightSpecSurvivesARoundTrip()
        {
            var original = new AircraftSpec(
                mass: 1200f,
                maxThrust: 9f,
                aero: new AircraftAeroSpec(
                    maxSpeed: 70f,
                    liftSpeed: 30f,
                    controlSpeed: 18f,
                    minControl: 0.2f,
                    pitchRate: 45f,
                    rollRate: 90f,
                    yawRate: 15f,
                    responsiveness: 3f,
                    alignment: 1.5f,
                    airBrakeDrag: 4f,
                    liftSlope: 0.09f,
                    criticalAngle: 14f,
                    postStallLift: 0.4f,
                    inducedDrag: 0.07f,
                    weathervane: 2.5f,
                    dihedral: 0.8f,
                    wingRoll: 0.3f));
            var definition = NewDefinition("Trainer");

            definition.CopyFrom(original);
            var spec = definition.ToSpec();

            Assert.That(definition.Mass, Is.EqualTo(1200f));
            Assert.That(definition.MaxThrust, Is.EqualTo(9f));
            Assert.That(spec.Mass, Is.EqualTo(1200f));
            Assert.That(spec.MaxThrust, Is.EqualTo(9f));
            Assert.That(spec.Aero.MaxSpeed, Is.EqualTo(70f));
            Assert.That(spec.Aero.LiftSpeed, Is.EqualTo(30f));
            Assert.That(spec.Aero.ControlSpeed, Is.EqualTo(18f));
            Assert.That(spec.Aero.MinControl, Is.EqualTo(0.2f));
            Assert.That(spec.Aero.PitchRate, Is.EqualTo(45f));
            Assert.That(spec.Aero.RollRate, Is.EqualTo(90f));
            Assert.That(spec.Aero.YawRate, Is.EqualTo(15f));
            Assert.That(spec.Aero.Responsiveness, Is.EqualTo(3f));
            Assert.That(spec.Aero.Alignment, Is.EqualTo(1.5f));
            Assert.That(spec.Aero.AirBrakeDrag, Is.EqualTo(4f));
            Assert.That(spec.Aero.LiftSlope, Is.EqualTo(0.09f));
            Assert.That(spec.Aero.CriticalAngle, Is.EqualTo(14f));
            Assert.That(spec.Aero.PostStallLift, Is.EqualTo(0.4f));
            Assert.That(spec.Aero.InducedDrag, Is.EqualTo(0.07f));
            Assert.That(spec.Aero.Weathervane, Is.EqualTo(2.5f));
            Assert.That(spec.Aero.Dihedral, Is.EqualTo(0.8f));
            Assert.That(spec.Aero.WingRoll, Is.EqualTo(0.3f));
        }

        [Test]
        public void TheAimBlockSurvivesARoundTrip()
        {
            var definition = NewDefinition("Trainer");

            definition.Aim.CopyFrom(new AircraftAimSpec(
                pitchGain: 2f,
                yawGain: 0.5f,
                rollGain: 4f,
                levelGain: 1f,
                throttleRate: 0.25f,
                maxLoadFactor: 5f,
                loadFactorMargin: 1f,
                criticalAngleOfAttack: 18f,
                angleOfAttackMargin: 3f,
                groundWarningSeconds: 2.5f,
                minSinkRate: 6f));
            var aim = definition.ToAimSpec();

            Assert.That(aim.PitchGain, Is.EqualTo(2f));
            Assert.That(aim.YawGain, Is.EqualTo(0.5f));
            Assert.That(aim.RollGain, Is.EqualTo(4f));
            Assert.That(aim.LevelGain, Is.EqualTo(1f));
            Assert.That(aim.ThrottleRate, Is.EqualTo(0.25f));
            Assert.That(aim.MaxLoadFactor, Is.EqualTo(5f));
            Assert.That(aim.LoadFactorMargin, Is.EqualTo(1f));
            Assert.That(aim.CriticalAngleOfAttack, Is.EqualTo(18f));
            Assert.That(aim.AngleOfAttackMargin, Is.EqualTo(3f));
            Assert.That(aim.GroundWarningSeconds, Is.EqualTo(2.5f));
            Assert.That(aim.MinSinkRate, Is.EqualTo(6f));
        }

        [Test]
        public void TheStickBlockSurvivesARoundTrip()
        {
            var definition = NewDefinition("Trainer");

            Assert.That(definition.ToStickSpec().Deadzone, Is.EqualTo(AircraftStickSpec.DefaultDeadzone));
            definition.Stick.CopyFrom(new AircraftStickSpec(deadzone: 0.25f, exponent: 1.5f, throttleRate: 0.8f, forwardPitchesDown: false));
            var stick = definition.ToStickSpec();

            Assert.That(stick.Deadzone, Is.EqualTo(0.25f));
            Assert.That(stick.Exponent, Is.EqualTo(1.5f));
            Assert.That(stick.ThrottleRate, Is.EqualTo(0.8f));
            Assert.That(stick.ForwardPitchesDown, Is.False);
        }

        [Test]
        public void TheDamageBlockStartsFromTheDefaultPartsAndSurvivesARoundTrip()
        {
            var definition = NewDefinition("Trainer");

            var fresh = definition.ToDamageSpec();
            Assert.That(fresh.Parts.Count, Is.EqualTo(4));
            Assert.That(fresh.FindPart(AircraftDamageSpec.DefaultLeftWingId).Effect, Is.EqualTo(AircraftDamageEffect.LeftLift));
            Assert.That(fresh.FindPart(AircraftDamageSpec.DefaultEngineId).Durability, Is.EqualTo(AircraftDamageSpec.DefaultEngineDurability));
            Assert.That(fresh.MinLift, Is.EqualTo(AircraftDamageSpec.DefaultMinLift));
            Assert.That(fresh.MinControl, Is.EqualTo(AircraftDamageSpec.DefaultMinControl));
            Assert.That(fresh.MinThrust, Is.EqualTo(AircraftDamageSpec.DefaultMinThrust));

            definition.Damage.CopyFrom(new AircraftDamageSpec(
                new[]
                {
                    new AircraftDamagePartSpec("engine_left", AircraftDamageEffect.Thrust, 120),
                    new AircraftDamagePartSpec("engine_right", AircraftDamageEffect.Thrust, 130),
                },
                minLift: 0.1f,
                minControl: 0.5f,
                minThrust: 0.05f));
            var spec = definition.ToDamageSpec();

            Assert.That(spec.Parts.Count, Is.EqualTo(2));
            Assert.That(spec.Parts[1].Id, Is.EqualTo("engine_right"));
            Assert.That(spec.Parts[1].Effect, Is.EqualTo(AircraftDamageEffect.Thrust));
            Assert.That(spec.Parts[1].Durability, Is.EqualTo(130));
            Assert.That(spec.MinLift, Is.EqualTo(0.1f));
            Assert.That(spec.MinControl, Is.EqualTo(0.5f));
            Assert.That(spec.MinThrust, Is.EqualTo(0.05f));
        }

        [Test]
        public void ToDamageSpecNamesTheAssetWhenAPartIsInvalid()
        {
            var definition = NewDefinition("BrokenPlane");
            definition.Damage.Parts = new[]
            {
                new AircraftDamageSettings.Part("tail", AircraftDamageEffect.Control, 10),
                new AircraftDamageSettings.Part("tail", AircraftDamageEffect.Control, 10),
            };

            var error = Assert.Throws<ArgumentException>(() => definition.ToDamageSpec());

            Assert.That(error.Message, Does.Contain("BrokenPlane"));
            Assert.That(error.Message, Does.Contain("tail"));
        }

        [Test]
        public void TheEffectsBlockStartsFromTheDefaultsAndSurvivesARoundTrip()
        {
            var definition = NewDefinition("Trainer");

            var fresh = definition.ToEffectsSpec();
            Assert.That(fresh.ElevatorAngle, Is.EqualTo(AircraftEffectsSpec.DefaultElevatorAngle));
            Assert.That(fresh.AileronAngle, Is.EqualTo(AircraftEffectsSpec.DefaultAileronAngle));
            Assert.That(fresh.RudderAngle, Is.EqualTo(AircraftEffectsSpec.DefaultRudderAngle));
            Assert.That(fresh.MinEnginePitch, Is.EqualTo(AircraftEffectsSpec.DefaultMinEnginePitch));
            Assert.That(fresh.MaxEnginePitch, Is.EqualTo(AircraftEffectsSpec.DefaultMaxEnginePitch));
            Assert.That(fresh.MinEngineVolume, Is.EqualTo(AircraftEffectsSpec.DefaultMinEngineVolume));
            Assert.That(fresh.MaxEngineVolume, Is.EqualTo(AircraftEffectsSpec.DefaultMaxEngineVolume));
            Assert.That(fresh.EngineSpeedShare, Is.EqualTo(AircraftEffectsSpec.DefaultEngineSpeedShare));
            Assert.That(fresh.VaporLoadFactor, Is.EqualTo(AircraftEffectsSpec.DefaultVaporLoadFactor));

            definition.Effects.CopyFrom(new AircraftEffectsSpec(10f, 12f, 14f, 0.7f, 1.4f, 0.2f, 0.8f, 0.5f, 5f));
            var effects = definition.ToEffectsSpec();

            Assert.That(effects.ElevatorAngle, Is.EqualTo(10f));
            Assert.That(effects.AileronAngle, Is.EqualTo(12f));
            Assert.That(effects.RudderAngle, Is.EqualTo(14f));
            Assert.That(effects.MinEnginePitch, Is.EqualTo(0.7f));
            Assert.That(effects.MaxEnginePitch, Is.EqualTo(1.4f));
            Assert.That(effects.MinEngineVolume, Is.EqualTo(0.2f));
            Assert.That(effects.MaxEngineVolume, Is.EqualTo(0.8f));
            Assert.That(effects.EngineSpeedShare, Is.EqualTo(0.5f));
            Assert.That(effects.VaporLoadFactor, Is.EqualTo(5f));
            Assert.Throws<ArgumentNullException>(() => definition.Effects.CopyFrom(null));
        }

        [Test]
        public void TheWarningBlockStartsFromTheDefaultsAndSurvivesARoundTrip()
        {
            var definition = NewDefinition("Trainer");

            Assert.That(definition.ToWarningSpec().StallAngleDegrees, Is.EqualTo(AircraftWarningSpec.DefaultStallAngleDegrees));
            Assert.That(definition.ToWarningSpec().StallSpeed, Is.EqualTo(AircraftWarningSpec.DefaultStallSpeed));

            definition.Warning.CopyFrom(new AircraftWarningSpec(stallAngleDegrees: 18f, stallSpeed: 42f));
            var warning = definition.ToWarningSpec();

            Assert.That(warning.StallAngleDegrees, Is.EqualTo(18f));
            Assert.That(warning.StallSpeed, Is.EqualTo(42f));
            Assert.Throws<ArgumentNullException>(() => definition.Warning.CopyFrom(null));
        }

        [Test]
        public void TheEmptyBlocksStillGiveASpec()
        {
            var definition = NewDefinition("Trainer");

            definition.Stick.CopyFrom(AircraftStickSpec.Default);
            definition.Ground.CopyFrom(AircraftGroundSpec.Default);
            definition.Damage.CopyFrom(AircraftDamageSpec.Default);

            Assert.That(definition.Stick.ToSpec(), Is.Not.Null);
            Assert.That(definition.Ground.ToSpec(), Is.Not.Null);
            Assert.That(definition.Damage.ToSpec(), Is.Not.Null);
        }

        [Test]
        public void CopyFromRejectsAMissingSpec()
        {
            var definition = NewDefinition("Trainer");

            Assert.Throws<ArgumentNullException>(() => definition.CopyFrom(null));
            Assert.Throws<ArgumentNullException>(() => definition.Aero.CopyFrom(null));
            Assert.Throws<ArgumentNullException>(() => definition.Aim.CopyFrom(null));
            Assert.Throws<ArgumentNullException>(() => definition.Stick.CopyFrom(null));
            Assert.Throws<ArgumentNullException>(() => definition.Ground.CopyFrom(null));
            Assert.Throws<ArgumentNullException>(() => definition.Damage.CopyFrom(null));
        }

        [Test]
        public void ToSpecNamesTheAssetWhenAValueIsInvalid()
        {
            var definition = NewDefinition("BrokenPlane");
            definition.MaxThrust = 0f;

            var error = Assert.Throws<ArgumentException>(() => definition.ToSpec());

            Assert.That(error.Message, Does.Contain("BrokenPlane"));
            Assert.That(error.InnerException, Is.InstanceOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void ToEffectsSpecNamesTheAssetWhenAValueIsInvalid()
        {
            var definition = NewDefinition("LoudPlane");
            definition.Effects.CopyFrom(new AircraftEffectsSpec(minEnginePitch: 1f, maxEnginePitch: 1f));
            var settings = definition.Effects;
            typeof(AircraftEffectsSettings)
                .GetField("_maxEnginePitch", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(settings, 0.5f);

            var error = Assert.Throws<ArgumentException>(() => definition.ToEffectsSpec());

            Assert.That(error.Message, Does.Contain("LoudPlane"));
            Assert.That(error.InnerException, Is.InstanceOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void TheControlSchemeReadsTheAimBlock()
        {
            var definition = NewDefinition("Trainer");
            definition.Aim.CopyFrom(new AircraftAimSpec(pitchGain: 5f));
            var aircraft = new GameObject("Aircraft");
            _created.Add(aircraft);
            var scheme = aircraft.AddComponent<AircraftControlScheme>();

            Assert.That(scheme.Spec.PitchGain, Is.EqualTo(AircraftAimSpec.DefaultPitchGain), "no asset uses the defaults");
            scheme.Definition = definition;

            Assert.That(scheme.Spec.PitchGain, Is.EqualTo(5f));
        }

        private AircraftDefinition NewDefinition(string assetName)
        {
            var definition = ScriptableObject.CreateInstance<AircraftDefinition>();
            definition.name = assetName;
            _created.Add(definition);
            return definition;
        }
    }
}
