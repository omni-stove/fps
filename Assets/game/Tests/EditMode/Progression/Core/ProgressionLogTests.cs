using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ProgressionLogTests
    {
        private readonly ProgressionLedger _ledger = new ProgressionLedger(ProgressionSpec.Default);

        [Test]
        public void TheConfiguredLineSummarisesTheSpec()
        {
            Assert.That(
                ProgressionLog.Configured(ProgressionSpec.Default),
                Is.EqualTo("[Progression] configured kill=100 headshot=50 assist=50 match=250 win=500 levels=10 maxXp=22500 unlocks=0"));
        }

        [Test]
        public void TheKillLineShowsTheGainAndTheLevel()
        {
            var award = _ledger.RecordKill(PlayerProfile.Empty, true);

            Assert.That(ProgressionLog.Killed(true, award), Is.EqualTo("[Progression] kill head=true xp=+150 total=150 level=1 levelUp=false"));
        }

        [Test]
        public void TheAssistLineShowsTheGainAndTheLevel()
        {
            var award = _ledger.RecordAssist(new PlayerProfile(460, 4, 0, 0, 0));

            Assert.That(ProgressionLog.Assisted(award), Is.EqualTo("[Progression] assist xp=+50 total=510 level=2 levelUp=true"));
        }

        [Test]
        public void TheMatchLineShowsTheResultAndTheLevelUp()
        {
            var result = new MatchResult(MatchOutcome.Won, 3, 1, 30);
            var award = _ledger.RecordMatch(new PlayerProfile(150, 1, 0, 0, 0), result);

            Assert.That(
                ProgressionLog.MatchRecorded(result, award),
                Is.EqualTo("[Progression] match outcome=Won kills=3 deaths=1 xp=+750 total=900 level=2 levelUp=true"));
        }

        [Test]
        public void TheProfileLinesShowTheValues()
        {
            Assert.That(
                ProgressionLog.Loaded(new PlayerProfile(900, 1, 1, 1, 1, 2), 2),
                Is.EqualTo(
                    "[Progression] profile xp=900 kills=1 assists=2 deaths=1 matches=1 wins=1"
                    + " headshots=0 meleeKills=0 playSeconds=0 achievements= level=2"));
            Assert.That(ProgressionLog.ProfileRejected("bad"), Is.EqualTo("[Progression] profile rejected: bad"));
            Assert.That(ProgressionLog.ProfileKept("profile-rejected"), Is.EqualTo("[Progression] kept the rejected profile in slot=profile-rejected"));
        }

        [Test]
        public void TheLevelUpLineShowsBothLevelsAndTheTotal()
        {
            var award = _ledger.RecordMatch(new PlayerProfile(150, 1, 0, 0, 0), new MatchResult(MatchOutcome.Won, 1, 0, 10));

            Assert.That(ProgressionLog.LeveledUp(award), Is.EqualTo("[Progression] level up 1->2 total=900"));
        }

        [Test]
        public void TheSaveLinesShowTheProfileOrTheReason()
        {
            Assert.That(
                ProgressionLog.Saved(new PlayerProfile(900, 1, 1, 1, 1, 0)),
                Is.EqualTo(
                    "[Progression] saved xp=900 kills=1 assists=0 deaths=1 matches=1 wins=1"
                    + " headshots=0 meleeKills=0 playSeconds=0 achievements="));
            Assert.That(ProgressionLog.SaveFailed("disk full"), Is.EqualTo("[Progression] could not save the profile: disk full"));
        }

        [Test]
        public void TheProfileLinesHaveNoRatingEvenForAnOldSave()
        {
            var old = PlayerProfile.FromJson("{\"version\":3,\"xp\":900,\"kills\":1,\"assists\":0,\"deaths\":1,\"matches\":1,\"wins\":1,\"rating\":1234}");

            Assert.That(ProgressionLog.Loaded(old, 2), Does.Not.Contain("rating"), "the rating is logged by SkillLog");
            Assert.That(ProgressionLog.Saved(old), Does.Not.Contain("rating"));
        }

        [Test]
        public void RejectsMissingValues()
        {
            Assert.Throws<ArgumentNullException>(() => ProgressionLog.Configured(null));
            Assert.Throws<ArgumentNullException>(() => ProgressionLog.Loaded(null, 1));
            Assert.Throws<ArgumentNullException>(() => ProgressionLog.Killed(false, null));
            Assert.Throws<ArgumentNullException>(() => ProgressionLog.Assisted(null));
            Assert.Throws<ArgumentNullException>(() => ProgressionLog.MatchRecorded(new MatchResult(MatchOutcome.Lost, 0, 0, 0), null));
            Assert.Throws<ArgumentNullException>(() => ProgressionLog.LeveledUp(null));
            Assert.Throws<ArgumentNullException>(() => ProgressionLog.Saved(null));
        }
    }
}
