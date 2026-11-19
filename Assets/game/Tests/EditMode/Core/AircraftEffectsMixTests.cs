using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class AircraftEffectsMixTests
    {
        private const float _epsilon = 1e-4f;

        [Test]
        public void EachSurfaceFollowsItsOwnAxis()
        {
            var spec = new AircraftEffectsSpec(elevatorAngle: 30f, aileronAngle: 20f, rudderAngle: 10f);
            var input = new VehicleMotorInput(1f, -0.5f, 0.25f, 1f);

            Assert.That(AircraftEffectsMix.SurfaceAngle(spec, input, AircraftControlSurfaceRole.Elevator), Is.EqualTo(-15f).Within(_epsilon));
            Assert.That(AircraftEffectsMix.SurfaceAngle(spec, input, AircraftControlSurfaceRole.Aileron), Is.EqualTo(5f).Within(_epsilon));
            Assert.That(AircraftEffectsMix.SurfaceAngle(spec, input, AircraftControlSurfaceRole.Rudder), Is.EqualTo(10f).Within(_epsilon));
            Assert.That(AircraftEffectsMix.SurfaceAngle(spec, VehicleMotorInput.Idle, AircraftControlSurfaceRole.Elevator), Is.EqualTo(0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => AircraftEffectsMix.SurfaceAngle(spec, input, (AircraftControlSurfaceRole)99));
        }

        [Test]
        public void TheEngineLevelMixesThrottleAndSpeed()
        {
            var spec = new AircraftEffectsSpec(engineSpeedShare: 0.4f);

            Assert.That(AircraftEffectsMix.EngineLevel(spec, VehicleMotorInput.Idle, 0f, 100f), Is.EqualTo(0f));
            Assert.That(AircraftEffectsMix.EngineLevel(spec, new VehicleMotorInput(1f), 0f, 100f), Is.EqualTo(0.6f).Within(_epsilon));
            Assert.That(AircraftEffectsMix.EngineLevel(spec, VehicleMotorInput.Idle, 50f, 100f), Is.EqualTo(0.2f).Within(_epsilon));
            Assert.That(AircraftEffectsMix.EngineLevel(spec, new VehicleMotorInput(1f), 200f, 100f), Is.EqualTo(1f), "a dive past top speed is clamped");
            Assert.That(AircraftEffectsMix.EngineLevel(spec, new VehicleMotorInput(-1f), 0f, 100f), Is.EqualTo(0f), "a negative throttle gives no thrust");
            Assert.Throws<ArgumentOutOfRangeException>(() => AircraftEffectsMix.EngineLevel(spec, VehicleMotorInput.Idle, 0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => AircraftEffectsMix.EngineLevel(spec, VehicleMotorInput.Idle, float.NaN, 100f));
        }

        [Test]
        public void PitchAndVolumeSpanTheirRanges()
        {
            var spec = new AircraftEffectsSpec(minEnginePitch: 0.8f, maxEnginePitch: 1.8f, minEngineVolume: 0.1f, maxEngineVolume: 0.9f);

            Assert.That(AircraftEffectsMix.EnginePitch(spec, 0f), Is.EqualTo(0.8f).Within(_epsilon));
            Assert.That(AircraftEffectsMix.EnginePitch(spec, 0.5f), Is.EqualTo(1.3f).Within(_epsilon));
            Assert.That(AircraftEffectsMix.EnginePitch(spec, 1f), Is.EqualTo(1.8f).Within(_epsilon));
            Assert.That(AircraftEffectsMix.EnginePitch(spec, 2f), Is.EqualTo(1.8f).Within(_epsilon));
            Assert.That(AircraftEffectsMix.EngineVolume(spec, 0f), Is.EqualTo(0.1f).Within(_epsilon));
            Assert.That(AircraftEffectsMix.EngineVolume(spec, 1f), Is.EqualTo(0.9f).Within(_epsilon));
        }

        [Test]
        public void VaporNeedsMoreThanTheThresholdInTheAir()
        {
            var spec = new AircraftEffectsSpec(vaporLoadFactor: 3f);

            Assert.That(AircraftEffectsMix.VaporOn(spec, State(1f, false)), Is.False);
            Assert.That(AircraftEffectsMix.VaporOn(spec, State(3f, false)), Is.False, "at the threshold is not past it");
            Assert.That(AircraftEffectsMix.VaporOn(spec, State(3.1f, false)), Is.True);
            Assert.That(AircraftEffectsMix.VaporOn(spec, State(-3.1f, false)), Is.True);
            Assert.That(AircraftEffectsMix.VaporOn(spec, State(6f, true)), Is.False);
        }

        [Test]
        public void TheSpecRejectsBrokenRanges()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftEffectsSpec(elevatorAngle: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftEffectsSpec(minEnginePitch: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftEffectsSpec(minEnginePitch: 2f, maxEnginePitch: 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftEffectsSpec(minEngineVolume: 0.8f, maxEngineVolume: 0.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftEffectsSpec(maxEngineVolume: 1.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftEffectsSpec(engineSpeedShare: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftEffectsSpec(vaporLoadFactor: float.PositiveInfinity));
            Assert.Throws<ArgumentNullException>(() => AircraftEffectsMix.VaporOn(null, State(1f, false)));
        }

        private static AircraftFlightState State(float loadFactor, bool grounded)
        {
            return new AircraftFlightState(50f, 0f, 0f, loadFactor, grounded);
        }
    }
}
