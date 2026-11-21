using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class HorseStrideTests
    {
        private const float _tolerance = 1e-4f;

        private static readonly HorseSpec _spec = new HorseSpec(
            mass: 500f,
            walkSpeed: 2f,
            trotSpeed: 5f,
            canterSpeed: 10f,
            reverseSpeed: 1f,
            acceleration: 4f,
            deceleration: 8f,
            turnRate: 100f,
            canterTurnScale: 0.4f,
            jumpSpeed: 5f,
            maxSlopeAngle: 40f,
            walkThreshold: 0.25f,
            trotThreshold: 3f,
            canterThreshold: 7f);

        private static VehicleMotorInput Input(float throttle = 0f, float turn = 0f, float gallop = 0f, float jump = 0f)
        {
            return VehicleMotorInput.Idle
                .With(HorseStride.ThrottleAxis, throttle)
                .With(HorseStride.TurnAxis, turn)
                .With(HorseStride.GallopAxis, gallop)
                .With(HorseStride.JumpAxis, jump);
        }

        [Test]
        public void TheAxesAreFixed()
        {
            Assert.That(HorseStride.ThrottleAxis, Is.EqualTo(0));
            Assert.That(HorseStride.TurnAxis, Is.EqualTo(1));
            Assert.That(HorseStride.GallopAxis, Is.EqualTo(2));
            Assert.That(HorseStride.JumpAxis, Is.EqualTo(3));
        }

        [Test]
        public void NoInputTargetsAStop()
        {
            Assert.That(HorseStride.TargetSpeed(_spec, VehicleMotorInput.Idle, 0f), Is.EqualTo(0f));
            Assert.That(HorseStride.TargetSpeed(_spec, VehicleMotorInput.Idle, 8f), Is.EqualTo(0f));
        }

        [Test]
        public void HalfThrottleTargetsTheWalkTopSpeed()
        {
            Assert.That(HorseStride.TargetSpeed(_spec, Input(throttle: 0.5f), 0f), Is.EqualTo(2f).Within(_tolerance));
        }

        [Test]
        public void FullThrottleTargetsTheTrotTopSpeed()
        {
            Assert.That(HorseStride.TargetSpeed(_spec, Input(throttle: 1f), 0f), Is.EqualTo(5f).Within(_tolerance));
        }

        [Test]
        public void ThrottleBetweenTheStepsBlendsTheTopSpeeds()
        {
            Assert.That(HorseStride.TargetSpeed(_spec, Input(throttle: 0.25f), 0f), Is.EqualTo(1f).Within(_tolerance));
            Assert.That(HorseStride.TargetSpeed(_spec, Input(throttle: 0.75f), 0f), Is.EqualTo(3.5f).Within(_tolerance));
        }

        [Test]
        public void AFullGallopTargetsTheCanterTopSpeed()
        {
            Assert.That(HorseStride.TargetSpeed(_spec, Input(throttle: 1f, gallop: 1f), 0f), Is.EqualTo(10f).Within(_tolerance));
            Assert.That(HorseStride.TargetSpeed(_spec, Input(throttle: 0.5f, gallop: 1f), 3f), Is.EqualTo(10f).Within(_tolerance));
        }

        [Test]
        public void AHalfGallopTargetsHalfwayToTheCanterTopSpeed()
        {
            Assert.That(HorseStride.TargetSpeed(_spec, Input(throttle: 1f, gallop: 0.5f), 0f), Is.EqualTo(7.5f).Within(_tolerance));
        }

        [Test]
        public void GallopWithoutThrottleOrBelowZeroDoesNotSpeedUp()
        {
            Assert.That(HorseStride.TargetSpeed(_spec, Input(gallop: 1f), 0f), Is.EqualTo(0f));
            Assert.That(HorseStride.TargetSpeed(_spec, Input(throttle: 1f, gallop: -1f), 0f), Is.EqualTo(5f).Within(_tolerance));
        }

        [Test]
        public void BackThrottleTargetsTheReverseSpeed()
        {
            Assert.That(HorseStride.TargetSpeed(_spec, Input(throttle: -1f), 0f), Is.EqualTo(-1f).Within(_tolerance));
            Assert.That(HorseStride.TargetSpeed(_spec, Input(throttle: -0.5f), 0f), Is.EqualTo(-0.5f).Within(_tolerance));
            Assert.That(HorseStride.TargetSpeed(_spec, Input(throttle: -1f, gallop: 1f), 0f), Is.EqualTo(-1f).Within(_tolerance));
        }

        [Test]
        public void ThrottleAgainstTheMotionTargetsAStopFirst()
        {
            Assert.That(HorseStride.TargetSpeed(_spec, Input(throttle: -1f), 3f), Is.EqualTo(0f));
            Assert.That(HorseStride.TargetSpeed(_spec, Input(throttle: 1f), -1f), Is.EqualTo(0f));
        }

        [Test]
        public void ThrottleAgainstASlowMotionTurnsAround()
        {
            Assert.That(HorseStride.TargetSpeed(_spec, Input(throttle: -1f), 0.4f), Is.EqualTo(-1f).Within(_tolerance));
            Assert.That(HorseStride.TargetSpeed(_spec, Input(throttle: 1f), -0.4f), Is.EqualTo(5f).Within(_tolerance));
        }

        [Test]
        public void TargetSpeedRejectsBadArguments()
        {
            Assert.Throws<ArgumentNullException>(() => HorseStride.TargetSpeed(null, Input(throttle: 1f), 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => HorseStride.TargetSpeed(_spec, Input(throttle: 1f), float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => HorseStride.TargetSpeed(_spec, Input(throttle: 1f), float.PositiveInfinity));
        }

        [Test]
        public void StepSpeedsUpByTheAcceleration()
        {
            Assert.That(HorseStride.Step(_spec, 0f, 5f, 0.5f), Is.EqualTo(2f).Within(_tolerance));
            Assert.That(HorseStride.Step(_spec, 0f, -1f, 0.1f), Is.EqualTo(-0.4f).Within(_tolerance));
        }

        [Test]
        public void StepSlowsDownByTheDeceleration()
        {
            Assert.That(HorseStride.Step(_spec, 5f, 0f, 0.25f), Is.EqualTo(3f).Within(_tolerance));
            Assert.That(HorseStride.Step(_spec, 10f, 5f, 0.25f), Is.EqualTo(8f).Within(_tolerance));
            Assert.That(HorseStride.Step(_spec, -1f, 0f, 0.1f), Is.EqualTo(-0.2f).Within(_tolerance));
        }

        [Test]
        public void StepStopsAtTheTarget()
        {
            Assert.That(HorseStride.Step(_spec, 4f, 5f, 1f), Is.EqualTo(5f));
            Assert.That(HorseStride.Step(_spec, 1f, 0f, 1f), Is.EqualTo(0f));
            Assert.That(HorseStride.Step(_spec, 5f, 5f, 1f), Is.EqualTo(5f));
        }

        [Test]
        public void StepHaltsAtZeroBeforeTurningAround()
        {
            Assert.That(HorseStride.Step(_spec, 3f, -1f, 0.25f), Is.EqualTo(1f).Within(_tolerance));
            Assert.That(HorseStride.Step(_spec, 0.5f, -1f, 0.25f), Is.EqualTo(0f));
            Assert.That(HorseStride.Step(_spec, -0.5f, 5f, 0.25f), Is.EqualTo(0f));
        }

        [Test]
        public void StepWithNoTimeKeepsTheSpeed()
        {
            Assert.That(HorseStride.Step(_spec, 3f, 10f, 0f), Is.EqualTo(3f));
        }

        [Test]
        public void StepRejectsBadArguments()
        {
            Assert.Throws<ArgumentNullException>(() => HorseStride.Step(null, 0f, 1f, 0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => HorseStride.Step(_spec, float.NaN, 1f, 0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => HorseStride.Step(_spec, 0f, float.NegativeInfinity, 0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => HorseStride.Step(_spec, 0f, 1f, -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => HorseStride.Step(_spec, 0f, 1f, float.NaN));
        }

        [Test]
        public void TurnRateIsFullAtRest()
        {
            Assert.That(HorseStride.TurnRate(_spec, 0f), Is.EqualTo(100f).Within(_tolerance));
        }

        [Test]
        public void TurnRateNarrowsWithSpeed()
        {
            Assert.That(HorseStride.TurnRate(_spec, 2f), Is.EqualTo(88f).Within(_tolerance));
            Assert.That(HorseStride.TurnRate(_spec, 5f), Is.EqualTo(70f).Within(_tolerance));
            Assert.That(HorseStride.TurnRate(_spec, 10f), Is.EqualTo(40f).Within(_tolerance));
        }

        [Test]
        public void CanterTurnsSlowerThanWalk()
        {
            Assert.That(HorseStride.TurnRate(_spec, _spec.CanterSpeed), Is.LessThan(HorseStride.TurnRate(_spec, _spec.WalkSpeed)));
        }

        [Test]
        public void TurnRateStopsNarrowingPastTheCanterTopSpeedAndIgnoresDirection()
        {
            Assert.That(HorseStride.TurnRate(_spec, 20f), Is.EqualTo(40f).Within(_tolerance));
            Assert.That(HorseStride.TurnRate(_spec, -2f), Is.EqualTo(88f).Within(_tolerance));
        }

        [Test]
        public void TurnRateRejectsBadArguments()
        {
            Assert.Throws<ArgumentNullException>(() => HorseStride.TurnRate(null, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => HorseStride.TurnRate(_spec, float.NaN));
        }

        [Test]
        public void GaitOfSwitchesAtEachThreshold()
        {
            Assert.That(HorseStride.GaitOf(_spec, 0f), Is.EqualTo(HorseGait.Idle));
            Assert.That(HorseStride.GaitOf(_spec, 0.24f), Is.EqualTo(HorseGait.Idle));
            Assert.That(HorseStride.GaitOf(_spec, 0.25f), Is.EqualTo(HorseGait.Walk));
            Assert.That(HorseStride.GaitOf(_spec, 2.99f), Is.EqualTo(HorseGait.Walk));
            Assert.That(HorseStride.GaitOf(_spec, 3f), Is.EqualTo(HorseGait.Trot));
            Assert.That(HorseStride.GaitOf(_spec, 6.99f), Is.EqualTo(HorseGait.Trot));
            Assert.That(HorseStride.GaitOf(_spec, 7f), Is.EqualTo(HorseGait.Canter));
            Assert.That(HorseStride.GaitOf(_spec, 12f), Is.EqualTo(HorseGait.Canter));
        }

        [Test]
        public void GaitOfBackingUpIsAWalk()
        {
            Assert.That(HorseStride.GaitOf(_spec, -1f), Is.EqualTo(HorseGait.Walk));
            Assert.That(HorseStride.GaitOf(_spec, -0.1f), Is.EqualTo(HorseGait.Idle));
        }

        [Test]
        public void GaitOfRejectsBadArguments()
        {
            Assert.Throws<ArgumentNullException>(() => HorseStride.GaitOf(null, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => HorseStride.GaitOf(_spec, float.NaN));
        }
    }
}
