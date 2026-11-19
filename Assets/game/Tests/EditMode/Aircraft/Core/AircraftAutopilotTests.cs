using System;
using NUnit.Framework;
using Vector3 = System.Numerics.Vector3;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class AircraftAutopilotTests
    {
        private const float _epsilon = 1e-4f;
        private const float _degrees = (float)(Math.PI / 180.0);

        // 既定値が変わっても期待値が動かないよう、既定と同じ値をここで書き出す。傾け切って回る輪の半径は 55² / (9.81 × tan 40°) ≒ 367.5 m。
        private readonly AircraftAutopilotSpec _spec = new AircraftAutopilotSpec(
            cruiseSpeed: 55f,
            cruiseThrottle: 0.5f,
            speedGain: 0.05f,
            maxBankAngle: 40f,
            headingGain: 1.5f,
            rollGain: 0.02f,
            yawGain: 0.005f,
            maxClimbAngle: 15f,
            altitudeGain: 0.3f,
            pitchGain: 0.04f,
            maxLoadFactor: 5f,
            loadFactorMargin: 1.5f,
            criticalAngleOfAttack: 15f,
            angleOfAttackMargin: 5f,
            minGroundClearance: 40f,
            groundLookAheadSeconds: 3f,
            arrivalRadius: 80f);

        [Test]
        public void LevelAtTheAltitudeAndHeadingForThePointItHoldsTheStickStill()
        {
            var input = Compute(Level(100f), new Vector3(0f, 100f, 1000f), 100f);

            Assert.That(input[AircraftFlight.ThrottleAxis], Is.EqualTo(0.5f).Within(_epsilon));
            Assert.That(input[AircraftFlight.PitchAxis], Is.EqualTo(0f).Within(_epsilon));
            Assert.That(input[AircraftFlight.RollAxis], Is.EqualTo(0f).Within(_epsilon));
            Assert.That(input[AircraftFlight.YawAxis], Is.EqualTo(0f).Within(_epsilon));
        }

        [Test]
        public void APointToTheSideBanksTowardItUpToTheBankLimit()
        {
            var right = Compute(Level(100f), new Vector3(1000f, 100f, 0f), 100f);
            var left = Compute(Level(100f), new Vector3(-1000f, 100f, 0f), 100f);

            Assert.That(right[AircraftFlight.RollAxis], Is.EqualTo(0.8f).Within(_epsilon), "40 degrees of bank at 0.02 per degree");
            Assert.That(right[AircraftFlight.YawAxis], Is.EqualTo(0.45f).Within(_epsilon), "90 degrees off at 0.005 per degree");
            Assert.That(left[AircraftFlight.RollAxis], Is.EqualTo(-0.8f).Within(_epsilon));
            Assert.That(left[AircraftFlight.YawAxis], Is.EqualTo(-0.45f).Within(_epsilon));
        }

        [Test]
        public void ASmallTurnBanksInProportionAndHoldsTheBankOnceReached()
        {
            var target = new Vector3((float)Math.Sin(10f * _degrees) * 1000f, 100f, (float)Math.Cos(10f * _degrees) * 1000f);

            var level = Compute(Level(100f), target, 100f);
            Assert.That(level[AircraftFlight.RollAxis], Is.EqualTo(0.3f).Within(_epsilon), "10 degrees off asks for 15 degrees of bank");
            Assert.That(level[AircraftFlight.YawAxis], Is.EqualTo(0.05f).Within(_epsilon));

            var banked = Compute(Banked(100f, 15f), target, 100f);
            Assert.That(banked[AircraftFlight.RollAxis], Is.EqualTo(0f).Within(_epsilon));
        }

        [Test]
        public void APointInsideTheTurningCircleIsFlownAwayFromStraightBeforeTurning()
        {
            var input = Compute(Level(100f), new Vector3(200f, 100f, 0f), 100f);

            Assert.That(input[AircraftFlight.RollAxis], Is.EqualTo(0f).Within(_epsilon));
            Assert.That(input[AircraftFlight.YawAxis], Is.EqualTo(0f).Within(_epsilon));
        }

        [Test]
        public void BelowTheAltitudeItClimbsAndAboveItDescendsAtNoMoreThanTheClimbAngle()
        {
            var below = Compute(Level(50f), Ahead(50f), 100f);
            var above = Compute(Level(150f), Ahead(150f), 100f);
            var slightlyBelow = Compute(Level(90f), Ahead(90f), 100f);

            Assert.That(below[AircraftFlight.PitchAxis], Is.EqualTo(0.6f).Within(_epsilon), "15 degrees of climb at 0.04 per degree");
            Assert.That(above[AircraftFlight.PitchAxis], Is.EqualTo(-0.6f).Within(_epsilon));
            Assert.That(slightlyBelow[AircraftFlight.PitchAxis], Is.EqualTo(0.12f).Within(_epsilon), "10 m below asks for 3 degrees of climb");
        }

        [Test]
        public void ThePullUpStopsBeforeTheStallingAngleOfAttack()
        {
            Assert.That(Compute(Level(50f, angleOfAttack: 15f), Ahead(50f), 100f)[AircraftFlight.PitchAxis], Is.EqualTo(0f));
            Assert.That(Compute(Level(50f, angleOfAttack: 20f), Ahead(50f), 100f)[AircraftFlight.PitchAxis], Is.EqualTo(0f));
            Assert.That(
                Compute(Level(50f, angleOfAttack: 12.5f), Ahead(50f), 100f)[AircraftFlight.PitchAxis],
                Is.EqualTo(0.3f).Within(_epsilon),
                "halfway through the margin passes half the stick");
            Assert.That(
                Compute(Level(50f, angleOfAttack: 10f), Ahead(50f), 100f)[AircraftFlight.PitchAxis],
                Is.EqualTo(0.6f).Within(_epsilon),
                "before the margin nothing is eased off");
        }

        [Test]
        public void NearTheStallTheNoseCanStillBeLowered()
        {
            var input = Compute(Level(150f, angleOfAttack: 15f), Ahead(150f), 100f);

            Assert.That(input[AircraftFlight.PitchAxis], Is.EqualTo(-0.6f).Within(_epsilon));
        }

        [Test]
        public void ThePullUpIsEasedOffNearTheLoadFactorLimit()
        {
            var input = Compute(Level(50f, loadFactor: 4.25f), Ahead(50f), 100f);

            Assert.That(input[AircraftFlight.PitchAxis], Is.EqualTo(0.3f).Within(_epsilon));
        }

        [Test]
        public void CloseToTheGroundItLevelsTheWingsAndClimbsAtFullThrottle()
        {
            var low = Compute(Banked(20f, 40f, groundDistance: 20f), new Vector3(1000f, 20f, 0f), 0f);

            Assert.That(low[AircraftFlight.ThrottleAxis], Is.EqualTo(1f));
            Assert.That(low[AircraftFlight.PitchAxis], Is.EqualTo(0.6f).Within(_epsilon), "climbs even though the altitude asked for is lower");
            Assert.That(low[AircraftFlight.RollAxis], Is.EqualTo(-0.8f).Within(_epsilon), "rolls back from 40 degrees instead of turning");
            Assert.That(low[AircraftFlight.YawAxis], Is.EqualTo(0f));
        }

        [Test]
        public void SinkingTowardTheGroundCountsAsLowBeforeItIsReached()
        {
            var velocity = new Vector3(0f, -10f, 55f);
            var sinking = Situation(new Vector3(0f, 200f, 0f), Vector3.UnitZ, Vector3.UnitY, velocity, groundDistance: 60f);
            var clear = Situation(new Vector3(0f, 200f, 0f), Vector3.UnitZ, Vector3.UnitY, velocity, groundDistance: 200f);

            Assert.That(AircraftAutopilot.IsLow(_spec, sinking), Is.True, "60 m minus 3 s at 10 m/s leaves 30 m");
            Assert.That(AircraftAutopilot.IsLow(_spec, clear), Is.False);

            var pullUp = AircraftAutopilot.Compute(_spec, sinking, Ahead(200f), 200f);
            Assert.That(pullUp[AircraftFlight.PitchAxis], Is.EqualTo(1f));
            Assert.That(pullUp[AircraftFlight.ThrottleAxis], Is.EqualTo(1f));

            var hold = AircraftAutopilot.Compute(_spec, clear, Ahead(200f), 200f);
            Assert.That(hold[AircraftFlight.PitchAxis], Is.EqualTo(0.41219f).Within(1e-3f), "levels out of a 10.3 degree descent");
            Assert.That(hold[AircraftFlight.ThrottleAxis], Is.EqualTo(0.45492f).Within(1e-3f));
        }

        [Test]
        public void OnTheGroundItOpensTheThrottleAndKeepsTheWingsLevel()
        {
            var grounded = Situation(new Vector3(0f, 0f, 0f), Vector3.UnitZ, Vector3.UnitY, new Vector3(0f, 0f, 20f), groundDistance: 0f, grounded: true);

            var input = AircraftAutopilot.Compute(_spec, grounded, new Vector3(1000f, 0f, 0f), 100f);

            Assert.That(input[AircraftFlight.ThrottleAxis], Is.EqualTo(1f));
            Assert.That(input[AircraftFlight.RollAxis], Is.EqualTo(0f).Within(_epsilon));
            Assert.That(input[AircraftFlight.PitchAxis], Is.EqualTo(0.6f).Within(_epsilon));
        }

        [Test]
        public void UpsideDownItRollsUprightBeforePulling()
        {
            var inverted = Situation(new Vector3(0f, 50f, 0f), Vector3.UnitZ, -Vector3.UnitY, new Vector3(0f, 0f, 55f));

            var input = AircraftAutopilot.Compute(_spec, inverted, Ahead(50f), 100f);

            Assert.That(input[AircraftFlight.PitchAxis], Is.EqualTo(0f));
            Assert.That(Math.Abs(input[AircraftFlight.RollAxis]), Is.EqualTo(1f));
        }

        [Test]
        public void TheThrottleHoldsTheCruiseSpeed()
        {
            Assert.That(Compute(Level(100f, speed: 45f), Ahead(100f), 100f)[AircraftFlight.ThrottleAxis], Is.EqualTo(1f).Within(_epsilon));
            Assert.That(Compute(Level(100f, speed: 50f), Ahead(100f), 100f)[AircraftFlight.ThrottleAxis], Is.EqualTo(0.75f).Within(_epsilon));
            Assert.That(Compute(Level(100f, speed: 70f), Ahead(100f), 100f)[AircraftFlight.ThrottleAxis], Is.EqualTo(0f));
        }

        [Test]
        public void ThePointIsReachedByItsDistanceAcrossTheGround()
        {
            var position = new Vector3(0f, 300f, 0f);

            Assert.That(AircraftAutopilot.HasArrived(_spec, position, new Vector3(50f, 0f, 60f)), Is.True, "78 m across, 300 m below");
            Assert.That(AircraftAutopilot.HasArrived(_spec, position, new Vector3(0f, 300f, 81f)), Is.False);
        }

        [Test]
        public void TheBankAndClimbAreMeasuredInDegrees()
        {
            Assert.That(AircraftAutopilot.BankAngle(Banked(100f, 30f)), Is.EqualTo(30f).Within(_epsilon));
            Assert.That(AircraftAutopilot.BankAngle(Banked(100f, -20f)), Is.EqualTo(-20f).Within(_epsilon));

            var climbing = Situation(Vector3.Zero, Vector3.UnitZ, Vector3.UnitY, new Vector3(0f, 1f, 1f));
            Assert.That(AircraftAutopilot.FlightPathAngle(climbing), Is.EqualTo(45f).Within(_epsilon));

            var still = Situation(Vector3.Zero, new Vector3(0f, 1f, 1f), new Vector3(0f, 1f, -1f), Vector3.Zero);
            Assert.That(AircraftAutopilot.FlightPathAngle(still), Is.EqualTo(45f).Within(_epsilon), "with no speed the nose decides");
        }

        [Test]
        public void TheSpecRejectsValuesItCannotFlyWith()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftAutopilotSpec(cruiseSpeed: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftAutopilotSpec(cruiseThrottle: 1.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftAutopilotSpec(maxBankAngle: 90f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftAutopilotSpec(maxClimbAngle: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftAutopilotSpec(rollGain: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftAutopilotSpec(maxLoadFactor: 3f, loadFactorMargin: 4f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftAutopilotSpec(criticalAngleOfAttack: 10f, angleOfAttackMargin: 11f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftAutopilotSpec(arrivalRadius: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftAutopilotSpec(groundLookAheadSeconds: float.PositiveInfinity));
        }

        [Test]
        public void TheSituationRejectsDirectionsAndDistancesItCannotUse()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Situation(Vector3.Zero, Vector3.Zero, Vector3.UnitY, Vector3.Zero));
            Assert.Throws<ArgumentOutOfRangeException>(() => Situation(Vector3.Zero, Vector3.UnitZ, Vector3.UnitY, new Vector3(float.NaN, 0f, 0f)));
            Assert.Throws<ArgumentOutOfRangeException>(() => Situation(Vector3.Zero, Vector3.UnitZ, Vector3.UnitY, Vector3.Zero, groundDistance: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => AircraftAutopilot.Compute(_spec, Level(0f), new Vector3(float.NaN, 0f, 0f), 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => AircraftAutopilot.Compute(_spec, Level(0f), Vector3.UnitZ, float.PositiveInfinity));
            Assert.Throws<ArgumentNullException>(() => AircraftAutopilot.Compute(null, Level(0f), Vector3.UnitZ, 0f));
        }

        private VehicleMotorInput Compute(AircraftAutopilotSituation situation, Vector3 target, float altitude)
        {
            return AircraftAutopilot.Compute(_spec, situation, target, altitude);
        }

        private static Vector3 Ahead(float altitude) => new Vector3(0f, altitude, 1000f);

        private static AircraftAutopilotSituation Level(float altitude, float speed = 55f, float angleOfAttack = 0f, float loadFactor = 1f)
        {
            return Situation(
                new Vector3(0f, altitude, 0f), Vector3.UnitZ, Vector3.UnitY, new Vector3(0f, 0f, speed), angleOfAttack: angleOfAttack, loadFactor: loadFactor);
        }

        private static AircraftAutopilotSituation Banked(float altitude, float bankDegrees, float groundDistance = float.PositiveInfinity)
        {
            var bank = bankDegrees * _degrees;
            var up = new Vector3((float)Math.Sin(bank), (float)Math.Cos(bank), 0f);
            return Situation(new Vector3(0f, altitude, 0f), Vector3.UnitZ, up, new Vector3(0f, 0f, 55f), groundDistance: groundDistance);
        }

        private static AircraftAutopilotSituation Situation(
            Vector3 position,
            Vector3 forward,
            Vector3 up,
            Vector3 velocity,
            float groundDistance = float.PositiveInfinity,
            bool grounded = false,
            float angleOfAttack = 0f,
            float loadFactor = 1f)
        {
            var state = new AircraftFlightState(velocity.Length(), angleOfAttack * _degrees, 0f, loadFactor, grounded);
            return new AircraftAutopilotSituation(position, forward, up, velocity, state, groundDistance, 9.81f);
        }
    }
}
