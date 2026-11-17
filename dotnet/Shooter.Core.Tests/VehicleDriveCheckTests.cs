using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class VehicleDriveCheckTests
    {
        [Test]
        public void AMoveWithinTheSavedDistanceIsAcceptedAndSpendsIt()
        {
            // 上限 10 m/秒・0.5 秒分・見逃し 1 m なら、貯められるのは 6 m
            var check = new VehicleDriveCheck(new VehicleDriveSpec(10f, 1f, 0.5f));

            Assert.That(check.Allowance, Is.EqualTo(6f));
            Assert.That(check.Check(Vector3.Zero, new Vector3(4f, 0f, 0f)), Is.EqualTo(VehicleMoveRejection.None));
            Assert.That(check.Allowance, Is.EqualTo(2f).Within(1e-5f));
            Assert.That(check.Check(Vector3.Zero, new Vector3(3f, 0f, 0f)), Is.EqualTo(VehicleMoveRejection.TooFast));
            Assert.That(check.Allowance, Is.EqualTo(2f).Within(1e-5f), "a rejected move spends nothing");
        }

        [Test]
        public void TheDistanceRefillsWithTimeUpToTheBurst()
        {
            var check = new VehicleDriveCheck(new VehicleDriveSpec(10f, 1f, 0.5f));
            check.Check(Vector3.Zero, new Vector3(6f, 0f, 0f));

            check.Refill(0.1f);
            Assert.That(check.Allowance, Is.EqualTo(1f).Within(1e-5f));
            check.Refill(10f);
            Assert.That(check.Allowance, Is.EqualTo(6f));
        }

        [Test]
        public void ResetRefillsAtOnce()
        {
            var check = new VehicleDriveCheck(new VehicleDriveSpec(10f, 1f, 0.5f));
            check.Check(Vector3.Zero, new Vector3(5f, 0f, 0f));

            check.Reset();

            Assert.That(check.Allowance, Is.EqualTo(6f));
        }

        [Test]
        public void VelocityIsClampedToTheMaxSpeed()
        {
            var check = new VehicleDriveCheck(new VehicleDriveSpec(10f));

            Assert.That(check.ClampVelocity(new Vector3(0f, 0f, 30f)), Is.EqualTo(new Vector3(0f, 0f, 10f)));
            Assert.That(check.ClampVelocity(new Vector3(3f, 0f, 4f)), Is.EqualTo(new Vector3(3f, 0f, 4f)));
            Assert.That(check.ClampVelocity(new Vector3(float.NaN, 0f, 0f)), Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void TheTopSpeedGetsTheMargin()
        {
            Assert.That(VehicleDriveSpec.FromTopSpeed(20f).MaxSpeed, Is.EqualTo(30f));
            Assert.That(VehicleDriveSpec.FromTopSpeed(20f, 1.2f).MaxSpeed, Is.EqualTo(24f));
            Assert.That(() => VehicleDriveSpec.FromTopSpeed(20f, 0.5f), Throws.InstanceOf<ArgumentOutOfRangeException>());
        }

        [TestCase(0f, 1f, 0.5f)]
        [TestCase(10f, -1f, 0.5f)]
        [TestCase(10f, 1f, 0f)]
        [TestCase(float.PositiveInfinity, 1f, 0.5f)]
        public void InvalidLimitsAreRefused(float maxSpeed, float correction, float burst)
        {
            Assert.That(() => new VehicleDriveSpec(maxSpeed, correction, burst), Throws.InstanceOf<ArgumentOutOfRangeException>());
        }
    }
}
