using System;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2.Tests
{
    public sealed class ClientKillFeedTests
    {
        private const string _rifleId = "3a7f5e4d-6b8c-4d9e-9f0a-2b3c4d5e6f70";
        private static readonly int[] _assisters42 = { 4, 2 };
        private static readonly int[] _assisters4 = { 4 };
        private static readonly int[] _assisters98 = { 9, 8 };
        private static readonly int[] _assisters60 = { 6, 0 };

        [Test]
        public void CarriesEveryFieldAndTheAssistersInOrder()
        {
            var killed = Killed();
            killed.AddAssister(4);
            killed.AddAssister(2);

            var entry = ClientKillFeed.FromMessage(killed);

            Assert.That(entry, Is.EqualTo(new KillFeedEntry(1, 5, _rifleId, true, false, _assisters42)));
            Assert.That(entry.AssisterIds, Is.EqualTo(_assisters42));
        }

        [Test]
        public void AKillWithoutAssistersGivesAnEmptyList()
        {
            var entry = ClientKillFeed.FromMessage(Killed());

            Assert.That(entry.AssisterIds, Is.Empty);
            Assert.That(entry.ToString(), Is.EqualTo("killer=1 victim=5 weapon=" + _rifleId + " headshot=True friendly=False"));
        }

        [Test]
        public void TheEntryKeepsItsAssistersWhenTheMessageIsReused()
        {
            var killed = Killed();
            killed.AddAssister(4);
            var first = ClientKillFeed.FromMessage(killed);

            killed.ClearAssisters();
            killed.AddAssister(9);
            killed.AddAssister(8);
            var second = ClientKillFeed.FromMessage(killed);

            Assert.That(first.AssisterIds, Is.EqualTo(_assisters4));
            Assert.That(second.AssisterIds, Is.EqualTo(_assisters98));
        }

        [Test]
        public void ReadsTheAssistersOfAMessageThatCameOverTheWire()
        {
            var sent = Killed();
            sent.AddAssister(6);
            sent.AddAssister(0);
            var writer = new MessageWriter();
            sent.Write(writer);
            var received = new KilledMessage();
            received.Read(new MessageReader(writer.ToArray()));

            var entry = ClientKillFeed.FromMessage(received);

            Assert.That(entry, Is.EqualTo(new KillFeedEntry(1, 5, _rifleId, true, false, _assisters60)));
        }

        [Test]
        public void ANullMessageIsRejected()
        {
            var error = Assert.Throws<ArgumentNullException>(() => ClientKillFeed.FromMessage(null));

            Assert.That(error.ParamName, Is.EqualTo("killed"));
        }

        private static KilledMessage Killed()
        {
            return new KilledMessage { KillerId = 1, VictimId = 5, WeaponId = _rifleId, Headshot = true, Friendly = false };
        }
    }
}
