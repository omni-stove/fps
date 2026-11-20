using System;
using NUnit.Framework;
using Shooter.Core;

namespace Shooter.Protocol.Tests
{
    public sealed class PlayerNamesMessageTests
    {
        private readonly MessageTable _client = MatchMessages.CreateClientTable();

        [Test]
        public void RoundTripsEveryEntryInOrder()
        {
            var sent = new PlayerNamesMessage();
            sent.Add(3, "Ace");
            sent.Add(7, "名無しのたろう");

            var got = Read(sent);

            Assert.That(got.Count, Is.EqualTo(2));
            Assert.That(got.PlayerId(0), Is.EqualTo(3));
            Assert.That(got.Name(0), Is.EqualTo("Ace"));
            Assert.That(got.PlayerId(1), Is.EqualTo(7));
            Assert.That(got.Name(1), Is.EqualTo("名無しのたろう"));
        }

        [Test]
        public void AnEmptyRosterIsOneCountByte()
        {
            var writer = new MessageWriter();
            writer.WriteMessage(new PlayerNamesMessage());

            // Id・版 (2 + 1)、数 (1)
            Assert.That(writer.Length, Is.EqualTo(3 + 1));
            Assert.That(Read(new PlayerNamesMessage()).Count, Is.Zero);
        }

        [Test]
        public void AnEntryIsTheIdAndTheNameInUtf8()
        {
            var one = new PlayerNamesMessage();
            one.Add(1, "Ace");
            var writer = new MessageWriter();
            writer.WriteMessage(one);

            // Id・版 (2 + 1)、数 (1)、playerId (4)、名前の長さ・中身 (2 + 3)
            Assert.That(writer.Length, Is.EqualTo(3 + 1 + 4 + 2 + 3));
        }

        [Test]
        public void TheLongestNameEveryRuleAllowsRoundTrips()
        {
            var longest = new string('あ', NetMatchSettings.MaxMaxDisplayNameLength);
            var sent = new PlayerNamesMessage();
            sent.Add(1, longest);

            Assert.That(Read(sent).Name(0), Is.EqualTo(longest));
        }

        [Test]
        public void AShorterRosterDropsTheOldEntries()
        {
            var two = new PlayerNamesMessage();
            two.Add(1, "Ace");
            two.Add(2, "Bee");
            Read(two);
            var one = new PlayerNamesMessage();
            one.Add(2, "Bee");

            var got = Read(one);

            Assert.That(got.Count, Is.EqualTo(1));
            Assert.That(got.Name(0), Is.EqualTo("Bee"));
            Assert.Throws<ArgumentOutOfRangeException>(() => got.PlayerId(1));
        }

        [Test]
        public void AddRefusesWhatCannotBeSent()
        {
            var roster = new PlayerNamesMessage();
            roster.Add(1, "Ace");

            Assert.Throws<ArgumentOutOfRangeException>(() => roster.Add(-1, "Bee"));
            Assert.Throws<ArgumentException>(() => roster.Add(2, ""));
            Assert.Throws<ArgumentException>(() => roster.Add(2, null));
            Assert.Throws<ArgumentException>(() => roster.Add(2, " Bee"), "names are trimmed before they are sent");
            Assert.Throws<ArgumentException>(() => roster.Add(2, "B\u0007ee"));
            Assert.Throws<ArgumentException>(() => roster.Add(2, new string('a', NetMatchSettings.MaxMaxDisplayNameLength + 1)));
            Assert.Throws<ArgumentException>(() => roster.Add(1, "Bee"), "one player has one name");
            Assert.That(roster.Count, Is.EqualTo(1));

            roster.Clear();
            roster.Add(1, "Bee");
            Assert.That(roster.Name(0), Is.EqualTo("Bee"));
        }

        [Test]
        public void ARosterHoldsAtMostTheLargestMatch()
        {
            var roster = new PlayerNamesMessage();
            for (var i = 0; i < PlayerNamesMessage.MaxEntries; i++)
            {
                roster.Add(i, new string('w', NetMatchSettings.MaxMaxDisplayNameLength));
            }

            Assert.That(PlayerNamesMessage.MaxEntries, Is.EqualTo(NetMatchSettings.MaxMaxPlayers));
            Assert.Throws<InvalidOperationException>(() => roster.Add(1000, "Ace"));
            Assert.That(Read(roster).Count, Is.EqualTo(PlayerNamesMessage.MaxEntries));
        }

        [Test]
        public void ARepeatedPlayerIdIsUnreadable()
        {
            var writer = Header(2);
            writer.WriteInt32(1);
            writer.WriteString("Ace");
            writer.WriteInt32(1);
            writer.WriteString("Bee");

            Assert.Throws<MessageReadException>(() => _client.Read(writer.AsSegment()));
        }

        [Test]
        public void ANegativePlayerIdIsUnreadable()
        {
            var writer = Header(1);
            writer.WriteInt32(-1);
            writer.WriteString("Ace");

            Assert.Throws<MessageReadException>(() => _client.Read(writer.AsSegment()));
        }

        [TestCase("")]
        [TestCase(" Ace")]
        [TestCase("Ace\n")]
        [TestCase("A\u0000ce")]
        [TestCaseSource(nameof(_bidiOverrideName))]
        public void ANameTheServerWouldNotSendIsUnreadable(string name)
        {
            var writer = Header(1);
            writer.WriteInt32(1);
            writer.WriteString(name);

            Assert.Throws<MessageReadException>(() => _client.Read(writer.AsSegment()));
        }

        // An attribute argument must be a constant, and a raw U+202E in the source is invisible.
        private static readonly string[] _bidiOverrideName = { "A" + (char)0x202E + "ce" };

        [Test]
        public void ANameLongerThanAnyRuleAllowsIsUnreadable()
        {
            var writer = Header(1);
            writer.WriteInt32(1);
            writer.WriteString(new string('a', NetMatchSettings.MaxMaxDisplayNameLength + 1));

            Assert.Throws<MessageReadException>(() => _client.Read(writer.AsSegment()));
        }

        [Test]
        public void ANameOverTheByteLimitIsUnreadable()
        {
            var writer = Header(1);
            writer.WriteInt32(1);
            writer.WriteString(new string('a', PlayerDisplayName.MaxBytes + 1));

            Assert.Throws<MessageReadException>(() => _client.Read(writer.AsSegment()));
        }

        [Test]
        public void TooManyEntriesAreUnreadable()
        {
            Assert.Throws<MessageReadException>(() => _client.Read(Header(PlayerNamesMessage.MaxEntries + 1).AsSegment()));
        }

        [Test]
        public void TheConnectRequestCarriesAsManyNameBytesAsTheRoster()
        {
            Assert.That(ConnectRequestMessage.MaxDisplayNameBytes, Is.EqualTo(PlayerDisplayName.MaxBytes));
            Assert.That(ConnectRequestMessage.MaxDisplayNameBytes, Is.EqualTo(64));
        }

        private static MessageWriter Header(int count)
        {
            var writer = new MessageWriter();
            writer.WriteUInt16(MessageIds.PlayerNames);
            writer.WriteByte(PlayerNamesMessage.CurrentVersion);
            writer.WriteByte((byte)count);
            return writer;
        }

        private PlayerNamesMessage Read(PlayerNamesMessage message)
        {
            var writer = new MessageWriter();
            writer.WriteMessage(message);
            return (PlayerNamesMessage)_client.Read(writer.AsSegment());
        }
    }
}
