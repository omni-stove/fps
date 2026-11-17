using System;
using System.Collections.Generic;
using System.Linq;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // 本物の設定を壊さないよう、保存先はテストごとの別のキーにする。
    public sealed class SettingsViewVoiceTests
    {
        private ControlSettings _appliedBefore;
        private GameLanguage _languageBefore;
        private ControlSettingsStore _store;
        private LanguagePreferenceStore _languageStore;
        private VoiceSettingsStore _voiceStore;
        private SettingsView _view;
        private VisualElement _container;

        [SetUp]
        public void SetUp()
        {
            _appliedBefore = AppliedControlSettings.Current;
            _languageBefore = Localizer.Language;
            AppliedControlSettings.Apply(ControlSettings.Default);
            Localizer.Apply(GameLanguage.Japanese);
            var suffix = Guid.NewGuid().ToString("N");
            _store = new ControlSettingsStore("Shooter.Tests.SettingsViewVoice." + suffix);
            _languageStore = new LanguagePreferenceStore("Shooter.Tests.SettingsViewVoice.Language." + suffix, _store.Key, GameLanguage.Japanese);
            _voiceStore = new VoiceSettingsStore("Shooter.Tests.SettingsViewVoice.Voice." + suffix);
            _view = new SettingsView(_store, languageStore: _languageStore, voiceStore: _voiceStore);
            _container = new VisualElement();
        }

        [TearDown]
        public void TearDown()
        {
            _view.Close();
            PlayerPrefs.DeleteKey(_store.Key);
            PlayerPrefs.DeleteKey(_languageStore.Key);
            PlayerPrefs.DeleteKey(_voiceStore.Key);
            PlayerPrefs.Save();
            AppliedControlSettings.Apply(_appliedBefore);
            Localizer.Apply(_languageBefore);
        }

        [Test]
        public void TheVoiceRowsStartAtTheDefaultsInJapanese()
        {
            _view.Open(_container);

            Assert.That(_view.VoiceStore, Is.SameAs(_voiceStore));
            Assert.That(_view.Voice, Is.EqualTo(VoiceSettings.Default));
            Assert.That(LabelTexts(), Does.Contain("ボイスチャット"));
            Assert.That(LabelTexts(), Does.Contain("マイク"));
            Assert.That(LabelTexts(), Does.Contain("声の音量"));
            Assert.That(MicrophoneButton().text, Is.EqualTo("オン"));
            var slider = VolumeSlider();
            Assert.That(slider.lowValue, Is.EqualTo(0));
            Assert.That(slider.highValue, Is.EqualTo(200));
            Assert.That(slider.value, Is.EqualTo(100));
            Assert.That(VolumeLabel().text, Is.EqualTo("100%"));
            Assert.That(_view.HasUnsavedChanges, Is.False);
        }

        [Test]
        public void ChangingTheVoiceRowsIsWrittenToTheStoreOnClose()
        {
            var saved = new List<KeyValuePair<string, VoiceSettings>>();
            Action<string, VoiceSettings> record = (key, settings) => saved.Add(new KeyValuePair<string, VoiceSettings>(key, settings));
            VoiceSettingsStore.Saved += record;
            try
            {
                _view.Open(_container);

                _view.SetMicrophoneEnabled(false);
                _view.SetVoiceVolume(150);

                Assert.That(MicrophoneButton().text, Is.EqualTo("オフ"));
                Assert.That(VolumeSlider().value, Is.EqualTo(150));
                Assert.That(VolumeLabel().text, Is.EqualTo("150%"));
                Assert.That(_view.HasUnsavedChanges, Is.True);
                Assert.That(PlayerPrefs.HasKey(_voiceStore.Key), Is.False, "dragging the slider does not write to disk");
                Assert.That(saved, Is.Empty);

                Assert.That(_view.Close(), Is.True);

                Assert.That(new VoiceSettingsStore(_voiceStore.Key).Load(), Is.EqualTo(new VoiceSettings(false, 150)));
                Assert.That(saved.Count, Is.EqualTo(1));
                Assert.That(saved[0].Key, Is.EqualTo(_voiceStore.Key));
                Assert.That(saved[0].Value, Is.EqualTo(new VoiceSettings(false, 150)));
                Assert.That(PlayerPrefs.HasKey(_store.Key), Is.False, "the control settings did not change");
            }
            finally
            {
                VoiceSettingsStore.Saved -= record;
            }
        }

        [Test]
        public void TheSavedVoiceSettingsAreShownWhenOpenedAgain()
        {
            _voiceStore.Save(new VoiceSettings(false, 40));

            // 起動し直したときと同じく、新しい画面が保存した値を読む
            var view = new SettingsView(_store, languageStore: _languageStore, voiceStore: new VoiceSettingsStore(_voiceStore.Key));
            try
            {
                view.Open(_container);

                Assert.That(view.Voice, Is.EqualTo(new VoiceSettings(false, 40)));
                Assert.That(MicrophoneButton().text, Is.EqualTo("オフ"));
                Assert.That(VolumeSlider().value, Is.EqualTo(40));
                Assert.That(VolumeLabel().text, Is.EqualTo("40%"));
                Assert.That(view.HasUnsavedChanges, Is.False);
            }
            finally
            {
                view.Close();
            }
        }

        [Test]
        public void ClosingWithoutVoiceChangesDoesNotWriteTheVoiceStore()
        {
            _view.Open(_container);
            _view.SetMicrophoneEnabled(false);
            _view.SetMicrophoneEnabled(true);

            Assert.That(_view.Close(), Is.False);
            Assert.That(PlayerPrefs.HasKey(_voiceStore.Key), Is.False);
        }

        [Test]
        public void ResetToDefaultsRestoresTheVoiceSettings()
        {
            _voiceStore.Save(new VoiceSettings(false, 10));
            _view.Open(_container);

            _view.ResetToDefaults();

            Assert.That(_view.Voice, Is.EqualTo(VoiceSettings.Default));
            Assert.That(MicrophoneButton().text, Is.EqualTo("オン"));
            Assert.That(VolumeLabel().text, Is.EqualTo("100%"));
            _view.Close();
            Assert.That(_voiceStore.Load(), Is.EqualTo(VoiceSettings.Default));
        }

        [Test]
        public void RejectsVolumesOutsideTheRangeAndEditsWhileClosed()
        {
            Assert.Throws<InvalidOperationException>(() => _view.SetMicrophoneEnabled(false));
            Assert.Throws<InvalidOperationException>(() => _view.SetVoiceVolume(50));
            Assert.That(_view.Voice, Is.Null);

            _view.Open(_container);
            Assert.Throws<ArgumentOutOfRangeException>(() => _view.SetVoiceVolume(201));
            Assert.Throws<ArgumentOutOfRangeException>(() => _view.SetVoiceVolume(-1));
            Assert.That(_view.Voice, Is.EqualTo(VoiceSettings.Default));

            _view.SetVoiceVolume(200);
            _view.SetVoiceVolume(0);
            Assert.That(_view.Voice.VolumePercent, Is.EqualTo(0));
        }

        [Test]
        public void SwitchingTheLanguageKeepsTheVoiceEditsAndRenamesTheRows()
        {
            _view.Open(_container);
            _view.SetMicrophoneEnabled(false);
            _view.SetVoiceVolume(80);

            _view.SetLanguage(GameLanguage.English);

            Assert.That(LabelTexts(), Does.Contain("Voice chat"));
            Assert.That(LabelTexts(), Does.Contain("Microphone"));
            Assert.That(LabelTexts(), Does.Contain("Voice volume"));
            Assert.That(MicrophoneButton().text, Is.EqualTo("Off"));
            Assert.That(VolumeSlider().value, Is.EqualTo(80));
            Assert.That(VolumeLabel().text, Is.EqualTo("80%"));
        }

        [Test]
        public void ThePauseMenuShowsTheVoiceRowsAndClosingItSavesThem()
        {
            var hud = Hud.Create("SettingsViewVoiceTests.Hud");
            try
            {
                var pause = hud.PauseMenu;
                var settings = hud.PauseSettings;
                settings.UseSettingsView(_view);
                var root = Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath).CloneTree();
                pause.Bind(root);
                settings.Bind(root);
                pause.Open();
                settings.OpenSettings();

                Assert.That(settings.Slot.Q<Button>(SettingsView.VoiceMicrophoneButtonName), Is.Not.Null);
                Assert.That(settings.Slot.Q<SliderInt>(SettingsView.VoiceVolumeSliderName), Is.Not.Null);
                _view.SetMicrophoneEnabled(false);

                pause.Close();

                Assert.That(settings.IsSettingsOpen, Is.False);
                Assert.That(_voiceStore.Load().MicrophoneEnabled, Is.False);
            }
            finally
            {
                var panelSettings = hud.OwnedPanelSettings;
                Object.DestroyImmediate(hud.gameObject);
                if (panelSettings != null)
                {
                    Object.DestroyImmediate(panelSettings);
                }
            }
        }

        [Test]
        public void TheMainMenuShowsTheVoiceRowsAndSavesThemOnClose()
        {
            var layout = Resources.Load<VisualTreeAsset>(MainMenuScreen.LayoutResourcePath);
            var owner = new GameObject("SettingsViewVoiceTests.Menu");
            try
            {
                var screen = owner.AddComponent<MainMenuScreen>();
                screen.Bind(layout.CloneTree());
                screen.InstallSettingsView(_view);
                screen.OpenSettings();

                Assert.That(screen.SettingsSlot.Q<Button>(SettingsView.VoiceMicrophoneButtonName), Is.Not.Null);
                _view.SetVoiceVolume(120);
                screen.CloseSettings();

                Assert.That(_voiceStore.Load().VolumePercent, Is.EqualTo(120));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void EveryVoiceKeyIsInBothLanguages()
        {
            foreach (var language in new[] { GameLanguage.Japanese, GameLanguage.English })
            {
                var table = Localizer.TableOf(language);
                foreach (var key in VoiceText.Keys)
                {
                    Assert.That(table.Get(key), Is.Not.EqualTo(key), language + " " + key);
                }
            }
        }

        private Button MicrophoneButton()
        {
            var button = _container.Q<Button>(SettingsView.VoiceMicrophoneButtonName);
            Assert.That(button, Is.Not.Null);
            return button;
        }

        private SliderInt VolumeSlider()
        {
            var slider = _container.Q<SliderInt>(SettingsView.VoiceVolumeSliderName);
            Assert.That(slider, Is.Not.Null);
            return slider;
        }

        private Label VolumeLabel()
        {
            var label = _container.Q<Label>(SettingsView.VoiceVolumeValueName);
            Assert.That(label, Is.Not.Null);
            return label;
        }

        private List<string> LabelTexts()
        {
            return _container.Query<Label>().ToList().Select(label => label.text).ToList();
        }
    }
}
