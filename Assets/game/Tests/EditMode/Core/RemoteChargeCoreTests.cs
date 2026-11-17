using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class RemoteChargeCoreTests
    {
        [Test]
        public void TheDefaultSpecIsValid()
        {
            var spec = RemoteChargeSpec.Default;

            Assert.That(spec.DetonateTapSeconds, Is.EqualTo(RemoteChargeSpec.DefaultDetonateTapSeconds));
            Assert.That(spec.Explosion.Damage.MaxDamage, Is.EqualTo(RemoteChargeSpec.DefaultMaxDamage));
            Assert.That(spec.Explosion.Damage.Radius, Is.EqualTo(RemoteChargeSpec.DefaultDamageRadius));
            Assert.That(spec.Explosion.Damage.FullDamageRadius, Is.EqualTo(RemoteChargeSpec.DefaultFullDamageRadius));
            Assert.That(spec.Explosion.StructureDamageAtCenter, Is.EqualTo(RemoteChargeSpec.DefaultStructureDamageAtCenter));
            Assert.That(RemoteChargeSpec.DefaultOverflow, Is.EqualTo(DeployOverflow.Refuse), "placing past the limit is refused");
        }

        [Test]
        public void TheDefaultShapeFitsTheDeploySpec()
        {
            Assert.DoesNotThrow(() => new DeploySpec(
                RemoteChargeSpec.DefaultMaxActive,
                RemoteChargeSpec.DefaultOverflow,
                RemoteChargeSpec.DefaultMaxHealth,
                RemoteChargeSpec.DefaultMaxRange,
                DeploySpec.DefaultMaxSlopeDegrees,
                DeploySpec.DefaultMaxStepHeight,
                RemoteChargeSpec.DefaultWidth,
                RemoteChargeSpec.DefaultHeight,
                RemoteChargeSpec.DefaultDepth));
        }

        [TestCase(-0.1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void AnInvalidTapTimeIsRejected(float tapSeconds)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RemoteChargeSpec(tapSeconds, RemoteChargeSpec.Default.Explosion));
        }

        [Test]
        public void AMissingExplosionIsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new RemoteChargeSpec(0.25f, null));
        }

        [Test]
        public void LettingGoBeforeTheTapTimeDetonates()
        {
            Assert.That(DeployReuse.Decide(false, 0f, 0.25f), Is.EqualTo(DeployReuseStep.Detonate));
            Assert.That(DeployReuse.Decide(false, 0.2f, 0.25f), Is.EqualTo(DeployReuseStep.Detonate));
        }

        [Test]
        public void HoldingWaitsUntilTheTapTimeThenAims()
        {
            Assert.That(DeployReuse.Decide(true, 0.2f, 0.25f), Is.EqualTo(DeployReuseStep.Wait));
            Assert.That(DeployReuse.Decide(true, 0.25f, 0.25f), Is.EqualTo(DeployReuseStep.Aim));
            Assert.That(DeployReuse.Decide(false, 0.3f, 0.25f), Is.EqualTo(DeployReuseStep.Aim), "letting go after holding places");
        }

        [Test]
        public void AZeroTapTimeAlwaysAims()
        {
            Assert.That(DeployReuse.Decide(false, 0f, 0f), Is.EqualTo(DeployReuseStep.Aim));
        }

        [Test]
        public void InvalidTimesAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => DeployReuse.Decide(true, -1f, 0.25f));
            Assert.Throws<ArgumentOutOfRangeException>(() => DeployReuse.Decide(true, float.NaN, 0.25f));
            Assert.Throws<ArgumentOutOfRangeException>(() => DeployReuse.Decide(true, 0f, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => DeployReuse.Decide(true, 0f, float.PositiveInfinity));
        }

        [Test]
        public void TheLogsNameWhatItStuckToAndWhereItWentOff()
        {
            Assert.That(
                RemoteChargeLog.Attached("P1", "deploy-remote-charge", 2, "Dummy"),
                Is.EqualTo("[RemoteCharge] owner=P1 kind=deploy-remote-charge serial=2 attached to=Dummy"));
            Assert.That(
                RemoteChargeLog.Attached("P1", "deploy-remote-charge", 2, null),
                Is.EqualTo("[RemoteCharge] owner=P1 kind=deploy-remote-charge serial=2 attached to=-"));
            Assert.That(
                RemoteChargeLog.Detonating("P1", "deploy-remote-charge", 3),
                Is.EqualTo("[RemoteCharge] owner=P1 kind=deploy-remote-charge detonating count=3"));
            Assert.That(
                RemoteChargeLog.Detonated("P1", "deploy-remote-charge", 2, new Vector3(1f, 0.1f, -2f)),
                Is.EqualTo("[RemoteCharge] owner=P1 kind=deploy-remote-charge serial=2 detonated at=(1.00, 0.10, -2.00)"));
        }
    }
}
