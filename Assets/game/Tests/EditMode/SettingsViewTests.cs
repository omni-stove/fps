using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// <see cref="SettingsView"/> が値を変えた直後から <see cref="AppliedControlSettings"/> で効かせ、閉じたときに保存すること。
    /// メインメニューとポーズ画面のどちらからも開けること。本物の設定を壊さないよう、保存先はテストごとの別のキーにする。
    /// </summary>
    public sealed class SettingsViewTests
    {
        private ControlSettings _appliedBefore;
        private float _volumeBefore;
        private ControlSettingsStore _store;
        private SettingsView _view;
        private VisualElement _container;

        [SetUp]
        public void SetUp()
        {
            _appliedBefore = AppliedControlSettings.Current;
            _volumeBefore = AudioListener.volume;
            AppliedControlSettings.Apply(ControlSettings.Default);
            _store = new ControlSettingsStore("Fps.Tests.SettingsView." + Guid.NewGuid().ToString("N"));
            _view = new SettingsView(_store);
            _container = new VisualElement();
        }

        [TearDown]
        public void TearDown()
        {
            _view.Close();
            PlayerPrefs.DeleteKey(_store.Key);
            PlayerPrefs.Save();
            AppliedControlSettings.Apply(_appliedBefore);
            AudioListener.volume = _volumeBefore;
        }

        [Test]
        public void TheStyleIsInResources()
        {
            Assert.That(Resources.Load<StyleSheet>(SettingsView.StyleResourcePath), Is.Not.Null);
        }

        [Test]
        public void OpenBuildsASliderForEveryValueAndAButtonForEveryMode()
        {
            _view.Open(_container);

            Assert.That(_view.IsOpen, Is.True);
            Assert.That(_view.Root.parent, Is.SameAs(_container));
            foreach (var value in ControlSettings.Values)
            {
                var slider = _container.Q<SliderInt>(SettingsView.SliderName(value));
                var range = ControlSettings.RangeOf(value);
                Assert.That(slider, Is.Not.Null, value.ToString());
                Assert.That(slider.lowValue, Is.EqualTo(range.Min), value.ToString());
                Assert.That(slider.highValue, Is.EqualTo(range.Max), value.ToString());
                Assert.That(slider.value, Is.EqualTo(range.Default), value.ToString());
                Assert.That(
                    _container.Q<Label>(SettingsView.ValueLabelName(value)).text,
                    Is.EqualTo(SettingsView.FormatValue(value, range.Default)));
            }

            foreach (var device in new[] { InputDeviceKind.KeyboardMouse, InputDeviceKind.Gamepad })
            {
                foreach (var action in new[] { HoldAction.Sprint, HoldAction.Crouch, HoldAction.Aim })
                {
                    var button = _container.Q<Button>(SettingsView.ModeButtonName(device, action));
                    Assert.That(button, Is.Not.Null, $"{device} {action}");
                    Assert.That(button.text, Is.EqualTo(SettingsView.FormatMode(ControlSettings.Default.Get(device, action))));
                }
            }

            Assert.That(_container.Q<Button>(SettingsView.BackButtonName), Is.Not.Null);
            Assert.That(_container.Q<Button>(SettingsView.ResetButtonName), Is.Not.Null);
        }

        [Test]
        public void OpenShowsTheSettingsInEffect()
        {
            AppliedControlSettings.Apply(ControlSettings.Default.With(ControlValue.MouseSensitivity, 250));

            _view.Open(_container);

            Assert.That(_container.Q<SliderInt>(SettingsView.SliderName(ControlValue.MouseSensitivity)).value, Is.EqualTo(250));
            Assert.That(_view.HasUnsavedChanges, Is.False);
        }

        [Test]
        public void ChangingAValueTakesEffectAtOnceButIsSavedOnlyOnClose()
        {
            var received = new List<ControlSettings>();
            AppliedControlSettings.Changed += received.Add;
            try
            {
                _view.Open(_container);

                _view.SetValue(ControlValue.MouseSensitivity, 300);

                Assert.That(AppliedControlSettings.Current.Get(ControlValue.MouseSensitivity), Is.EqualTo(300));
                Assert.That(received, Has.Count.EqualTo(1));
                Assert.That(received[0].LookSensitivity(InputDeviceKind.KeyboardMouse), Is.EqualTo(3f));
                Assert.That(_container.Q<SliderInt>(SettingsView.SliderName(ControlValue.MouseSensitivity)).value, Is.EqualTo(300));
                Assert.That(_container.Q<Label>(SettingsView.ValueLabelName(ControlValue.MouseSensitivity)).text, Is.EqualTo("300%"));
                Assert.That(PlayerPrefs.HasKey(_store.Key), Is.False, "dragging a slider does not write to disk");

                Assert.That(_view.Close(), Is.True);

                Assert.That(_view.IsOpen, Is.False);
                Assert.That(_container.childCount, Is.Zero);
                Assert.That(new ControlSettingsStore(_store.Key).Load().Get(ControlValue.MouseSensitivity), Is.EqualTo(300));
            }
            finally
            {
                AppliedControlSettings.Changed -= received.Add;
            }
        }

        [Test]
        public void MasterVolumeGoesToTheAudioListener()
        {
            _view.Open(_container);

            _view.SetValue(ControlValue.MasterVolume, 25);

            Assert.That(AudioListener.volume, Is.EqualTo(0.25f).Within(1e-6f));
        }

        [Test]
        public void HoldAndToggleChangePerDevice()
        {
            _view.Open(_container);

            _view.SetMode(InputDeviceKind.KeyboardMouse, HoldAction.Crouch, HoldMode.Toggle);
            _view.SetMode(InputDeviceKind.Gamepad, HoldAction.Crouch, HoldMode.Hold);

            var current = AppliedControlSettings.Current;
            Assert.That(current.Get(InputDeviceKind.KeyboardMouse, HoldAction.Crouch), Is.EqualTo(HoldMode.Toggle));
            Assert.That(current.Get(InputDeviceKind.Gamepad, HoldAction.Crouch), Is.EqualTo(HoldMode.Hold));
            Assert.That(
                _container.Q<Button>(SettingsView.ModeButtonName(InputDeviceKind.KeyboardMouse, HoldAction.Crouch)).text,
                Is.EqualTo(SettingsView.ToggleText));
            Assert.That(
                _container.Q<Button>(SettingsView.ModeButtonName(InputDeviceKind.Gamepad, HoldAction.Crouch)).text,
                Is.EqualTo(SettingsView.HoldText));

            _view.Close();

            var saved = _store.Load();
            Assert.That(saved.Get(InputDeviceKind.KeyboardMouse, HoldAction.Crouch), Is.EqualTo(HoldMode.Toggle));
            Assert.That(saved.Get(InputDeviceKind.Gamepad, HoldAction.Crouch), Is.EqualTo(HoldMode.Hold));
        }

        [Test]
        public void ResetToDefaultsRestoresEverything()
        {
            _view.Open(_container);
            _view.SetValue(ControlValue.EffectsVolume, 10);
            _view.SetMode(InputDeviceKind.Gamepad, HoldAction.Aim, HoldMode.Toggle);

            _view.ResetToDefaults();

            Assert.That(AppliedControlSettings.Current, Is.EqualTo(ControlSettings.Default));
            Assert.That(_container.Q<SliderInt>(SettingsView.SliderName(ControlValue.EffectsVolume)).value, Is.EqualTo(100));
            Assert.That(_view.HasUnsavedChanges, Is.False, "back to what was in effect when it opened");
        }

        [Test]
        public void ThePaletteButtonShowsTheStandardPaletteWithUnmarkedSamples()
        {
            _view.Open(_container);

            Assert.That(_container.Q<Button>(SettingsView.PaletteButtonName).text, Is.EqualTo(SettingsView.FormatPalette(ColorPalette.Standard)));
            var friendly = _container.Q<Label>(SettingsView.PaletteFriendlySampleName);
            var enemy = _container.Q<Label>(SettingsView.PaletteEnemySampleName);
            Assert.That(friendly.text, Is.EqualTo(SettingsView.FriendlySampleText));
            Assert.That(enemy.text, Is.EqualTo(SettingsView.EnemySampleText));
            Assert.That(friendly.style.color.value, Is.EqualTo(TeamColors.ToColor(TeamPalette.Standard.Friendly)));
            Assert.That(enemy.style.color.value, Is.EqualTo(TeamColors.ToColor(TeamPalette.Standard.Enemy)));
        }

        // 赤緑を選ぶと、味方と敵が青と橙になり、味方に ◆・敵に ▲ が付く。閉じると保存され、起動し直しても残る
        [Test]
        public void ChoosingRedGreenRecolorsAtOnceMarksTheSamplesAndIsSavedOnClose()
        {
            var received = new List<ControlSettings>();
            AppliedControlSettings.Changed += received.Add;
            try
            {
                _view.Open(_container);

                _view.SetPalette(ColorPalette.RedGreen);

                Assert.That(AppliedControlSettings.Current.Palette, Is.EqualTo(ColorPalette.RedGreen));
                Assert.That(received, Has.Count.EqualTo(1));
                Assert.That(TeamColors.Of(Team.Alpha, Team.Alpha), Is.EqualTo(TeamColors.ToColor(TeamPalette.RedGreen.Friendly)));
                Assert.That(TeamColors.Of(Team.Bravo, Team.Alpha), Is.EqualTo(TeamColors.ToColor(TeamPalette.RedGreen.Enemy)));
                Assert.That(_container.Q<Button>(SettingsView.PaletteButtonName).text, Is.EqualTo(SettingsView.FormatPalette(ColorPalette.RedGreen)));
                var friendly = _container.Q<Label>(SettingsView.PaletteFriendlySampleName);
                var enemy = _container.Q<Label>(SettingsView.PaletteEnemySampleName);
                Assert.That(friendly.text, Is.EqualTo("◆ " + SettingsView.FriendlySampleText));
                Assert.That(enemy.text, Is.EqualTo("▲ " + SettingsView.EnemySampleText));
                Assert.That(friendly.style.color.value, Is.EqualTo(TeamColors.ToColor(TeamPalette.RedGreen.Friendly)));
                Assert.That(enemy.style.color.value, Is.EqualTo(TeamColors.ToColor(TeamPalette.RedGreen.Enemy)));
                Assert.That(PlayerPrefs.HasKey(_store.Key), Is.False, "not written until it closes");

                _view.Close();

                Assert.That(new ControlSettingsStore(_store.Key).Load().Palette, Is.EqualTo(ColorPalette.RedGreen));
            }
            finally
            {
                AppliedControlSettings.Changed -= received.Add;
            }
        }

        [Test]
        public void NextPaletteCyclesThroughEveryPalette()
        {
            Assert.That(SettingsView.NextPalette(ColorPalette.Standard), Is.EqualTo(ColorPalette.RedGreen));
            Assert.That(SettingsView.NextPalette(ColorPalette.RedGreen), Is.EqualTo(ColorPalette.BlueYellow));
            Assert.That(SettingsView.NextPalette(ColorPalette.BlueYellow), Is.EqualTo(ColorPalette.Standard));
        }

        [Test]
        public void ResetToDefaultsRestoresTheStandardPalette()
        {
            _view.Open(_container);
            _view.SetPalette(ColorPalette.BlueYellow);

            _view.ResetToDefaults();

            Assert.That(AppliedControlSettings.Current.Palette, Is.EqualTo(ColorPalette.Standard));
            Assert.That(_container.Q<Button>(SettingsView.PaletteButtonName).text, Is.EqualTo(SettingsView.FormatPalette(ColorPalette.Standard)));
        }

        [Test]
        public void TheSubtitlesButtonStartsOff()
        {
            _view.Open(_container);

            Assert.That(_container.Q<Button>(SettingsView.SubtitlesButtonName).text, Is.EqualTo(SettingsView.OffText));
        }

        // 字幕を On にするとすぐ効き (HUD の字幕は Changed で切り替わる)、閉じると保存され、起動し直しても残る
        [Test]
        public void TurningSubtitlesOnAppliesAtOnceAndIsSavedOnClose()
        {
            var received = new List<ControlSettings>();
            AppliedControlSettings.Changed += received.Add;
            try
            {
                _view.Open(_container);

                _view.SetShowSubtitles(true);

                Assert.That(AppliedControlSettings.Current.ShowSubtitles, Is.True);
                Assert.That(received, Has.Count.EqualTo(1));
                Assert.That(_container.Q<Button>(SettingsView.SubtitlesButtonName).text, Is.EqualTo(SettingsView.OnText));
                Assert.That(PlayerPrefs.HasKey(_store.Key), Is.False, "not written until it closes");

                _view.Close();

                Assert.That(new ControlSettingsStore(_store.Key).Load().ShowSubtitles, Is.True);
            }
            finally
            {
                AppliedControlSettings.Changed -= received.Add;
            }
        }

        [Test]
        public void ResetToDefaultsTurnsTheSubtitlesOff()
        {
            _view.Open(_container);
            _view.SetShowSubtitles(true);

            _view.ResetToDefaults();

            Assert.That(AppliedControlSettings.Current.ShowSubtitles, Is.False);
            Assert.That(_container.Q<Button>(SettingsView.SubtitlesButtonName).text, Is.EqualTo(SettingsView.OffText));
        }

        [Test]
        public void TheNetStatsToggledWhileOpenSurviveEditsResetAndSave()
        {
            _view.Open(_container);

            // 開いている間に F3 (NetHud) で出した
            AppliedControlSettings.Apply(AppliedControlSettings.Current.WithShowNetStats(true));

            _view.SetValue(ControlValue.FieldOfView, 70);
            Assert.That(AppliedControlSettings.Current.ShowNetStats, Is.True, "an edit keeps it");

            _view.ResetToDefaults();
            Assert.That(AppliedControlSettings.Current, Is.EqualTo(ControlSettings.Default.WithShowNetStats(true)), "it is not on this screen");

            _view.SetValue(ControlValue.FieldOfView, 75);
            _view.Close();
            Assert.That(_store.Load().ShowNetStats, Is.True);
        }

        [Test]
        public void ClosingWithoutChangesDoesNotWrite()
        {
            _view.Open(_container);

            Assert.That(_view.Close(), Is.False);
            Assert.That(PlayerPrefs.HasKey(_store.Key), Is.False);
            Assert.That(_view.Close(), Is.False, "closing twice does nothing");
        }

        [Test]
        public void RejectsValuesOutsideTheRangeAndEditsWhileClosed()
        {
            Assert.Throws<InvalidOperationException>(() => _view.SetValue(ControlValue.FieldOfView, 70));
            Assert.Throws<InvalidOperationException>(() => _view.SetMode(InputDeviceKind.Gamepad, HoldAction.Aim, HoldMode.Toggle));
            Assert.Throws<InvalidOperationException>(() => _view.ResetToDefaults());
            Assert.Throws<ArgumentNullException>(() => _view.Open(null));

            _view.Open(_container);
            Assert.Throws<ArgumentOutOfRangeException>(() => _view.SetValue(ControlValue.FieldOfView, 200));
            Assert.That(AppliedControlSettings.Current.FieldOfView, Is.EqualTo(60f));
        }

        [Test]
        public void OpeningAgainSavesThePreviousEdit()
        {
            _view.Open(_container);
            _view.SetValue(ControlValue.AdsSensitivity, 150);

            var other = new VisualElement();
            _view.Open(other);

            Assert.That(_container.childCount, Is.Zero);
            Assert.That(other.childCount, Is.EqualTo(1));
            Assert.That(_store.Load().Get(ControlValue.AdsSensitivity), Is.EqualTo(150));
        }

        [Test]
        public void ValuesAndModesAreLabelledForThePlayer()
        {
            Assert.That(SettingsView.FormatValue(ControlValue.FieldOfView, 75), Is.EqualTo("75°"));
            Assert.That(SettingsView.FormatValue(ControlValue.MasterVolume, 40), Is.EqualTo("40%"));
            Assert.That(SettingsView.FormatMode(HoldMode.Hold), Is.EqualTo(SettingsView.HoldText));
            Assert.That(SettingsView.FormatMode(HoldMode.Toggle), Is.EqualTo(SettingsView.ToggleText));
        }

        [Test]
        public void AppliedSettingsNotifyOnlyOnChangeAndKeepNotifyingAfterAHandlerThrows()
        {
            var calls = 0;
            Action<ControlSettings> throwing = _ => throw new InvalidOperationException("boom");
            Action<ControlSettings> counting = _ => calls++;
            AppliedControlSettings.Changed += throwing;
            AppliedControlSettings.Changed += counting;
            try
            {
                LogAssert.Expect(LogType.Error, new Regex(@"^\[Settings\] .*boom"));
                LogAssert.Expect(LogType.Exception, new Regex("boom"));
                AppliedControlSettings.Apply(ControlSettings.Default.With(ControlValue.MasterVolume, 50));
                AppliedControlSettings.Apply(ControlSettings.Default.With(ControlValue.MasterVolume, 50));

                Assert.That(calls, Is.EqualTo(1), "the same settings again are not a change");
                Assert.Throws<ArgumentNullException>(() => AppliedControlSettings.Apply(null));
            }
            finally
            {
                AppliedControlSettings.Changed -= throwing;
                AppliedControlSettings.Changed -= counting;
            }
        }

        [Test]
        public void TheMainMenuOpensTheSettingsInItsSlotAndSavesOnClose()
        {
            var layout = Resources.Load<VisualTreeAsset>(MainMenuScreen.LayoutResourcePath);
            var owner = new GameObject("SettingsViewTests.Menu");
            try
            {
                var screen = owner.AddComponent<MainMenuScreen>();
                screen.Bind(layout.CloneTree());
                screen.InstallSettingsView(_view);
                Assert.Throws<InvalidOperationException>(() => screen.InstallSettingsView(new SettingsView(_store)));

                screen.OpenSettings();

                Assert.That(screen.IsSettingsOpen, Is.True);
                Assert.That(_view.IsOpen, Is.True);
                Assert.That(_view.Root.parent, Is.SameAs(screen.SettingsSlot));
                Assert.That(screen.Notice, Is.Null);

                _view.SetValue(ControlValue.GamepadSensitivity, 220);
                screen.CloseSettings();

                Assert.That(_view.IsOpen, Is.False);
                Assert.That(screen.SettingsSlot.childCount, Is.Zero);
                Assert.That(_store.Load().Get(ControlValue.GamepadSensitivity), Is.EqualTo(220));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void BackFromTheSettingsClosesTheMainMenuSlot()
        {
            var layout = Resources.Load<VisualTreeAsset>(MainMenuScreen.LayoutResourcePath);
            var owner = new GameObject("SettingsViewTests.MenuBack");
            try
            {
                var screen = owner.AddComponent<MainMenuScreen>();
                screen.Bind(layout.CloneTree());
                screen.InstallSettingsView(_view);
                screen.OpenSettings();
                _view.SetValue(ControlValue.EffectsVolume, 35);

                _view.RequestBack();

                Assert.That(screen.IsSettingsOpen, Is.False);
                Assert.That(_view.IsOpen, Is.False);
                Assert.That(screen.FocusIndex, Is.EqualTo(new List<string>(MainMenuScreen.ButtonNames).IndexOf(MainMenuScreen.SettingsButtonName)));
                Assert.That(_store.Load().Get(ControlValue.EffectsVolume), Is.EqualTo(35));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void ThePauseMenuOpensTheSettingsInPlaceOfItsButtons()
        {
            var hud = Hud.Create("SettingsViewTests.Hud", null, null, null);
            try
            {
                var pause = hud.PauseMenu;
                var settings = hud.PauseSettings;
                Assert.That(settings, Is.Not.Null, "Hud.AddViews attaches the pause settings");
                settings.UseSettingsView(_view);

                var root = Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath).CloneTree();
                pause.Bind(root);
                settings.Bind(root);
                Assert.That(pause.Element.Q<Button>(PauseMenu.SettingsButtonName).enabledSelf, Is.True, "the pause menu can open the settings");

                pause.Open();
                pause.OpenSettings();

                Assert.That(settings.IsSettingsOpen, Is.True);
                Assert.That(_view.Root.parent, Is.SameAs(settings.Slot));
                Assert.That(settings.Slot.style.display.value, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(settings.Panel.style.display.value, Is.EqualTo(DisplayStyle.None));

                _view.SetValue(ControlValue.MouseSensitivity, 40);
                settings.CloseSettings();

                Assert.That(settings.IsSettingsOpen, Is.False);
                Assert.That(settings.Slot.style.display.value, Is.EqualTo(DisplayStyle.None));
                Assert.That(settings.Panel.style.display.value, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(pause.IsOpen, Is.True, "going back keeps the game paused");
                Assert.That(_store.Load().Get(ControlValue.MouseSensitivity), Is.EqualTo(40));
            }
            finally
            {
                hud.PauseMenu.Close();
                var panelSettings = hud.OwnedPanelSettings;
                Object.DestroyImmediate(hud.gameObject);
                if (panelSettings != null)
                {
                    Object.DestroyImmediate(panelSettings);
                }
            }
        }

        [Test]
        public void ClosingThePauseMenuClosesAndSavesTheSettings()
        {
            var hud = Hud.Create("SettingsViewTests.HudClose", null, null, null);
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
                _view.SetMode(InputDeviceKind.KeyboardMouse, HoldAction.Sprint, HoldMode.Toggle);

                pause.Close();

                Assert.That(settings.IsSettingsOpen, Is.False);
                Assert.That(settings.Panel.style.display.value, Is.EqualTo(DisplayStyle.Flex), "the next pause starts on the buttons");
                Assert.That(_store.Load().Get(InputDeviceKind.KeyboardMouse, HoldAction.Sprint), Is.EqualTo(HoldMode.Toggle));
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
    }
}
