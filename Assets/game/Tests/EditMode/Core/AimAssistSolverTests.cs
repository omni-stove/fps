using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class AimAssistSolverTests
    {
        private const float _tolerance = 1e-3f;

        private static readonly AimAssistSpec _spec = new AimAssistSpec(
            slowdownDegrees: 5f,
            slowdownScale: 0.4f,
            magnetismDegrees: 3f,
            magnetismDegreesPerSecond: 20f,
            magnetismSeconds: 0.5f,
            maxDistance: 50f);

        [Test]
        public void ATargetInsideTheSlowdownAngleScalesTheLook()
        {
            var result = Solve(false, 0f, 1f, Point(4f, 0f, 20f));

            Assert.That(result.LookScale, Is.EqualTo(0.4f));
            Assert.That(result.YawDelta, Is.EqualTo(0f));
            Assert.That(result.PitchUpDelta, Is.EqualTo(0f));
        }

        [Test]
        public void AZeroSlowdownScaleTurnsTheSlowdownOff()
        {
            var off = new AimAssistSpec(
                slowdownDegrees: 5f,
                slowdownScale: 0f,
                magnetismDegrees: 3f,
                magnetismDegreesPerSecond: 20f,
                magnetismSeconds: 0.5f,
                maxDistance: 50f);

            var result = AimAssistSolver.Solve(off, 1f, false, 0f, Vector3.Zero, new Vector3(0f, 0f, 1f), new[] { Point(4f, 0f, 20f) });

            Assert.That(result.LookScale, Is.EqualTo(1f));
        }

        [Test]
        public void ADeadCenterTargetDoesNotTriggerPartsTurnedOffWithZeroDegrees()
        {
            var off = new AimAssistSpec(
                slowdownDegrees: 0f,
                slowdownScale: 0.4f,
                magnetismDegrees: 0f,
                magnetismDegreesPerSecond: 20f,
                magnetismSeconds: 0.5f,
                maxDistance: 50f);

            var result = AimAssistSolver.Solve(off, 1f, true, 0f, Vector3.Zero, Vector3.UnitZ, new[] { Point(0f, 0f, 20f) });

            Assert.That(result.LookScale, Is.EqualTo(1f));
            Assert.That(result.YawDelta, Is.EqualTo(0f));
            Assert.That(result.PitchUpDelta, Is.EqualTo(0f));
        }

        [Test]
        public void ATargetOutsideTheSlowdownAngleLeavesTheLookAlone()
        {
            var result = Solve(true, 0f, 1f, Point(6f, 0f, 20f), Point(0f, -7f, 20f));

            Assert.That(result.LookScale, Is.EqualTo(1f));
            Assert.That(result.YawDelta, Is.EqualTo(0f));
            Assert.That(result.PitchUpDelta, Is.EqualTo(0f));
        }

        [Test]
        public void NoCandidatesLeavesTheLookAlone()
        {
            var result = Solve(true, 0f, 1f);

            Assert.That(result.LookScale, Is.EqualTo(1f));
            Assert.That(result.YawDelta, Is.EqualTo(0f));
            Assert.That(result.PitchUpDelta, Is.EqualTo(0f));
        }

        [Test]
        public void MagnetismPullsTowardTheCandidateNearestInAngle()
        {
            var result = Solve(true, 0f, 1f, Point(-2.5f, 0f, 10f), Point(1f, 0f, 30f));

            Assert.That(result.LookScale, Is.EqualTo(0.4f));
            Assert.That(result.YawDelta, Is.EqualTo(1f).Within(_tolerance));
            Assert.That(result.PitchUpDelta, Is.EqualTo(0f).Within(_tolerance));
        }

        [Test]
        public void MagnetismTurnsAtMostItsRatePerStep()
        {
            // 狙いまで yaw 2°・pitch 1.5° で 2.5°。0.05 秒の刻みでは 20°/s × 0.05 = 1° だけ同じ向きに寄せる。
            var result = Solve(true, 0.1f, 0.05f, Point(2f, 1.5f, 20f));

            Assert.That(result.YawDelta, Is.EqualTo(0.8f).Within(_tolerance));
            Assert.That(result.PitchUpDelta, Is.EqualTo(0.6f).Within(_tolerance));
        }

        [Test]
        public void MagnetismStopsAtTheTarget()
        {
            var result = Solve(true, 0f, 1f, Point(-2f, -1.5f, 20f));

            Assert.That(result.YawDelta, Is.EqualTo(-2f).Within(_tolerance));
            Assert.That(result.PitchUpDelta, Is.EqualTo(-1.5f).Within(_tolerance));
        }

        [Test]
        public void MagnetismOnlyWorksWhileAimingDownSightsWithinItsSeconds()
        {
            var target = Point(2f, 0f, 20f);

            var hip = Solve(false, 0f, 1f, target);
            var late = Solve(true, 0.5f, 1f, target);
            var early = Solve(true, 0.49f, 1f, target);

            Assert.That(hip.YawDelta, Is.EqualTo(0f));
            Assert.That(hip.LookScale, Is.EqualTo(0.4f), "the slowdown does not need ADS");
            Assert.That(late.YawDelta, Is.EqualTo(0f));
            Assert.That(late.LookScale, Is.EqualTo(0.4f));
            Assert.That(early.YawDelta, Is.EqualTo(2f).Within(_tolerance));
        }

        [Test]
        public void ATargetInsideTheSlowdownButOutsideTheMagnetismAngleIsNotPulled()
        {
            var result = Solve(true, 0f, 1f, Point(4f, 0f, 20f));

            Assert.That(result.LookScale, Is.EqualTo(0.4f));
            Assert.That(result.YawDelta, Is.EqualTo(0f));
            Assert.That(result.PitchUpDelta, Is.EqualTo(0f));
        }

        [Test]
        public void CandidatesBeyondTheMaxDistanceAreIgnored()
        {
            var far = Solve(true, 0f, 1f, Point(1f, 0f, 60f));
            var farAndNear = Solve(true, 0f, 1f, Point(1f, 0f, 60f), Point(-4f, 0f, 40f));
            var atTheEdge = Solve(true, 0f, 1f, Point(1f, 0f, 49.9f));

            Assert.That(far.LookScale, Is.EqualTo(1f));
            Assert.That(far.YawDelta, Is.EqualTo(0f));
            Assert.That(farAndNear.LookScale, Is.EqualTo(0.4f), "slowed by the nearer one at 4 degrees");
            Assert.That(farAndNear.YawDelta, Is.EqualTo(0f), "4 degrees is outside the magnetism angle");
            Assert.That(atTheEdge.YawDelta, Is.EqualTo(1f).Within(_tolerance));
        }

        [Test]
        public void TheYawDeltaTakesTheShortWayAcrossTheBack()
        {
            var forward = Point(179f, 0f, 1f);
            var result = AimAssistSolver.Solve(_spec, 1f, true, 0f, Vector3.Zero, forward, new[] { Point(-179f, 0f, 20f) });

            Assert.That(result.YawDelta, Is.EqualTo(2f).Within(_tolerance));
        }

        [Test]
        public void TheAimIsMeasuredFromTheEye()
        {
            var eye = new Vector3(10f, 2f, -5f);
            var result = AimAssistSolver.Solve(_spec, 1f, true, 0f, eye, Vector3.UnitZ, new[] { eye + Point(1f, 0f, 20f) });

            Assert.That(result.LookScale, Is.EqualTo(0.4f));
            Assert.That(result.YawDelta, Is.EqualTo(1f).Within(_tolerance));
        }

        [Test]
        public void ALongerForwardGivesTheSameResult()
        {
            var result = AimAssistSolver.Solve(_spec, 1f, true, 0f, Vector3.Zero, Vector3.UnitZ * 7f, new[] { Point(1f, 0f, 20f) });

            Assert.That(result.YawDelta, Is.EqualTo(1f).Within(_tolerance));
        }

        [Test]
        public void AZeroForwardOrANonFinitePointDoesNothing()
        {
            var zero = AimAssistSolver.Solve(_spec, 1f, true, 0f, Vector3.Zero, Vector3.Zero, new[] { Point(1f, 0f, 20f) });
            var nan = Solve(true, 0f, 1f, new Vector3(float.NaN, 0f, 20f));

            Assert.That(zero.LookScale, Is.EqualTo(1f));
            Assert.That(zero.YawDelta, Is.EqualTo(0f));
            Assert.That(nan.LookScale, Is.EqualTo(1f));
            Assert.That(nan.YawDelta, Is.EqualTo(0f));
        }

        [Test]
        public void TheNoneResultLeavesTheLookAlone()
        {
            Assert.That(AimAssistResult.None.LookScale, Is.EqualTo(1f));
            Assert.That(AimAssistResult.None.YawDelta, Is.EqualTo(0f));
            Assert.That(AimAssistResult.None.PitchUpDelta, Is.EqualTo(0f));
        }

        [Test]
        public void BadArgumentsAreRejected()
        {
            var points = new[] { Point(1f, 0f, 20f) };

            Assert.Throws<ArgumentNullException>(() => AimAssistSolver.Solve(null, 1f, true, 0f, Vector3.Zero, Vector3.UnitZ, points));
            Assert.Throws<ArgumentNullException>(() => AimAssistSolver.Solve(_spec, 1f, true, 0f, Vector3.Zero, Vector3.UnitZ, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => AimAssistSolver.Solve(_spec, -1f, true, 0f, Vector3.Zero, Vector3.UnitZ, points));
            Assert.Throws<ArgumentOutOfRangeException>(() => AimAssistSolver.Solve(_spec, float.NaN, true, 0f, Vector3.Zero, Vector3.UnitZ, points));
            Assert.Throws<ArgumentOutOfRangeException>(() => AimAssistSolver.Solve(_spec, 1f, true, -0.1f, Vector3.Zero, Vector3.UnitZ, points));
        }

        private static AimAssistResult Solve(bool aiming, float aimingSeconds, float deltaSeconds, params Vector3[] candidates)
        {
            return AimAssistSolver.Solve(_spec, deltaSeconds, aiming, aimingSeconds, Vector3.Zero, Vector3.UnitZ, candidates);
        }

        private static Vector3 Point(float yawDegrees, float pitchUpDegrees, float distance)
        {
            var yaw = yawDegrees * Math.PI / 180.0;
            var pitch = pitchUpDegrees * Math.PI / 180.0;
            return new Vector3(
                (float)(Math.Sin(yaw) * Math.Cos(pitch) * distance),
                (float)(Math.Sin(pitch) * distance),
                (float)(Math.Cos(yaw) * Math.Cos(pitch) * distance));
        }
    }
}
