using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// <see cref="Localizer"/> が言語ごとの表を重ねて読み、読めない言語は日本語に戻し、
    /// <see cref="Localizer.SwitchTo"/> で読み直して保存すること。
    /// </summary>
    public sealed class LocalizationLayersTests
    {
        private ControlSettings _appliedBefore;
        private LanguagePreferenceStore _store;

        [SetUp]
        public void SetUp()
        {
            _appliedBefore = AppliedControlSettings.Current;
            AppliedControlSettings.Apply(ControlSettings.Default);
            // 本物の設定 (DefaultKey) を壊さないよう、テストごとに別のキーを使う。
            _store = new LanguagePreferenceStore("Fps.Tests.Language." + Guid.NewGuid().ToString("N"), GameLanguage.Japanese);
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(_store.Key);
            PlayerPrefs.Save();
            AppliedControlSettings.Apply(_appliedBefore);
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
            var folder = names.Skip(1).ToList();
            Assert.That(folder, Is.EqualTo(folder.OrderBy(n => n, StringComparer.Ordinal).ToList()));
        }

        [Test]
        public void TheJapaneseTableIncludesTheFolderAndStillReadsWithoutWarnings()
        {
            Localizer.Reload();

            Assert.That(Localizer.Get("settings.title"), Is.EqualTo("設定"));
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
        public void ThePreferenceStoreUsesTheDefaultUntilSavedAndKeepsTheOtherSettings()
        {
            Assert.That(_store.HasSaved, Is.False);
            Assert.That(_store.Load(), Is.EqualTo(GameLanguage.Japanese));

            var settings = new ControlSettingsStore(_store.Key, GameLanguage.Japanese);
            settings.Save(ControlSettings.Default.With(ControlValue.MasterVolume, 30));
            _store.Save(GameLanguage.English);

            Assert.That(_store.HasSaved, Is.True);
            Assert.That(_store.Load(), Is.EqualTo(GameLanguage.English));
            Assert.That(settings.Load(), Is.EqualTo(ControlSettings.Default.With(ControlValue.MasterVolume, 30).WithLanguage(GameLanguage.English)));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void RejectsUndefinedLanguagesAndNullReaders()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _store.Save((GameLanguage)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => Localizer.SwitchTo((GameLanguage)99, _store));
            Assert.Throws<ArgumentNullException>(() => Localizer.Build(GameLanguage.Japanese, null));
            Assert.That(_store.HasSaved, Is.False);
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
