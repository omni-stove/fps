using NUnit.Framework;
using Shooter.NetV2;
using Shooter.Protocol;

namespace Shooter.Net.Tests
{
    public sealed class NetV2PlayerNamesPartTests
    {
        private NetV2PlayerNamesPart _part;
        private int _changes;

        [SetUp]
        public void SetUp()
        {
            _part = new NetV2PlayerNamesPart();
            _changes = 0;
            _part.Names.Changed += () => _changes++;
        }

        [TearDown]
        public void TearDown()
        {
            _part.Dispose();
        }

        [Test]
        public void TheRosterIsTakenIntoTheNames()
        {
            Assert.That(_part.Handle(Roster((3, "Alice"))), Is.True);

            Assert.That(_part.Names.NameOf(3), Is.EqualTo("Alice"));
            Assert.That(_part.Names.NameOf(4), Is.EqualTo("P4"));
            Assert.That(_changes, Is.EqualTo(1));
        }

        [Test]
        public void OtherMessagesAreLeftForOtherParts()
        {
            Assert.That(_part.Handle(new VoiceRosterMessage()), Is.False);
            Assert.That(_changes, Is.Zero);
        }

        [Test]
        public void DisposingForgetsTheNamesAndIgnoresLaterRosters()
        {
            _part.Handle(Roster((3, "Alice")));

            _part.Dispose();
            Assert.That(_part.Handle(Roster((3, "Bob"))), Is.True);
            _part.Dispose();

            Assert.That(_part.Names.Count, Is.Zero);
            Assert.That(_part.Names.NameOf(3), Is.EqualTo("P3"));
            Assert.That(_changes, Is.EqualTo(2));
        }

        [Test]
        public void AGivenRosterIsTheOneFilled()
        {
            var shared = new ClientPlayerNames();
            var part = new NetV2PlayerNamesPart(shared);

            part.Handle(Roster((3, "Alice")));

            Assert.That(part.Names, Is.SameAs(shared));
            Assert.That(shared.NameOf(3), Is.EqualTo("Alice"));
            part.Dispose();
            Assert.That(shared.Count, Is.Zero);
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
    }
}
