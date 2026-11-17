using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
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
            var supply = new ServerGrenadeSupply(GrenadeCatalog.Default, 0.15f);
            Assert.That(supply.Check(0d, "", out _), Is.EqualTo(GrenadeThrowRejection.Disabled));

            supply.Refill(ActionRules.Default.WithGrenade(Grenade.On).WithGrenadeCount(2));
            Assert.That(supply.Take(0d, "", out var spec), Is.EqualTo(GrenadeThrowRejection.None));
            Assert.That(spec, Is.SameAs(GrenadeSpec.Default), "a request without a type throws the first carried type");
            Assert.That(supply.Take(0.4d, "", out _), Is.EqualTo(GrenadeThrowRejection.TooSoon));
            Assert.That(supply.Take(0.5d, GrenadeSpec.DefaultId, out _), Is.EqualTo(GrenadeThrowRejection.None), "a request up to the jitter early is accepted");
            Assert.That(supply.Take(5d, "", out _), Is.EqualTo(GrenadeThrowRejection.Empty));
            Assert.That(supply.RemainingOf(GrenadeSpec.DefaultId), Is.EqualTo(0));
        }

        [Test]
        public void EachTypeIsCountedOnItsOwnAndSetsTheNextInterval()
        {
            var slow = new GrenadeSpec(
                "ecaf6e57-cb77-4707-9321-6ea5b659408f", "grenade.smoke", new ImpactSpec(damage: 0), throwSeconds: 2f);
            var supply = new ServerGrenadeSupply(new GrenadeCatalog(new[] { GrenadeSpec.Default, slow }, new[] { GrenadeSpec.DefaultId, slow.Id }));
            supply.Refill(ActionRules.Default.WithGrenade(Grenade.On).WithGrenadeCount(1));

            Assert.That(supply.Take(0d, slow.Id, out var thrown), Is.EqualTo(GrenadeThrowRejection.None));
            Assert.That(thrown, Is.SameAs(slow));
            Assert.That(supply.Take(1d, GrenadeSpec.DefaultId, out _), Is.EqualTo(GrenadeThrowRejection.TooSoon), "the smoke's own throw time applies");
            Assert.That(supply.Take(2d, GrenadeSpec.DefaultId, out _), Is.EqualTo(GrenadeThrowRejection.None));
            Assert.That(supply.Take(9d, slow.Id, out _), Is.EqualTo(GrenadeThrowRejection.Empty));
            Assert.That(supply.Take(9d, "5eb3292d-6300-4ec2-b325-6ad793017e49", out _), Is.EqualTo(GrenadeThrowRejection.UnknownType));

            var sync = new GrenadePouchSync();
            supply.Fill(sync);
            Assert.That(sync.Enabled, Is.True);
            Assert.That(sync.Count, Is.EqualTo(2));
            Assert.That(sync.TypeIdAt(1), Is.EqualTo(slow.Id));
            Assert.That(sync.TotalRemaining, Is.Zero);
        }
    }
}
