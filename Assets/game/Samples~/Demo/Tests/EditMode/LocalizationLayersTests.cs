using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Shooter.Tests.EditMode
{
    public sealed class LocalizationLayersTests
    {
        private const string VersionSevenSettings =
            "{\"version\":7,\"keyboardMouse\":{\"sprint\":\"hold\",\"crouch\":\"hold\",\"aim\":\"hold\"},"
            + "\"gamepad\":{\"sprint\":\"toggle\",\"crouch\":\"toggle\",\"aim\":\"hold\"},"
            + "\"mouseSensitivity\":100,\"gamepadSensitivity\":100,\"adsSensitivity\":100,\"fieldOfView\":60,"
            + "\"masterVolume\":30,\"effectsVolume\":100,\"cameraShake\":100,\"viewmodelMotion\":100,\"showBlood\":true,"
            + "\"showNetStats\":false,\"colorPalette\":\"standard\",\"showSubtitles\":false,\"language\":\"ja\"}";

        private GameLanguage _languageBefore;
        private string _legacyKey;
        private LanguagePreferenceStore _store;

        [SetUp]
        public void SetUp()
        {
            _languageBefore = Localizer.Language;
            Localizer.Apply(GameLanguage.Japanese);
            // 本物の設定 (DefaultKey) を壊さないよう、テストごとに別のキーを使う。
            var suffix = Guid.NewGuid().ToString("N");
            _legacyKey = "Shooter.Tests.LegacySettings." + suffix;
            _store = new LanguagePreferenceStore("Shooter.Tests.Language." + suffix, _legacyKey, GameLanguage.Japanese);
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(_store.Key);
            PlayerPrefs.DeleteKey(_legacyKey);
            PlayerPrefs.Save();
            Localizer.Apply(_languageBefore);
            Localizer.Reload();
        }

        [Test]
        public void LaterLayersWinForTheSameKey()
        {
            var table = Localizer.Build(GameLanguage.English, Reader(english: new[]
            {
                Layer("en.json", "{\"a\":\"base\",\"b\":\"base\"}"),
                Layer("common", "{\"b\":\"common\",\"c\":\"common\"}"),
            }));

            Assert.That(table.Get("a"), Is.EqualTo("base"));
            Assert.That(table.Get("b"), Is.EqualTo("common"));
            Assert.That(table.Get("c"), Is.EqualTo("common"));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ALanguageWithoutStringsFallsBackToJapaneseWithAWarning()
        {
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Localization\] en: no strings were found .*; using ja instead$"));

            var table = Localizer.Build(GameLanguage.English, Reader(english: Array.Empty<KeyValuePair<string, string>>()));

            Assert.That(table.Get("a"), Is.EqualTo("日本語"));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ABrokenLayerMakesTheLanguageFallBackToJapaneseWithAWarning()
        {
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Localization\] en: strings 'broken' could not be read: .*; using ja instead$"));

            var table = Localizer.Build(GameLanguage.English, Reader(english: new[]
            {
                Layer("good", "{\"a\":\"English\"}"),
                Layer("broken", "{\"a\":"),
            }));

            Assert.That(table.Get("a"), Is.EqualTo("日本語"));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ABrokenJapaneseLayerIsSkippedWithAnError()
        {
            LogAssert.Expect(LogType.Error, new Regex(@"^\[Localization\] ja: strings 'broken' could not be read: .*; skipping it$"));

            var table = Localizer.Build(GameLanguage.Japanese, Reader(japanese: new[]
            {
                Layer("base", "{\"a\":\"日本語\"}"),
                Layer("broken", "not json"),
            }));

            Assert.That(table.Get("a"), Is.EqualTo("日本語"));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void WithoutAnyJapaneseStringsTheKeysAreShownWithAnError()
        {
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Localization\] en: no strings were found"));
            LogAssert.Expect(LogType.Error, new Regex(@"^\[Localization\] ja: no strings were found .*; showing the keys instead$"));

            var empty = Array.Empty<KeyValuePair<string, string>>();
            var table = Localizer.Build(GameLanguage.English, Reader(japanese: empty, english: empty));

            Assert.That(table.Count, Is.Zero);
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Localization\] ja: missing key 'a'"));
            Assert.That(table.Get("a"), Is.EqualTo("a"));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ResourcesAreReadAsTheSingleTableThenTheFolderByName()
        {
            var layers = Localizer.ReadResources(GameLanguage.Japanese);
            var names = layers.Select(l => l.Key).ToList();

            Assert.That(names.First(), Is.EqualTo("Resources/Strings/ja.json"));
            Assert.That(names, Does.Contain("Resources/Localization/ja/common"));
            Assert.That(names, Does.Contain("Resources/Localization/ja/menu"));
            var folder = names.Skip(1).ToList();
            Assert.That(folder, Is.EqualTo(folder.OrderBy(n => n, StringComparer.Ordinal).ToList()));
        }

        [Test]
        public void TheJapaneseTableIncludesTheFolderAndStillReadsWithoutWarnings()
        {
            Localizer.Reload();

            Assert.That(Localizer.Get("settings.title"), Is.EqualTo("設定"));
            Assert.That(Localizer.Get("menu.quit"), Is.EqualTo("終了"));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void SwitchToAppliesSavesAndRaisesChanged()
        {
            var received = new List<GameLanguage>();
            Action<GameLanguage> record = received.Add;
            Localizer.Changed += record;
            try
            {
                Localizer.SwitchTo(GameLanguage.English, _store);

                Assert.That(Localizer.Language, Is.EqualTo(GameLanguage.English));
                Assert.That(Localizer.Get("settings.title"), Is.EqualTo("Settings"));
                Assert.That(_store.Load(), Is.EqualTo(GameLanguage.English));

                Localizer.SwitchTo(GameLanguage.English, _store);
            }
            finally
            {
                Localizer.Changed -= record;
            }

            Assert.That(received, Is.EqualTo(new[] { GameLanguage.English, GameLanguage.English }), "switching to the same language reloads and tells again");
        }

        [Test]
        public void SwitchToReReadsTheTables()
        {
            var before = Localizer.TableOf(GameLanguage.Japanese);

            Localizer.SwitchTo(GameLanguage.Japanese, _store);

            Assert.That(Localizer.TableOf(GameLanguage.Japanese), Is.Not.SameAs(before));
        }

        [Test]
        public void ThePreferenceStoreUsesTheDefaultUntilSavedAndLeavesTheControlSettingsAlone()
        {
            Assert.That(_store.HasSaved, Is.False);
            Assert.That(_store.Load(), Is.EqualTo(GameLanguage.Japanese));

            var settings = new ControlSettingsStore(_legacyKey);
            settings.Save(ControlSettings.Default.With(ControlValue.MasterVolume, 30));
            _store.Save(GameLanguage.English);

            Assert.That(_store.HasSaved, Is.True);
            Assert.That(PlayerPrefs.GetString(_store.Key), Is.EqualTo("en"));
            Assert.That(_store.Load(), Is.EqualTo(GameLanguage.English));
            Assert.That(settings.Load(), Is.EqualTo(ControlSettings.Default.With(ControlValue.MasterVolume, 30)));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void TheLanguageSavedInTheOldControlSettingsIsCarriedOver()
        {
            PlayerPrefs.SetString(_legacyKey, VersionSevenSettings.Replace("\"language\":\"ja\"", "\"language\":\"en\""));
            var store = new LanguagePreferenceStore(_store.Key, _legacyKey, GameLanguage.Japanese);

            Assert.That(store.Load(), Is.EqualTo(GameLanguage.English));
            Assert.That(store.HasSaved, Is.True, "carried over to its own key right away");
            Assert.That(PlayerPrefs.GetString(store.Key), Is.EqualTo("en"));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void JapaneseSavedInTheOldFormatStaysJapaneseAndTheOtherSettingsStay()
        {
            PlayerPrefs.SetString(_legacyKey, VersionSevenSettings);
            var store = new LanguagePreferenceStore(_store.Key, _legacyKey, GameLanguage.English);
            var settings = new ControlSettingsStore(_legacyKey);

            Assert.That(store.Load(), Is.EqualTo(GameLanguage.Japanese));
            Assert.That(settings.Load(), Is.EqualTo(ControlSettings.Default.With(ControlValue.MasterVolume, 30)));

            settings.Save(settings.Load());
            Assert.That(PlayerPrefs.GetString(_legacyKey), Does.Not.Contain("language"));
            Assert.That(store.Load(), Is.EqualTo(GameLanguage.Japanese));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ItsOwnKeyWinsOverTheOldControlSettings()
        {
            PlayerPrefs.SetString(_legacyKey, VersionSevenSettings);
            PlayerPrefs.SetString(_store.Key, "en");

            Assert.That(_store.Load(), Is.EqualTo(GameLanguage.English));
        }

        [TestCase("")]
        [TestCase("not json")]
        [TestCase("{\"version\":8,\"masterVolume\":30}")]
        [TestCase("{\"version\":7,\"language\":\"xx\"}")]
        public void OldControlSettingsWithoutAKnownLanguageUseTheDefault(string legacy)
        {
            PlayerPrefs.SetString(_legacyKey, legacy);
            var store = new LanguagePreferenceStore(_store.Key, _legacyKey, GameLanguage.English);

            Assert.That(store.Load(), Is.EqualTo(GameLanguage.English));
            Assert.That(store.HasSaved, Is.False);
        }

        [Test]
        public void AnUnknownSavedCodeFallsBackToTheDefaultWithAWarning()
        {
            PlayerPrefs.SetString(_store.Key, "xx");
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Localization\] language '.*': 'xx' is not a known language code; using ja$"));

            Assert.That(_store.Load(), Is.EqualTo(GameLanguage.Japanese));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ThePreferenceStoreRejectsAnEmptyKeyOrALanguageWithoutACode()
        {
            Assert.Throws<ArgumentNullException>(() => new LanguagePreferenceStore(null));
            Assert.Throws<ArgumentException>(() => new LanguagePreferenceStore(string.Empty));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LanguagePreferenceStore(_store.Key, null, default(GameLanguage)));
            Assert.Throws<ArgumentOutOfRangeException>(() => _store.Save(default));
        }

        [Test]
        public void OnlyLanguagesWithAFolderOfStringsCanBeChosen()
        {
            var onlyJapanese = Localizer.AvailableLanguages(Reader(english: Array.Empty<KeyValuePair<string, string>>()));
            var both = Localizer.AvailableLanguages(Reader(english: new[] { Layer("settings", "{}") }));

            Assert.That(onlyJapanese, Is.EqualTo(new[] { GameLanguage.Japanese }));
            Assert.That(both, Is.EqualTo(new[] { GameLanguage.Japanese, GameLanguage.English }));
            Assert.That(Localizer.AvailableLanguages(_ => null), Is.Empty);
        }

        [Test]
        public void TheResourcesOfferJapaneseAndEnglish()
        {
            Assert.That(Localizer.AvailableLanguages(), Is.EqualTo(new[] { GameLanguage.Japanese, GameLanguage.English }));
            Assert.That(Localizer.ReadLayers(GameLanguage.Japanese).Select(l => l.Key), Does.Contain("Resources/Localization/ja/settings"));
        }

        [Test]
        public void TheNativeNameComesFromTheLanguagesOwnSettings()
        {
            Assert.That(Localizer.NativeNameOf(GameLanguage.Japanese), Is.EqualTo("日本語"));
            Assert.That(Localizer.NativeNameOf(GameLanguage.English), Is.EqualTo("English"));

            LogAssert.Expect(LogType.Log, Localizer.LogTag + " language is now en");
            Localizer.Apply(GameLanguage.English);
            Assert.That(Localizer.NativeNameOf(GameLanguage.Japanese), Is.EqualTo("日本語"));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void TheLastLayerWithTheNameWinsAndBrokenLayersAreSkipped()
        {
            var name = Localizer.NativeNameOf(GameLanguage.English, Reader(english: new[]
            {
                Layer("a", "{\"settings.language.name\":\"English\"}"),
                Layer("b", "{\"settings.language.name\":\"English (UK)\"}"),
                Layer("broken", "{\"settings.language.name\":"),
                Layer("c", "{\"other\":\"x\"}"),
            }));

            Assert.That(name, Is.EqualTo("English (UK)"));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void AMissingNativeNameFallsBackWithAWarning()
        {
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Localization\] en: 'settings\.language\.name' was not found"));

            var name = Localizer.NativeNameOf(GameLanguage.English, Reader(english: new[] { Layer("settings", "{}") }));

            Assert.That(name, Is.EqualTo(GameLanguages.NativeName(GameLanguage.English)));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void RejectsUndefinedLanguagesAndNullReaders()
        {
            Assert.Throws<ArgumentNullException>(() => Localizer.AvailableLanguages(null));
            Assert.Throws<ArgumentNullException>(() => Localizer.NativeNameOf(GameLanguage.Japanese, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => Localizer.NativeNameOf(default));
            Assert.Throws<ArgumentOutOfRangeException>(() => _store.Save(default));
            Assert.Throws<ArgumentOutOfRangeException>(() => Localizer.SwitchTo(default, _store));
            Assert.Throws<ArgumentNullException>(() => Localizer.Build(GameLanguage.Japanese, null));
            Assert.Throws<ArgumentNullException>(() => Localizer.LoadLanguageList(null));
            Assert.That(_store.HasSaved, Is.False);
        }

        [Test]
        public void TheResourcesListJapaneseAndEnglish()
        {
            var list = Localizer.LoadLanguageList(Localizer.ReadLanguageLists);

            Assert.That(list.Languages, Is.EqualTo(new[] { GameLanguage.Japanese, GameLanguage.English }));
            Assert.That(Localizer.ReadLanguageLists().Select(l => l.Key), Does.Contain("Resources/Localization/Languages/languages"));
            Assert.That(Localizer.LanguageList.Languages, Is.EqualTo(list.Languages));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ALanguageInTheListWithAFolderIsOfferedAndShowsItsStrings()
        {
            var french = new GameLanguage("fr");
            var list = Localizer.LoadLanguageList(() => new[]
            {
                Layer("kit", "{\"languages\":[{\"code\":\"ja\",\"name\":\"日本語\"},{\"code\":\"en\",\"name\":\"English\"}]}"),
                Layer("zz-game", "{\"languages\":[{\"code\":\"fr\",\"name\":\"Français\",\"systemLanguages\":[\"French\"]}]}"),
            });
            var frenchLayers = new[] { Layer("settings", "{\"settings.language.name\":\"Français\",\"settings.title\":\"Paramètres\"}") };
            Func<GameLanguage, IReadOnlyList<KeyValuePair<string, string>>> read =
                language => language == french ? frenchLayers : Reader(english: new[] { Layer("settings", "{}") })(language);

            var before = GameLanguages.Source;
            GameLanguages.Use(() => list);
            try
            {
                Assert.That(Localizer.AvailableLanguages(read), Is.EqualTo(new[] { GameLanguage.Japanese, GameLanguage.English, french }));
                Assert.That(Localizer.NativeNameOf(french, read), Is.EqualTo("Français"));
                Assert.That(Localizer.Build(french, read).Get("settings.title"), Is.EqualTo("Paramètres"));
                Assert.That(Localizer.FromSystemLanguage(SystemLanguage.French), Is.EqualTo(french));
                Assert.That(Localizer.FromSystemLanguage(SystemLanguage.German), Is.EqualTo(GameLanguage.Japanese), "no otherSystemLanguages: the first");
            }
            finally
            {
                GameLanguages.Use(before);
            }

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void AnUnreadableListFallsBackToTheBuiltInOneWithAnError()
        {
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Localization\] broken: .*skipping it"));
            LogAssert.Expect(LogType.Error, new Regex(@"^\[Localization\] no language could be read .*using ja, en instead"));

            var list = Localizer.LoadLanguageList(() => new[] { Layer("broken", "{\"languages\":") });

            Assert.That(list, Is.SameAs(GameLanguageList.BuiltIn));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void NoListFallsBackToTheBuiltInOneWithAnError()
        {
            LogAssert.Expect(LogType.Error, new Regex(@"^\[Localization\] no language list was found in Resources/Localization/Languages/"));

            Assert.That(Localizer.LoadLanguageList(() => null), Is.SameAs(GameLanguageList.BuiltIn));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void AnUnknownSystemLanguageNameIsWarned()
        {
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Localization\] ja: 'Nihongo' is not a UnityEngine\.SystemLanguage name"));
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Localization\] ja: '10' is not a UnityEngine\.SystemLanguage name"));

            var list = Localizer.LoadLanguageList(() => new[]
            {
                Layer("a", "{\"languages\":[{\"code\":\"ja\",\"name\":\"日本語\",\"systemLanguages\":[\"Nihongo\",\"10\",\"Japanese\"]}]}"),
            });

            Assert.That(list.ForSystemLanguage(SystemLanguage.Japanese.ToString()), Is.EqualTo(GameLanguage.Japanese));
            LogAssert.NoUnexpectedReceived();
        }

        private static KeyValuePair<string, string> Layer(string name, string json)
        {
            return new KeyValuePair<string, string>(name, json);
        }

        private static Func<GameLanguage, IReadOnlyList<KeyValuePair<string, string>>> Reader(
            KeyValuePair<string, string>[] japanese = null,
            KeyValuePair<string, string>[] english = null)
        {
            japanese ??= new[] { Layer("ja", "{\"a\":\"日本語\"}") };
            english ??= Array.Empty<KeyValuePair<string, string>>();
            return language => language == GameLanguage.Japanese ? japanese : english;
        }
    }
}
