using Fps.Core;
using NUnit.Framework;

namespace Fps.Server.Tests
{
    public sealed class ServerShotSequenceTests
    {
        [Test]
        public void MatchingIndicesNeedNoCorrection()
        {
            var sequence = new ServerShotSequence();

            Assert.That(sequence.Assign(0, 0).NeedsCorrection, Is.False);
            Assert.That(sequence.Assign(1, 0).Index, Is.EqualTo(1));
        }

        [Test]
        public void AMismatchIsCorrectedOncePerGeneration()
        {
            var sequence = new ServerShotSequence();

            var first = sequence.Assign(3, 0);
            var stale = sequence.Assign(4, 0);
            var resynced = sequence.Assign(2, 1);

            Assert.That(first.NeedsCorrection, Is.True);
            Assert.That(first.Index, Is.EqualTo(0));
            Assert.That(first.Generation, Is.EqualTo(1));
            Assert.That(stale.NeedsCorrection, Is.False, "shots sent before the correction arrived are off by the same amount");
            Assert.That(resynced.NeedsCorrection, Is.False);
            Assert.That(sequence.NextIndex, Is.EqualTo(3));
        }

        [Test]
        public void TheSupplyFollowsTheRulesAndTheInterval()
        {
            var supply = new ServerGrenadeSupply(0.6f, 0.15f);
            Assert.That(supply.Check(0d), Is.EqualTo(GrenadeThrowRejection.Disabled));

            supply.Refill(ActionRules.Default.WithGrenade(Grenade.On).WithGrenadeCount(2));
            Assert.That(supply.Take(0d), Is.EqualTo(GrenadeThrowRejection.None));
            Assert.That(supply.Take(0.4d), Is.EqualTo(GrenadeThrowRejection.TooSoon));
            Assert.That(supply.Take(0.5d), Is.EqualTo(GrenadeThrowRejection.None), "a request up to the jitter early is accepted");
            Assert.That(supply.Take(5d), Is.EqualTo(GrenadeThrowRejection.Empty));
            Assert.That(supply.Remaining, Is.EqualTo(0));
        }
    }
}
