using System;
using NUnit.Framework;
using Shooter.Protocol;

namespace Shooter.NetV2.Tests
{
    public sealed class VoiceRosterTests
    {
        private const string _alice = "0123456789abcdef0123456789abcdef";
        private const string _bob = "fedcba9876543210fedcba9876543210";

        private VoiceRoster _roster;
        private int _changes;

        [SetUp]
        public void SetUp()
        {
            _roster = new VoiceRoster();
            _changes = 0;
            _roster.Changed += () => _changes++;
        }

        [Test]
        public void ApplyReplacesTheWholeRosterWithOneChange()
        {
            _roster.Set(_bob, 9, true);
            _changes = 0;

            _roster.Apply(Message((1, _alice), (2, _bob)));

            Assert.That(_changes, Is.EqualTo(1));
            Assert.That(_roster.Count, Is.EqualTo(2));
            Assert.That(_roster.TryGetPlayerId(_alice, out var alice), Is.True);
            Assert.That(alice, Is.EqualTo(1));
            Assert.That(_roster.TryGetPlayerId(_bob.ToUpperInvariant(), out var bob), Is.True);
            Assert.That(bob, Is.EqualTo(2));
        }

        [Test]
        public void TheSameRosterAgainIsNotAChange()
        {
            _roster.Apply(Message((1, _alice), (2, _bob)));
            _changes = 0;

            _roster.Apply(Message((2, _bob), (1, _alice)));

            Assert.That(_changes, Is.Zero);
        }

        [Test]
        public void ADroppedOrMovedEntryIsAChange()
        {
            _roster.Apply(Message((1, _alice), (2, _bob)));
            _changes = 0;

            _roster.Apply(Message((1, _alice)));
            Assert.That(_changes, Is.EqualTo(1));
            Assert.That(_roster.TryGetPlayerId(_bob, out _), Is.False);

            _roster.Apply(Message((3, _alice)));
            Assert.That(_changes, Is.EqualTo(2));
            Assert.That(_roster.TryGetPlayerId(_alice, out var playerId), Is.True);
            Assert.That(playerId, Is.EqualTo(3));
        }

        [Test]
        public void AnEmptyRosterClearsEveryone()
        {
            _roster.Apply(Message((1, _alice)));

            _roster.Apply(Message());

            Assert.That(_roster.Count, Is.Zero);
            Assert.That(_changes, Is.EqualTo(2));
            Assert.Throws<ArgumentNullException>(() => _roster.Apply(null));
        }

        [Test]
        public void ApplyKeepsWhetherEachIdWasVerifiedAndAChangeOfItIsAChange()
        {
            var message = new VoiceRosterMessage();
            message.Add(1, _alice, true);
            message.Add(2, _bob, false);
            _roster.Apply(message);

            Assert.That(_roster.IsVerified(_alice), Is.True);
            Assert.That(_roster.IsVerified(_bob), Is.False);
            Assert.That(_roster.TryGetPlayerId(_bob, out _), Is.True, "an unverified id still routes the voice");

            _changes = 0;
            _roster.Apply(Message((1, _alice), (2, _bob)));

            Assert.That(_changes, Is.EqualTo(1));
            Assert.That(_roster.IsVerified(_bob), Is.True);
        }

        private static VoiceRosterMessage Message(params (int PlayerId, string UserId)[] entries)
        {
            var message = new VoiceRosterMessage();
            foreach (var entry in entries)
            {
                message.Add(entry.PlayerId, entry.UserId, true);
            }

            return message;
        }
    }
}
