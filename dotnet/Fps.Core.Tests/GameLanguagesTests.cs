using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class GameLanguagesTests
    {
        private static readonly string[] _shippedNativeNames ={ "日本語", "English" };
        private static readonly string[] _expectedWarningKinds ={ "missing", "notJson", "array", "noLanguages" };
        private static readonly string[] _emptySystemLanguage ={ "" };

        private Func<GameLanguageList> _before;

        [SetUp]
        public void RememberSource()
        {
            _before = GameLanguages.Source;
        }

        [TearDown]
        public void RestoreSource()
        {
            GameLanguages.Use(_before);
        }

        [Test]
        public void TheShippedListKeepsJapaneseThenEnglish()
        {
            var shipped = JapaneseGameTextSetUp.LoadLanguageList();

            Assert.That(shipped.Languages, Is.EqualTo(new[] { GameLanguage.Japanese, GameLanguage.English }));
            Assert.That(shipped.Entries.Select(e => e.NativeName), Is.EqualTo(_shippedNativeNames));
            Assert.That(shipped.ForSystemLanguage("Japanese"), Is.EqualTo(GameLanguage.Japanese));
            Assert.That(shipped.ForSystemLanguage("French"), Is.EqualTo(GameLanguage.English));
        }

        [Test]
        public void WithoutASourceTheBuiltInListIsUsed()
        {
            GameLanguages.Use(null);

            Assert.That(GameLanguages.Current, Is.SameAs(GameLanguageList.BuiltIn));
            Assert.That(GameLanguages.All, Is.EqualTo(new[] { GameLanguage.Japanese, GameLanguage.English }));

            GameLanguages.Use(() => null);
            Assert.That(GameLanguages.Current, Is.SameAs(GameLanguageList.BuiltIn));
        }

        [TestCase("ja", "日本語")]
        [TestCase("en", "English")]
        public void CodesAndNamesComeFromTheList(string code, string nativeName)
        {
            GameLanguages.Use(null);

            Assert.That(GameLanguages.TryParseCode(code, out var parsed), Is.True);
            Assert.That(GameLanguages.Code(parsed), Is.EqualTo(code));
            Assert.That(GameLanguages.NativeName(parsed), Is.EqualTo(nativeName));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("JA")]
        [TestCase("ja-JP")]
        [TestCase("fr")]
        public void CodesNotInTheListAreRejected(string code)
        {
            GameLanguages.Use(null);

            Assert.That(GameLanguages.TryParseCode(code, out _), Is.False);
        }

        [Test]
        public void NextCyclesThroughEveryLanguage()
        {
            GameLanguages.Use(null);

            Assert.That(GameLanguages.Next(GameLanguage.Japanese), Is.EqualTo(GameLanguage.English));
            Assert.That(GameLanguages.Next(GameLanguage.English), Is.EqualTo(GameLanguage.Japanese));
            Assert.That(GameLanguages.Next(new GameLanguage("fr")), Is.EqualTo(GameLanguage.Japanese), "a language not in the list goes to the first");
        }

        [Test]
        public void RejectsTheDefaultLanguage()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => GameLanguages.Code(default));
            Assert.Throws<ArgumentOutOfRangeException>(() => GameLanguages.NativeName(default));
            Assert.That(default(GameLanguage).IsDefined, Is.False);
        }

        [Test]
        public void ALanguageNotInTheListIsNamedByItsCode()
        {
            GameLanguages.Use(null);

            Assert.That(GameLanguages.NativeName(new GameLanguage("fr")), Is.EqualTo("fr"));
        }

        [TestCase("ja")]
        [TestCase("pt-BR")]
        [TestCase("zh_Hans")]
        [TestCase("abcdefghijklmnopqrstuvwxyzabcdefghi")]
        public void AcceptsLanguageCodes(string code)
        {
            Assert.That(GameLanguage.IsValidCode(code), Is.True);
            Assert.That(new GameLanguage(code).Code, Is.EqualTo(code));
        }

        [TestCase("")]
        [TestCase("1ja")]
        [TestCase("-ja")]
        [TestCase("ja/en")]
        [TestCase("ja.json")]
        [TestCase("ja ")]
        [TestCase("日本")]
        [TestCase("abcdefghijklmnopqrstuvwxyzabcdefghij")]
        public void RejectsTextThatCannotBeACode(string code)
        {
            Assert.That(GameLanguage.IsValidCode(code), Is.False);
            Assert.Throws<ArgumentException>(() => _ = new GameLanguage(code));
        }

        [Test]
        public void LanguagesAreEqualByTheirCaseSensitiveCode()
        {
            Assert.That(new GameLanguage("ja"), Is.EqualTo(GameLanguage.Japanese));
            Assert.That(new GameLanguage("ja") == GameLanguage.Japanese, Is.True);
            Assert.That(new GameLanguage("JA") != GameLanguage.Japanese, Is.True);
            Assert.That(new GameLanguage("ja").GetHashCode(), Is.EqualTo(GameLanguage.Japanese.GetHashCode()));
            Assert.That(GameLanguage.Japanese.ToString(), Is.EqualTo("ja"));
            Assert.That(default(GameLanguage).ToString(), Is.EqualTo("(none)"));
            Assert.Throws<ArgumentNullException>(() => _ = new GameLanguage(null));
        }

        [Test]
        public void ALanguageAddedToTheListIsChosenAndSaved()
        {
            var french = new GameLanguage("fr");
            GameLanguages.Use(() => Read(
                File("languages", "{'otherSystemLanguages':'en','languages':[{'code':'ja','name':'日本語'},{'code':'en','name':'English'}]}"),
                File("zz-game", "{'languages':[{'code':'fr','name':'Français','systemLanguages':['French']}]}")));

            Assert.That(GameLanguages.All, Is.EqualTo(new[] { GameLanguage.Japanese, GameLanguage.English, french }));
            Assert.That(GameLanguages.NativeName(french), Is.EqualTo("Français"));
            Assert.That(GameLanguages.Next(GameLanguage.English), Is.EqualTo(french));
            Assert.That(GameLanguages.Next(french), Is.EqualTo(GameLanguage.Japanese));
            Assert.That(GameLanguages.Current.ForSystemLanguage("French"), Is.EqualTo(french));

            var saved = ControlSettings.Default.WithLanguage(french).ToJson();
            var loaded = ControlSettings.FromJson(saved, GameLanguage.Japanese, out var problems);

            Assert.That(saved, Does.Contain("\"language\":\"fr\""));
            Assert.That(problems, Is.Empty);
            Assert.That(loaded.Language, Is.EqualTo(french));
        }

        [TestCase("ja")]
        [TestCase("en")]
        public void SettingsSavedBeforeTheListStillLoad(string code)
        {
            GameLanguages.Use(() => JapaneseGameTextSetUp.LoadLanguageList());
            var japaneseSaved = ControlSettings.Default.ToJson();
            Assert.That(japaneseSaved, Does.EndWith(Json(",'language':'ja'}")));
            var saved = japaneseSaved.Replace(Json("'language':'ja'"), Json("'language':'" + code + "'"), StringComparison.Ordinal);
            var other = code == "ja" ? GameLanguage.English : GameLanguage.Japanese;

            var loaded = ControlSettings.FromJson(saved, other, out var problems);

            Assert.That(problems, Is.Empty);
            Assert.That(loaded.Language, Is.EqualTo(new GameLanguage(code)));
        }

        [Test]
        public void ACodeRemovedFromTheListFallsBack()
        {
            GameLanguages.Use(() => Read(File("languages", "{'languages':[{'code':'ja','name':'日本語'}]}")));
            var saved = ControlSettings.Default.WithLanguage(GameLanguage.English).ToJson();

            var loaded = ControlSettings.FromJson(saved, GameLanguage.Japanese, out var problems);

            Assert.That(loaded.Language, Is.EqualTo(GameLanguage.Japanese));
            Assert.That(problems, Has.Count.EqualTo(1));
            Assert.That(problems[0], Does.Contain("\"ja\"").And.Contain("\"en\""));
        }

        [Test]
        public void ALaterFileReplacesALanguageInPlace()
        {
            var list = Read(
                File("a", "{'otherSystemLanguages':'en','languages':[{'code':'ja','name':'日本語'},{'code':'en','name':'English'}]}"),
                File("b", "{'otherSystemLanguages':'ja','languages':[{'code':'ja','name':'にほんご','systemLanguages':['Japanese']}]}"));

            Assert.That(list.Languages, Is.EqualTo(new[] { GameLanguage.Japanese, GameLanguage.English }));
            Assert.That(list.Entries[0].NativeName, Is.EqualTo("にほんご"));
            Assert.That(list.OtherSystemLanguages, Is.EqualTo(GameLanguage.Japanese), "the last file that sets it wins");
        }

        [Test]
        public void SystemLanguagesPickTheFirstMatchOrTheOtherLanguage()
        {
            var list = Read(File("a",
                "{'otherSystemLanguages':'en','languages':["
                + "{'code':'ja','name':'日本語','systemLanguages':['Japanese']},"
                + "{'code':'en','name':'English','systemLanguages':['English']},"
                + "{'code':'zh-Hans','name':'简体中文','systemLanguages':['ChineseSimplified','Chinese']}]}"));

            Assert.That(list.ForSystemLanguage("Japanese"), Is.EqualTo(GameLanguage.Japanese));
            Assert.That(list.ForSystemLanguage("Chinese"), Is.EqualTo(new GameLanguage("zh-Hans")));
            Assert.That(list.ForSystemLanguage("French"), Is.EqualTo(GameLanguage.English));
            Assert.That(list.ForSystemLanguage("japanese"), Is.EqualTo(GameLanguage.English), "names are case-sensitive");
            Assert.That(list.ForSystemLanguage(null), Is.EqualTo(GameLanguage.English));
        }

        [Test]
        public void WithoutOtherSystemLanguagesTheFirstLanguageIsUsed()
        {
            var list = Read(File("a", "{'languages':[{'code':'en','name':'English'},{'code':'ja','name':'日本語'}]}"));

            Assert.That(list.OtherSystemLanguages, Is.EqualTo(GameLanguage.English));
            Assert.That(list.ForSystemLanguage("Japanese"), Is.EqualTo(GameLanguage.English), "no systemLanguages were given");
        }

        [Test]
        public void BrokenRowsAreSkippedWithAWarning()
        {
            var warnings = new List<string>();
            var ok = GameLanguageList.TryRead(
                new[]
                {
                    File("a", "{'otherSystemLanguages':'de','extra':1,'languages':["
                        + "{'code':'ja','name':'日本語','note':'kept'},"
                        + "{'code':'ja','name':'二度目'},"
                        + "{'code':'JA JP','name':'x'},"
                        + "{'code':'fr'},"
                        + "{'code':'es','name':'  '},"
                        + "{'code':'it','name':'Italiano','systemLanguages':'Italian'},"
                        + "{'code':'ko','name':'한국어','systemLanguages':[1]},"
                        + "'en',"
                        + "{'code':'en','name':'English'}]}"),
                },
                warnings.Add,
                out var list,
                out var error);

            Assert.That(ok, Is.True, error);
            Assert.That(list.Languages, Is.EqualTo(new[] { GameLanguage.Japanese, GameLanguage.English }));
            Assert.That(list.Entries[0].NativeName, Is.EqualTo("日本語"), "the first of a doubled code is kept");
            Assert.That(list.OtherSystemLanguages, Is.EqualTo(GameLanguage.Japanese), "'de' is not in the list");
            Assert.That(warnings, Has.Count.EqualTo(10));
            Assert.That(warnings, Has.Some.Contain("'extra'"));
            Assert.That(warnings, Has.Some.Contain("'note'"));
            Assert.That(warnings, Has.Some.Contain("listed twice"));
            Assert.That(warnings, Has.Some.Contain("'de'"));
            Assert.That(warnings.All(w => w.StartsWith("a:", StringComparison.Ordinal)), Is.True);
        }

        [Test]
        public void BrokenFilesAreSkippedWithAWarning()
        {
            var warnings = new List<string>();
            var ok = GameLanguageList.TryRead(
                new[]
                {
                    new KeyValuePair<string, string>("missing", null),
                    File("notJson", "{'languages':["),
                    File("array", "[]"),
                    File("noLanguages", "{'language':[]}"),
                    new KeyValuePair<string, string>("bom", "﻿" + Json("{'languages':[{'code':'en','name':'English'}]}")),
                },
                warnings.Add,
                out var list,
                out _);

            Assert.That(ok, Is.True);
            Assert.That(list.Languages, Is.EqualTo(new[] { GameLanguage.English }));
            Assert.That(warnings.Select(w => w.Substring(0, w.IndexOf(':'))), Is.EqualTo(_expectedWarningKinds));
        }

        [Test]
        public void NoLanguageAtAllIsAnError()
        {
            Assert.That(GameLanguageList.TryRead(Array.Empty<KeyValuePair<string, string>>(), null, out var none, out var noneError), Is.False);
            Assert.That(none, Is.Null);
            Assert.That(noneError, Does.Contain("no language list"));

            Assert.That(GameLanguageList.TryRead(new[] { File("a", "{'languages':[]}") }, null, out var empty, out var emptyError), Is.False);
            Assert.That(empty, Is.Null);
            Assert.That(emptyError, Does.Contain("no language could be read"));

            Assert.Throws<ArgumentNullException>(() => GameLanguageList.TryRead(null, null, out _, out _));
        }

        [Test]
        public void TheListRejectsBadArguments()
        {
            var japanese = new GameLanguageEntry(GameLanguage.Japanese, "日本語");

            Assert.Throws<ArgumentNullException>(() => _ = new GameLanguageList(null));
            Assert.Throws<ArgumentNullException>(() => _ = new GameLanguageList(new GameLanguageEntry[] { null }));
            Assert.Throws<ArgumentException>(() => _ = new GameLanguageList(Array.Empty<GameLanguageEntry>()));
            Assert.Throws<ArgumentException>(() => _ = new GameLanguageList(new[] { japanese, new GameLanguageEntry(GameLanguage.Japanese, "x") }));
            Assert.Throws<ArgumentException>(() => _ = new GameLanguageList(new[] { japanese }, GameLanguage.English));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new GameLanguageEntry(default, "x"));
            Assert.Throws<ArgumentException>(() => _ = new GameLanguageEntry(GameLanguage.English, " "));
            Assert.Throws<ArgumentException>(() => _ = new GameLanguageEntry(GameLanguage.English, "English", _emptySystemLanguage));
            Assert.That(new GameLanguageList(new[] { japanese }).Contains(default), Is.False);
            Assert.That(new GameLanguageList(new[] { japanese }).TryGet(default, out _), Is.False);
        }

        private static GameLanguageList Read(params KeyValuePair<string, string>[] files)
        {
            var warnings = new List<string>();
            Assert.That(GameLanguageList.TryRead(files, warnings.Add, out var list, out var error), Is.True, error);
            Assert.That(warnings, Is.Empty);
            return list;
        }

        private static KeyValuePair<string, string> File(string name, string json)
        {
            return new KeyValuePair<string, string>(name, Json(json));
        }

        /// <summary>読みやすさのため ' で書いた JSON を " に直す。</summary>
        private static string Json(string text)
        {
            return text.Replace('\'', '"');
        }
    }
}
