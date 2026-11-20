using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    public sealed class ServerPlayerNamesTests
    {
        private const int _maxLength = NetMatchSettings.DefaultMaxDisplayNameLength;

        [Test]
        public void EachJoinSendsEveryoneInJoinOrder()
        {
            var names = Names();

            var first = Entries(names.Join(Human(1, "Alice")));
            Assert.That(first, Is.EqualTo(new[] { (1, "Alice") }));

            var second = Entries(names.Join(Human(2, "Bob")));
            Assert.That(second, Is.EqualTo(new[] { (1, "Alice"), (2, "Bob") }));
            Assert.That(names.Count, Is.EqualTo(2));
        }

        [Test]
        public void ALeaveSendsTheRestAndAnUnknownIdSendsTheSameRoster()
        {
            var names = Names();
            names.Join(Human(1, "Alice"));
            names.Join(Human(2, "Bob"));
            names.Join(Human(3, "Carol"));

            Assert.That(Entries(names.Leave(2)), Is.EqualTo(new[] { (1, "Alice"), (3, "Carol") }));
            Assert.That(Entries(names.Leave(99)), Is.EqualTo(new[] { (1, "Alice"), (3, "Carol") }));
            Assert.That(names.TryGetName(2, out _), Is.False);
            Assert.That(Entries(names.Leave(1)), Is.EqualTo(new[] { (3, "Carol") }));
            Assert.That(Entries(names.Leave(3)), Is.Empty);
        }

        [Test]
        public void TheRosterIsTheOneTheLastCallReturned()
        {
            var names = Names();

            var roster = names.Join(Human(1, "Alice"));

            Assert.That(names.Current, Is.SameAs(roster));
            Assert.That(names.Leave(1), Is.SameAs(roster));
        }

        [TestCase(ChatWordFilterAction.Mask)]
        [TestCase(ChatWordFilterAction.Drop)]
        public void ANameWithAFilteredWordFallsBackToTheIdName(ChatWordFilterAction action)
        {
            var names = new ServerPlayerNames(new ChatWordFilter(new ChatWordFilterSpec(new[] { "badword" }, action: action)), _maxLength);

            var roster = Entries(names.Join(Human(3, "xBADWORDx")));

            Assert.That(roster, Is.EqualTo(new[] { (3, "P3") }), "a masked name would still show which word it was");
        }

        [Test]
        public void TheFilterMatchesFullWidthLettersAsInChat()
        {
            var names = new ServerPlayerNames(new ChatWordFilter(new ChatWordFilterSpec(new[] { "bad" })), _maxLength);

            Assert.That(Entries(names.Join(Human(4, "ＢＡＤguy"))), Is.EqualTo(new[] { (4, "P4") }));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("Al\tice")]
        [TestCase("ABCDEFGHIJKLMNOPQ")]
        public void AnEmptyOrRefusedNameFallsBackToTheIdName(string displayName)
        {
            var names = Names();

            Assert.That(Entries(names.Join(Human(7, displayName))), Is.EqualTo(new[] { (7, "P7") }));
        }

        [Test]
        public void SurroundingSpacesAreTrimmed()
        {
            var names = Names();

            Assert.That(Entries(names.Join(Human(1, "  Alice "))), Is.EqualTo(new[] { (1, "Alice") }));
        }

        [Test]
        public void ALaterPlayerWithTheSameNameGetsTheNextNumber()
        {
            var names = Names();
            names.Join(Human(1, "Alice"));
            names.Join(Human(2, "alice"));

            var roster = Entries(names.Join(Human(3, "ＡＬＩＣＥ")));

            Assert.That(roster, Is.EqualTo(new[] { (1, "Alice"), (2, "alice (2)"), (3, "ＡＬＩＣＥ (3)") }));
        }

        [Test]
        public void ANumberIsKeptWhenTheFirstPlayerLeavesAndTheFreedNameCanBeTakenAgain()
        {
            var names = Names();
            names.Join(Human(1, "Alice"));
            names.Join(Human(2, "Alice"));
            names.Leave(1);

            var roster = Entries(names.Join(Human(3, "Alice")));

            Assert.That(roster, Is.EqualTo(new[] { (2, "Alice (2)"), (3, "Alice") }));
        }

        [Test]
        public void ANumberSkipsANameSomeoneAlreadyTyped()
        {
            var names = Names();
            names.Join(Human(1, "Alice"));
            names.Join(Human(2, "Alice (2)"));

            Assert.That(Entries(names.Join(Human(3, "Alice"))), Is.EqualTo(new[] { (1, "Alice"), (2, "Alice (2)"), (3, "Alice (3)") }));
        }

        [Test]
        public void AFallbackNameThatSomeoneTypedIsNumberedToo()
        {
            var names = Names();
            names.Join(Human(1, "P2"));

            Assert.That(Entries(names.Join(Human(2, ""))), Is.EqualTo(new[] { (1, "P2"), (2, "P2 (2)") }));
        }

        [Test]
        public void ANumberedFullLengthNameKeepsTheWholeName()
        {
            var names = Names();
            names.Join(Human(1, "ABCDEFGHIJKLMNOP"));

            var roster = Entries(names.Join(Human(2, "ABCDEFGHIJKLMNOP")));

            Assert.That(roster[1], Is.EqualTo((2, "ABCDEFGHIJKLMNOP (2)")));
            Assert.That(PlayerDisplayName.IsValid(roster[1].Item2, NetMatchSettings.MaxMaxDisplayNameLength), Is.True);
        }

        [Test]
        public void ANumberedNameAtTheSendableLimitIsCutBeforeTheNumber()
        {
            var names = new ServerPlayerNames(new ChatWordFilter(ChatWordFilterSpec.Default), NetMatchSettings.MaxMaxDisplayNameLength);
            var full = new string('A', NetMatchSettings.MaxMaxDisplayNameLength);
            names.Join(Human(1, full));

            var roster = Entries(names.Join(Human(2, full)));

            Assert.That(roster[1], Is.EqualTo((2, new string('A', NetMatchSettings.MaxMaxDisplayNameLength - 4) + " (2)")));
        }

        [Test]
        public void BotsTakeTheirRosterNameWhateverTheyCarry()
        {
            var names = Names();
            names.Join(Human(1, "Alice"));
            names.Join(Bot(BotRoster.FirstBotPlayerId, "Mallory"));

            var roster = Entries(names.Join(Bot(BotRoster.FirstBotPlayerId + 1, null)));

            Assert.That(roster, Is.EqualTo(new[]
            {
                (1, "Alice"),
                (BotRoster.FirstBotPlayerId, "[BOT] B1"),
                (BotRoster.FirstBotPlayerId + 1, "[BOT] B2"),
            }));
        }

        [Test]
        public void ABotJoiningAfterAHumanWithItsNameIsNumbered()
        {
            var names = Names();
            names.Join(Human(1, "[BOT] B1"));

            var roster = Entries(names.Join(Bot(BotRoster.FirstBotPlayerId, null)));

            Assert.That(roster, Is.EqualTo(new[] { (1, "[BOT] B1"), (BotRoster.FirstBotPlayerId, "[BOT] B1 (2)") }));
        }

        [Test]
        public void JoiningAgainKeepsThePlaceAndTakesTheNewName()
        {
            var names = Names();
            names.Join(Human(1, "Alice"));
            names.Join(Human(2, "Bob"));

            var roster = Entries(names.Join(Human(1, "Alicia")));

            Assert.That(roster, Is.EqualTo(new[] { (1, "Alicia"), (2, "Bob") }));
        }

        [Test]
        public void JoiningAgainWithTheSameNameDoesNotNumberItself()
        {
            var names = Names();
            names.Join(Human(1, "Alice"));

            Assert.That(Entries(names.Join(Human(1, "Alice"))), Is.EqualTo(new[] { (1, "Alice") }));
        }

        [Test]
        public void TheRosterRoundTripsThroughTheWire()
        {
            var names = Names();
            names.Join(Human(1, "Alice"));
            names.Join(Human(2, "Alice"));
            var roster = names.Join(Bot(BotRoster.FirstBotPlayerId, null));

            var writer = new MessageWriter();
            writer.WriteMessage(roster);
            var read = (PlayerNamesMessage)MatchMessages.CreateClientTable().Read(writer.AsSegment());

            Assert.That(Entries(read), Is.EqualTo(new[] { (1, "Alice"), (2, "Alice (2)"), (BotRoster.FirstBotPlayerId, "[BOT] B1") }));
        }

        [Test]
        public void ClearEmptiesTheRoster()
        {
            var names = Names();
            names.Join(Human(1, "Alice"));

            names.Clear();

            Assert.That(names.Count, Is.Zero);
            Assert.That(names.Current.Count, Is.Zero);
            Assert.That(Entries(names.Join(Human(2, "Alice"))), Is.EqualTo(new[] { (2, "Alice") }));
        }

        [Test]
        public void TheRosterRefusesMorePlayersThanAMessageHolds()
        {
            var names = Names();
            for (var i = 1; i <= PlayerNamesMessage.MaxEntries; i++)
            {
                names.Join(Human(i, null));
            }

            Assert.Throws<InvalidOperationException>(() => names.Join(Human(PlayerNamesMessage.MaxEntries + 1, null)));
            Assert.That(names.Count, Is.EqualTo(PlayerNamesMessage.MaxEntries));
        }

        [Test]
        public void TheConstructorRefusesAMissingFilterAndAnOutOfRangeLength()
        {
            var filter = new ChatWordFilter(ChatWordFilterSpec.Default);

            Assert.Throws<ArgumentNullException>(() => new ServerPlayerNames(null, _maxLength));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ServerPlayerNames(filter, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ServerPlayerNames(filter, NetMatchSettings.MaxMaxDisplayNameLength + 1));
            Assert.Throws<ArgumentNullException>(() => Names().Join(null));
        }

        private static ServerPlayerNames Names()
        {
            return new ServerPlayerNames(new ChatWordFilter(ChatWordFilterSpec.Default), _maxLength);
        }

        private static ServerPlayer Human(int playerId, string displayName)
        {
            return new ServerPlayer(new ConnectionId((ulong)playerId), playerId, Team.Alpha, "k" + playerId, displayName: displayName);
        }

        private static ServerPlayer Bot(int playerId, string displayName)
        {
            return new ServerPlayer(default, playerId, Team.Bravo, "bot" + playerId, displayName: displayName);
        }

        private static List<(int, string)> Entries(PlayerNamesMessage message)
        {
            var entries = new List<(int, string)>();
            for (var i = 0; i < message.Count; i++)
            {
                entries.Add((message.PlayerId(i), message.Name(i)));
            }

            return entries;
        }
    }
}
