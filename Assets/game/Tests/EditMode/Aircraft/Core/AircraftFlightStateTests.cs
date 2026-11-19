using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class AircraftFlightStateTests
    {
        private const float _epsilon = 1e-4f;

        [Test]
        public void LevelFlightHasNoAngleOfAttackAndOneG()
        {
            var state = AircraftFlightState.From(new Vector3(0f, 0f, 50f), new Vector3(0f, 9.8f, 0f), 9.8f, false);

            Assert.That(state.Airspeed, Is.EqualTo(50f).Within(_epsilon));
            Assert.That(state.AngleOfAttackRadians, Is.EqualTo(0f).Within(_epsilon));
            Assert.That(state.SideslipRadians, Is.EqualTo(0f).Within(_epsilon));
            Assert.That(state.LoadFactor, Is.EqualTo(1f).Within(_epsilon));
            Assert.That(state.Grounded, Is.False);
        }

        [Test]
        public void AirFlowingUpPastTheNoseIsAPositiveAngleOfAttack()
        {
            var state = AircraftFlightState.From(new Vector3(0f, -5f, 50f), Vector3.Zero, 9.8f, false);

            Assert.That(state.AngleOfAttackRadians, Is.EqualTo((float)Math.Atan(5.0 / 50.0)).Within(_epsilon));
            Assert.That(state.Airspeed, Is.EqualTo((float)Math.Sqrt(2525.0)).Within(_epsilon));
        }

        [Test]
        public void AirFlowingDownPastTheNoseIsANegativeAngleOfAttack()
        {
            var state = AircraftFlightState.From(new Vector3(0f, 5f, 50f), Vector3.Zero, 9.8f, false);

            Assert.That(state.AngleOfAttackRadians, Is.EqualTo((float)-Math.Atan(5.0 / 50.0)).Within(_epsilon));
        }

        [Test]
        public void DriftingRightIsAPositiveSideslip()
        {
            var state = AircraftFlightState.From(new Vector3(30f, 0f, 40f), Vector3.Zero, 9.8f, false);

            Assert.That(state.SideslipRadians, Is.EqualTo((float)Math.Asin(30.0 / 50.0)).Within(_epsilon));
            Assert.That(state.AngleOfAttackRadians, Is.EqualTo(0f).Within(_epsilon));
            Assert.That(state.Airspeed, Is.EqualTo(50f).Within(_epsilon));
        }

        [Test]
        public void PullingUpAtTwiceTheWeightIsTwoG()
        {
            var state = AircraftFlightState.From(new Vector3(0f, 0f, 80f), new Vector3(0f, 19.6f, -3f), 9.8f, false);

            Assert.That(state.LoadFactor, Is.EqualTo(2f).Within(_epsilon));
        }

        [Test]
        public void AStoppedAircraftHasNoAnglesAndKeepsTheGroundFlag()
        {
            var state = AircraftFlightState.From(Vector3.Zero, Vector3.Zero, 9.8f, true);

            Assert.That(state.Airspeed, Is.EqualTo(0f));
            Assert.That(state.AngleOfAttackRadians, Is.EqualTo(0f));
            Assert.That(state.SideslipRadians, Is.EqualTo(0f));
            Assert.That(state.LoadFactor, Is.EqualTo(0f));
            Assert.That(state.Grounded, Is.True);
        }

        [Test]
        public void WithoutGravityTheLoadFactorIsZero()
        {
            var state = AircraftFlightState.From(new Vector3(0f, 0f, 50f), new Vector3(0f, 5f, 0f), 0f, false);

            Assert.That(state.LoadFactor, Is.EqualTo(0f));
        }

        [Test]
        public void FromRejectsBadArguments()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => AircraftFlightState.From(new Vector3(float.NaN, 0f, 0f), Vector3.Zero, 9.8f, false));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => AircraftFlightState.From(Vector3.Zero, new Vector3(0f, float.PositiveInfinity, 0f), 9.8f, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => AircraftFlightState.From(Vector3.Zero, Vector3.Zero, -1f, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => AircraftFlightState.From(Vector3.Zero, Vector3.Zero, float.NaN, false));
        }
    }
}
