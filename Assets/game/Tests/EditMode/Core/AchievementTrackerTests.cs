using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    // 期待値は AchievementSpec.Default (first-blood キル 1・marksman 頭 50・veteran 試合 25・victor 勝ち 10、近接は knife) から出す。
    public sealed class AchievementTrackerTests
    {
        private readonly AchievementTracker _tracker = new AchievementTracker(AchievementSpec.Default);

        [Test]
        public void EachStatIsReadFromTheProfile()
        {
            var profile = new PlayerProfile(0, 9, 8, 7, 6, 5, 1000, 4, 3, 0, null);

            Assert.That(AchievementTracker.CountOf(profile, PlayerStat.Kills), Is.EqualTo(9));
            Assert.That(AchievementTracker.CountOf(profile, PlayerStat.Deaths), Is.EqualTo(8));
            Assert.That(AchievementTracker.CountOf(profile, PlayerStat.MatchesPlayed), Is.EqualTo(7));
            Assert.That(AchievementTracker.CountOf(profile, PlayerStat.MatchesWon), Is.EqualTo(6));
            Assert.That(AchievementTracker.CountOf(profile, PlayerStat.Assists), Is.EqualTo(5));
            Assert.That(AchievementTracker.CountOf(profile, PlayerStat.Headshots), Is.EqualTo(4));
            Assert.That(AchievementTracker.CountOf(profile, PlayerStat.MeleeKills), Is.EqualTo(3));
            Assert.Throws<ArgumentOutOfRangeException>(() => AchievementTracker.CountOf(profile, (PlayerStat)99));
            Assert.Throws<ArgumentNullException>(() => AchievementTracker.CountOf(null, PlayerStat.Kills));
        }

        [Test]
        public void AKillCountsTheHeadshotAndTheMeleeWeapon()
        {
            var headshot = _tracker.RecordKill(PlayerProfile.Empty, true, "rifle");
            var melee = _tracker.RecordKill(PlayerProfile.Empty, false, "knife");
            var both = _tracker.RecordKill(PlayerProfile.Empty, true, "knife");
            var plain = _tracker.RecordKill(PlayerProfile.Empty, false, "rifle");

            Assert.That(headshot.Headshots, Is.EqualTo(1));
            Assert.That(headshot.MeleeKills, Is.Zero);
            Assert.That(melee.Headshots, Is.Zero);
            Assert.That(melee.MeleeKills, Is.EqualTo(1));
            Assert.That(both.Headshots, Is.EqualTo(1));
            Assert.That(both.MeleeKills, Is.EqualTo(1));
            Assert.That(plain, Is.SameAs(PlayerProfile.Empty));
            Assert.That(headshot.Kills, Is.Zero, "the kill itself is counted by the progression ledger");
        }

        [Test]
        public void TheMeleeWeaponsComeFromTheSpec()
        {
            var tracker = new AchievementTracker(new AchievementSpec(null, new[] { "axe" }));

            Assert.That(tracker.RecordKill(PlayerProfile.Empty, false, "axe").MeleeKills, Is.EqualTo(1));
            Assert.That(tracker.RecordKill(PlayerProfile.Empty, false, "knife").MeleeKills, Is.Zero);
            Assert.That(tracker.RecordKill(PlayerProfile.Empty, false, null).MeleeKills, Is.Zero);
        }

        [Test]
        public void ThePlayTimeAddsTheWholeSecondsAndStopsAtTheLimit()
        {
            var profile = PlayerProfile.Empty.WithStats(0, 0, 100);

            Assert.That(AchievementTracker.RecordPlayTime(profile, 59.9).PlaySeconds, Is.EqualTo(159));
            Assert.That(AchievementTracker.RecordPlayTime(profile, 0.5), Is.SameAs(profile));
            Assert.That(AchievementTracker.RecordPlayTime(PlayerProfile.Empty.WithStats(0, 0, int.MaxValue - 1), 10).PlaySeconds, Is.EqualTo(int.MaxValue));
            Assert.Throws<ArgumentOutOfRangeException>(() => AchievementTracker.RecordPlayTime(profile, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => AchievementTracker.RecordPlayTime(profile, double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => AchievementTracker.RecordPlayTime(profile, double.PositiveInfinity));
        }

        [Test]
        public void ReachingTheTargetUnlocksTheAchievement()
        {
            var unlocked = new List<AchievementDefinition>();

            var before = _tracker.Unlock(PlayerProfile.Empty, unlocked);
            Assert.That(before, Is.SameAs(PlayerProfile.Empty));
            Assert.That(unlocked, Is.Empty);

            var after = _tracker.Unlock(new PlayerProfile(100, 1, 0, 0, 0), unlocked);

            Assert.That(after.Achievements, Is.EqualTo(new[] { "first-blood" }));
            Assert.That(unlocked.Count, Is.EqualTo(1));
            Assert.That(unlocked[0].Id, Is.EqualTo("first-blood"));
        }

        [Test]
        public void AnUnlockedAchievementIsNotUnlockedAgain()
        {
            var unlocked = new List<AchievementDefinition>();
            var profile = new PlayerProfile(0, 5, 0, 0, 0, 0, 1000, 0, 0, 0, new[] { "first-blood" });

            var next = _tracker.Unlock(profile, unlocked);

            Assert.That(next, Is.SameAs(profile));
            Assert.That(unlocked, Is.Empty);
        }

        [Test]
        public void SeveralAchievementsUnlockTogetherInTheSpecOrder()
        {
            var unlocked = new List<AchievementDefinition>();
            var profile = new PlayerProfile(0, 1, 0, 25, 10, 0, 1000, 0, 0, 0, new[] { "old-event" });

            var next = _tracker.Unlock(profile, unlocked);

            Assert.That(next.Achievements, Is.EqualTo(new[] { "old-event", "first-blood", "veteran", "victor" }));
            Assert.That(unlocked.ConvertAll(a => a.Id), Is.EqualTo(new[] { "first-blood", "veteran", "victor" }));
        }

        [Test]
        public void TheProgressFollowsTheSpecAndKeepsUnlockedAchievementsDone()
        {
            var tracker = new AchievementTracker(new AchievementSpec(new[]
            {
                new AchievementDefinition("brawler", "a.brawler", "a.brawler.text", PlayerStat.MeleeKills, 20),
                new AchievementDefinition("survivor", "a.survivor", "a.survivor.text", PlayerStat.Deaths, 100, hidden: true),
            }));
            var profile = new PlayerProfile(0, 30, 3, 0, 0, 0, 1000, 0, 5, 0, new[] { "survivor", "removed" });

            var progress = tracker.Progress(profile);

            Assert.That(progress, Is.EqualTo(new[]
            {
                new AchievementProgress("brawler", 5, 20, false),
                new AchievementProgress("survivor", 3, 100, true),
            }));
            Assert.That(progress[0].Fraction, Is.EqualTo(0.25f));
            Assert.That(progress[1].Fraction, Is.EqualTo(1f), "an achievement unlocked before the target was raised stays done");
        }

        [Test]
        public void RejectsMissingArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new AchievementTracker(null));
            Assert.Throws<ArgumentNullException>(() => _tracker.RecordKill(null, false, "rifle"));
            Assert.Throws<ArgumentNullException>(() => AchievementTracker.RecordPlayTime(null, 1));
            Assert.Throws<ArgumentNullException>(() => _tracker.Unlock(null, new List<AchievementDefinition>()));
            Assert.Throws<ArgumentNullException>(() => _tracker.Unlock(PlayerProfile.Empty, null));
            Assert.Throws<ArgumentNullException>(() => _tracker.Progress(null));
        }

        [Test]
        public void TheLogLinesShowTheSpecTheUnlockAndThePlayTime()
        {
            Assert.That(AchievementLog.Configured(AchievementSpec.Default), Is.EqualTo("[Achievement] configured achievements=4 melee=knife"));
            Assert.That(
                AchievementLog.Unlocked(AchievementSpec.Default.Find("marksman")),
                Is.EqualTo("[Achievement] unlocked id=marksman stat=Headshots target=50"));
            Assert.That(AchievementLog.PlayTime(95, 3695), Is.EqualTo("[Achievement] play time +95s total=3695s"));
            Assert.Throws<ArgumentNullException>(() => AchievementLog.Configured(null));
            Assert.Throws<ArgumentNullException>(() => AchievementLog.Unlocked(null));
        }
    }
}
