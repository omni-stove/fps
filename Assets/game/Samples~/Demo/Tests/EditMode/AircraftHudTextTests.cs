using System.Linq;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;

namespace Shooter.Tests.EditMode
{
    // 文は JapaneseLanguageSetUp が効かせる日本語の表 (Resources/Localization/ja/aircraft.json) から引く。
    public sealed class AircraftHudTextTests
    {
        private static readonly string[] _keys =
        {
            AircraftHudText.SpeedKey,
            AircraftHudText.AltitudeKey,
            AircraftHudText.ThrottleKey,
            AircraftHudText.AirBrakeKey,
            AircraftHudText.AngleOfAttackKey,
            AircraftHudText.LoadFactorKey,
            AircraftHudText.StallKey,
        };

        [Test]
        public void TheReadingsBecomeRoundedLines()
        {
            var status = new AircraftStatus(50f, 120.4f, 0.5f, 4.6f, 2.345f, false, false);

            var lines = AircraftHudText.Readings(status).Select(Localizer.Resolve).ToArray();

            Assert.That(lines, Is.EqualTo(new[] { "速度 180 km/h", "高度 120 m", "スロットル 50%", "迎え角 5°", "2.3 G" }));
        }

        [Test]
        public void ANegativeThrottleReadsAsTheAirBrake()
        {
            var status = new AircraftStatus(40f, 0f, -0.25f, 0f, 1f, false, false);

            Assert.That(AircraftHudText.Throttle(status).Key, Is.EqualTo(AircraftHudText.AirBrakeKey));
            Assert.That(Localizer.Resolve(AircraftHudText.Throttle(status)), Is.EqualTo("エアブレーキ 25%"));
        }

        [Test]
        public void TheWarningIsWrittenOnlyWhileStalling()
        {
            var stalling = new AircraftStatus(20f, 300f, 1f, 18f, 0.4f, true, false);
            var flying = new AircraftStatus(60f, 300f, 1f, 3f, 1f, false, false);

            Assert.That(AircraftHudText.ShowsWarning(stalling), Is.True);
            Assert.That(Localizer.Resolve(AircraftHudText.Warning(stalling)), Is.EqualTo("失速"));
            Assert.That(AircraftHudText.ShowsWarning(flying), Is.False);
            Assert.That(AircraftHudText.Warning(flying).IsEmpty, Is.True);
            Assert.That(Localizer.Resolve(AircraftHudText.Warning(flying)), Is.Empty);
        }

        [Test]
        public void EveryKeyIsInBothLanguages()
        {
            foreach (var language in new[] { GameLanguage.Japanese, GameLanguage.English })
            {
                var table = Localizer.TableOf(language);
                foreach (var key in _keys)
                {
                    Assert.That(table.Get(key), Is.Not.EqualTo(key), language + " " + key);
                }
            }
        }

        [Test]
        public void TheEnglishLinesUseTheSameNumbers()
        {
            var table = Localizer.TableOf(GameLanguage.English);
            var status = new AircraftStatus(50f, 120.4f, 0.5f, 4.6f, 2.345f, true, false);

            Assert.That(table.Resolve(AircraftHudText.Speed(status)), Is.EqualTo("SPD 180 km/h"));
            Assert.That(table.Resolve(AircraftHudText.LoadFactor(status)), Is.EqualTo("2.3 G"));
            Assert.That(table.Resolve(AircraftHudText.Warning(status)), Is.EqualTo("STALL"));
        }
    }
}
