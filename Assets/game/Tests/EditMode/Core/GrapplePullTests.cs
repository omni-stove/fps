using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class GrapplePullTests
    {
        private const float _step = 0.02f;

        private static readonly GrappleSpec _spec = new GrappleSpec(
            pullSpeed: 10f, pullAcceleration: 50f, arriveDistance: 0.25f, maxPullSeconds: 3f);

        [Test]
        public void AnIdlePullDoesNotMove()
        {
            var pull = new GrapplePull(_spec);

            Assert.That(pull.IsPulling, Is.False);
            Assert.That(pull.Step(Vector3.Zero, Vector3.UnitX * 10f, _step), Is.EqualTo(Vector3.Zero));
            Assert.That(pull.Ended, Is.EqualTo(GrappleEnd.None));
        }

        [Test]
        public void SpeedsUpToThePullSpeedTowardTheTarget()
        {
            var pull = new GrapplePull(_spec);
            var target = new Vector3(0f, 0f, 20f);
            pull.Start(Vector3.Zero, Vector3.Zero, target);

            var moved = pull.Step(Vector3.Zero, target, 0.1f);

            Assert.That(pull.Speed, Is.EqualTo(5f).Within(1e-4f));
            Assert.That(moved.Z, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(moved.X, Is.EqualTo(0f).Within(1e-6f));
            Assert.That(pull.Velocity.Z, Is.EqualTo(5f).Within(1e-4f));

            var position = moved;
            for (var i = 0; i < 5; i++)
            {
                position += pull.Step(position, target, 0.1f);
            }

            Assert.That(pull.IsPulling, Is.True, "still on the way");
            Assert.That(pull.Speed, Is.EqualTo(_spec.PullSpeed), "capped at the pull speed");
        }

        [Test]
        public void KeepsTheMomentumAlongTheRopeButNeverPullsBackwards()
        {
            var pull = new GrapplePull(_spec);
            var target = new Vector3(10f, 0f, 0f);

            pull.Start(Vector3.Zero, new Vector3(4f, 0f, 3f), target);
            Assert.That(pull.Speed, Is.EqualTo(4f).Within(1e-5f));

            pull.Start(Vector3.Zero, new Vector3(-6f, 0f, 0f), target);
            Assert.That(pull.Speed, Is.EqualTo(0f));

            pull.Start(Vector3.Zero, new Vector3(50f, 0f, 0f), target);
            Assert.That(pull.Speed, Is.EqualTo(_spec.PullSpeed), "a faster run-up is capped");
        }

        [Test]
        public void ArrivingMovesExactlyToTheTargetAndStops()
        {
            var pull = new GrapplePull(_spec);
            var target = new Vector3(0f, 3f, 4f);
            pull.Start(Vector3.Zero, Vector3.Zero, target);

            var position = Vector3.Zero;
            for (var i = 0; i < 500 && pull.IsPulling; i++)
            {
                position += pull.Step(position, target, _step);
            }

            Assert.That(pull.IsPulling, Is.False);
            Assert.That(pull.Ended, Is.EqualTo(GrappleEnd.Arrived));
            Assert.That(Vector3.Distance(position, target), Is.LessThan(1e-4f));
            Assert.That(pull.Velocity, Is.EqualTo(Vector3.Zero), "stops at the edge");
            Assert.That(pull.ElapsedSeconds, Is.GreaterThan(0f));
        }

        [Test]
        public void StartingWithinTheArriveDistanceArrivesOnTheFirstStep()
        {
            var pull = new GrapplePull(_spec);
            var target = new Vector3(0.1f, 0f, 0f);
            pull.Start(Vector3.Zero, Vector3.Zero, target);

            var moved = pull.Step(Vector3.Zero, target, _step);

            Assert.That(pull.Ended, Is.EqualTo(GrappleEnd.Arrived));
            Assert.That(moved, Is.EqualTo(target));
        }

        [Test]
        public void ABlockedPullGivesUpWithoutWaitingForTheTimeLimit()
        {
            var pull = new GrapplePull(_spec);
            var target = new Vector3(0f, 0f, 10f);
            pull.Start(Vector3.Zero, Vector3.Zero, target);

            var elapsed = 0f;
            for (var i = 0; i < 500 && pull.IsPulling; i++)
            {
                // 壁に当たって動けないので、毎回同じ位置を渡す。
                pull.Step(Vector3.Zero, target, _step);
                elapsed += _step;
            }

            Assert.That(pull.Ended, Is.EqualTo(GrappleEnd.Blocked));
            Assert.That(elapsed, Is.EqualTo(GrapplePull.StuckSeconds).Within(_step * 1.5f));
            Assert.That(elapsed, Is.LessThan(_spec.MaxPullSeconds));
            Assert.That(pull.Velocity, Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void AFarTargetTimesOut()
        {
            var spec = new GrappleSpec(pullSpeed: 10f, pullAcceleration: 50f, maxPullSeconds: 0.5f);
            var pull = new GrapplePull(spec);
            var target = new Vector3(0f, 0f, 100f);
            pull.Start(Vector3.Zero, Vector3.Zero, target);

            var position = Vector3.Zero;
            for (var i = 0; i < 500 && pull.IsPulling; i++)
            {
                position += pull.Step(position, target, _step);
            }

            Assert.That(pull.Ended, Is.EqualTo(GrappleEnd.TimedOut));
            Assert.That(pull.ElapsedSeconds, Is.EqualTo(0.5f).Within(_step * 1.5f));
        }

        [Test]
        public void ReleasingHandsBackTheMomentum()
        {
            var pull = new GrapplePull(_spec);
            var target = new Vector3(0f, 0f, 20f);
            pull.Start(Vector3.Zero, Vector3.Zero, target);
            var position = Vector3.Zero;
            for (var i = 0; i < 10; i++)
            {
                position += pull.Step(position, target, _step);
            }

            var speed = pull.Speed;
            var velocity = pull.Release(GrappleEnd.Jumped);

            Assert.That(velocity.Z, Is.EqualTo(speed).Within(1e-5f));
            Assert.That(pull.IsPulling, Is.False);
            Assert.That(pull.Ended, Is.EqualTo(GrappleEnd.Jumped));
            Assert.That(pull.Release(GrappleEnd.Jumped), Is.EqualTo(Vector3.Zero), "releasing twice gives nothing");
        }

        [Test]
        public void BadInputIsRejected()
        {
            var pull = new GrapplePull(_spec);

            Assert.Throws<ArgumentNullException>(() => new GrapplePull(null));
            Assert.Throws<ArgumentException>(() => pull.Start(new Vector3(float.NaN, 0f, 0f), Vector3.Zero, Vector3.UnitX));
            Assert.Throws<ArgumentOutOfRangeException>(() => pull.Step(Vector3.Zero, Vector3.UnitX, -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => pull.Release(GrappleEnd.None));
            Assert.Throws<ArgumentOutOfRangeException>(() => pull.Release(GrappleEnd.Arrived));
        }
    }
}
