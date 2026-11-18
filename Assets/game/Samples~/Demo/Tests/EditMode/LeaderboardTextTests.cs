using System;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;

namespace Shooter.Tests.EditMode
{
    public sealed class LeaderboardTextTests
    {
        [Test]
        public void TheNoticeFollowsTheStage()
        {
            var rows = new[] { new LeaderboardRow(1, "Ace", 3, false) };

            Assert.That(LeaderboardText.Notice(null, true, false), Is.EqualTo(new LocalizedText(LeaderboardText.NoBoardsKey)));
            Assert.That(LeaderboardText.Notice(null, false, true), Is.EqualTo(new LocalizedText(LeaderboardText.UnavailableKey)));
            Assert.That(LeaderboardText.Notice(null, true, true), Is.EqualTo(new LocalizedText(LeaderboardText.LoadingKey)));
            Assert.That(
                LeaderboardText.Notice(new LeaderboardStatus("kills", LeaderboardStage.Loading, null), true, true),
                Is.EqualTo(new LocalizedText(LeaderboardText.LoadingKey)));
            Assert.That(LeaderboardText.Notice(new LeaderboardStatus("kills", LeaderboardStage.Loading, rows), true, true).IsEmpty, Is.True);
            Assert.That(
                LeaderboardText.Notice(new LeaderboardStatus("kills", LeaderboardStage.Unavailable, null), true, true),
                Is.EqualTo(new LocalizedText(LeaderboardText.UnavailableKey)));
            Assert.That(
                LeaderboardText.Notice(new LeaderboardStatus("kills", LeaderboardStage.Ready, null), true, true),
                Is.EqualTo(new LocalizedText(LeaderboardText.EmptyKey)));
            Assert.That(LeaderboardText.Notice(new LeaderboardStatus("kills", LeaderboardStage.Ready, rows), true, true).IsEmpty, Is.True);
        }

        [Test]
        public void RowsAreKeyedAndNamesAreShownAsGiven()
        {
            var ace = new LeaderboardRow(1, "<b>Ace</b>", 9000000000L, false);
            var me = new LeaderboardRow(57, "Me", 12, true);
            var nameless = new LeaderboardRow(3, null, 0, false);

            Assert.That(LeaderboardText.Rank(me), Is.EqualTo(new LocalizedText(LeaderboardText.RankKey, 57)));
            Assert.That(LeaderboardText.PlayerName(ace), Is.EqualTo(LocalizedText.Literal("<b>Ace</b>")));
            Assert.That(LeaderboardText.PlayerName(me), Is.EqualTo(new LocalizedText(LeaderboardText.YouKey, LocalizedText.Literal("Me"))));
            Assert.That(LeaderboardText.PlayerName(nameless), Is.EqualTo(new LocalizedText(LeaderboardText.UnknownPlayerKey)));
            Assert.That(LeaderboardText.Score(ace), Is.EqualTo(LocalizedText.Literal("9,000,000,000")));
        }

        [Test]
        public void ABoardWithoutANameKeyShowsItsId()
        {
            Assert.That(
                LeaderboardText.BoardName(new LeaderboardDefinition("kills", "leaderboard.kills", PlayerStat.Kills)),
                Is.EqualTo(new LocalizedText("leaderboard.kills")));
            Assert.That(
                LeaderboardText.BoardName(new LeaderboardDefinition("best-streak", null, PlayerStat.Kills)),
                Is.EqualTo(LocalizedText.Literal("best-streak")));
            Assert.Throws<ArgumentNullException>(() => LeaderboardText.BoardName(null));
        }
    }
}
