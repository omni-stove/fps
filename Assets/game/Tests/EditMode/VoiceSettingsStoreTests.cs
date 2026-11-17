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
    public sealed class VoiceSettingsStoreTests
    {
        private VoiceSettingsStore _store;

        [SetUp]
        public void SetUp()
        {
            // 本物の設定 (DefaultKey) を壊さないよう、テストごとに別のキーを使う。
            _store = new VoiceSettingsStore("Shooter.Tests.VoiceSettings." + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(_store.Key);
            PlayerPrefs.Save();
        }

        [Test]
        public void TheGameKeyIsNotTheControlSettingsKey()
        {
            Assert.That(VoiceSettingsStore.DefaultKey, Is.EqualTo("Shooter.VoiceSettings"));
            Assert.That(VoiceSettingsStore.DefaultKey, Is.Not.EqualTo(ControlSettingsStore.DefaultKey));
        }

        [Test]
        public void LoadsDefaultsSilentlyBeforeTheFirstSave()
        {
            Assert.That(_store.Load(), Is.EqualTo(VoiceSettings.Default));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void SavedSettingsLoadBack()
        {
            var settings = new VoiceSettings(false, 40);

            _store.Save(settings);

            Assert.That(PlayerPrefs.GetString(_store.Key), Is.EqualTo(settings.ToJson()));
            Assert.That(new VoiceSettingsStore(_store.Key).Load(), Is.EqualTo(settings));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void SavingTellsTheKeyAndTheSettings()
        {
            var saved = new List<KeyValuePair<string, VoiceSettings>>();
            void Record(string key, VoiceSettings settings) => saved.Add(new KeyValuePair<string, VoiceSettings>(key, settings));
            VoiceSettingsStore.Saved += Record;
            try
            {
                _store.Save(new VoiceSettings(true, 150));
            }
            finally
            {
                VoiceSettingsStore.Saved -= Record;
            }

            Assert.That(saved, Has.Count.EqualTo(1));
            Assert.That(saved[0].Key, Is.EqualTo(_store.Key));
            Assert.That(saved[0].Value, Is.EqualTo(new VoiceSettings(true, 150)));
        }

        [Test]
        public void ABrokenValueWarnsAndUsesTheDefault()
        {
            PlayerPrefs.SetString(_store.Key, "{not json");
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Voice\] voice settings '.*': not valid JSON"));

            Assert.That(_store.Load(), Is.EqualTo(VoiceSettings.Default));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void AVolumeOutOfRangeWarnsAndIsClamped()
        {
            PlayerPrefs.SetString(_store.Key, "{\"version\":1,\"microphoneEnabled\":true,\"volumePercent\":900}");
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Voice\] voice settings '.*': 'volumePercent' 900 is out of range"));

            Assert.That(_store.Load(), Is.EqualTo(new VoiceSettings(true, 200)));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ANumberUnderTheKeyWarnsAndUsesTheDefault()
        {
            PlayerPrefs.SetInt(_store.Key, 3);
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Voice\] voice settings '.*': not valid JSON"));

            Assert.That(_store.Load(), Is.EqualTo(VoiceSettings.Default));
        }

        [Test]
        public void DeleteForgetsTheSavedSettings()
        {
            _store.Save(new VoiceSettings(false, 0));

            _store.Delete();

            Assert.That(_store.Load(), Is.EqualTo(VoiceSettings.Default));
        }

        [Test]
        public void AnEmptyKeyIsRejected()
        {
            Assert.Throws<ArgumentException>(() => new VoiceSettingsStore(""));
            Assert.Throws<ArgumentNullException>(() => new VoiceSettingsStore(null));
            Assert.Throws<ArgumentNullException>(() => _store.Save(null));
        }
    }
}
