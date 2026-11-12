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
