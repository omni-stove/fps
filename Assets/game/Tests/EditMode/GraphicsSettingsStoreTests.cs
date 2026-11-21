using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace Shooter.Tests.EditMode
{
    public sealed class GraphicsSettingsStoreTests
    {
        private static readonly GraphicsSettings _gameDefaults = new GraphicsSettings(ScreenResolution.Native, 1, true, 60);

        private GraphicsSettingsStore _store;

        [SetUp]
        public void SetUp()
        {
            // 本物の設定 (DefaultKey) を壊さないよう、テストごとに別のキーを使う。
            _store = new GraphicsSettingsStore("Shooter.Tests.GraphicsSettings." + Guid.NewGuid().ToString("N"), _gameDefaults);
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(_store.Key);
            PlayerPrefs.Save();
        }

        [Test]
        public void TheGameKeyIsItsOwn()
        {
            Assert.That(GraphicsSettingsStore.DefaultKey, Is.EqualTo("Shooter.GraphicsSettings"));
            Assert.That(GraphicsSettingsStore.DefaultKey, Is.Not.EqualTo(ControlSettingsStore.DefaultKey));
            Assert.That(GraphicsSettingsStore.DefaultKey, Is.Not.EqualTo(VoiceSettingsStore.DefaultKey));
        }

        [Test]
        public void LoadsTheGivenDefaultsSilentlyBeforeTheFirstSave()
        {
            Assert.That(_store.Defaults, Is.SameAs(_gameDefaults));
            Assert.That(_store.Load(), Is.EqualTo(_gameDefaults));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void WithoutGivenDefaultsTheTuningDataIsUsed()
        {
            KitTuning.Reload();
            try
            {
                var store = new GraphicsSettingsStore(_store.Key);

                Assert.That(store.Defaults, Is.EqualTo(KitTuning.Graphics.Defaults));
                Assert.That(store.Load(), Is.EqualTo(KitTuning.Graphics.Defaults));
            }
            finally
            {
                KitTuning.Reload();
            }
        }

        [Test]
        public void SavedSettingsLoadBackAfterARestart()
        {
            var settings = new GraphicsSettings(new ScreenResolution(1600, 900), 0, false, 144);

            _store.Save(settings);

            Assert.That(PlayerPrefs.GetString(_store.Key), Is.EqualTo(settings.ToJson()));
            Assert.That(new GraphicsSettingsStore(_store.Key, GraphicsSettings.Default).Load(), Is.EqualTo(settings));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void SavingTellsTheKeyAndTheSettings()
        {
            var saved = new List<KeyValuePair<string, GraphicsSettings>>();
            void Record(string key, GraphicsSettings settings) => saved.Add(new KeyValuePair<string, GraphicsSettings>(key, settings));
            GraphicsSettingsStore.Saved += Record;
            try
            {
                _store.Save(GraphicsSettings.Default.WithVSync(true));
            }
            finally
            {
                GraphicsSettingsStore.Saved -= Record;
            }

            Assert.That(saved, Has.Count.EqualTo(1));
            Assert.That(saved[0].Key, Is.EqualTo(_store.Key));
            Assert.That(saved[0].Value, Is.EqualTo(GraphicsSettings.Default.WithVSync(true)));
        }

        [Test]
        public void ABrokenValueWarnsAndUsesTheDefaults()
        {
            PlayerPrefs.SetString(_store.Key, "{not json");
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Graphics\] graphics settings '.*': not valid JSON"));

            Assert.That(_store.Load(), Is.EqualTo(_gameDefaults));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void AFrameRateOutOfRangeWarnsAndIsClamped()
        {
            PlayerPrefs.SetString(
                _store.Key,
                "{\"version\":1,\"resolutionWidth\":0,\"resolutionHeight\":0,\"qualityLevel\":-1,\"vSync\":false,\"frameRateLimit\":9000}");
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Graphics\] graphics settings '.*': 'frameRateLimit' 9000 is not"));

            Assert.That(_store.Load(), Is.EqualTo(GraphicsSettings.Default.WithFrameRateLimit(1000)));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ANumberUnderTheKeyWarnsAndUsesTheDefaults()
        {
            PlayerPrefs.SetInt(_store.Key, 3);
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Graphics\] graphics settings '.*': not valid JSON"));

            Assert.That(_store.Load(), Is.EqualTo(_gameDefaults));
        }

        [Test]
        public void DeleteForgetsTheSavedSettings()
        {
            _store.Save(GraphicsSettings.Default.WithFrameRateLimit(30));

            _store.Delete();

            Assert.That(_store.Load(), Is.EqualTo(_gameDefaults));
        }

        [Test]
        public void AnEmptyKeyIsRejected()
        {
            Assert.Throws<ArgumentException>(() => new GraphicsSettingsStore(""));
            Assert.Throws<ArgumentNullException>(() => new GraphicsSettingsStore(null));
            Assert.Throws<ArgumentNullException>(() => _store.Save(null));
        }
    }
}
