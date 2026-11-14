using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class ZiplineMotionTests
    {
        private const float _stepSeconds = 0.02f;

        private static readonly Vector3 _forward = new Vector3(0f, 0f, 1f);

        [Test]
        public void ALevelLineMovesAtTheMinimumSpeedTowardsTheFacing()
        {
            var motion = new ZiplineMotion(new Vector3(0f, 3f, 0f), new Vector3(0f, 3f, 10f), ZiplineSpec.Default);

            Assert.That(motion.TryGrab(new Vector3(0f, 3f, 2f), Vector3.Zero, -_forward), Is.True);
            Assert.That(motion.TravelSign, Is.EqualTo(-1), "faces the start");

            var moved = motion.Step(0.1f);

            Assert.That(motion.Speed, Is.EqualTo(ZiplineSpec.DefaultMinSpeed).Within(1e-4f));
            Assert.That(moved.Z, Is.EqualTo(-ZiplineSpec.DefaultMinSpeed * 0.1f).Within(1e-4f));
        }

        [Test]
        public void ASlopedLineGoesDownhillWhicheverWayThePlayerFaces()
        {
            var motion = new ZiplineMotion(new Vector3(0f, 2f, 0f), new Vector3(0f, 6f, 20f), ZiplineSpec.Default);

            Assert.That(motion.TryGrab(new Vector3(0f, 4f, 10f), Vector3.Zero, _forward), Is.True);

            Assert.That(motion.TravelSign, Is.EqualTo(-1), "the start is lower");
            Assert.That(motion.TravelDirection.Y, Is.LessThan(0f));
        }

        [Test]
        public void TheSpeedComesFromTheHeightDropped()
        {
            var spec = new ZiplineSpec(1f, 100f, 10f, 0.5f);
            var motion = new ZiplineMotion(new Vector3(0f, 20f, 0f), new Vector3(0f, 0f, 40f), spec);
            motion.TryGrab(motion.Start, Vector3.Zero, _forward);
            var startHeight = motion.Position.Y;

            RideToTheEnd(motion);

            var dropped = startHeight - motion.Position.Y;
            var expected = (float)Math.Sqrt(1f + (2f * spec.Gravity * dropped));
            Assert.That(motion.SpeedAt(motion.Position.Y), Is.EqualTo(expected).Within(1e-3f));
            Assert.That(motion.HasArrived, Is.True);
            Assert.That(motion.IsRiding, Is.False);
            Assert.That(motion.Distance, Is.EqualTo(motion.Length - spec.EndMargin).Within(1e-4f), "stops before the anchor");
        }

        [Test]
        public void ASteeperLineArrivesSoonerThanAGentleOne()
        {
            var gentle = new ZiplineMotion(new Vector3(0f, 2f, 0f), new Vector3(0f, 0f, 30f), ZiplineSpec.Default);
            var steep = new ZiplineMotion(new Vector3(0f, 10f, 0f), new Vector3(0f, 0f, 30f), ZiplineSpec.Default);
            gentle.TryGrab(gentle.Start, Vector3.Zero, _forward);
            steep.TryGrab(steep.Start, Vector3.Zero, _forward);

            Assert.That(RideToTheEnd(steep), Is.LessThan(RideToTheEnd(gentle)));
        }

        [Test]
        public void TheSpeedNeverExceedsTheMaximum()
        {
            var spec = new ZiplineSpec(2f, 6f, 9.81f, 0.5f);
            var motion = new ZiplineMotion(new Vector3(0f, 50f, 0f), new Vector3(0f, 0f, 60f), spec);
            motion.TryGrab(motion.Start, new Vector3(0f, 0f, 100f), _forward);

            Assert.That(motion.Speed, Is.EqualTo(6f), "the entry speed is clamped as well");
            for (var i = 0; i < 100 && motion.IsRiding; i++)
            {
                motion.Step(_stepSeconds);
                Assert.That(motion.Speed, Is.LessThanOrEqualTo(6f));
            }
        }

        [Test]
        public void TheMomentumAlongTheLineIsKeptWhenGrabbing()
        {
            var motion = new ZiplineMotion(new Vector3(0f, 3f, 0f), new Vector3(0f, 3f, 20f), ZiplineSpec.Default);

            motion.TryGrab(new Vector3(0f, 3f, 1f), new Vector3(3f, 0f, 7f), _forward);

            Assert.That(motion.Speed, Is.EqualTo(7f).Within(1e-4f), "only the part along the line counts");
        }

        [Test]
        public void GrabbingKeepsClearOfTheEndsAndRefusesAtTheFarEnd()
        {
            var motion = new ZiplineMotion(new Vector3(0f, 5f, 0f), new Vector3(0f, 0f, 10f), ZiplineSpec.Default);

            Assert.That(motion.TryGrab(new Vector3(0f, 5f, -3f), Vector3.Zero, _forward), Is.True);
            Assert.That(motion.Distance, Is.EqualTo(ZiplineSpec.DefaultEndMargin).Within(1e-4f));

            Assert.That(motion.TryGrab(motion.End, Vector3.Zero, _forward), Is.False, "already at the stop");
        }

        [Test]
        public void ReleasingKeepsTheVelocityAndStopsMoving()
        {
            var motion = new ZiplineMotion(new Vector3(0f, 5f, 0f), new Vector3(0f, 0f, 20f), ZiplineSpec.Default);
            motion.TryGrab(motion.Start, Vector3.Zero, _forward);
            motion.Step(_stepSeconds);

            Assert.That(motion.Release(), Is.True);
            Assert.That(motion.Release(), Is.False);
            Assert.That(motion.Velocity.Length(), Is.GreaterThan(0f));
            Assert.That(motion.Step(_stepSeconds), Is.EqualTo(Vector3.Zero));
            Assert.That(motion.HasArrived, Is.False);
        }

        [Test]
        public void ClosestDistanceIsClampedToTheSegment()
        {
            var motion = new ZiplineMotion(Vector3.Zero, new Vector3(0f, 0f, 10f), ZiplineSpec.Default);

            Assert.That(motion.ClosestDistance(new Vector3(1f, 0f, 4f)), Is.EqualTo(4f).Within(1e-4f));
            Assert.That(motion.ClosestDistance(new Vector3(0f, 0f, -5f)), Is.Zero);
            Assert.That(motion.DistanceTo(new Vector3(0f, 0f, 13f)), Is.EqualTo(3f).Within(1e-4f));
        }

        [Test]
        public void RejectsInvalidValues()
        {
            Assert.Throws<ArgumentNullException>(() => new ZiplineMotion(Vector3.Zero, _forward * 10f, null));
            Assert.Throws<ArgumentException>(() => new ZiplineMotion(Vector3.Zero, _forward, ZiplineSpec.Default), "shorter than both margins");
            Assert.Throws<ArgumentException>(() => new ZiplineMotion(new Vector3(float.NaN, 0f, 0f), _forward * 10f, ZiplineSpec.Default));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ZiplineMotion(Vector3.Zero, _forward * 10f, ZiplineSpec.Default).Step(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ZiplineSpec(0f, 1f, 1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ZiplineSpec(2f, 1f, 1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ZiplineSpec(1f, 2f, -1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ZiplineSpec(1f, 2f, 1f, float.PositiveInfinity));
        }

        private static float RideToTheEnd(ZiplineMotion motion)
        {
            var seconds = 0f;
            for (var i = 0; i < 100000 && motion.IsRiding; i++)
            {
                motion.Step(_stepSeconds);
                seconds += _stepSeconds;
            }

            Assert.That(motion.HasArrived, Is.True, "reached the end");
            return seconds;
        }
    }
}
