using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class RespawnAnchorOrderTests
    {
        private static readonly RespawnAnchor[] _beaconSquadBase = { RespawnAnchor.Beacon, RespawnAnchor.Squad, RespawnAnchor.Base };

        [Test]
        public void TheFirstAnchorThatCanBeUsedIsChosenAndLaterOnesAreNotTried()
        {
            var tried = new List<RespawnAnchor>();

            var chosen = RespawnAnchorOrder.Choose(_beaconSquadBase, a =>
            {
                tried.Add(a);
                return true;
            });

            Assert.That(chosen, Is.EqualTo(RespawnAnchor.Beacon));
            Assert.That(tried, Is.EqualTo(new[] { RespawnAnchor.Beacon }), "the squad is not tried once the beacon is used");
        }

        [Test]
        public void AnAnchorThatCannotBeUsedFallsToTheNextInOrder()
        {
            var tried = new List<RespawnAnchor>();

            var chosen = RespawnAnchorOrder.Choose(_beaconSquadBase, a =>
            {
                tried.Add(a);
                return a == RespawnAnchor.Squad;
            });

            Assert.That(chosen, Is.EqualTo(RespawnAnchor.Squad));
            Assert.That(tried, Is.EqualTo(new[] { RespawnAnchor.Beacon, RespawnAnchor.Squad }));
        }

        [Test]
        public void TheWrittenOrderIsFollowed()
        {
            var tried = new List<RespawnAnchor>();

            var chosen = RespawnAnchorOrder.Choose(new[] { RespawnAnchor.Squad, RespawnAnchor.Beacon }, a =>
            {
                tried.Add(a);
                return true;
            });

            Assert.That(chosen, Is.EqualTo(RespawnAnchor.Squad));
            Assert.That(tried, Is.EqualTo(new[] { RespawnAnchor.Squad }));
        }

        [Test]
        public void WhenNothingCanBeUsedTheBaseIsChosenWithoutAskingForIt()
        {
            var tried = new List<RespawnAnchor>();

            var chosen = RespawnAnchorOrder.Choose(_beaconSquadBase, a =>
            {
                tried.Add(a);
                return false;
            });

            Assert.That(chosen, Is.EqualTo(RespawnAnchor.Base));
            Assert.That(tried, Is.EqualTo(new[] { RespawnAnchor.Beacon, RespawnAnchor.Squad }));
        }

        [Test]
        public void WithoutTheBaseInTheListNothingUsableStillReturnsTheBase()
        {
            Assert.That(RespawnAnchorOrder.Choose(new[] { RespawnAnchor.Squad, RespawnAnchor.Beacon }, _ => false), Is.EqualTo(RespawnAnchor.Base));
        }

        [Test]
        public void TheBaseStopsTheOrderBeforeTheAnchorsAfterIt()
        {
            var tried = new List<RespawnAnchor>();

            var chosen = RespawnAnchorOrder.Choose(new[] { RespawnAnchor.Base, RespawnAnchor.Beacon }, a =>
            {
                tried.Add(a);
                return true;
            });

            Assert.That(chosen, Is.EqualTo(RespawnAnchor.Base));
            Assert.That(tried, Is.Empty);
        }

        [Test]
        public void TheDefaultRulesRespawnAtTheBase()
        {
            Assert.That(RespawnAnchorOrder.Choose(GameRules.Default.RespawnAnchors, _ => true), Is.EqualTo(RespawnAnchor.Base));
        }

        [Test]
        public void NullArgumentsAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => RespawnAnchorOrder.Choose(null, _ => true));
            Assert.Throws<ArgumentNullException>(() => RespawnAnchorOrder.Choose(_beaconSquadBase, null));
        }
    }
}
