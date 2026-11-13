using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class HudFormatTests
    {
        [TestCase(100, "100")]
        [TestCase(75, "75")]
        [TestCase(0, "0")]
        public void HealthIsTheNumberOnly(int current, string expected)
        {
            Assert.That(HudFormat.Health(current), Is.EqualTo(expected));
        }

        [TestCase(10, "次の試合まで 10 秒")]
        [TestCase(1, "次の試合まで 1 秒")]
        [TestCase(0, "次の試合まで 0 秒")]
        public void NextMatchShowsTheSecondsLeft(int seconds, string expected)
        {
            Assert.That(HudFormat.NextMatch(seconds), Is.EqualTo(expected));
        }

        [Test]
        public void NextMatchRejectsNegative()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => HudFormat.NextMatch(-1));
        }

        [TestCase(3, "チェックポイントから再開まで 3 秒")]
        [TestCase(0, "チェックポイントから再開まで 0 秒")]
        public void CheckpointRestartShowsTheSecondsLeft(int seconds, string expected)
        {
            Assert.That(HudFormat.CheckpointRestart(seconds), Is.EqualTo(expected));
        }

        [Test]
        public void CheckpointRestartRejectsNegative()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => HudFormat.CheckpointRestart(-1));
        }

        [Test]
        public void HealthRejectsNegative()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => HudFormat.Health(-1));
        }

        [TestCase(30, 90, "30 / 90")]
        [TestCase(0, 0, "0 / 0")]
        [TestCase(7, 120, "7 / 120")]
        public void AmmoIsMagazineSlashReserve(int magazine, int reserve, string expected)
        {
            Assert.That(HudFormat.Ammo(magazine, reserve), Is.EqualTo(expected));
        }

        [Test]
        public void AmmoRejectsNegative()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => HudFormat.Ammo(-1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => HudFormat.Ammo(0, -1));
        }

        [TestCase(30, 90, false, "30 / 90")]
        [TestCase(0, 90, true, "リロード中 / 90")]
        [TestCase(12, 0, true, "リロード中 / 0")]
        public void AmmoShowsReloadingInPlaceOfTheMagazine(int magazine, int reserve, bool reloading, string expected)
        {
            Assert.That(HudFormat.Ammo(magazine, reserve, reloading), Is.EqualTo(expected));
        }

        [Test]
        public void AmmoWhileReloadingStillRejectsNegative()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => HudFormat.Ammo(-1, 0, true));
            Assert.Throws<ArgumentOutOfRangeException>(() => HudFormat.Ammo(0, -1, true));
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

        [TestCase(Team.Alpha, 0, "ALPHA 0")]
        [TestCase(Team.Bravo, 3, "BRAVO 3")]
        [TestCase(Team.Alpha, 12, "ALPHA 12")]
        public void TeamScoreIsTheTeamNameAndTheScore(Team team, int score, string expected)
        {
            Assert.That(HudFormat.TeamScore(team, score), Is.EqualTo(expected));
        }

        [Test]
        public void TeamScoreRejectsNegativeAndUnknownTeams()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => HudFormat.TeamScore(Team.Alpha, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => HudFormat.TeamName((Team)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => HudFormat.Opponent((Team)99));
        }

        [Test]
        public void OpponentIsTheOtherTeam()
        {
            Assert.That(HudFormat.Opponent(Team.Alpha), Is.EqualTo(Team.Bravo));
            Assert.That(HudFormat.Opponent(Team.Bravo), Is.EqualTo(Team.Alpha));
        }

        [Test]
        public void ResultNamesTheWinnerOrADraw()
        {
            Assert.That(HudFormat.Result(Team.Alpha), Is.EqualTo("ALPHA の勝ち"));
            Assert.That(HudFormat.Result(Team.Bravo), Is.EqualTo("BRAVO の勝ち"));
            Assert.That(HudFormat.Result(null), Is.EqualTo(HudFormat.Draw));
            Assert.That(HudFormat.Draw, Is.EqualTo("引き分け"));
        }

        [TestCase(3, "リスポーンまで 3 秒")]
        [TestCase(0, "リスポーンまで 0 秒")]
        public void RespawnShowsTheSecondsLeft(int seconds, string expected)
        {
            Assert.That(HudFormat.Respawn(seconds), Is.EqualTo(expected));
        }

        [Test]
        public void RespawnRejectsNegative()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => HudFormat.Respawn(-1));
        }

        [TestCase(180f, "03:00")]
        [TestCase(65f, "01:05")]
        [TestCase(59f, "00:59")]
        [TestCase(10f, "00:10")]
        [TestCase(0f, "00:00")]
        public void TimeIsMinutesAndSecondsWithTwoDigits(float remaining, string expected)
        {
            Assert.That(HudFormat.Time(remaining), Is.EqualTo(expected));
        }

        [TestCase(59.01f, "01:00")]
        [TestCase(0.2f, "00:01")]
        [TestCase(1f, "00:01")]
        public void TimeRoundsUpSoZeroMeansTimeUp(float remaining, string expected)
        {
            Assert.That(HudFormat.Time(remaining), Is.EqualTo(expected));
        }

        [Test]
        public void TimeTreatsNegativeAndNaNAsZero()
        {
            Assert.That(HudFormat.Time(-5f), Is.EqualTo("00:00"));
            Assert.That(HudFormat.Time(float.NaN), Is.EqualTo("00:00"));
        }

        [Test]
        public void TimeStopsAtNinetyNineMinutes()
        {
            Assert.That(HudFormat.Time(100f * 60f), Is.EqualTo("99:59"));
            Assert.That(HudFormat.Time(float.PositiveInfinity), Is.EqualTo("99:59"));
            Assert.That(HudFormat.DisplayedSeconds(float.MaxValue), Is.EqualTo(HudFormat.MaxDisplayedSeconds));
        }
    }
}
