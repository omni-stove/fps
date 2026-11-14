using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    /// <summary>
    /// <see cref="GameText"/> が渡された関数から引くたびに表を受け取り、表が無ければキーそのものを返すこと。
    /// Core の書式が言語の切り替えに付いていくこと (英語の表で日本語を出さないこと)。
    /// <see cref="GameText.Resolve"/> と <see cref="GameText.Format"/> が入れ子の <see cref="LocalizedText"/> を同じ表で文にすること。
    /// </summary>
    public sealed class GameTextTests
    {
        private Func<StringTable> _before;

        [SetUp]
        public void SetUp()
        {
            _before = GameText.Source;
        }

        [TearDown]
        public void TearDown()
        {
            GameText.Use(_before);
        }

        [Test]
        public void WithoutASourceTheKeyIsShown()
        {
            GameText.Use(null);

            Assert.That(GameText.Get("hud.reloading"), Is.EqualTo("hud.reloading"));
            Assert.That(GameText.Format("hud.respawn", 3), Is.EqualTo("hud.respawn"));
            Assert.That(GameText.Resolve(HudFormat.Respawn(3)), Is.EqualTo("hud.respawn"));
            Assert.That(GameText.Resolve(LocalizedText.Literal("01:30")), Is.EqualTo("01:30"));
            Assert.That(GameText.Resolve(default), Is.Empty);
        }

        [Test]
        public void ResolveFillsNestedTextsFromTheSameTable()
        {
            var table = new StringTable(new[]
            {
                new KeyValuePair<string, string>("hud.wins", "{0} の勝ち"),
                new KeyValuePair<string, string>("hud.team.alpha", "アルファ"),
            });
            GameText.Use(() => table);

            Assert.That(GameText.Resolve(HudFormat.Result(Team.Alpha)), Is.EqualTo("アルファ の勝ち"));
        }

        [Test]
        public void FormatResolvesLocalizedTextArgumentsWithoutChangingTheCallersArray()
        {
            var table = new StringTable(new[]
            {
                new KeyValuePair<string, string>("guardian.leader", "{0} リーダー {1}"),
                new KeyValuePair<string, string>("hud.team.bravo", "BRAVO"),
            });
            GameText.Use(() => table);
            var args = new object[] { HudFormat.TeamName(Team.Bravo), "生存" };

            Assert.That(GameText.Format("guardian.leader", args), Is.EqualTo("BRAVO リーダー 生存"));
            Assert.That(args[0], Is.EqualTo(HudFormat.TeamName(Team.Bravo)));
        }

        [Test]
        public void ASourceReturningNullShowsTheKey()
        {
            GameText.Use(() => null);

            Assert.That(GameText.Get("hud.reloading"), Is.EqualTo("hud.reloading"));
        }

        [Test]
        public void TheSourceIsAskedOnEveryLookup()
        {
            var japanese = Table("リロード中");
            var english = Table("Reloading");
            var current = japanese;
            GameText.Use(() => current);

            Assert.That(GameText.Resolve(HudFormat.Reloading), Is.EqualTo("リロード中"));
            Assert.That(HudFormat.Magazine(0, true), Is.EqualTo("リロード中"));

            current = english;

            Assert.That(GameText.Resolve(HudFormat.Reloading), Is.EqualTo("Reloading"));
            Assert.That(HudFormat.Magazine(0, true), Is.EqualTo("Reloading"));
        }

        [Test]
        public void NullArgumentsAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => GameText.Get(null));
            Assert.Throws<ArgumentNullException>(() => GameText.Format(null, 1));
            Assert.Throws<ArgumentNullException>(() => GameText.Format("hud.respawn", null));
        }

        [Test]
        public void TheEnglishTableFormatsTheHudInEnglish()
        {
            var english = JapaneseGameTextSetUp.LoadTable(GameLanguage.English);
            GameText.Use(() => english);

            Assert.That(HudFormat.Ammo(0, 90, true), Is.EqualTo("Reloading / 90"));
            Assert.That(GameText.Resolve(HudFormat.Respawn(3)), Is.EqualTo("Respawn in 3 s"));
            Assert.That(GameText.Resolve(HudFormat.Result(Team.Alpha)), Is.EqualTo("ALPHA wins"));
            Assert.That(GameText.Resolve(ScoreboardRows.LocalPlayerName), Is.EqualTo("You"));
            Assert.That(SubtitleText.Format(new SoundCue(SoundCueKind.Gunshot, SoundCueDirection.BackLeft, SoundCue.FarDistance)), Is.EqualTo("[Gunshot back left far]"));
            Assert.That(GameText.Resolve(KillFeedFormat.Weapon(string.Empty, false, true)), Is.EqualTo("[Suicide]"));
            Assert.That(GameText.Resolve(KillFeedFormat.Weapon("Rifle", true, false)), Is.EqualTo("[Rifle] Headshot"));
            Assert.That(GameText.Resolve(ZoneHudText.Closed), Is.EqualTo("Final zone"));
        }

        private static StringTable Table(string reloading)
        {
            return new StringTable(new[] { new KeyValuePair<string, string>("hud.reloading", reloading) });
        }
    }
}
