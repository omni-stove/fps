using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class RecordSignalTypesTests
    {
        [Test]
        public void AchievementProgressFillsUpToTheTargetAndStaysUnlocked()
        {
            var halfway = new AchievementProgress(" marksman ", 25, 50, false);
            var past = new AchievementProgress("marksman", 80, 50, false);
            var raisedAfterUnlock = new AchievementProgress("marksman", 50, 100, true);

            Assert.That(halfway.Id, Is.EqualTo("marksman"));
            Assert.That(halfway.Fraction, Is.EqualTo(0.5f));
            Assert.That(past.Fraction, Is.EqualTo(1f));
            Assert.That(raisedAfterUnlock.Fraction, Is.EqualTo(1f));
            Assert.That(halfway, Is.EqualTo(new AchievementProgress("marksman", 25, 50, false)));
            Assert.That(halfway, Is.Not.EqualTo(new AchievementProgress("marksman", 25, 50, true)));
            Assert.That(halfway.ToString(), Is.EqualTo("marksman 25/50"));
            Assert.That(raisedAfterUnlock.ToString(), Is.EqualTo("marksman 50/100 unlocked"));
            Assert.Throws<ArgumentException>(() => new AchievementProgress(" ", 0, 1, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AchievementProgress("a", -1, 1, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AchievementProgress("a", 0, 0, false));
        }

        [Test]
        public void LeaderboardStatusKeepsACopyOfTheRows()
        {
            var rows = new[] { new LeaderboardRow(1, "Ace", 9000000000L, false), new LeaderboardRow(2, "Me", 12, true) };
            var status = new LeaderboardStatus(" kills ", LeaderboardStage.Ready, rows);
            rows[0] = new LeaderboardRow(1, "Changed", 0, false);

            Assert.That(status.BoardId, Is.EqualTo("kills"));
            Assert.That(status.Rows[0].PlayerName, Is.EqualTo("Ace"));
            Assert.That(status.Rows[0].Score, Is.EqualTo(9000000000L), "scores past the int range are kept");
            Assert.That(status.Rows[1].IsLocal, Is.True);
            Assert.That(status.ToString(), Is.EqualTo("kills Ready rows 2"));
            Assert.That(status.Rows[0].ToString(), Is.EqualTo("#1 Ace 9000000000"));
            Assert.That(
                status,
                Is.EqualTo(new LeaderboardStatus("kills", LeaderboardStage.Ready, new[] { new LeaderboardRow(1, "Ace", 9000000000L, false), new LeaderboardRow(2, "Me", 12, true) })));
            Assert.That(status, Is.Not.EqualTo(new LeaderboardStatus("kills", LeaderboardStage.Loading, null)));
            Assert.That(new LeaderboardStatus("kills", LeaderboardStage.Loading, null).Rows, Is.Empty);
            Assert.That(default(LeaderboardStatus).Rows, Is.Empty);
            Assert.That(new LeaderboardRow(3, null, 0, false).PlayerName, Is.EqualTo(string.Empty));
            Assert.Throws<ArgumentException>(() => new LeaderboardStatus(" ", LeaderboardStage.Ready, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LeaderboardStatus("kills", (LeaderboardStage)9, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LeaderboardRow(0, "A", 1, false));
        }

        [Test]
        public void AReplayEntryIsRecordedInUtc()
        {
            var at = new DateTime(2026, 11, 18, 9, 30, 0, DateTimeKind.Utc);
            var entry = new ReplayEntry(" r1 ", "NetArena01", at, 312.5f);

            Assert.That(entry.Id, Is.EqualTo("r1"));
            Assert.That(entry.MapId, Is.EqualTo("NetArena01"));
            Assert.That(entry.RecordedAtUtc, Is.EqualTo(at));
            Assert.That(entry.DurationSeconds, Is.EqualTo(312.5f));
            Assert.That(entry.ToString(), Is.EqualTo("r1 NetArena01 2026-11-18T09:30:00.0000000Z 312.5s"));
            Assert.That(entry, Is.EqualTo(new ReplayEntry("r1", "NetArena01", at, 312.5f)));
            Assert.That(new ReplayEntry("r2", null, at, 0f).MapId, Is.EqualTo(string.Empty));
            Assert.Throws<ArgumentException>(() => new ReplayEntry("r1", "m", new DateTime(2026, 11, 18, 9, 30, 0, DateTimeKind.Local), 1f));
            Assert.Throws<ArgumentException>(() => new ReplayEntry("r1", "m", new DateTime(2026, 11, 18), 1f));
            Assert.Throws<ArgumentException>(() => new ReplayEntry("", "m", at, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ReplayEntry("r1", "m", at, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ReplayEntry("r1", "m", at, float.NaN));
        }

        [Test]
        public void ReportReasonValuesStayFixed()
        {
            Assert.That((int)ReportReason.Cheating, Is.EqualTo(0));
            Assert.That((int)ReportReason.Abuse, Is.EqualTo(1));
            Assert.That((int)ReportReason.Spam, Is.EqualTo(2));
            Assert.That((int)ReportReason.OffensiveName, Is.EqualTo(3));
            Assert.That((int)ReportReason.Other, Is.EqualTo(4));
            Assert.That(Enum.GetValues<ReportReason>().Length, Is.EqualTo(5));
        }
    }
}
