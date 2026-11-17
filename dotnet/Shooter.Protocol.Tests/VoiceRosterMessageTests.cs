using System;
using NUnit.Framework;
using Shooter.Core;

namespace Shooter.Protocol.Tests
{
    public sealed class VoiceRosterMessageTests
    {
        private const string _alice = "0123456789abcdef0123456789abcdef";
        private const string _bob = "fedcba9876543210fedcba9876543210";

        private readonly MessageTable _client = MatchMessages.CreateClientTable();

        [Test]
        public void RoundTripsEveryEntryInOrder()
        {
            var sent = new VoiceRosterMessage();
            sent.Add(3, _alice);
            sent.Add(7, _bob);

            var got = Read(sent);

            Assert.That(got.Count, Is.EqualTo(2));
            Assert.That(got.PlayerId(0), Is.EqualTo(3));
            Assert.That(got.ProductUserId(0), Is.EqualTo(_alice));
            Assert.That(got.PlayerId(1), Is.EqualTo(7));
            Assert.That(got.ProductUserId(1), Is.EqualTo(_bob));
        }

        [Test]
        public void AnEmptyRosterIsOneCountByte()
        {
            var writer = new MessageWriter();
            writer.WriteMessage(new VoiceRosterMessage());

            // Id・版 (2 + 1)、数 (1)
            Assert.That(writer.Length, Is.EqualTo(3 + 1));
            Assert.That(Read(new VoiceRosterMessage()).Count, Is.Zero);
        }

        [Test]
        public void AShorterRosterDropsTheOldEntries()
        {
            var two = new VoiceRosterMessage();
            two.Add(1, _alice);
            two.Add(2, _bob);
            Read(two);
            var one = new VoiceRosterMessage();
            one.Add(2, _bob);

            var got = Read(one);

            Assert.That(got.Count, Is.EqualTo(1));
            Assert.That(got.ProductUserId(0), Is.EqualTo(_bob));
            Assert.Throws<ArgumentOutOfRangeException>(() => got.PlayerId(1));
        }

        [Test]
        public void AddRefusesWhatCannotBeSent()
        {
            var roster = new VoiceRosterMessage();
            roster.Add(1, _alice);

            Assert.Throws<ArgumentOutOfRangeException>(() => roster.Add(-1, _bob));
            Assert.Throws<ArgumentException>(() => roster.Add(2, "not-a-product-user-id"));
            Assert.Throws<ArgumentException>(() => roster.Add(2, null));
            Assert.Throws<ArgumentException>(() => roster.Add(2, _alice.ToUpperInvariant()), "the same id in another case is the same user");
            Assert.That(roster.Count, Is.EqualTo(1));

            roster.Clear();
            roster.Add(2, _alice);
            Assert.That(roster.PlayerId(0), Is.EqualTo(2));
        }

        [Test]
        public void ARosterHoldsAtMostTheLargestMatch()
        {
            var roster = new VoiceRosterMessage();
            for (var i = 0; i < VoiceRosterMessage.MaxEntries; i++)
            {
                roster.Add(i, i.ToString("x32", System.Globalization.CultureInfo.InvariantCulture));
            }

            Assert.That(VoiceRosterMessage.MaxEntries, Is.EqualTo(NetMatchSettings.MaxMaxPlayers));
            Assert.Throws<InvalidOperationException>(() => roster.Add(1000, _alice));
            Assert.That(Read(roster).Count, Is.EqualTo(VoiceRosterMessage.MaxEntries));
        }

        [Test]
        public void ARepeatedProductUserIdIsUnreadable()
        {
            var writer = Header(2);
            writer.WriteInt32(1);
            writer.WriteString(_alice);
            writer.WriteInt32(2);
            writer.WriteString(_alice);

            Assert.Throws<MessageReadException>(() => _client.Read(writer.AsSegment()));
        }

        [Test]
        public void AMalformedProductUserIdIsUnreadable()
        {
            var writer = Header(1);
            writer.WriteInt32(1);
            writer.WriteString("zz");

            Assert.Throws<MessageReadException>(() => _client.Read(writer.AsSegment()));
        }

        [Test]
        public void ANegativePlayerIdIsUnreadable()
        {
            var writer = Header(1);
            writer.WriteInt32(-1);
            writer.WriteString(_alice);

            Assert.Throws<MessageReadException>(() => _client.Read(writer.AsSegment()));
        }

        [Test]
        public void TooManyEntriesAreUnreadable()
        {
            Assert.Throws<MessageReadException>(() => _client.Read(Header(VoiceRosterMessage.MaxEntries + 1).AsSegment()));
        }

        private static MessageWriter Header(int count)
        {
            var writer = new MessageWriter();
            writer.WriteUInt16(MessageIds.VoiceRoster);
            writer.WriteByte(VoiceRosterMessage.CurrentVersion);
            writer.WriteByte((byte)count);
            return writer;
        }

        private VoiceRosterMessage Read(VoiceRosterMessage message)
        {
            var writer = new MessageWriter();
            writer.WriteMessage(message);
            return (VoiceRosterMessage)_client.Read(writer.AsSegment());
        }
    }
}
