using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class BeaconCoreTests
    {
        [Test]
        public void TheDefaultSpecIsValid()
        {
            var spec = BeaconSpec.Default;

            Assert.That(spec.Uses, Is.EqualTo(3));
            Assert.That(spec.SpawnDistance, Is.EqualTo(1f));
        }

        [TestCase(0, 1f)]
        [TestCase(-1, 1f)]
        [TestCase(1, -0.1f)]
        [TestCase(1, float.PositiveInfinity)]
        [TestCase(1, float.NaN)]
        public void InvalidSpecsAreRejected(int uses, float spawnDistance)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BeaconSpec(uses, spawnDistance));
        }

        [Test]
        public void AZeroSpawnDistanceIsAllowed()
        {
            Assert.That(new BeaconSpec(1, 0f).SpawnDistance, Is.Zero);
        }

        [Test]
        public void EachUseTakesOneAndTheLastUseEndsIt()
        {
            var state = new BeaconState(new BeaconSpec(2, 1f));

            Assert.That(state.UsesLeft, Is.EqualTo(2));
            Assert.That(state.Over, Is.Null);

            Assert.That(state.TryUse(), Is.True);
            Assert.That(state.UsesLeft, Is.EqualTo(1));
            Assert.That(state.Over, Is.Null);

            Assert.That(state.TryUse(), Is.True);
            Assert.That(state.UsesLeft, Is.Zero);
            Assert.That(state.Over, Is.EqualTo(DeployRemoval.Expended));

            Assert.That(state.TryUse(), Is.False, "a used-up beacon cannot be used again");
            Assert.That(state.UsesLeft, Is.Zero);
        }

        [Test]
        public void TheStateNeedsASpec()
        {
            Assert.Throws<ArgumentNullException>(() => new BeaconState(null));
        }

        [Test]
        public void TheLogLinesNameTheBeaconTheSideAndTheUses()
        {
            Assert.That(BeaconLog.Armed("Owner", "beacon", 1, 3, Side.Bravo), Is.EqualTo("[Beacon] owner=Owner kind=beacon serial=1 armed uses=3 side=1"));
            Assert.That(BeaconLog.Armed(null, "beacon", 2, 1, null), Is.EqualTo("[Beacon] owner=- kind=beacon serial=2 armed uses=1 side=-"));
            Assert.That(
                BeaconLog.Used("Owner", "beacon", 1, "Mate", new Vector3(1f, 0f, -2.5f), 2, 3),
                Is.EqualTo("[Beacon] owner=Owner kind=beacon serial=1 used by=Mate at=(1.00, 0.00, -2.50) uses=2/3"));
        }

        [Test]
        public void TheDefaultShapeOfABeaconComesFromItsSpec()
        {
            var deploy = DeployType.DefaultDeploy(DeployRole.Beacon);

            Assert.That(deploy.MaxActive, Is.EqualTo(BeaconSpec.DefaultMaxActive));
            Assert.That(deploy.MaxHealth, Is.EqualTo(BeaconSpec.DefaultMaxHealth));
            Assert.That(deploy.Width, Is.EqualTo(BeaconSpec.DefaultWidth));
            Assert.That(deploy.Height, Is.EqualTo(BeaconSpec.DefaultHeight));
            Assert.That(deploy.Depth, Is.EqualTo(BeaconSpec.DefaultDepth));
            Assert.That(deploy.Overflow, Is.EqualTo(DeploySpec.DefaultOverflow));
        }
    }
}
