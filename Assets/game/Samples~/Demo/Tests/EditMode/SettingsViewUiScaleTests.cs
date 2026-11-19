using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // 本物の設定を壊さないよう、保存先はテストごとの別のキーにする。
    public sealed class SettingsViewUiScaleTests
    {
        private readonly List<Object> _created = new List<Object>();
        private ControlSettings _appliedBefore;
        private float _volumeBefore;
        private ControlSettingsStore _store;
        private LanguagePreferenceStore _languageStore;
        private VoiceSettingsStore _voiceStore;
        private SettingsView _view;

        [SetUp]
        public void SetUp()
        {
            _appliedBefore = AppliedControlSettings.Current;
            _volumeBefore = AudioListener.volume;
            AppliedControlSettings.Apply(ControlSettings.Default);
            var suffix = Guid.NewGuid().ToString("N");
            _store = new ControlSettingsStore("Shooter.Tests.SettingsViewUiScale." + suffix);
            _languageStore = new LanguagePreferenceStore(
                "Shooter.Tests.SettingsViewUiScale.Language." + suffix, _store.Key, Localizer.Language);
            _voiceStore = new VoiceSettingsStore("Shooter.Tests.SettingsViewUiScale.Voice." + suffix);
            _view = new SettingsView(_store, languageStore: _languageStore, voiceStore: _voiceStore);
        }

        [TearDown]
        public void TearDown()
        {
            _view.Close();
            for (var i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                {
                    Object.DestroyImmediate(_created[i]);
                }
            }

            _created.Clear();
            PlayerPrefs.DeleteKey(_store.Key);
            PlayerPrefs.DeleteKey(_languageStore.Key);
            PlayerPrefs.DeleteKey(_voiceStore.Key);
            PlayerPrefs.Save();
            AppliedControlSettings.Apply(_appliedBefore);
            AudioListener.volume = _volumeBefore;
            MenuNavigator.TakeReturnReason();
            GameSignals.ResetAll();
        }

        [Test]
        public void TheScaleIsTheBaseScaleTimesTheUiScale()
        {
            Assert.That(DemoPanelSettings.BaseScale, Is.EqualTo(1f));
            Assert.That(DemoPanelSettings.ScaleFor(ControlSettings.Default), Is.EqualTo(1f));
            Assert.That(DemoPanelSettings.ScaleFor(ControlSettings.Default.WithUiScale(150)), Is.EqualTo(1.5f));
            Assert.That(DemoPanelSettings.ScaleFor(ControlSettings.Default.WithUiScale(80)), Is.EqualTo(0.8f).Within(1e-6f));
            Assert.Throws<ArgumentNullException>(() => DemoPanelSettings.ScaleFor(null));
        }

        [Test]
        public void NewPanelSettingsStartAtTheUiScaleInEffect()
        {
            AppliedControlSettings.Apply(ControlSettings.Default.WithUiScale(130));

            var settings = Track(DemoPanelSettings.Create("SettingsViewUiScaleTests", LoadTheme()));

            Assert.That(settings.scale, Is.EqualTo(1.3f).Within(1e-6f));
        }

        [Test]
        public void MovingTheSliderScalesTheOpenHudAndMenu()
        {
            var hud = CreateHud("SettingsViewUiScaleTests.Hud");
            var menu = CreateMenu("SettingsViewUiScaleTests.Menu");
            Assert.That(hud.OwnedPanelSettings.scale, Is.EqualTo(1f));
            Assert.That(menu.OwnedPanelSettings.scale, Is.EqualTo(1f));

            // 値の変わった知らせは、パネルに付いた欄からしか届かない。
            var container = new VisualElement();
            hud.GetComponent<UIDocument>().rootVisualElement.Add(container);
            _view.Open(container);
            var slider = container.Q<SliderInt>(SettingsView.SliderName(ControlValue.UiScale));
            Assert.That(slider.panel, Is.Not.Null);

            slider.value = 150;

            Assert.That(AppliedControlSettings.Current.Get(ControlValue.UiScale), Is.EqualTo(150));
            Assert.That(hud.OwnedPanelSettings.scale, Is.EqualTo(1.5f));
            Assert.That(menu.OwnedPanelSettings.scale, Is.EqualTo(1.5f));
            Assert.That(container.Q<Label>(SettingsView.ValueLabelName(ControlValue.UiScale)).text, Is.EqualTo("150%"));

            slider.value = 80;

            Assert.That(hud.OwnedPanelSettings.scale, Is.EqualTo(0.8f).Within(1e-6f));
            Assert.That(menu.OwnedPanelSettings.scale, Is.EqualTo(0.8f).Within(1e-6f));
        }

        [Test]
        public void ResettingTheSettingsPutsTheScaleBack()
        {
            var hud = CreateHud("SettingsViewUiScaleTests.HudReset");
            _view.Open(new VisualElement());
            _view.SetValue(ControlValue.UiScale, 120);
            Assert.That(hud.OwnedPanelSettings.scale, Is.EqualTo(1.2f).Within(1e-6f));

            _view.ResetToDefaults();

            Assert.That(hud.OwnedPanelSettings.scale, Is.EqualTo(1f));
        }

        [Test]
        public void OtherSettingsLeaveTheScaleAlone()
        {
            var hud = CreateHud("SettingsViewUiScaleTests.HudOther");
            _view.Open(new VisualElement());
            _view.SetValue(ControlValue.UiScale, 140);

            _view.SetValue(ControlValue.MouseSensitivity, 250);
            _view.SetShowSubtitles(true);

            Assert.That(hud.OwnedPanelSettings.scale, Is.EqualTo(1.4f).Within(1e-6f));
        }

        [Test]
        public void ADestroyedPanelSettingsIsSkippedWithoutBreakingTheOthers()
        {
            var theme = LoadTheme();
            var gone = DemoPanelSettings.Create("SettingsViewUiScaleTests.Gone", theme);
            var kept = Track(DemoPanelSettings.Create("SettingsViewUiScaleTests.Kept", theme));
            Object.DestroyImmediate(gone);

            AppliedControlSettings.Apply(ControlSettings.Default.WithUiScale(110));

            Assert.That(kept.scale, Is.EqualTo(1.1f).Within(1e-6f));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void TheUiScaleSurvivesARestart()
        {
            _view.Open(new VisualElement());
            _view.SetValue(ControlValue.UiScale, 150);
            _view.Close();

            // 起動し直したときと同じく、保存から読み直した設定で画面を作る。
            AppliedControlSettings.Apply(new ControlSettingsStore(_store.Key).Load());
            var settings = Track(DemoPanelSettings.Create("SettingsViewUiScaleTests.Restart", LoadTheme()));

            Assert.That(settings.scale, Is.EqualTo(1.5f));
        }

        private Hud CreateHud(string name)
        {
            var hud = Hud.Create(name);
            Track(hud.gameObject);
            Track(hud.OwnedPanelSettings);
            return hud;
        }

        private MainMenuScreen CreateMenu(string name)
        {
            var menuObject = Track(new GameObject(name));
            var menu = menuObject.AddComponent<MainMenuScreen>();
            menu.Show();
            Track(menu.OwnedPanelSettings);
            return menu;
        }

        private static ThemeStyleSheet LoadTheme()
        {
            var theme = Resources.Load<ThemeStyleSheet>(Hud.ThemeResourcePath);
            Assert.That(theme, Is.Not.Null, "Resources/" + Hud.ThemeResourcePath + ".tss is missing");
            return theme;
        }

        private T Track<T>(T created) where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}
