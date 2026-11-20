using System;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2.Tests
{
    public sealed class ClientPlayerNamesTests
    {
        private ClientPlayerNames _names;
        private int _changes;

        [SetUp]
        public void SetUp()
        {
            _names = new ClientPlayerNames();
            _changes = 0;
            _names.Changed += () => _changes++;
        }

        [Test]
        public void AReceivedRosterNamesItsPlayers()
        {
            Assert.That(_names.Handle(Read(Roster((3, "Alice"), (7, "Bob")))), Is.True);

            Assert.That(_names.Count, Is.EqualTo(2));
            Assert.That(_names.NameOf(3), Is.EqualTo("Alice"));
            Assert.That(_names.NameOf(7), Is.EqualTo("Bob"));
            Assert.That(_names.TryGetName(7, out var name), Is.True);
            Assert.That(name, Is.EqualTo("Bob"));
            Assert.That(_changes, Is.EqualTo(1));
        }

        [Test]
        public void OtherMessagesAreNotTaken()
        {
            Assert.That(_names.Handle(new VoiceRosterMessage()), Is.False);

            Assert.That(_names.Count, Is.Zero);
            Assert.That(_changes, Is.Zero);
        }

        [Test]
        public void ANewRosterReplacesTheOldOne()
        {
            _names.Handle(Read(Roster((3, "Alice"), (7, "Bob"))));

            _names.Handle(Read(Roster((7, "Robert"), (9, "Carol"))));

            Assert.That(_names.Count, Is.EqualTo(2));
            Assert.That(_names.NameOf(7), Is.EqualTo("Robert"));
            Assert.That(_names.NameOf(9), Is.EqualTo("Carol"));
            Assert.That(_names.TryGetName(3, out _), Is.False, "a player who left is no longer in the roster");
            Assert.That(_names.NameOf(3), Is.EqualTo("P3"));
            Assert.That(_changes, Is.EqualTo(2));
        }

        [Test]
        public void TheSameRosterAgainIsNotAChange()
        {
            _names.Handle(Read(Roster((3, "Alice"), (7, "Bob"))));

            _names.Handle(Read(Roster((7, "Bob"), (3, "Alice"))));

            Assert.That(_changes, Is.EqualTo(1));
        }

        [Test]
        public void ARenameIsAChange()
        {
            _names.Handle(Read(Roster((3, "Alice"))));

            _names.Handle(Read(Roster((3, "alice"))));

            Assert.That(_names.NameOf(3), Is.EqualTo("alice"), "names are compared by exact characters");
            Assert.That(_changes, Is.EqualTo(2));
        }

        [Test]
        public void AnEmptyRosterForgetsEveryName()
        {
            _names.Handle(Read(Roster((3, "Alice"))));

            _names.Handle(Read(Roster()));

            Assert.That(_names.Count, Is.Zero);
            Assert.That(_names.NameOf(3), Is.EqualTo("P3"));
            Assert.That(_changes, Is.EqualTo(2));
        }

        [Test]
        public void AnUnknownPlayerIsCalledByTheKillFeedName()
        {
            _names.Handle(Read(Roster((3, "Alice"))));

            Assert.That(_names.TryGetName(4, out var name), Is.False);
            Assert.That(name, Is.Null);
            Assert.That(_names.NameOf(4), Is.EqualTo("P4"));
            Assert.That(_names.NameOf(BotRoster.FirstBotPlayerId), Is.EqualTo("[BOT] B1"));
            Assert.That(_names.NameOf(BotRoster.FirstBotPlayerId + 2), Is.EqualTo(BotRoster.KillFeedName(BotRoster.FirstBotPlayerId + 2)));
        }

        [Test]
        public void BeforeAnyRosterEveryoneIsCalledByTheKillFeedName()
        {
            Assert.That(_names.NameOf(0), Is.EqualTo("P0"));
            Assert.That(_names.NameOf(12), Is.EqualTo("P12"));
        }

        [Test]
        public void ABotInTheRosterUsesItsRosterName()
        {
            _names.Handle(Read(Roster((BotRoster.FirstBotPlayerId, "Sentry"))));

            Assert.That(_names.NameOf(BotRoster.FirstBotPlayerId), Is.EqualTo("Sentry"));
        }

        [Test]
        public void ARosterOfAnotherVersionIsNotReadAndKeepsTheNames()
        {
            var table = MatchMessages.CreateClientTable();
            _names.Handle(Read(table, Roster((3, "Alice"))));
            var bytes = Bytes(Roster((3, "Mallory")));
            bytes[2] = PlayerNamesMessage.CurrentVersion + 1;

            Assert.That(() => table.Read(new ArraySegment<byte>(bytes)), Throws.TypeOf<MessageReadException>());

            Assert.That(_names.NameOf(3), Is.EqualTo("Alice"));
            Assert.That(_changes, Is.EqualTo(1));
        }

        [Test]
        public void ABrokenRosterBodyIsNotReadAndKeepsTheNames()
        {
            var table = MatchMessages.CreateClientTable();
            _names.Handle(Read(table, Roster((3, "Alice"))));
            var bytes = Bytes(Roster((3, "Alice"), (4, "Bob")));
            const int messageId = 2;
            const int version = 1;
            const int count = 1;
            const int playerId = 4;
            const int nameLength = 2;
            var secondPlayerId = messageId + version + count + playerId + nameLength + "Alice".Length;
            Assert.That(bytes[secondPlayerId], Is.EqualTo(4));
            bytes[secondPlayerId] = 3;

            Assert.That(() => table.Read(new ArraySegment<byte>(bytes)), Throws.TypeOf<MessageReadException>());

            Assert.That(_names.Count, Is.EqualTo(1));
            Assert.That(_names.NameOf(3), Is.EqualTo("Alice"));
            Assert.That(_names.NameOf(4), Is.EqualTo("P4"));
            Assert.That(_changes, Is.EqualTo(1));
        }

        [Test]
        public void ClearForgetsTheNamesAndTellsOnce()
        {
            _names.Handle(Read(Roster((3, "Alice"))));

            _names.Clear();
            _names.Clear();

            Assert.That(_names.Count, Is.Zero);
            Assert.That(_names.NameOf(3), Is.EqualTo("P3"));
            Assert.That(_changes, Is.EqualTo(2));
        }

        [Test]
        public void ApplyRefusesNull()
        {
            Assert.That(() => _names.Apply(null), Throws.TypeOf<ArgumentNullException>());
        }

        private static PlayerNamesMessage Roster(params (int PlayerId, string Name)[] entries)
        {
            var message = new PlayerNamesMessage();
            foreach (var (playerId, name) in entries)
            {
                message.Add(playerId, name);
            }

            return message;
        }

        private static byte[] Bytes(PlayerNamesMessage sent)
        {
            var writer = new MessageWriter();
            writer.WriteMessage(sent);
            return writer.ToArray();
        }

        private static IMessage Read(PlayerNamesMessage sent)
        {
            return Read(MatchMessages.CreateClientTable(), sent);
        }

        private static IMessage Read(MessageTable table, PlayerNamesMessage sent)
        {
            return table.Read(new ArraySegment<byte>(Bytes(sent)));
        }
    }
}
