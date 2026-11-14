using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    /// <summary>
    /// <see cref="GameText"/> が渡された関数から引くたびに表を受け取り、表が無ければキーそのものを返すこと。
    /// Core の書式が言語の切り替えに付いていくこと (英語の表で日本語を出さないこと)。
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
        }

        [Test]
        public void ResolveUsesTheCurrentTableAndShowsTheKeyWithoutOne()
        {
            GameText.Use(() => Table("リロード中"));

            Assert.That(GameText.Resolve(new LocalizedText("hud.reloading")), Is.EqualTo("リロード中"));
            Assert.That(GameText.Resolve(LocalizedText.Literal("as is")), Is.EqualTo("as is"));
            Assert.That(GameText.Resolve(default), Is.Empty);

            GameText.Use(null);

            Assert.That(GameText.Resolve(new LocalizedText("hud.reloading")), Is.EqualTo("hud.reloading"));
            Assert.That(GameText.Resolve(LocalizedText.Literal("as is")), Is.EqualTo("as is"));
            Assert.That(GameText.Resolve(default), Is.Empty);
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

            Assert.That(HudFormat.Reloading, Is.EqualTo("リロード中"));

            current = english;

            Assert.That(HudFormat.Reloading, Is.EqualTo("Reloading"));
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
            Assert.That(HudFormat.Respawn(3), Is.EqualTo("Respawn in 3 s"));
            Assert.That(HudFormat.Result(Team.Alpha), Is.EqualTo("ALPHA wins"));
            Assert.That(ScoreboardRows.LocalPlayerName, Is.EqualTo("You"));
            Assert.That(SubtitleText.Format(new SoundCue(SoundCueKind.Gunshot, SoundCueDirection.BackLeft, SoundCue.FarDistance)), Is.EqualTo("[Gunshot back left far]"));
            Assert.That(KillFeedFormat.Weapon(string.Empty, false, true), Is.EqualTo("[Suicide]"));
        }

        private static StringTable Table(string reloading)
        {
            return new StringTable(new[] { new KeyValuePair<string, string>("hud.reloading", reloading) });
        }
    }
}
