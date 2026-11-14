using System;
using System.Linq;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class ScoreboardRowsTests
    {
        // 定数配列を直接渡すと CA1861 になるので params で受ける
        private static int[] Ids(params int[] expected) => expected;

        private static ScoreboardEntry Entry(
            int id, Team team, int kills, int deaths,
            ScoreboardConnection connection = ScoreboardConnection.Connected, bool isLocal = false)
        {
            return new ScoreboardEntry(id, ScoreboardRows.PlayerName(id), team, kills, deaths, connection, isLocal);
        }

        [Test]
        public void SplitsByTeamWithTheFirstTeamFirst()
        {
            var entries = new[]
            {
                Entry(0, Team.Alpha, 1, 0),
                Entry(1, Team.Bravo, 2, 0),
                Entry(2, Team.Alpha, 3, 0),
            };

            var sections = ScoreboardRows.Build(entries, Team.Bravo);

            Assert.That(sections.Select(s => s.Team), Is.EqualTo(new[] { Team.Bravo, Team.Alpha }));
            Assert.That(sections[0].Rows.Select(r => r.PlayerId), Is.EqualTo(Ids(1)));
            Assert.That(sections[1].Rows.Select(r => r.PlayerId), Is.EqualTo(Ids(2, 0)));
        }

        [Test]
        public void OrdersByKillsThenFewerDeathsThenPlayerId()
        {
            var entries = new[]
            {
                Entry(5, Team.Alpha, 2, 3),
                Entry(1, Team.Alpha, 4, 0),
                Entry(3, Team.Alpha, 2, 1),
                Entry(2, Team.Alpha, 2, 1),
                Entry(4, Team.Alpha, 0, 0),
            };

            var rows = ScoreboardRows.Build(entries, Team.Alpha)[0].Rows;

            Assert.That(rows.Select(r => r.PlayerId), Is.EqualTo(Ids(1, 2, 3, 5, 4)));
        }

        [Test]
        public void TheOrderDoesNotDependOnTheInputOrder()
        {
            var entries = new[]
            {
                Entry(0, Team.Alpha, 1, 1),
                Entry(1, Team.Alpha, 1, 1),
                Entry(2, Team.Bravo, 0, 2),
                Entry(3, Team.Bravo, 0, 2),
            };

            var forward = ScoreboardRows.Build(entries, Team.Alpha);
            var backward = ScoreboardRows.Build(entries.Reverse(), Team.Alpha);

            for (var i = 0; i < forward.Count; i++)
            {
                Assert.That(backward[i].Rows, Is.EqualTo(forward[i].Rows));
            }
        }

        [Test]
        public void AnEmptyTeamStillHasASection()
        {
            var sections = ScoreboardRows.Build(new[] { Entry(0, Team.Alpha, 0, 0) }, Team.Alpha);

            Assert.That(sections, Has.Count.EqualTo(2));
            Assert.That(sections[1].Team, Is.EqualTo(Team.Bravo));
            Assert.That(sections[1].Rows, Is.Empty);
            Assert.That(sections[1].Kills, Is.Zero);
            Assert.That(sections[1].Deaths, Is.Zero);
        }

        [Test]
        public void NoEntriesGiveTwoEmptySections()
        {
            var sections = ScoreboardRows.Build(Array.Empty<ScoreboardEntry>(), Team.Alpha);

            Assert.That(sections.Select(s => s.Team), Is.EqualTo(new[] { Team.Alpha, Team.Bravo }));
            Assert.That(sections.All(s => s.Rows.Count == 0), Is.True);
        }

        [Test]
        public void SectionsSumTheirKillsAndDeaths()
        {
            var entries = new[]
            {
                Entry(0, Team.Alpha, 3, 1),
                Entry(1, Team.Alpha, 2, 4),
                Entry(2, Team.Bravo, 5, 5),
            };

            var sections = ScoreboardRows.Build(entries, Team.Alpha);

            Assert.That(sections[0].Kills, Is.EqualTo(5));
            Assert.That(sections[0].Deaths, Is.EqualTo(5));
            Assert.That(sections[1].Kills, Is.EqualTo(5));
            Assert.That(sections[1].Deaths, Is.EqualTo(5));
        }

        [Test]
        public void SectionTotalsStopAtIntMaxValue()
        {
            var entries = new[]
            {
                Entry(0, Team.Alpha, int.MaxValue, int.MaxValue),
                Entry(1, Team.Alpha, 1, 1),
            };

            var section = ScoreboardRows.Build(entries, Team.Alpha)[0];

            Assert.That(section.Kills, Is.EqualTo(int.MaxValue));
            Assert.That(section.Deaths, Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void KeepsTheConnectionAndLocalFlags()
        {
            var entries = new[]
            {
                Entry(0, Team.Alpha, 0, 0, ScoreboardConnection.Disconnected),
                Entry(1, Team.Alpha, 1, 0, ScoreboardConnection.Connected, isLocal: true),
            };

            var rows = ScoreboardRows.Build(entries, Team.Alpha)[0].Rows;

            Assert.That(rows[0].IsLocal, Is.True);
            Assert.That(rows[0].Connection, Is.EqualTo(ScoreboardConnection.Connected));
            Assert.That(rows[1].IsLocal, Is.False);
            Assert.That(rows[1].Connection, Is.EqualTo(ScoreboardConnection.Disconnected));
        }

        [Test]
        public void RejectsNullEntries()
        {
            Assert.Throws<ArgumentNullException>(() => ScoreboardRows.Build(null, Team.Alpha));
        }

        [Test]
        public void RejectsAnInvalidFirstTeam()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ScoreboardRows.Build(Array.Empty<ScoreboardEntry>(), (Team)7));
        }

        [Test]
        public void RejectsAnEntryWithAnInvalidTeam()
        {
            var entries = new[] { Entry(0, (Team)7, 0, 0) };

            Assert.Throws<ArgumentOutOfRangeException>(() => ScoreboardRows.Build(entries, Team.Alpha));
        }

        [Test]
        public void RejectsDuplicatePlayerIds()
        {
            var entries = new[] { Entry(1, Team.Alpha, 0, 0), Entry(1, Team.Bravo, 0, 0) };

            Assert.Throws<ArgumentException>(() => ScoreboardRows.Build(entries, Team.Alpha));
        }

        [TestCase(-1, 0)]
        [TestCase(0, -1)]
        public void EntriesRejectNegativeCounts(int kills, int deaths)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new ScoreboardEntry(0, LocalizedText.Literal("x"), Team.Alpha, kills, deaths, ScoreboardConnection.None, false));
        }

        [Test]
        public void DefaultEntryHasAnEmptyName()
        {
            Assert.That(default(ScoreboardEntry).Name.IsEmpty, Is.True);
            Assert.That(GameText.Resolve(default(ScoreboardEntry).Name), Is.EqualTo(string.Empty));
        }

        [Test]
        public void ALiteralNameIsShownAsItIs()
        {
            var entry = new ScoreboardEntry(0, LocalizedText.Literal("[BOT] Bot 1"), Team.Alpha, 0, 0, ScoreboardConnection.None, false);

            Assert.That(GameText.Resolve(entry.Name), Is.EqualTo("[BOT] Bot 1"));
        }

        [Test]
        public void EntriesWithDifferentNamesAreNotEqual()
        {
            var you = new ScoreboardEntry(0, ScoreboardRows.LocalPlayerName, Team.Alpha, 0, 0, ScoreboardConnection.None, true);

            Assert.That(you, Is.EqualTo(new ScoreboardEntry(0, ScoreboardRows.LocalPlayerName, Team.Alpha, 0, 0, ScoreboardConnection.None, true)));
            Assert.That(you.GetHashCode(), Is.EqualTo(new ScoreboardEntry(0, ScoreboardRows.LocalPlayerName, Team.Alpha, 0, 0, ScoreboardConnection.None, true).GetHashCode()));
            Assert.That(you, Is.Not.EqualTo(new ScoreboardEntry(0, ScoreboardRows.PlayerName(0), Team.Alpha, 0, 0, ScoreboardConnection.None, true)));
        }

        [TestCase(ScoreboardConnection.None, "")]
        [TestCase(ScoreboardConnection.Connected, "接続中")]
        [TestCase(ScoreboardConnection.Disconnected, "切断")]
        public void ConnectionText(ScoreboardConnection connection, string expected)
        {
            Assert.That(GameText.Resolve(ScoreboardRows.ConnectionText(connection)), Is.EqualTo(expected));
        }

        [Test]
        public void ConnectionTextIsKeyedAndNoneIsEmpty()
        {
            Assert.That(ScoreboardRows.ConnectionText(ScoreboardConnection.None).IsEmpty, Is.True);
            Assert.That(ScoreboardRows.ConnectionText(ScoreboardConnection.Connected), Is.EqualTo(new LocalizedText("hud.scoreboard.connected")));
            Assert.That(ScoreboardRows.ConnectionText(ScoreboardConnection.Disconnected), Is.EqualTo(new LocalizedText("hud.scoreboard.disconnected")));
        }

        [Test]
        public void ConnectionTextRejectsUnknownValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ScoreboardRows.ConnectionText((ScoreboardConnection)9));
        }

        [Test]
        public void Names()
        {
            Assert.That(ScoreboardRows.PlayerName(3), Is.EqualTo(new LocalizedText("hud.scoreboard.player", 3)));
            Assert.That(ScoreboardRows.TargetName(2), Is.EqualTo(new LocalizedText("hud.scoreboard.target", 2)));
            Assert.That(ScoreboardRows.LocalPlayerName, Is.EqualTo(new LocalizedText("hud.scoreboard.you")));
            Assert.That(GameText.Resolve(ScoreboardRows.PlayerName(3)), Is.EqualTo("Player 3"));
            Assert.That(GameText.Resolve(ScoreboardRows.TargetName(2)), Is.EqualTo("標的 2"));
            Assert.That(GameText.Resolve(ScoreboardRows.LocalPlayerName), Is.EqualTo("あなた"));
            Assert.That(GameText.Resolve(ScoreboardRows.ConnectionCaption), Is.EqualTo("状態"));
            Assert.Throws<ArgumentOutOfRangeException>(() => ScoreboardRows.TargetName(0));
        }

        [Test]
        public void CountRejectsNegatives()
        {
            Assert.That(ScoreboardRows.Count(12), Is.EqualTo("12"));
            Assert.Throws<ArgumentOutOfRangeException>(() => ScoreboardRows.Count(-1));
        }
    }
}
