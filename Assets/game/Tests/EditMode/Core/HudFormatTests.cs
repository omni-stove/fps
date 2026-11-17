using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class HudFormatTests
    {
        [TestCase(0f, 0)]
        [TestCase(0.426f, 42)]
        [TestCase(0.999f, 99)]
        [TestCase(0.99999994f, 99)]
        [TestCase(1f, 100)]
        [TestCase(1.5f, 100)]
        [TestCase(-0.5f, 0)]
        [TestCase(float.NaN, 0)]
        public void RevivePercentRoundsDownAndReachesOneHundredOnlyWhenDone(float progress, int expected)
        {
            Assert.That(HudFormat.RevivePercent(progress), Is.EqualTo(expected));
        }

        [TestCase(25, 100, true)]
        [TestCase(1, 100, true)]
        [TestCase(26, 100, false)]
        [TestCase(100, 100, false)]
        [TestCase(0, 100, false)]
        [TestCase(150, 100, false)]
        public void LowHealthIsAQuarterOrLessWhileAlive(int current, int max, bool expected)
        {
            Assert.That(HudFormat.IsLowHealth(current, max), Is.EqualTo(expected));
        }

        [Test]
        public void LowHealthRejectsInvalidValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => HudFormat.IsLowHealth(-1, 100));
            Assert.Throws<ArgumentOutOfRangeException>(() => HudFormat.IsLowHealth(0, 0));
        }

        [Test]
        public void OpponentIsTheOtherTeam()
        {
            Assert.That(HudFormat.Opponent(Team.Alpha), Is.EqualTo(Team.Bravo));
            Assert.That(HudFormat.Opponent(Team.Bravo), Is.EqualTo(Team.Alpha));
        }

        [Test]
        public void DisplayedSecondsRoundsUpAndStopsAtNinetyNineMinutes()
        {
            Assert.That(HudFormat.DisplayedSeconds(65f), Is.EqualTo(65));
            Assert.That(HudFormat.DisplayedSeconds(59.01f), Is.EqualTo(60));
            Assert.That(HudFormat.DisplayedSeconds(0.2f), Is.EqualTo(1));
            Assert.That(HudFormat.DisplayedSeconds(0f), Is.EqualTo(0));
            Assert.That(HudFormat.DisplayedSeconds(-5f), Is.EqualTo(0));
            Assert.That(HudFormat.DisplayedSeconds(float.NaN), Is.EqualTo(0));
            Assert.That(HudFormat.DisplayedSeconds(100f * 60f), Is.EqualTo(HudFormat.MaxDisplayedSeconds));
            Assert.That(HudFormat.DisplayedSeconds(float.PositiveInfinity), Is.EqualTo(HudFormat.MaxDisplayedSeconds));
            Assert.That(HudFormat.DisplayedSeconds(float.MaxValue), Is.EqualTo(HudFormat.MaxDisplayedSeconds));
            Assert.That(HudFormat.MaxDisplayedSeconds, Is.EqualTo(99 * 60 + 59));
        }

        [Test]
        public void OpponentRejectsUnknownTeams()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => HudFormat.Opponent((Team)99));
        }

        [Test]
        public void DisplayedScoreRoundsDownAndClamps()
        {
            Assert.That(HudFormat.DisplayedScore(3.7d), Is.EqualTo(3));
            Assert.That(HudFormat.DisplayedScore(-1d), Is.Zero);
            Assert.That(HudFormat.DisplayedScore(double.NaN), Is.Zero);
            Assert.That(HudFormat.DisplayedScore(1e12d), Is.EqualTo(int.MaxValue));
        }

        [TestCase(3.42f, 35)]
        [TestCase(3f, 30)]
        [TestCase(0.01f, 1)]
        [TestCase(0f, 0)]
        public void AbilityWaitTenthsRoundsUpToATenth(float seconds, int expected)
        {
            Assert.That(HudFormat.AbilityWaitTenths(seconds), Is.EqualTo(expected));
        }

        [Test]
        public void AbilityWaitTenthsRejectsNegativeAndInfiniteSeconds()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => HudFormat.AbilityWaitTenths(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => HudFormat.AbilityWaitTenths(float.PositiveInfinity));
        }
    }
}
