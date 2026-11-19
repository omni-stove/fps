using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class VehicleRouteFollowerTests
    {
        private const float _tolerance = 1e-3f;

        private static readonly Vector3 _north = new Vector3(0f, 0f, 1f);

        [Test]
        public void TheDefaultSpecKeepsTheDefaults()
        {
            var spec = VehicleRouteSpec.Default;

            Assert.That(spec.LookAheadDistance, Is.EqualTo(5f));
            Assert.That(spec.LookAheadSeconds, Is.EqualTo(0.5f));
            Assert.That(spec.ArriveRadius, Is.EqualTo(3f));
            Assert.That(spec.FullSteerAngle, Is.EqualTo(35f));
            Assert.That(spec.CruiseSpeed, Is.EqualTo(12f));
            Assert.That(spec.CornerSpeed, Is.EqualTo(5f));
            Assert.That(spec.SharpCornerAngle, Is.EqualTo(90f));
            Assert.That(spec.Deceleration, Is.EqualTo(5f));
            Assert.That(spec.SpeedBand, Is.EqualTo(2f));
            Assert.That(spec.StuckSpeed, Is.EqualTo(0.5f));
            Assert.That(spec.StuckSeconds, Is.EqualTo(1.5f));
            Assert.That(spec.ReverseSeconds, Is.EqualTo(1.5f));
            Assert.That(spec.ReverseThrottle, Is.EqualTo(0.8f));
        }

        [Test]
        public void TheSpecRejectsValuesOutOfRange()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleRouteSpec(lookAheadDistance: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleRouteSpec(lookAheadSeconds: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleRouteSpec(arriveRadius: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleRouteSpec(fullSteerAngle: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleRouteSpec(fullSteerAngle: 181f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleRouteSpec(cruiseSpeed: float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleRouteSpec(cornerSpeed: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleRouteSpec(cruiseSpeed: 4f, cornerSpeed: 5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleRouteSpec(sharpCornerAngle: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleRouteSpec(deceleration: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleRouteSpec(speedBand: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleRouteSpec(stuckSpeed: -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleRouteSpec(stuckSeconds: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleRouteSpec(reverseSeconds: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleRouteSpec(reverseThrottle: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleRouteSpec(reverseThrottle: 1.5f));
        }

        [Test]
        public void TheSpecSlowsForSharperCornersAndAllowsMoreSpeedFurtherAway()
        {
            var spec = VehicleRouteSpec.Default;

            Assert.That(spec.CornerSpeedFor(0f), Is.EqualTo(12f));
            Assert.That(spec.CornerSpeedFor(45f), Is.EqualTo(8.5f).Within(_tolerance));
            Assert.That(spec.CornerSpeedFor(-90f), Is.EqualTo(5f));
            Assert.That(spec.CornerSpeedFor(170f), Is.EqualTo(5f));
            Assert.That(spec.SpeedToReach(5f, 0f), Is.EqualTo(5f));
            Assert.That(spec.SpeedToReach(5f, 6f), Is.EqualTo((float)Math.Sqrt(85.0)).Within(_tolerance));
            Assert.That(spec.LookAheadAt(-12f), Is.EqualTo(11f));
        }

        [Test]
        public void OnAStraightItSteersStraightAndHoldsTheCruiseSpeed()
        {
            var follower = new VehicleRouteFollower(VehicleRouteSpec.Default);
            var points = new[] { At(0f, 10f), At(0f, 40f), At(0f, 80f) };

            var fromRest = follower.Tick(points, false, Vector3.Zero, _north, 0f, 0.02f);
            Assert.That(fromRest.Phase, Is.EqualTo(VehicleRoutePhase.Driving));
            Assert.That(fromRest.TargetIndex, Is.EqualTo(0));
            Assert.That(fromRest.Steer, Is.EqualTo(0f).Within(_tolerance));
            Assert.That(fromRest.Throttle, Is.EqualTo(1f));
            Assert.That(fromRest.Brake, Is.False);
            Assert.That(fromRest.TargetSpeed, Is.EqualTo(12f).Within(_tolerance));

            var atCruise = follower.Tick(points, false, Vector3.Zero, _north, 12f, 0.02f);
            Assert.That(atCruise.Throttle, Is.EqualTo(0f));
            Assert.That(atCruise.Brake, Is.False);

            var tooFast = follower.Tick(points, false, Vector3.Zero, _north, 15f, 0.02f);
            Assert.That(tooFast.Throttle, Is.EqualTo(0f));
            Assert.That(tooFast.Brake, Is.True);
        }

        [Test]
        public void ItSlowsBeforeACornerAndTurnsTowardIt()
        {
            var points = new[] { At(0f, 20f), At(20f, 20f), At(20f, 60f) };

            var far = new VehicleRouteFollower(VehicleRouteSpec.Default).Tick(points, false, Vector3.Zero, _north, 12f, 0.02f);
            Assert.That(far.Steer, Is.EqualTo(0f).Within(_tolerance), "the corner is beyond the look-ahead");
            Assert.That(far.Brake, Is.False, "there is room to slow down later");

            var near = new VehicleRouteFollower(VehicleRouteSpec.Default).Tick(points, false, At(0f, 14f), _north, 12f, 0.02f);
            Assert.That(near.TargetIndex, Is.EqualTo(0));
            Assert.That(near.Steer, Is.EqualTo(1f), "the look-ahead point is already round the right-hand corner");
            Assert.That(near.TargetSpeed, Is.LessThan(10f));
            Assert.That(near.Brake, Is.True);
            Assert.That(near.Throttle, Is.EqualTo(0f));
        }

        [Test]
        public void ReachingACornerMovesOnToTheNextPoint()
        {
            var follower = new VehicleRouteFollower(VehicleRouteSpec.Default);
            var points = new[] { At(0f, 20f), At(20f, 20f), At(20f, 60f) };

            var command = follower.Tick(points, false, At(0f, 18f), _north, 0f, 0.02f);

            Assert.That(command.TargetIndex, Is.EqualTo(1));
            Assert.That(follower.TargetIndex, Is.EqualTo(1));
            Assert.That(command.Steer, Is.EqualTo(1f));
        }

        [Test]
        public void CuttingInsideACornerStillCountsAsPassingIt()
        {
            var follower = new VehicleRouteFollower(VehicleRouteSpec.Default);
            var points = new[] { At(0f, 0f), At(0f, 20f), At(20f, 20f), At(20f, 60f) };
            follower.Restart(1);

            var command = follower.Tick(points, false, At(5f, 17f), new Vector3(1f, 0f, 0f), 6f, 0.02f);

            Assert.That(command.TargetIndex, Is.EqualTo(2), "about 5.8 m from the corner is outside the arrive radius but past the bisector");
        }

        [Test]
        public void ASharpTurnBackDoesNotCountAsPassedFarFromTheCorner()
        {
            var follower = new VehicleRouteFollower(VehicleRouteSpec.Default);
            var points = new[] { At(0f, 0f), At(0f, 40f), At(6f, 0f) };
            follower.Restart(1);

            var command = follower.Tick(points, false, At(2.3f, 10f), _north, 0f, 0.02f);

            Assert.That(command.TargetIndex, Is.EqualTo(1), "30 m from the corner is past the bisector but outside the look-ahead");
        }

        [Test]
        public void ASlowCornerSpeedStillKeepsThrottleAndCanReverseWhenStuck()
        {
            var follower = new VehicleRouteFollower(new VehicleRouteSpec(cornerSpeed: 0f));
            var points = new[] { At(10f, 0f) };

            var first = follower.Tick(points, false, Vector3.Zero, _north, 0f, 0.25f);
            Assert.That(first.Throttle, Is.GreaterThan(0f), "a target to the side must not drop the target speed to zero");

            VehicleRouteCommand command = first;
            for (var i = 0; i < 5; i++)
            {
                command = follower.Tick(points, false, Vector3.Zero, _north, 0f, 0.25f);
            }

            Assert.That(command.Phase, Is.EqualTo(VehicleRoutePhase.Reversing));
        }

        [Test]
        public void ATargetToTheLeftSteersLeft()
        {
            var follower = new VehicleRouteFollower(VehicleRouteSpec.Default);

            var command = follower.Tick(new[] { At(-10f, 10f) }, false, Vector3.Zero, _north, 0f, 0.02f);

            Assert.That(command.Steer, Is.EqualTo(-1f));
            Assert.That(command.TargetSpeed, Is.LessThan(12f), "a large heading error slows the car");
        }

        [Test]
        public void WhenStuckItBacksUpSteeringTheOtherWayThenDrivesOnAgain()
        {
            var follower = new VehicleRouteFollower(VehicleRouteSpec.Default);
            var points = new[] { At(-10f, 10f) };

            for (var i = 0; i < 5; i++)
            {
                var pushing = follower.Tick(points, false, Vector3.Zero, _north, 0f, 0.25f);
                Assert.That(pushing.Phase, Is.EqualTo(VehicleRoutePhase.Driving), $"tick {i}");
                Assert.That(pushing.Throttle, Is.EqualTo(1f), $"tick {i}");
            }

            var reversing = follower.Tick(points, false, Vector3.Zero, _north, 0f, 0.25f);
            Assert.That(reversing.Phase, Is.EqualTo(VehicleRoutePhase.Reversing));
            Assert.That(reversing.Throttle, Is.EqualTo(-0.8f));
            Assert.That(reversing.Steer, Is.EqualTo(1f), "backing with the wheels turned right swings the nose left toward the target");
            Assert.That(reversing.Brake, Is.False);
            Assert.That(follower.ReverseSecondsLeft, Is.EqualTo(1.5f));

            for (var i = 0; i < 5; i++)
            {
                var backing = follower.Tick(points, false, Vector3.Zero, _north, -1f, 0.25f);
                Assert.That(backing.Phase, Is.EqualTo(VehicleRoutePhase.Reversing), $"reverse tick {i}");
                Assert.That(backing.Throttle, Is.EqualTo(-0.8f), $"reverse tick {i}");
            }

            var again = follower.Tick(points, false, Vector3.Zero, _north, 0f, 0.25f);
            Assert.That(again.Phase, Is.EqualTo(VehicleRoutePhase.Driving));
            Assert.That(again.Throttle, Is.GreaterThan(0f));
            Assert.That(follower.StalledSeconds, Is.EqualTo(0.25f), "the stall clock starts over after backing up");
        }

        [Test]
        public void ADeadEndStraightAheadBacksUpTurningLeft()
        {
            var follower = new VehicleRouteFollower(VehicleRouteSpec.Default);
            var points = new[] { At(0f, 30f) };

            VehicleRouteCommand command = default;
            for (var i = 0; i < 6; i++)
            {
                command = follower.Tick(points, false, Vector3.Zero, _north, 0f, 0.25f);
            }

            Assert.That(command.Phase, Is.EqualTo(VehicleRoutePhase.Reversing));
            Assert.That(command.Steer, Is.EqualTo(-1f));
        }

        [Test]
        public void MovingIsNeverStuck()
        {
            var follower = new VehicleRouteFollower(VehicleRouteSpec.Default);
            var points = new[] { At(0f, 100f) };

            for (var i = 0; i < 20; i++)
            {
                Assert.That(follower.Tick(points, false, Vector3.Zero, _north, 5f, 0.25f).Phase, Is.EqualTo(VehicleRoutePhase.Driving));
            }

            Assert.That(follower.StalledSeconds, Is.EqualTo(0f));
        }

        [Test]
        public void ItBrakesForTheLastPointAndStopsThere()
        {
            var follower = new VehicleRouteFollower(VehicleRouteSpec.Default);
            var points = new[] { At(0f, 10f), At(0f, 20f) };

            var approaching = follower.Tick(points, false, At(0f, 12f), _north, 12f, 0.02f);
            Assert.That(approaching.TargetIndex, Is.EqualTo(1));
            Assert.That(approaching.TargetSpeed, Is.EqualTo((float)Math.Sqrt(80.0)).Within(_tolerance));
            Assert.That(approaching.Brake, Is.True);

            var arrived = follower.Tick(points, false, At(0f, 18f), _north, 4f, 0.02f);
            Assert.That(arrived.Phase, Is.EqualTo(VehicleRoutePhase.Finished));
            Assert.That(follower.IsFinished, Is.True);
            Assert.That(arrived.Steer, Is.EqualTo(0f));
            Assert.That(arrived.Throttle, Is.EqualTo(0f));
            Assert.That(arrived.Brake, Is.True);

            var pushedAway = follower.Tick(points, false, Vector3.Zero, _north, 0f, 0.02f);
            Assert.That(pushedAway.Phase, Is.EqualTo(VehicleRoutePhase.Finished), "it stays stopped until restarted");
            Assert.That(pushedAway.Brake, Is.True);

            follower.Restart();
            var restarted = follower.Tick(points, false, Vector3.Zero, _north, 0f, 0.02f);
            Assert.That(restarted.Phase, Is.EqualTo(VehicleRoutePhase.Driving));
            Assert.That(restarted.TargetIndex, Is.EqualTo(0));
            Assert.That(restarted.Throttle, Is.EqualTo(1f));
        }

        [Test]
        public void ALoopGoesBackToTheFirstPointInsteadOfStopping()
        {
            var follower = new VehicleRouteFollower(VehicleRouteSpec.Default);
            var points = Square(20f);
            follower.Restart(3);

            var command = follower.Tick(points, true, At(0.5f, 0.5f), _north, 0f, 0.02f);

            Assert.That(command.Phase, Is.EqualTo(VehicleRoutePhase.Driving));
            Assert.That(command.TargetIndex, Is.EqualTo(0));
        }

        [Test]
        public void ASinglePointLoopStopsAtThePoint()
        {
            var follower = new VehicleRouteFollower(VehicleRouteSpec.Default);

            var command = follower.Tick(new[] { At(0f, 1f) }, true, Vector3.Zero, _north, 0f, 0.02f);

            Assert.That(command.Phase, Is.EqualTo(VehicleRoutePhase.Finished));
        }

        [Test]
        public void NoPointsMeansBrakeWithoutChangingProgress()
        {
            var follower = new VehicleRouteFollower(VehicleRouteSpec.Default);

            var command = follower.Tick(Array.Empty<Vector3>(), true, Vector3.Zero, _north, 3f, 0.02f);

            Assert.That(command.Brake, Is.True);
            Assert.That(command.Throttle, Is.EqualTo(0f));
            Assert.That(follower.Phase, Is.EqualTo(VehicleRoutePhase.Driving));
        }

        [Test]
        public void AShorterRouteClampsTheTargetToItsLastPoint()
        {
            var follower = new VehicleRouteFollower(VehicleRouteSpec.Default);
            follower.Restart(5);

            var command = follower.Tick(new[] { At(0f, 10f), At(0f, 40f) }, false, Vector3.Zero, _north, 0f, 0.02f);

            Assert.That(command.TargetIndex, Is.EqualTo(1));
        }

        [Test]
        public void BadInputIsRejected()
        {
            var follower = new VehicleRouteFollower(VehicleRouteSpec.Default);
            var points = new[] { At(0f, 10f) };

            Assert.Throws<ArgumentNullException>(() => new VehicleRouteFollower(null));
            Assert.Throws<ArgumentNullException>(() => follower.Tick(null, false, Vector3.Zero, _north, 0f, 0.02f));
            Assert.Throws<ArgumentOutOfRangeException>(() => follower.Tick(points, false, Vector3.Zero, _north, 0f, -0.02f));
            Assert.Throws<ArgumentOutOfRangeException>(() => follower.Tick(points, false, new Vector3(float.NaN, 0f, 0f), _north, 0f, 0.02f));
            Assert.Throws<ArgumentOutOfRangeException>(() => follower.Tick(points, false, Vector3.Zero, new Vector3(0f, 0f, float.PositiveInfinity), 0f, 0.02f));
            Assert.Throws<ArgumentOutOfRangeException>(() => follower.Tick(points, false, Vector3.Zero, _north, float.NaN, 0.02f));
            Assert.Throws<ArgumentException>(() => follower.Tick(new[] { new Vector3(float.NaN, 0f, 0f) }, false, Vector3.Zero, _north, 0f, 0.02f));
            Assert.Throws<ArgumentOutOfRangeException>(() => follower.Restart(-1));
        }

        // Core のテストでは物理を回せないので、横滑りしない簡単な車で何周もなぞれるかを見る。
        [Test]
        public void ASimpleCarLapsASquareLoopManyTimes()
        {
            const float deltaSeconds = 0.02f;
            const float turnRadius = 5f;
            const float acceleration = 6f;
            const float braking = 8f;
            var points = Square(40f);
            var follower = new VehicleRouteFollower(VehicleRouteSpec.Default);
            double x = 0.0, z = 0.0, heading = 0.0;
            var speed = 0f;
            var laps = 0;
            var previousIndex = follower.TargetIndex;
            var worstOffset = 0f;

            for (var step = 0; step < 6000; step++)
            {
                var forward = new Vector3((float)Math.Sin(heading), 0f, (float)Math.Cos(heading));
                var command = follower.Tick(points, true, new Vector3((float)x, 0f, (float)z), forward, speed, deltaSeconds);
                Assert.That(command.Phase, Is.EqualTo(VehicleRoutePhase.Driving), $"step {step}");
                if (previousIndex == points.Length - 1 && command.TargetIndex == 0)
                {
                    laps++;
                }

                previousIndex = command.TargetIndex;
                var accel = command.Brake ? -braking : command.Throttle * acceleration;
                speed = Math.Max(0f, speed + (accel * deltaSeconds));
                heading += command.Steer * speed / turnRadius * deltaSeconds;
                x += Math.Sin(heading) * speed * deltaSeconds;
                z += Math.Cos(heading) * speed * deltaSeconds;
                worstOffset = Math.Max(worstOffset, OffsetFromSquare((float)x, (float)z, 40f));
            }

            Assert.That(laps, Is.GreaterThanOrEqualTo(3));
            Assert.That(worstOffset, Is.LessThan(8f));
        }

        private static Vector3 At(float x, float z)
        {
            return new Vector3(x, 0f, z);
        }

        private static Vector3[] Square(float size)
        {
            return new[] { At(0f, size), At(size, size), At(size, 0f), At(0f, 0f) };
        }

        private static float OffsetFromSquare(float x, float z, float size)
        {
            var insideX = Math.Min(Math.Abs(x), Math.Abs(x - size));
            var insideZ = Math.Min(Math.Abs(z), Math.Abs(z - size));
            var outsideX = Math.Max(0f, Math.Max(-x, x - size));
            var outsideZ = Math.Max(0f, Math.Max(-z, z - size));
            if (outsideX > 0f || outsideZ > 0f)
            {
                return (float)Math.Sqrt((outsideX * outsideX) + (outsideZ * outsideZ));
            }

            return Math.Min(insideX, insideZ);
        }
    }
}
