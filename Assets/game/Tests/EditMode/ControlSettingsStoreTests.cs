using System;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Fps.Tests.EditMode
{
    public sealed class ControlSettingsStoreTests
    {
        private ControlSettingsStore _store;

        [SetUp]
        public void SetUp()
        {
            // 本物の設定 (DefaultKey) を壊さないよう、テストごとに別のキーを使う。
            _store = new ControlSettingsStore("Fps.Tests.ControlSettings." + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(_store.Key);
            PlayerPrefs.Save();
        }

        [Test]
        public void LoadsDefaultsSilentlyBeforeTheFirstSave()
        {
            Assert.That(_store.Load(), Is.EqualTo(ControlSettings.Default));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void SavesToPlayerPrefsAndLoadsItBack()
        {
            var settings = ControlSettings.Default
                .With(InputDeviceKind.KeyboardMouse, HoldAction.Crouch, HoldMode.Toggle)
                .With(InputDeviceKind.Gamepad, HoldAction.Sprint, HoldMode.Hold);

            _store.Save(settings);

            Assert.That(PlayerPrefs.HasKey(_store.Key), Is.True);
            Assert.That(PlayerPrefs.GetString(_store.Key), Is.EqualTo(settings.ToJson()));
            Assert.That(new ControlSettingsStore(_store.Key).Load(), Is.EqualTo(settings));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void SaveReplacesTheEarlierValue()
        {
            _store.Save(ControlSettings.Default.With(InputDeviceKind.Gamepad, HoldAction.Aim, HoldMode.Toggle));

            _store.Save(ControlSettings.Default);

            Assert.That(_store.Load(), Is.EqualTo(ControlSettings.Default));
        }

        [Test]
        public void DeleteRemovesTheKey()
        {
            _store.Save(ControlSettings.Default.With(InputDeviceKind.Gamepad, HoldAction.Aim, HoldMode.Toggle));

            _store.Delete();

            Assert.That(PlayerPrefs.HasKey(_store.Key), Is.False);
            Assert.That(_store.Load(), Is.EqualTo(ControlSettings.Default));
        }

        [Test]
        public void BrokenTextFallsBackToDefaultsWithAWarning()
        {
            PlayerPrefs.SetString(_store.Key, "{not json");
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Input\] .*not valid JSON"));

            Assert.That(_store.Load(), Is.EqualTo(ControlSettings.Default));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ValueOfAnotherTypeFallsBackToDefaultsWithAWarning()
        {
            PlayerPrefs.SetInt(_store.Key, 1);
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Input\] "));

            Assert.That(_store.Load(), Is.EqualTo(ControlSettings.Default));
        }

        [Test]
        public void BrokenOrMissingEntriesFallBackOneByOneWithAWarningEach()
        {
            PlayerPrefs.SetString(
                _store.Key,
                "{\"version\":1,\"keyboardMouse\":{\"sprint\":\"toggle\",\"crouch\":\"sometimes\"}," +
                "\"gamepad\":{\"sprint\":\"hold\",\"crouch\":\"toggle\",\"aim\":\"toggle\"}}");
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Input\] .*keyboardMouse\.crouch"));
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Input\] .*keyboardMouse\.aim"));

            var loaded = _store.Load();

            Assert.That(loaded.Get(InputDeviceKind.KeyboardMouse, HoldAction.Sprint), Is.EqualTo(HoldMode.Toggle));
            Assert.That(loaded.Get(InputDeviceKind.KeyboardMouse, HoldAction.Crouch), Is.EqualTo(HoldMode.Hold));
            Assert.That(loaded.Get(InputDeviceKind.KeyboardMouse, HoldAction.Aim), Is.EqualTo(HoldMode.Hold));
            Assert.That(loaded.Get(InputDeviceKind.Gamepad, HoldAction.Sprint), Is.EqualTo(HoldMode.Hold));
            Assert.That(loaded.Get(InputDeviceKind.Gamepad, HoldAction.Aim), Is.EqualTo(HoldMode.Toggle));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void RejectsInvalidArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new ControlSettingsStore(null));
            Assert.Throws<ArgumentException>(() => new ControlSettingsStore(string.Empty));
            Assert.Throws<ArgumentNullException>(() => _store.Save(null));
        }

        [Test]
        public void UsesTheGameKeyByDefault()
        {
            Assert.That(new ControlSettingsStore().Key, Is.EqualTo(ControlSettingsStore.DefaultKey));
        }
    }
}
