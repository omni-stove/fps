using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ProgressionLedgerTests
    {
        private static readonly ProgressionSpec _spec = new ProgressionSpec(
            100,
            50,
            30,
            250,
            500,
            new[] { 0, 100, 300, 600 },
            new[] { new ProgressionUnlock("burst", 2), new ProgressionUnlock("scope", 4) });

        private static readonly int[] _singleLevel = { 0 };
        private static readonly string[] _firstBlood = { "first-blood" };

        private static readonly int[] _twoLevels = { 0, 1 };

        private readonly ProgressionLedger _ledger = new ProgressionLedger(_spec);

        [TestCase(0, 1)]
        [TestCase(99, 1)]
        [TestCase(100, 2)]
        [TestCase(299, 2)]
        [TestCase(300, 3)]
        [TestCase(599, 3)]
        [TestCase(600, 4)]
        [TestCase(int.MaxValue, 4)]
        public void TheLevelIsTheHighestRowTheXpReaches(int xp, int level)
        {
            Assert.That(_ledger.LevelOf(xp), Is.EqualTo(level));
            Assert.That(_ledger.LevelOf(new PlayerProfile(xp, 0, 0, 0, 0)), Is.EqualTo(level));
        }

        [Test]
        public void ATableWithOnlyLevelOneStaysAtLevelOne()
        {
            var ledger = new ProgressionLedger(new ProgressionSpec(1, 1, 1, 1, 1, _singleLevel, Array.Empty<ProgressionUnlock>()));

            Assert.That(ledger.LevelOf(0), Is.EqualTo(1));
            Assert.That(ledger.LevelOf(int.MaxValue), Is.EqualTo(1));
        }

        [Test]
        public void AKillAddsTheKillXpAndCountsTheKill()
        {
            var before = new PlayerProfile(0, 0, 2, 1, 0);

            var award = _ledger.RecordKill(before, false);

            Assert.That(award.Xp, Is.EqualTo(100));
            Assert.That(award.Profile, Is.EqualTo(new PlayerProfile(100, 1, 2, 1, 0)));
            Assert.That(award.PreviousLevel, Is.EqualTo(1));
            Assert.That(award.Level, Is.EqualTo(2));
            Assert.That(award.LeveledUp, Is.True, "100 xp is exactly the level 2 row");
            Assert.That(before, Is.EqualTo(new PlayerProfile(0, 0, 2, 1, 0)), "the old profile does not change");
        }

        [Test]
        public void AHeadshotKillAddsTheHeadshotXpOnTop()
        {
            var award = _ledger.RecordKill(new PlayerProfile(140, 3, 0, 0, 0), true);

            Assert.That(award.Xp, Is.EqualTo(150));
            Assert.That(award.Profile, Is.EqualTo(new PlayerProfile(290, 4, 0, 0, 0)));
            Assert.That(award.PreviousLevel, Is.EqualTo(2));
            Assert.That(award.Level, Is.EqualTo(2));
            Assert.That(award.LeveledUp, Is.False, "290 xp is 10 short of level 3");
        }

        [Test]
        public void AnAssistAddsTheAssistXpAndCountsTheAssistButNotAKill()
        {
            var before = new PlayerProfile(80, 2, 1, 1, 0, 4);

            var award = _ledger.RecordAssist(before);

            Assert.That(award.Xp, Is.EqualTo(30));
            Assert.That(award.Profile, Is.EqualTo(new PlayerProfile(110, 2, 1, 1, 0, 5)));
            Assert.That(award.PreviousLevel, Is.EqualTo(1));
            Assert.That(award.Level, Is.EqualTo(2));
            Assert.That(award.LeveledUp, Is.True, "110 xp passes the level 2 row");
            Assert.That(before, Is.EqualTo(new PlayerProfile(80, 2, 1, 1, 0, 4)), "the old profile does not change");
        }

        [Test]
        public void KillsAndMatchesKeepTheAssists()
        {
            var kill = _ledger.RecordKill(new PlayerProfile(0, 0, 0, 0, 0, 3), false);
            var match = _ledger.RecordMatch(kill.Profile, new MatchResult(MatchOutcome.Lost, 1, 0, 10));

            Assert.That(kill.Profile.Assists, Is.EqualTo(3));
            Assert.That(match.Profile, Is.EqualTo(new PlayerProfile(350, 1, 0, 1, 0, 3)));
        }

        [Test]
        public void AWonMatchAddsTheMatchAndWinXpAndCountsTheWin()
        {
            var award = _ledger.RecordMatch(new PlayerProfile(100, 3, 1, 4, 2), new MatchResult(MatchOutcome.Won, 3, 2, 30));

            Assert.That(award.Xp, Is.EqualTo(750));
            Assert.That(award.Profile, Is.EqualTo(new PlayerProfile(850, 3, 3, 5, 3)), "kills come from RecordKill, so the result's kills are not added again");
            Assert.That(award.PreviousLevel, Is.EqualTo(2));
            Assert.That(award.Level, Is.EqualTo(4));
            Assert.That(award.LeveledUp, Is.True);
        }

        [TestCase(MatchOutcome.Lost)]
        [TestCase(MatchOutcome.Draw)]
        public void AMatchNotWonAddsOnlyTheMatchXp(MatchOutcome outcome)
        {
            var award = _ledger.RecordMatch(PlayerProfile.Empty, new MatchResult(outcome, 0, 4, 0));

            Assert.That(award.Xp, Is.EqualTo(250));
            Assert.That(award.Profile, Is.EqualTo(new PlayerProfile(250, 0, 4, 1, 0)));
            Assert.That(award.Level, Is.EqualTo(2));
        }

        [Test]
        public void ZeroXpSourcesGiveNothingButStillCount()
        {
            var ledger = new ProgressionLedger(new ProgressionSpec(0, 0, 0, 0, 0, _twoLevels, Array.Empty<ProgressionUnlock>()));

            var kill = ledger.RecordKill(PlayerProfile.Empty, true);
            var assist = ledger.RecordAssist(kill.Profile);
            var match = ledger.RecordMatch(assist.Profile, new MatchResult(MatchOutcome.Won, 1, 0, 1));

            Assert.That(kill.Xp, Is.EqualTo(0));
            Assert.That(assist.Xp, Is.EqualTo(0));
            Assert.That(match.Xp, Is.EqualTo(0));
            Assert.That(match.Profile, Is.EqualTo(new PlayerProfile(0, 1, 0, 1, 1, 1)));
            Assert.That(match.LeveledUp, Is.False);
        }

        [Test]
        public void TheCountsStopAtTheIntLimit()
        {
            var max = int.MaxValue;

            var kill = _ledger.RecordKill(new PlayerProfile(max - 10, max, 0, 0, 0), true);
            Assert.That(kill.Xp, Is.EqualTo(10), "only the xp that fit is reported");
            Assert.That(kill.Profile, Is.EqualTo(new PlayerProfile(max, max, 0, 0, 0)));

            var match = _ledger.RecordMatch(new PlayerProfile(max, 0, max - 1, max, max), new MatchResult(MatchOutcome.Won, 0, 5, 0));
            Assert.That(match.Xp, Is.EqualTo(0));
            Assert.That(match.Profile, Is.EqualTo(new PlayerProfile(max, 0, max, max, max)));

            var assist = _ledger.RecordAssist(new PlayerProfile(max - 5, 0, 0, 0, 0, max));
            Assert.That(assist.Xp, Is.EqualTo(5));
            Assert.That(assist.Profile, Is.EqualTo(new PlayerProfile(max, 0, 0, 0, 0, max)));

            var huge = new ProgressionLedger(new ProgressionSpec(max, max, max, max, max, _singleLevel, Array.Empty<ProgressionUnlock>()));
            Assert.That(huge.RecordKill(PlayerProfile.Empty, true).Profile.Xp, Is.EqualTo(max));
            Assert.That(huge.RecordAssist(PlayerProfile.Empty).Profile.Xp, Is.EqualTo(max));
            Assert.That(huge.RecordMatch(PlayerProfile.Empty, new MatchResult(MatchOutcome.Won, 0, 0, 0)).Profile.Xp, Is.EqualTo(max));
        }

        [Test]
        public void AnIdOutsideTheUnlockTableIsUsableFromTheStart()
        {
            Assert.That(_ledger.UnlockLevelOf("rifle"), Is.EqualTo(1));
            Assert.That(_ledger.IsUnlocked("rifle", 1), Is.True);
            Assert.That(_ledger.IsUnlocked("rifle", PlayerProfile.Empty), Is.True);
            Assert.That(_ledger.IsUnlocked("Burst", 1), Is.True, "ids are compared exactly");
        }

        [Test]
        public void AnIdInTheTableUnlocksAtItsLevel()
        {
            Assert.That(_ledger.UnlockLevelOf("burst"), Is.EqualTo(2));
            Assert.That(_ledger.IsUnlocked("burst", 1), Is.False);
            Assert.That(_ledger.IsUnlocked("burst", 2), Is.True);
            Assert.That(_ledger.IsUnlocked("burst", 4), Is.True);

            Assert.That(_ledger.UnlockLevelOf("scope"), Is.EqualTo(4));
            Assert.That(_ledger.IsUnlocked("scope", new PlayerProfile(599, 0, 0, 0, 0)), Is.False);
            Assert.That(_ledger.IsUnlocked("scope", new PlayerProfile(600, 0, 0, 0, 0)), Is.True);
        }

        [Test]
        public void AKillThatLevelsUpUnlocksTheNextRow()
        {
            var award = _ledger.RecordKill(new PlayerProfile(50, 0, 0, 0, 0), true);

            Assert.That(_ledger.IsUnlocked("burst", PlayerProfile.Empty), Is.False);
            Assert.That(_ledger.IsUnlocked("burst", award.Profile), Is.True);
        }

        [Test]
        public void RejectsBadArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new ProgressionLedger(null));
            Assert.Throws<ArgumentNullException>(() => _ledger.RecordKill(null, false));
            Assert.Throws<ArgumentNullException>(() => _ledger.RecordAssist(null));
            Assert.Throws<ArgumentNullException>(() => _ledger.RecordMatch(null, new MatchResult(MatchOutcome.Won, 0, 0, 0)));
            Assert.Throws<ArgumentNullException>(() => _ledger.LevelOf(null));
            Assert.Throws<ArgumentNullException>(() => _ledger.IsUnlocked("burst", (PlayerProfile)null));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ledger.LevelOf(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ledger.IsUnlocked("burst", 0));
            Assert.Throws<ArgumentException>(() => _ledger.IsUnlocked(null, 1));
            Assert.Throws<ArgumentException>(() => _ledger.UnlockLevelOf(string.Empty));
        }

        [Test]
        public void TheAwardChecksItsArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new ProgressionAward(null, 0, 1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ProgressionAward(PlayerProfile.Empty, -1, 1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ProgressionAward(PlayerProfile.Empty, 0, 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ProgressionAward(PlayerProfile.Empty, 0, 2, 1));
            Assert.That(
                new ProgressionAward(new PlayerProfile(150, 1, 0, 0, 0), 150, 1, 2).ToString(),
                Is.EqualTo(
                    "+150 xp level 1->2 (xp=150 kills=1 assists=0 deaths=0 matches=0 wins=0"
                    + " headshots=0 meleeKills=0 playSeconds=0 achievements=)"));
        }

        [Test]
        public void TheLedgerKeepsTheAchievementsAndStats()
        {
            var profile = new PlayerProfile(0, 0, 0, 0, 0, 0, 4, 2, 600, _firstBlood);

            var kill = _ledger.RecordKill(profile, true).Profile;
            var assist = _ledger.RecordAssist(profile).Profile;
            var match = _ledger.RecordMatch(profile, new MatchResult(MatchOutcome.Won, 1, 2, 10)).Profile;

            foreach (var next in new[] { kill, assist, match })
            {
                Assert.That(next.Headshots, Is.EqualTo(4), "the headshots are counted by the achievement tracker");
                Assert.That(next.MeleeKills, Is.EqualTo(2));
                Assert.That(next.PlaySeconds, Is.EqualTo(600));
                Assert.That(next.Achievements, Is.EqualTo(_firstBlood));
            }
        }
    }
}
