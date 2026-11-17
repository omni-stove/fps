using System;
using System.Collections.Generic;
using Shooter.Runtime;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
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
            Assert.That(GameText.Resolve(new LocalizedText("hud.respawn", 3)), Is.EqualTo("hud.respawn"));
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

            Assert.That(GameText.Resolve(new LocalizedText("hud.wins", new LocalizedText("hud.team.alpha"))), Is.EqualTo("アルファ の勝ち"));
        }

        [Test]
        public void FormatResolvesLocalizedTextArgumentsWithoutChangingTheCallersArray()
        {
            var table = new StringTable(new[]
            {
                new KeyValuePair<string, string>("mode.guardian.leader", "{0} リーダー {1}"),
                new KeyValuePair<string, string>("hud.team.bravo", "BRAVO"),
            });
            GameText.Use(() => table);
            var args = new object[] { new LocalizedText("hud.team.bravo"), "生存" };

            Assert.That(GameText.Format("mode.guardian.leader", args), Is.EqualTo("BRAVO リーダー 生存"));
            Assert.That(args[0], Is.EqualTo(new LocalizedText("hud.team.bravo")));
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

            Assert.That(GameText.Resolve(new LocalizedText("hud.reloading")), Is.EqualTo("リロード中"));

            current = english;

            Assert.That(GameText.Resolve(new LocalizedText("hud.reloading")), Is.EqualTo("Reloading"));
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

            Assert.That(GameText.Resolve(new LocalizedText("hud.reloading")), Is.EqualTo("Reloading"));
            Assert.That(GameText.Resolve(new LocalizedText("hud.respawn", 3)), Is.EqualTo("Respawn in 3 s"));
            Assert.That(GameText.Resolve(new LocalizedText("hud.wins", new LocalizedText("hud.team.alpha"))), Is.EqualTo("ALPHA wins"));
            Assert.That(GameText.Resolve(new LocalizedText("hud.scoreboard.you")), Is.EqualTo("You"));
            Assert.That(SubtitleText.Format(new SoundCue(SoundCueKind.Gunshot, SoundCueDirection.BackLeft, SoundCue.FarDistance)), Is.EqualTo("[Gunshot back left far]"));
            Assert.That(GameText.Resolve(new LocalizedText("hud.zone.closed")), Is.EqualTo("Final zone"));
        }

        private static StringTable Table(string reloading)
        {
            return new StringTable(new[] { new KeyValuePair<string, string>("hud.reloading", reloading) });
        }
    }
}
