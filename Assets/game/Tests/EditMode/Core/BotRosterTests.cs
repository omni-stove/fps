using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class BotRosterTests
    {
        [Test]
        public void TheFillTargetIsTheLargerOfTheLaunchBotsAndTheMinimumPlayers()
        {
            Assert.That(BotRoster.TargetPlayers(6, 0), Is.EqualTo(6));
            Assert.That(BotRoster.TargetPlayers(2, 8), Is.EqualTo(8));
            Assert.That(BotRoster.TargetPlayers(0, 0), Is.EqualTo(0));
        }

        [Test]
        public void EachHumanTakesTheSeatOfOneBot()
        {
            Assert.That(BotRoster.DesiredBots(6, 0, 1), Is.EqualTo(5));
            Assert.That(BotRoster.DesiredBots(6, 0, 2), Is.EqualTo(4), "a second human removes one bot");
            Assert.That(BotRoster.DesiredBots(6, 0, 6), Is.EqualTo(0));
            Assert.That(BotRoster.DesiredBots(6, 0, 9), Is.EqualTo(0), "more humans than the target never makes the count negative");
        }

        [Test]
        public void NoBotsAreKeptWithoutAnyHuman()
        {
            Assert.That(BotRoster.DesiredBots(6, 4, 0), Is.EqualTo(0));
        }

        [Test]
        public void NegativeCountsAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => BotRoster.TargetPlayers(-1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => BotRoster.TargetPlayers(0, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => BotRoster.DesiredBots(1, 0, -1));
        }

        [Test]
        public void ABotLeavesTheTeamTheHumanJoined()
        {
            // Alpha: 人 1 + Bot 1、Bravo: Bot 2 に人が Alpha へ入った後 (Alpha 3、Bravo 2)
            Assert.That(BotRoster.TeamToTrim(Team.Alpha, 3, 2, 1, 2), Is.EqualTo(Team.Alpha));
        }

        [Test]
        public void WithoutABotOnTheJoinedTeamTheLargerTeamLosesOne()
        {
            Assert.That(BotRoster.TeamToTrim(Team.Alpha, 2, 3, 0, 3), Is.EqualTo(Team.Bravo));
            Assert.That(BotRoster.TeamToTrim(null, 4, 2, 1, 2), Is.EqualTo(Team.Alpha), "more players on Alpha");
            Assert.That(BotRoster.TeamToTrim(null, 3, 3, 1, 2), Is.EqualTo(Team.Bravo), "same size: the team with more bots");
            Assert.That(BotRoster.TeamToTrim(null, 3, 3, 2, 2), Is.EqualTo(Team.Alpha), "a full tie picks Alpha");
        }

        [Test]
        public void ATeamWithoutBotsIsNeverTrimmed()
        {
            Assert.That(BotRoster.TeamToTrim(null, 5, 1, 0, 1), Is.EqualTo(Team.Bravo));
            Assert.That(BotRoster.TeamToTrim(Team.Bravo, 2, 2, 0, 0), Is.Null);
        }

        [Test]
        public void TrimRejectsMoreBotsThanPlayers()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => BotRoster.TeamToTrim(null, 1, 0, 2, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => BotRoster.TeamToTrim(null, -1, 0, 0, 0));
        }

        [Test]
        public void BotIdsStartAfterEveryClientId()
        {
            Assert.That(BotRoster.IsBotId(BotRoster.FirstBotPlayerId), Is.True);
            Assert.That(BotRoster.IsBotId(BotRoster.FirstBotPlayerId - 1), Is.False);
            Assert.That(BotRoster.IsBotId(0), Is.False);
            Assert.That(BotRoster.IsBotId((ulong)BotRoster.FirstBotPlayerId), Is.True);
            Assert.That(BotRoster.IsBotId((ulong)int.MaxValue + 1), Is.False, "not a player id at all");
            Assert.That(BotRoster.BotNumber(BotRoster.FirstBotPlayerId + 2), Is.EqualTo(3));
            Assert.Throws<ArgumentOutOfRangeException>(() => BotRoster.BotNumber(4));
        }

        [Test]
        public void BotNamesCarryTheTagAndHumansKeepTheirIdNames()
        {
            var bot = BotRoster.FirstBotPlayerId;

            Assert.That(BotRoster.ScoreboardName(bot), Is.EqualTo("[BOT] Bot 1"));
            Assert.That(BotRoster.KillFeedName(bot + 1), Is.EqualTo("[BOT] B2"));
            Assert.That(BotRoster.ScoreboardName(3), Is.EqualTo(ScoreboardRows.PlayerName(3)));
            Assert.That(BotRoster.KillFeedName(3), Is.EqualTo(KillFeedFormat.Player(3)));
        }
    }
}
