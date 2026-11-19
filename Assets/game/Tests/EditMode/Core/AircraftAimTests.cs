using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class AircraftAimTests
    {
        private const float _epsilon = 1e-4f;

        private readonly AircraftAimSpec _spec = new AircraftAimSpec(pitchGain: 2f, yawGain: 1f, rollGain: 3f, levelGain: 1f, throttleRate: 0.5f);

        [Test]
        public void TheThrottleOpensAndClosesAtItsRateWithinZeroAndOne()
        {
            Assert.That(AircraftAim.StepThrottle(_spec, 0.5f, 1f, 0.2f), Is.EqualTo(0.6f).Within(_epsilon));
            Assert.That(AircraftAim.StepThrottle(_spec, 0.5f, -0.5f, 0.4f), Is.EqualTo(0.4f).Within(_epsilon));
            Assert.That(AircraftAim.StepThrottle(_spec, 0.5f, -1f, 2f), Is.EqualTo(0f));
            Assert.That(AircraftAim.StepThrottle(_spec, 0.5f, 1f, 10f), Is.EqualTo(1f));
            Assert.That(AircraftAim.StepThrottle(_spec, 0.5f, 0f, 1f), Is.EqualTo(0.5f));
        }

        [Test]
        public void PullingBackOnAClosedThrottleIsTheAirBrake()
        {
            Assert.That(AircraftAim.ThrottleAxis(0.6f, -1f), Is.EqualTo(0.6f));
            Assert.That(AircraftAim.ThrottleAxis(0f, -1f), Is.EqualTo(-1f));
            Assert.That(AircraftAim.ThrottleAxis(0f, -0.5f), Is.EqualTo(-0.5f));
            Assert.That(AircraftAim.ThrottleAxis(0f, 0.5f), Is.EqualTo(0f));
            Assert.That(AircraftAim.ThrottleAxis(0f, 0f), Is.EqualTo(0f));
        }

        [Test]
        public void LookingAheadOnlyPassesTheThrottle()
        {
            var input = AircraftAim.Compute(_spec, Vector3.UnitZ, 0f, 0f, 0.7f);

            Assert.That(input, Is.EqualTo(new VehicleMotorInput(0.7f)));
        }

        [Test]
        public void LookingUpAndRightPitchesYawsAndBanksTowardIt()
        {
            var input = AircraftAim.Compute(_spec, new Vector3(0.1f, 0.2f, 1f), 0f, 0f, 0f);

            Assert.That(input[AircraftFlight.PitchAxis], Is.EqualTo(0.39036f).Within(_epsilon));
            Assert.That(input[AircraftFlight.YawAxis], Is.EqualTo(0.09759f).Within(_epsilon));
            Assert.That(input[AircraftFlight.RollAxis], Is.EqualTo(0.29277f).Within(_epsilon));
        }

        [Test]
        public void ABankedAircraftRollsBackToLevelAndTheRollKeysAddToIt()
        {
            Assert.That(AircraftAim.Compute(_spec, Vector3.UnitZ, 0.5f, 0f, 0f)[AircraftFlight.RollAxis], Is.EqualTo(0.5f).Within(_epsilon));
            Assert.That(AircraftAim.Compute(_spec, Vector3.UnitZ, 0.5f, -1f, 0f)[AircraftFlight.RollAxis], Is.EqualTo(-0.5f).Within(_epsilon));
            Assert.That(AircraftAim.Compute(_spec, Vector3.Zero, -0.25f, 0f, 0f)[AircraftFlight.RollAxis], Is.EqualTo(-0.25f).Within(_epsilon));
        }

        [Test]
        public void EachAxisIsClampedToOne()
        {
            var input = AircraftAim.Compute(_spec, Vector3.UnitX, 0f, 0f, 2f);

            Assert.That(input, Is.EqualTo(new VehicleMotorInput(1f, 0f, 1f, 1f)));
        }

        [Test]
        public void LookingBehindTurnsAtFullStick()
        {
            Assert.That(AircraftAim.Compute(_spec, -Vector3.UnitZ, 0f, 0f, 0f), Is.EqualTo(new VehicleMotorInput(0f, 1f, 0f, 0f)), "straight behind pulls up");
            Assert.That(
                AircraftAim.Compute(_spec, new Vector3(-0.1f, 0f, -1f), 0f, 0f, 0f),
                Is.EqualTo(new VehicleMotorInput(0f, 0f, -1f, -1f)),
                "behind and to the left turns left");
        }

        [Test]
        public void PitchIsEasedOffAsTheLoadFactorNearsTheLimit()
        {
            var spec = new AircraftAimSpec(pitchGain: 2f, maxLoadFactor: 6f, loadFactorMargin: 2f, criticalAngleOfAttack: 90f, angleOfAttackMargin: 0f);
            var up = new Vector3(0f, 1f, 1f);
            var down = new Vector3(0f, -1f, 1f);

            Assert.That(Pitch(spec, up, Flying(loadFactor: 3f)), Is.EqualTo(1f), "below the margin the stick is untouched");
            Assert.That(Pitch(spec, up, Flying(loadFactor: 5f)), Is.EqualTo(0.5f).Within(_epsilon), "halfway through the margin");
            Assert.That(Pitch(spec, up, Flying(loadFactor: 6.5f)), Is.EqualTo(0f), "at or past the limit nothing more is pulled");
            Assert.That(Pitch(spec, down, Flying(loadFactor: 6.5f)), Is.EqualTo(-1f), "pushing away from the limit is left alone");
            Assert.That(Pitch(spec, down, Flying(loadFactor: -5f)), Is.EqualTo(-0.5f).Within(_epsilon), "negative G limits pushing");
        }

        [Test]
        public void PitchIsEasedOffBeforeTheStallAngle()
        {
            var spec = new AircraftAimSpec(pitchGain: 2f, maxLoadFactor: 100f, loadFactorMargin: 0f, criticalAngleOfAttack: 15f, angleOfAttackMargin: 5f);
            var up = new Vector3(0f, 1f, 1f);
            var down = new Vector3(0f, -1f, 1f);

            Assert.That(Pitch(spec, up, Flying(angleOfAttackDegrees: 8f)), Is.EqualTo(1f));
            Assert.That(Pitch(spec, up, Flying(angleOfAttackDegrees: 12f)), Is.EqualTo(0.6f).Within(_epsilon));
            Assert.That(Pitch(spec, up, Flying(angleOfAttackDegrees: 16f)), Is.EqualTo(0f), "past the stall angle the nose is not raised further");
            Assert.That(Pitch(spec, down, Flying(angleOfAttackDegrees: 16f)), Is.EqualTo(-1f), "lowering the nose to recover still works");
            Assert.That(Pitch(spec, down, Flying(angleOfAttackDegrees: -12f)), Is.EqualTo(-0.6f).Within(_epsilon));
        }

        [Test]
        public void TheTighterOfTheTwoLimitsWins()
        {
            var spec = new AircraftAimSpec(maxLoadFactor: 6f, loadFactorMargin: 2f, criticalAngleOfAttack: 15f, angleOfAttackMargin: 5f);

            Assert.That(AircraftAim.LimitPitch(spec, 1f, Flying(loadFactor: 5f, angleOfAttackDegrees: 14f)), Is.EqualTo(0.2f).Within(_epsilon));
            Assert.That(AircraftAim.LimitPitch(spec, 0.5f, Flying(loadFactor: 5f)), Is.EqualTo(0.25f).Within(_epsilon));
        }

        [Test]
        public void NothingIsLimitedOnTheGround()
        {
            var grounded = new AircraftFlightState(10f, 0.5f, 0f, 12f, true);

            Assert.That(AircraftAim.LimitPitch(AircraftAimSpec.Default, 1f, grounded), Is.EqualTo(1f));
        }

        [Test]
        public void DivingTwoSecondsFromTheGroundPullsUp()
        {
            var spec = new AircraftAimSpec(pitchGain: 2f, groundWarningSeconds: 3f, minSinkRate: 4f);
            var noseDown = NoseDownUp(30f);
            var ahead = Vector3.UnitZ;

            var twoSeconds = new AircraftAimSituation(default, noseDown, -50f, 100f);
            var input = AircraftAim.Compute(spec, ahead, 0f, -1f, 0f, twoSeconds);

            Assert.That(twoSeconds.SecondsToGround, Is.EqualTo(2f).Within(_epsilon));
            Assert.That(AircraftAim.ShouldPullUp(spec, twoSeconds), Is.True);
            Assert.That(input[AircraftFlight.PitchAxis], Is.EqualTo(1f), "the nose is raised although the aim is at the ground");
            Assert.That(input[AircraftFlight.RollAxis], Is.EqualTo(0f).Within(_epsilon), "the roll keys are ignored while pulling up");
            Assert.That(input[AircraftFlight.YawAxis], Is.EqualTo(0f).Within(_epsilon));

            var fiveSeconds = new AircraftAimSituation(default, noseDown, -20f, 100f);
            Assert.That(AircraftAim.ShouldPullUp(spec, fiveSeconds), Is.False);
            Assert.That(AircraftAim.Compute(spec, ahead, 0f, 0f, 0f, fiveSeconds)[AircraftFlight.PitchAxis], Is.EqualTo(0f).Within(_epsilon));
        }

        [Test]
        public void AnInvertedDiveRollsTowardTheSkyWhilePullingUp()
        {
            var spec = new AircraftAimSpec(pitchGain: 2f, rollGain: 3f, levelGain: 0f, groundWarningSeconds: 3f, minSinkRate: 4f);
            var invertedNoseDown = new Vector3(0.2f, -0.6f, -0.5f);

            var input = AircraftAim.Compute(spec, Vector3.UnitZ, 0f, 0f, 0f, new AircraftAimSituation(default, invertedNoseDown, -50f, 50f));

            Assert.That(input[AircraftFlight.PitchAxis], Is.LessThan(0f), "upside down, pushing raises the nose toward the sky");
            Assert.That(input[AircraftFlight.RollAxis], Is.GreaterThan(0f), "and it rolls toward the sky");
        }

        [Test]
        public void ASlowDescentOrTheGroundRollIsNotPulledUp()
        {
            var spec = new AircraftAimSpec(groundWarningSeconds: 3f, minSinkRate: 4f);
            var noseDown = NoseDownUp(5f);
            var grounded = new AircraftFlightState(30f, 0f, 0f, 1f, true);

            Assert.That(AircraftAim.ShouldPullUp(spec, new AircraftAimSituation(default, noseDown, -3f, 2f)), Is.False, "a landing approach");
            Assert.That(AircraftAim.ShouldPullUp(spec, new AircraftAimSituation(grounded, noseDown, -5f, 0f)), Is.False, "touching down");
            Assert.That(AircraftAim.ShouldPullUp(spec, new AircraftAimSituation(default, noseDown, -50f, float.PositiveInfinity)), Is.False, "no ground below");
            Assert.That(AircraftAim.ShouldPullUp(spec, default), Is.False);
            Assert.That(AircraftAim.ShouldPullUp(new AircraftAimSpec(groundWarningSeconds: 0f), new AircraftAimSituation(default, noseDown, -50f, 1f)), Is.False, "0 seconds turns it off");
        }

        [Test]
        public void RejectsBadSafetyArguments()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftAimSpec(maxLoadFactor: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftAimSpec(maxLoadFactor: 4f, loadFactorMargin: 5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftAimSpec(criticalAngleOfAttack: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftAimSpec(criticalAngleOfAttack: 10f, angleOfAttackMargin: 11f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftAimSpec(groundWarningSeconds: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftAimSpec(minSinkRate: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftAimSituation(default, Vector3.UnitY, float.NaN, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftAimSituation(default, Vector3.UnitY, -1f, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftAimSituation(default, Vector3.UnitY, -1f, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftAimSituation(default, new Vector3(float.NaN, 1f, 0f), -1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new AircraftAimSituation(new AircraftFlightState(1f, float.NaN, 0f, 1f, false), Vector3.UnitY, -1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => AircraftAim.LimitPitch(_spec, 1.5f, default));
            Assert.Throws<ArgumentNullException>(() => AircraftAim.LimitPitch(null, 0f, default));
            Assert.Throws<ArgumentNullException>(() => AircraftAim.ShouldPullUp(null, default));
        }

        [Test]
        public void RejectsBadArguments()
        {
            Assert.Throws<ArgumentNullException>(() => AircraftAim.Compute(null, Vector3.UnitZ, 0f, 0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => AircraftAim.Compute(_spec, new Vector3(float.NaN, 0f, 1f), 0f, 0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => AircraftAim.Compute(_spec, Vector3.UnitZ, 1.5f, 0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => AircraftAim.StepThrottle(_spec, 1.2f, 0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => AircraftAim.StepThrottle(_spec, 0f, 2f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => AircraftAim.StepThrottle(_spec, 0f, 0f, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftAimSpec(pitchGain: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftAimSpec(throttleRate: float.PositiveInfinity));
        }

        private static float Pitch(AircraftAimSpec spec, Vector3 localAim, AircraftFlightState state)
        {
            return AircraftAim.Compute(spec, localAim, 0f, 0f, 0f, new AircraftAimSituation(state, Vector3.UnitY, 0f, float.PositiveInfinity))[AircraftFlight.PitchAxis];
        }

        private static AircraftFlightState Flying(float loadFactor = 1f, float angleOfAttackDegrees = 0f)
        {
            return new AircraftFlightState(60f, angleOfAttackDegrees * (float)(Math.PI / 180.0), 0f, loadFactor, false);
        }

        private static Vector3 NoseDownUp(float degrees)
        {
            var radians = degrees * Math.PI / 180.0;
            return new Vector3(0f, (float)Math.Cos(radians), (float)-Math.Sin(radians));
        }
    }
}
