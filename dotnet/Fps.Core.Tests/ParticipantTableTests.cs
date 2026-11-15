using System.Linq;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class ParticipantTableTests
    {
        private static readonly int[] _oneThenTwo = { 1, 2 };
        private static readonly int[] _withoutTwo = { 1, 3, 4 };

        [Test]
        public void FindsAParticipantById()
        {
            var table = new ParticipantTable();
            var me = new Participant(1, "P1", Team.Alpha, true);
            table.Set(me);
            table.Set(new Participant(2, "P2", Team.Bravo, false));

            Assert.That(table.TryGet(1, out var found), Is.True);
            Assert.That(found, Is.EqualTo(me));
            Assert.That(table.All.Count, Is.EqualTo(2));
        }

        [Test]
        public void AnUnknownIdIsNotFound()
        {
            var table = new ParticipantTable();
            table.Set(new Participant(1, "P1", Team.Alpha, false));

            Assert.That(table.TryGet(9, out var found), Is.False);
            Assert.That(found, Is.EqualTo(default(Participant)));
        }

        [Test]
        public void SettingTheSameIdReplacesItInPlace()
        {
            var table = new ParticipantTable();
            table.Set(new Participant(1, "P1", Team.Alpha, false));
            table.Set(new Participant(2, "P2", Team.Alpha, false));
            table.Set(new Participant(1, "P1", Team.Bravo, false));

            Assert.That(table.All.Select(p => p.Id), Is.EqualTo(_oneThenTwo));
            Assert.That(table.TryGet(1, out var found) && found.Team == Team.Bravo, Is.True);
        }

        [Test]
        public void KeepsTheOrderOfInsertionAcrossRemovals()
        {
            var table = new ParticipantTable();
            for (var id = 1; id <= 4; id++)
            {
                table.Set(new Participant(id, "P" + id, Team.Alpha, false));
            }

            Assert.That(table.Remove(2), Is.True);
            Assert.That(table.Remove(2), Is.False);
            Assert.That(table.All.Select(p => p.Id), Is.EqualTo(_withoutTwo));
            Assert.That(table.TryGet(4, out var last) && last.Id == 4, Is.True);
        }

        [Test]
        public void ClearForgetsEveryone()
        {
            var table = new ParticipantTable();
            table.Set(new Participant(1, "P1", Team.Alpha, false));
            table.Clear();

            Assert.That(table.All.Count, Is.Zero);
            Assert.That(table.TryGet(1, out _), Is.False);
        }

        [Test]
        public void ADefaultParticipantHasAnEmptyNameAndNoTeam()
        {
            Assert.That(default(Participant).Name, Is.Empty);
            Assert.That(default(Participant).Team, Is.Null);
            Assert.That(new Participant(1, null, null, false).Name, Is.Empty);
        }

        [Test]
        public void ParticipantsAreEqualByEveryField()
        {
            var a = new Participant(1, "P1", Team.Bravo, true);

            Assert.That(a, Is.EqualTo(new Participant(1, "P1", Team.Bravo, true)));
            Assert.That(a.GetHashCode(), Is.EqualTo(new Participant(1, "P1", Team.Bravo, true).GetHashCode()));
            Assert.That(a, Is.Not.EqualTo(new Participant(2, "P1", Team.Bravo, true)));
            Assert.That(a, Is.Not.EqualTo(new Participant(1, "P2", Team.Bravo, true)));
            Assert.That(a, Is.Not.EqualTo(new Participant(1, "P1", null, true)));
            Assert.That(a, Is.Not.EqualTo(new Participant(1, "P1", Team.Bravo, false)));
        }
    }
}
