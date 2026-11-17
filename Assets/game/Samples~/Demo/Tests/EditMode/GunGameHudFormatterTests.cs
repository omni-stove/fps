using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;

namespace Fps.Tests.EditMode
{
    public sealed class GunGameHudFormatterTests
    {
        private static readonly GunGameTier[] _tiers =
        {
            new GunGameTier("rifle", "weapon.rifle"),
            new GunGameTier("launcher", "weapon.launcher"),
            new GunGameTier("handgun", "weapon.handgun"),
        };

        private static readonly string[] _firstTierLines = { "リスポーンまで 2 秒", "段 1/3 Rifle", "次: Launcher" };

        [Test]
        public void TheTierLinesCountTiersFromOne()
        {
            Assert.That(Localizer.Resolve(GunGameHudFormatter.Tier(1, 3, DisplayNames.Of(_tiers[1]))), Is.EqualTo("段 2/3 Launcher"));
            Assert.That(Localizer.Resolve(GunGameHudFormatter.Tier(3, 3, DisplayNames.Of(_tiers[2]))), Is.EqualTo("段 3/3 Handgun"), "the finisher stays on the last tier");
            Assert.That(Localizer.Resolve(GunGameHudFormatter.Next(DisplayNames.Of(_tiers[2]))), Is.EqualTo("次: Handgun"));
            Assert.That(Localizer.Resolve(GunGameHudFormatter.Next(default)), Is.EqualTo("次: なし (この武器で倒せば勝ち)"));
            Assert.That(GunGameHudFormatter.Tier(1, 3, DisplayNames.Of(_tiers[1])), Is.EqualTo(new LocalizedText("mode.gungame.tier", 2, 3, new LocalizedText("weapon.launcher"))));
            Assert.That(GunGameHudFormatter.Tier(1, 3, default), Is.EqualTo(new LocalizedText("mode.gungame.tier", 2, 3, "-")));
            Assert.That(GunGameHudFormatter.Next(default), Is.EqualTo(GunGameHudFormatter.LastTier));
            Assert.That(GunGameHudFormatter.LastTier.Key, Is.EqualTo("mode.gungame.lastTier"));
        }

        [Test]
        public void TheLinesShowTheRespawnThenTheOwnTierAndTheNextWeapon()
        {
            var status = Status(new GunGameStatus(Game(), 0), 1.2f);
            var lines = new List<LocalizedText>();

            new GunGameHudFormatter().AppendLines(status, lines);

            Assert.That(lines.ConvertAll(Localizer.Resolve), Is.EqualTo(_firstTierLines));
        }

        [Test]
        public void WithoutALocalPlayerOnlyTheCommonLinesAreShown()
        {
            var formatter = new GunGameHudFormatter();
            var status = Status(new GunGameStatus(Game(), null), null);
            var lines = new List<LocalizedText>();

            formatter.AppendLines(status, lines);

            Assert.That(lines, Is.Empty);
            Assert.That(Localizer.Resolve(formatter.Time(status)), Is.EqualTo("01:30"));
            Assert.That(Localizer.Resolve(formatter.Scores(status)[0].Text), Is.EqualTo(Localizer.Resolve(ModeHudText.ScoreText(new Side(0), 2d))));
            Assert.That(formatter.Result(status).IsEmpty, Is.True);
        }

        private static GunGame Game()
        {
            return new GunGame(new GunGameLadder(_tiers, new[] { "knife" }, true));
        }

        private static ModeStatus Status(GunGameStatus detail, float? respawnSeconds)
        {
            var scores = new[] { new SideScore(new Side(0), 2d), new SideScore(new Side(1), 1d) };
            return new ModeStatus(90f, scores, new Side(0), respawnSeconds, false, null, detail);
        }
    }
}
