using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
    public sealed class SettingsViewTests
    {
        private ControlSettings _appliedBefore;
        private float _volumeBefore;
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
            _volumeBefore = AudioListener.volume;
            _languageBefore = Localizer.Language;
            AppliedControlSettings.Apply(ControlSettings.Default);
            Localizer.Apply(GameLanguage.Japanese);
            var suffix = Guid.NewGuid().ToString("N");
            _store = new ControlSettingsStore("Shooter.Tests.SettingsView." + suffix);
            _languageStore = new LanguagePreferenceStore("Shooter.Tests.SettingsView.Language." + suffix, _store.Key, GameLanguage.Japanese);
            _voiceStore = new VoiceSettingsStore("Shooter.Tests.SettingsView.Voice." + suffix);
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
            AudioListener.volume = _volumeBefore;
        }

        [Test]
        public void TheStyleIsInResources()
        {
            Assert.That(Resources.Load<StyleSheet>(SettingsView.StyleResourcePath), Is.Not.Null);
        }

        [Test]
        public void TheLayoutIsInResources()
        {
            Assert.That(
                Resources.Load<VisualTreeAsset>(SettingsView.LayoutResourcePath),
                Is.Not.Null,
                "Assets/game/Samples~/Demo/UI/Resources/Menu/Settings.uxml should be loadable from Resources");
        }

        [Test]
        public void TheLayoutHasEveryElementTheViewLooksUp()
        {
            var root = Resources.Load<VisualTreeAsset>(SettingsView.LayoutResourcePath).Instantiate().Q<VisualElement>(SettingsView.RootName);

            Assert.That(root, Is.Not.Null);
            Assert.That(root.ClassListContains("settings"), Is.True);
            Assert.That(root.Q<Label>(SettingsView.TitleName), Is.Not.Null);
            Assert.That(root.Q<ScrollView>(SettingsView.ScrollName), Is.Not.Null);
            foreach (var value in ControlSettings.Values)
            {
                Assert.That(root.Q<SliderInt>(SettingsView.SliderName(value)), Is.Not.Null, value.ToString());
                Assert.That(root.Q<Label>(SettingsView.ValueLabelName(value)), Is.Not.Null, value.ToString());
            }

            foreach (var device in new[] { InputDeviceKind.KeyboardMouse, InputDeviceKind.Gamepad })
            {
                foreach (var action in new[] { HoldAction.Sprint, HoldAction.Crouch, HoldAction.Aim })
                {
                    Assert.That(root.Q<Button>(SettingsView.ModeButtonName(device, action)), Is.Not.Null, $"{device} {action}");
                }
            }

            var buttons = new[]
            {
                SettingsView.AimAssistButtonName,
                SettingsView.InvertLookYButtonName,
                SettingsView.RumbleButtonName,
                SettingsView.GyroButtonName,
                SettingsView.GyroAimOnlyButtonName,
                SettingsView.InvertGyroYButtonName,
                SettingsView.BloodButtonName,
                SettingsView.PaletteButtonName,
                SettingsView.SubtitlesButtonName,
                SettingsView.VoiceMicrophoneButtonName,
                SettingsView.LanguageButtonName,
                SettingsView.RebindCancelButtonName,
                SettingsView.RebindUndoButtonName,
                SettingsView.RebindKeepButtonName,
                SettingsView.ResetButtonName,
                SettingsView.BindingsResetButtonName,
                SettingsView.BackButtonName,
            };
            foreach (var name in buttons)
            {
                Assert.That(root.Q<Button>(name), Is.Not.Null, name);
            }

            Assert.That(root.Q<Label>(SettingsView.PaletteFriendlySampleName), Is.Not.Null);
            Assert.That(root.Q<Label>(SettingsView.PaletteEnemySampleName), Is.Not.Null);
            Assert.That(root.Q<SliderInt>(SettingsView.GyroSensitivitySliderName), Is.Not.Null);
            Assert.That(root.Q<Label>(SettingsView.GyroSensitivityValueName), Is.Not.Null);
            Assert.That(root.Q<Label>(SettingsView.GyroUnavailableName), Is.Not.Null);
            Assert.That(root.Q<SliderInt>(SettingsView.VoiceVolumeSliderName), Is.Not.Null);
            Assert.That(root.Q<Label>(SettingsView.VoiceVolumeValueName), Is.Not.Null);
            Assert.That(root.Q<VisualElement>(SettingsView.RebindNoticeRowName), Is.Not.Null);
            Assert.That(root.Q<Label>(SettingsView.RebindNoticeName), Is.Not.Null);
            Assert.That(root.Q<VisualElement>(SettingsView.BindingsName), Is.Not.Null);
            Assert.That(root.Q<Label>(SettingsView.BindingsUnavailableName), Is.Not.Null);
        }

        [Test]
        public void OpenMovesTheStyledLayoutRootIntoTheContainer()
        {
            _view.Open(_container);

            Assert.That(_container.childCount, Is.EqualTo(1));
            Assert.That(_view.Root.name, Is.EqualTo(SettingsView.RootName));
            Assert.That(_view.Root.parent, Is.SameAs(_container));
            Assert.That(_view.Root.styleSheets.Contains(Resources.Load<StyleSheet>(SettingsView.StyleResourcePath)), Is.True, "Settings.uss comes with the root");
        }

        [Test]
        public void OpenTurnsEveryTextKeyOfTheLayoutIntoTheCurrentLanguage()
        {
            _view.Open(_container);

            _view.Root.Query<TextElement>().ForEach(element =>
                Assert.That(element.text, Does.Not.StartWith(LocalizedTexts.KeyPrefix), element.name));
            var sections = _view.Root.Query<Label>(className: "settings__section").ToList().Select(label => label.text).ToList();
            Assert.That(sections, Is.EqualTo(new[]
            {
                Localizer.Get("settings.section.viewAndSound"),
                Localizer.Get("settings.section.gamepad"),
                Localizer.Get("settings.section.gyro"),
                Localizer.Get("settings.section.effects"),
                Localizer.Get("settings.section.accessibility"),
                Localizer.Get(VoiceText.SettingsSectionKey),
                Localizer.Get("settings.section.online"),
                Localizer.Get("settings.section.privacy"),
                Localizer.Get("settings.section.language"),
                Localizer.Get("settings.section.holdModes"),
                Localizer.Get("settings.section.bindings"),
            }));
            Assert.That(sections[0], Is.EqualTo("視点と音"));
        }

        [Test]
        public void ChoosingEnglishRebuildsTheLayoutTextsInEnglish()
        {
            _view.Open(_container);

            LogAssert.Expect(LogType.Log, Localizer.LogTag + " language is now en");
            _view.SetLanguage(GameLanguage.English);

            Assert.That(_view.Root.Q<Label>(className: "settings__section").text, Is.EqualTo("View and sound"));
            _view.Root.Query<TextElement>().ForEach(element =>
                Assert.That(element.text, Does.Not.StartWith(LocalizedTexts.KeyPrefix), element.name));
        }

        [Test]
        public void TheBindingsListAndItsUnavailableNoteShowOneAtATime()
        {
            _view.Open(_container);

            var hasBindings = _view.Bindings != null;
            var list = _view.Root.Q<VisualElement>(SettingsView.BindingsName);
            var note = _view.Root.Q<Label>(SettingsView.BindingsUnavailableName);
            Assert.That(list.style.display.value, Is.EqualTo(hasBindings ? DisplayStyle.Flex : DisplayStyle.None));
            Assert.That(note.style.display.value, Is.EqualTo(hasBindings ? DisplayStyle.None : DisplayStyle.Flex));
            Assert.That(note.text, Is.EqualTo("入力の割り当てが見つからないため、変えられません。"));
            Assert.That(_view.Root.Q<Button>(SettingsView.BindingsResetButtonName).enabledSelf, Is.EqualTo(hasBindings));
            Assert.That(list.childCount, Is.GreaterThanOrEqualTo(1), "the device header row is in the layout");
        }

        [Test]
        public void TheLayoutKeepsTheAimRowsRightAfterTheSensitivities()
        {
            _view.Open(_container);

            var order = _view.Root.Query<VisualElement>().ToList();
            int IndexOf(string name) => order.IndexOf(_view.Root.Q<VisualElement>(name));

            Assert.That(IndexOf(SettingsView.SliderName(ControlValue.AdsSensitivity)), Is.LessThan(IndexOf(SettingsView.AimAssistButtonName)));
            Assert.That(IndexOf(SettingsView.AimAssistButtonName), Is.LessThan(IndexOf(SettingsView.InvertLookYButtonName)));
            Assert.That(IndexOf(SettingsView.InvertLookYButtonName), Is.LessThan(IndexOf(SettingsView.SliderName(ControlValue.FieldOfView))));
            Assert.That(IndexOf(SettingsView.SliderName(ControlValue.StickDeadzone)), Is.LessThan(IndexOf(SettingsView.RumbleButtonName)));
            Assert.That(IndexOf(SettingsView.RumbleButtonName), Is.LessThan(IndexOf(SettingsView.SliderName(ControlValue.RumbleStrength))));
            Assert.That(IndexOf(SettingsView.ScrollName), Is.LessThan(IndexOf(SettingsView.RebindNoticeRowName)), "the notice stays below the scrolled rows");
            Assert.That(IndexOf(SettingsView.RebindNoticeRowName), Is.LessThan(IndexOf(SettingsView.BackButtonName)));
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
                Assert.That(received.Count(), Is.EqualTo(1));
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
                Assert.That(received.Count(), Is.EqualTo(1));
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
                Assert.That(received.Count(), Is.EqualTo(1));
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
        public void TheEffectsSectionShowsTheShakeSlidersAndTheBloodSwitchInJapanese()
        {
            _view.Open(_container);

            var shake = _container.Q<SliderInt>(SettingsView.SliderName(ControlValue.CameraShakeIntensity));
            var sway = _container.Q<SliderInt>(SettingsView.SliderName(ControlValue.ViewmodelMotionIntensity));
            var blood = _container.Q<Button>(SettingsView.BloodButtonName);
            Assert.That(CaptionOf(shake), Is.EqualTo("画面の揺れの強さ"));
            Assert.That(CaptionOf(sway), Is.EqualTo("武器の揺れの強さ"));
            Assert.That(CaptionOf(blood), Is.EqualTo("血の表示"));
            Assert.That(shake.value, Is.EqualTo(100));
            Assert.That(_container.Q<Label>(SettingsView.ValueLabelName(ControlValue.CameraShakeIntensity)).text, Is.EqualTo("100%"));
            Assert.That(blood.text, Is.EqualTo("オン"));

            var rows = shake.parent.parent;
            var heading = rows[rows.IndexOf(shake.parent) - 1] as Label;
            Assert.That(heading, Is.Not.Null);
            Assert.That(heading.text, Is.EqualTo("演出"));
            Assert.That(rows.IndexOf(sway.parent), Is.EqualTo(rows.IndexOf(shake.parent) + 1));
            Assert.That(rows.IndexOf(blood.parent), Is.EqualTo(rows.IndexOf(shake.parent) + 2));
        }

        [Test]
        public void TheShakeSlidersAndTheBloodSwitchReachThePresentersAndAreSavedOnClose()
        {
            try
            {
                _view.Open(_container);

                _view.SetValue(ControlValue.CameraShakeIntensity, 0);
                _view.SetValue(ControlValue.ViewmodelMotionIntensity, 30);
                _view.SetShowBlood(false);

                Assert.That(CameraShake.GlobalMultiplier, Is.Zero);
                Assert.That(ViewmodelMotion.GlobalMultiplier, Is.EqualTo(0.3f).Within(1e-6f));
                Assert.That(BloodPresenter.BloodEnabled, Is.False);
                Assert.That(_container.Q<Button>(SettingsView.BloodButtonName).text, Is.EqualTo(SettingsView.OffText));
                Assert.That(_container.Q<Label>(SettingsView.ValueLabelName(ControlValue.ViewmodelMotionIntensity)).text, Is.EqualTo("30%"));
                Assert.That(PlayerPrefs.HasKey(_store.Key), Is.False, "not written until it closes");

                _view.Close();

                var saved = new ControlSettingsStore(_store.Key).Load();
                Assert.That(saved.Get(ControlValue.CameraShakeIntensity), Is.Zero);
                Assert.That(saved.Get(ControlValue.ViewmodelMotionIntensity), Is.EqualTo(30));
                Assert.That(saved.ShowBlood, Is.False);
            }
            finally
            {
                AppliedControlSettings.Apply(ControlSettings.Default);
            }
        }

        [Test]
        public void ResetToDefaultsTurnsTheEffectsBackOn()
        {
            _view.Open(_container);
            _view.SetValue(ControlValue.CameraShakeIntensity, 10);
            _view.SetShowBlood(false);

            _view.ResetToDefaults();

            Assert.That(AppliedControlSettings.Current.ShowBlood, Is.True);
            Assert.That(CameraShake.GlobalMultiplier, Is.EqualTo(1f));
            Assert.That(BloodPresenter.BloodEnabled, Is.True);
            Assert.That(_container.Q<Button>(SettingsView.BloodButtonName).text, Is.EqualTo(SettingsView.OnText));
            Assert.That(_container.Q<SliderInt>(SettingsView.SliderName(ControlValue.CameraShakeIntensity)).value, Is.EqualTo(100));
        }

        [Test]
        public void TheAimAssistSwitchStartsOnRightAfterTheAdsSensitivity()
        {
            _view.Open(_container);

            var aimAssist = _container.Q<Button>(SettingsView.AimAssistButtonName);
            var ads = _container.Q<SliderInt>(SettingsView.SliderName(ControlValue.AdsSensitivity));
            Assert.That(CaptionOf(aimAssist), Is.EqualTo("エイムアシスト (ゲームパッド)"));
            Assert.That(aimAssist.text, Is.EqualTo("オン"));
            var rows = ads.parent.parent;
            Assert.That(rows.IndexOf(aimAssist.parent), Is.EqualTo(rows.IndexOf(ads.parent) + 1));
        }

        [Test]
        public void TurningAimAssistOffAppliesAtOnceAndIsSavedOnClose()
        {
            var received = new List<ControlSettings>();
            AppliedControlSettings.Changed += received.Add;
            try
            {
                _view.Open(_container);

                _view.SetAimAssist(false);

                Assert.That(AppliedControlSettings.Current.AimAssist, Is.False);
                Assert.That(received.Count(), Is.EqualTo(1));
                Assert.That(_container.Q<Button>(SettingsView.AimAssistButtonName).text, Is.EqualTo(SettingsView.OffText));
                Assert.That(PlayerPrefs.HasKey(_store.Key), Is.False, "not written until it closes");

                _view.Close();

                Assert.That(new ControlSettingsStore(_store.Key).Load().AimAssist, Is.False);

                _view.Open(_container);
                Assert.That(_container.Q<Button>(SettingsView.AimAssistButtonName).text, Is.EqualTo(SettingsView.OffText));
            }
            finally
            {
                AppliedControlSettings.Changed -= received.Add;
            }
        }

        [Test]
        public void ResetToDefaultsTurnsTheAimAssistBackOn()
        {
            _view.Open(_container);
            _view.SetAimAssist(false);

            _view.ResetToDefaults();

            Assert.That(AppliedControlSettings.Current.AimAssist, Is.True);
            Assert.That(_container.Q<Button>(SettingsView.AimAssistButtonName).text, Is.EqualTo(SettingsView.OnText));
        }

        [Test]
        public void TheAimAssistSwitchIsInEnglishToo()
        {
            LogAssert.Expect(LogType.Log, Localizer.LogTag + " language is now en");
            Localizer.Apply(GameLanguage.English);

            _view.Open(_container);

            var aimAssist = _container.Q<Button>(SettingsView.AimAssistButtonName);
            Assert.That(CaptionOf(aimAssist), Is.EqualTo("Aim assist (gamepad)"));
            Assert.That(aimAssist.text, Is.EqualTo("On"));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void TheInvertLookYSwitchStartsOffRightAfterTheAimAssist()
        {
            _view.Open(_container);

            var invert = _container.Q<Button>(SettingsView.InvertLookYButtonName);
            var aimAssist = _container.Q<Button>(SettingsView.AimAssistButtonName);
            Assert.That(CaptionOf(invert), Is.EqualTo("視点の上下反転"));
            Assert.That(invert.text, Is.EqualTo("オフ"));
            var rows = aimAssist.parent.parent;
            Assert.That(rows.IndexOf(invert.parent), Is.EqualTo(rows.IndexOf(aimAssist.parent) + 1));
        }

        [Test]
        public void TheGamepadSectionShowsTheDeadzoneTheRumbleSwitchAndItsStrengthInJapanese()
        {
            _view.Open(_container);

            var deadzone = _container.Q<SliderInt>(SettingsView.SliderName(ControlValue.StickDeadzone));
            var rumble = _container.Q<Button>(SettingsView.RumbleButtonName);
            var strength = _container.Q<SliderInt>(SettingsView.SliderName(ControlValue.RumbleStrength));
            Assert.That(CaptionOf(deadzone), Is.EqualTo("スティックのデッドゾーン"));
            Assert.That(CaptionOf(rumble), Is.EqualTo("振動"));
            Assert.That(CaptionOf(strength), Is.EqualTo("振動の強さ"));
            Assert.That(deadzone.lowValue, Is.EqualTo(0));
            Assert.That(deadzone.highValue, Is.EqualTo(500));
            Assert.That(deadzone.value, Is.EqualTo(125));
            Assert.That(_container.Q<Label>(SettingsView.ValueLabelName(ControlValue.StickDeadzone)).text, Is.EqualTo("12.5%"));
            Assert.That(rumble.text, Is.EqualTo("オン"));
            Assert.That(strength.value, Is.EqualTo(100));
            Assert.That(strength.enabledSelf, Is.True);
            Assert.That(_container.Q<Label>(SettingsView.ValueLabelName(ControlValue.RumbleStrength)).text, Is.EqualTo("100%"));

            var rows = deadzone.parent.parent;
            var heading = rows[rows.IndexOf(deadzone.parent) - 1] as Label;
            Assert.That(heading, Is.Not.Null);
            Assert.That(heading.text, Is.EqualTo("ゲームパッド"));
            Assert.That(rows.IndexOf(rumble.parent), Is.EqualTo(rows.IndexOf(deadzone.parent) + 1));
            Assert.That(rows.IndexOf(strength.parent), Is.EqualTo(rows.IndexOf(deadzone.parent) + 2));
        }

        [Test]
        public void ChangingTheInputFeelAppliesAtOnceAndIsSavedOnClose()
        {
            var received = new List<ControlSettings>();
            AppliedControlSettings.Changed += received.Add;
            try
            {
                _view.Open(_container);

                _view.SetInvertLookY(true);
                _view.SetValue(ControlValue.StickDeadzone, 200);
                _view.SetValue(ControlValue.RumbleStrength, 40);
                _view.SetRumbleEnabled(false);

                var current = AppliedControlSettings.Current;
                Assert.That(current.InvertLookY, Is.True);
                Assert.That(current.StickDeadzone, Is.EqualTo(0.2f).Within(1e-6f));
                Assert.That(current.RumbleStrength, Is.EqualTo(0.4f).Within(1e-6f));
                Assert.That(current.RumbleEnabled, Is.False);
                Assert.That(received.Count, Is.EqualTo(4));
                Assert.That(_container.Q<Button>(SettingsView.InvertLookYButtonName).text, Is.EqualTo(SettingsView.OnText));
                Assert.That(_container.Q<Button>(SettingsView.RumbleButtonName).text, Is.EqualTo(SettingsView.OffText));
                Assert.That(_container.Q<Label>(SettingsView.ValueLabelName(ControlValue.StickDeadzone)).text, Is.EqualTo("20%"));
                Assert.That(
                    _container.Q<SliderInt>(SettingsView.SliderName(ControlValue.RumbleStrength)).enabledSelf,
                    Is.False,
                    "the strength does nothing while the rumble is off");
                Assert.That(PlayerPrefs.HasKey(_store.Key), Is.False, "not written until it closes");

                _view.Close();

                var saved = new ControlSettingsStore(_store.Key).Load();
                Assert.That(saved.InvertLookY, Is.True);
                Assert.That(saved.Get(ControlValue.StickDeadzone), Is.EqualTo(200));
                Assert.That(saved.RumbleEnabled, Is.False);
                Assert.That(saved.Get(ControlValue.RumbleStrength), Is.EqualTo(40));

                _view.Open(_container);
                Assert.That(_container.Q<Button>(SettingsView.InvertLookYButtonName).text, Is.EqualTo(SettingsView.OnText));
                Assert.That(_container.Q<Button>(SettingsView.RumbleButtonName).text, Is.EqualTo(SettingsView.OffText));
                Assert.That(_container.Q<SliderInt>(SettingsView.SliderName(ControlValue.StickDeadzone)).value, Is.EqualTo(200));
                Assert.That(_container.Q<SliderInt>(SettingsView.SliderName(ControlValue.RumbleStrength)).value, Is.EqualTo(40));
                Assert.That(_container.Q<SliderInt>(SettingsView.SliderName(ControlValue.RumbleStrength)).enabledSelf, Is.False);
            }
            finally
            {
                AppliedControlSettings.Changed -= received.Add;
            }
        }

        [Test]
        public void TurningTheRumbleBackOnLetsTheStrengthBeChangedAgain()
        {
            _view.Open(_container);
            _view.SetRumbleEnabled(false);

            _view.SetRumbleEnabled(true);

            Assert.That(_container.Q<SliderInt>(SettingsView.SliderName(ControlValue.RumbleStrength)).enabledSelf, Is.True);
            Assert.That(_container.Q<Button>(SettingsView.RumbleButtonName).text, Is.EqualTo(SettingsView.OnText));
        }

        [Test]
        public void ResetToDefaultsRestoresTheInputFeel()
        {
            _view.Open(_container);
            _view.SetInvertLookY(true);
            _view.SetValue(ControlValue.StickDeadzone, 0);
            _view.SetRumbleEnabled(false);
            _view.SetValue(ControlValue.RumbleStrength, 10);

            _view.ResetToDefaults();

            var current = AppliedControlSettings.Current;
            Assert.That(current.InvertLookY, Is.False);
            Assert.That(current.Get(ControlValue.StickDeadzone), Is.EqualTo(125));
            Assert.That(current.RumbleEnabled, Is.True);
            Assert.That(current.Get(ControlValue.RumbleStrength), Is.EqualTo(100));
            Assert.That(_container.Q<Button>(SettingsView.InvertLookYButtonName).text, Is.EqualTo(SettingsView.OffText));
            Assert.That(_container.Q<Button>(SettingsView.RumbleButtonName).text, Is.EqualTo(SettingsView.OnText));
            Assert.That(_container.Q<SliderInt>(SettingsView.SliderName(ControlValue.StickDeadzone)).value, Is.EqualTo(125));
        }

        [Test]
        public void TheInputFeelRowsAreInEnglishToo()
        {
            LogAssert.Expect(LogType.Log, Localizer.LogTag + " language is now en");
            Localizer.Apply(GameLanguage.English);

            _view.Open(_container);

            var deadzone = _container.Q<SliderInt>(SettingsView.SliderName(ControlValue.StickDeadzone));
            var rows = deadzone.parent.parent;
            Assert.That(((Label)rows[rows.IndexOf(deadzone.parent) - 1]).text, Is.EqualTo("Gamepad"));
            Assert.That(CaptionOf(_container.Q<Button>(SettingsView.InvertLookYButtonName)), Is.EqualTo("Invert look Y"));
            Assert.That(_container.Q<Button>(SettingsView.InvertLookYButtonName).text, Is.EqualTo("Off"));
            Assert.That(CaptionOf(deadzone), Is.EqualTo("Stick deadzone"));
            Assert.That(CaptionOf(_container.Q<Button>(SettingsView.RumbleButtonName)), Is.EqualTo("Vibration"));
            Assert.That(_container.Q<Button>(SettingsView.RumbleButtonName).text, Is.EqualTo("On"));
            Assert.That(
                CaptionOf(_container.Q<SliderInt>(SettingsView.SliderName(ControlValue.RumbleStrength))),
                Is.EqualTo("Vibration strength"));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void TheUiScaleRowIsInTheAccessibilitySectionInBothLanguages()
        {
            _view.Open(_container);

            var order = _view.Root.Query<VisualElement>().ToList();
            var slider = _container.Q<SliderInt>(SettingsView.SliderName(ControlValue.UiScale));
            var sections = _view.Root.Query<Label>(className: "settings__section").ToList();
            var accessibility = sections.Single(label => label.text == Localizer.Get("settings.section.accessibility"));
            var next = sections[sections.IndexOf(accessibility) + 1];
            Assert.That(order.IndexOf(slider), Is.GreaterThan(order.IndexOf(accessibility)));
            Assert.That(order.IndexOf(slider), Is.LessThan(order.IndexOf(next)));
            Assert.That(order.IndexOf(slider), Is.GreaterThan(order.IndexOf(_container.Q<Button>(SettingsView.SubtitlesButtonName))));
            Assert.That(CaptionOf(slider), Is.EqualTo("UI の拡大"));
            Assert.That(slider.lowValue, Is.EqualTo(80));
            Assert.That(slider.highValue, Is.EqualTo(150));
            Assert.That(slider.value, Is.EqualTo(100));
            Assert.That(_container.Q<Label>(SettingsView.ValueLabelName(ControlValue.UiScale)).text, Is.EqualTo("100%"));

            LogAssert.Expect(LogType.Log, Localizer.LogTag + " language is now en");
            _view.SetLanguage(GameLanguage.English);

            Assert.That(CaptionOf(_view.Root.Q<SliderInt>(SettingsView.SliderName(ControlValue.UiScale))), Is.EqualTo("UI scale"));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ChangingTheUiScaleTakesEffectAtOnceAndIsSavedOnClose()
        {
            var received = new List<ControlSettings>();
            AppliedControlSettings.Changed += received.Add;
            try
            {
                _view.Open(_container);

                _view.SetValue(ControlValue.UiScale, 150);

                Assert.That(AppliedControlSettings.Current.UiScale, Is.EqualTo(1.5f));
                Assert.That(received, Has.Count.EqualTo(1));
                Assert.That(received[0].Get(ControlValue.UiScale), Is.EqualTo(150));
                Assert.That(_container.Q<Label>(SettingsView.ValueLabelName(ControlValue.UiScale)).text, Is.EqualTo("150%"));
                Assert.That(PlayerPrefs.HasKey(_store.Key), Is.False);

                Assert.That(_view.Close(), Is.True);

                Assert.That(new ControlSettingsStore(_store.Key).Load().Get(ControlValue.UiScale), Is.EqualTo(150));
            }
            finally
            {
                AppliedControlSettings.Changed -= received.Add;
            }
        }

        private static string CaptionOf(VisualElement control)
        {
            Assert.That(control, Is.Not.Null);
            return ((Label)control.parent[0]).text;
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
            Assert.That(SettingsView.FormatValue(ControlValue.StickDeadzone, 125), Is.EqualTo("12.5%"));
            Assert.That(SettingsView.FormatValue(ControlValue.StickDeadzone, 80), Is.EqualTo("8%"));
            Assert.That(SettingsView.FormatValue(ControlValue.StickDeadzone, 0), Is.EqualTo("0%"));
            Assert.That(SettingsView.FormatValue(ControlValue.StickDeadzone, 500), Is.EqualTo("50%"));
            Assert.That(SettingsView.FormatValue(ControlValue.RumbleStrength, 70), Is.EqualTo("70%"));
            Assert.That(SettingsView.FormatValue(ControlValue.UiScale, 150), Is.EqualTo("150%"));
            Assert.That(SettingsView.FormatMode(HoldMode.Hold), Is.EqualTo(SettingsView.HoldText));
            Assert.That(SettingsView.FormatMode(HoldMode.Toggle), Is.EqualTo(SettingsView.ToggleText));
        }

        [Test]
        public void TheScreenIsInTheCurrentLanguage()
        {
            LogAssert.Expect(LogType.Log, Localizer.LogTag + " language is now en");
            Localizer.Apply(GameLanguage.English);

            _view.Open(_container);

            Assert.That(_container.Q<Label>(SettingsView.TitleName).text, Is.EqualTo("Settings"));
            Assert.That(_container.Q<Button>(SettingsView.LanguageButtonName).text, Is.EqualTo("English"));
            Assert.That(_container.Q<Button>(SettingsView.BackButtonName).text, Is.EqualTo("Back"));
            Assert.That(_container.Q<Button>(SettingsView.SubtitlesButtonName).text, Is.EqualTo("Off"));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ChoosingEnglishTurnsTheOpenScreenEnglishAtOnceAndSavesItOnClose()
        {
            _view.Open(_container);
            Assert.That(_container.Q<Label>(SettingsView.TitleName).text, Is.EqualTo("設定"));
            Assert.That(_container.Q<Button>(SettingsView.LanguageButtonName).text, Is.EqualTo("日本語"));
            _view.SetValue(ControlValue.FieldOfView, 70);
            var changes = new List<GameLanguage>();
            Action<GameLanguage> record = changes.Add;
            Localizer.Changed += record;
            try
            {
                LogAssert.Expect(LogType.Log, Localizer.LogTag + " language is now en");
                _view.SetLanguage(SettingsView.NextLanguage(GameLanguage.Japanese, _view.Languages));

                Assert.That(changes, Is.EqualTo(new[] { GameLanguage.English }));
            }
            finally
            {
                Localizer.Changed -= record;
            }

            Assert.That(Localizer.Language, Is.EqualTo(GameLanguage.English));
            Assert.That(_view.Root.parent, Is.SameAs(_container));
            Assert.That(_container.childCount, Is.EqualTo(1), "the old screen is gone");
            Assert.That(_container.Q<Label>(SettingsView.TitleName).text, Is.EqualTo("Settings"));
            Assert.That(_container.Q<Button>(SettingsView.LanguageButtonName).text, Is.EqualTo("English"));
            Assert.That(_container.Q<Button>(SettingsView.ResetButtonName).text, Is.EqualTo("Restore defaults"));
            Assert.That(
                _container.Q<Button>(SettingsView.ModeButtonName(InputDeviceKind.Gamepad, HoldAction.Sprint)).text,
                Is.EqualTo("Toggle"));
            Assert.That(_container.Q<SliderInt>(SettingsView.SliderName(ControlValue.FieldOfView)).value, Is.EqualTo(70), "the edit survives");

            _view.SetShowSubtitles(true);
            Assert.That(_container.Q<Button>(SettingsView.SubtitlesButtonName).text, Is.EqualTo("On"));

            LogAssert.Expect(LogType.Log, new Regex("^" + Regex.Escape(SettingsView.LogTag) + " saved "));
            _view.Close();

            // 起動し直したときと同じく、OS の言語が日本語でも保存した言語で読む
            Assert.That(new LanguagePreferenceStore(_languageStore.Key, null, GameLanguage.Japanese).Load(), Is.EqualTo(GameLanguage.English));
            Assert.That(new ControlSettingsStore(_store.Key).Load().Get(ControlValue.FieldOfView), Is.EqualTo(70));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void TheLanguageItemOffersTheLanguagesInResourcesByTheirOwnNames()
        {
            _view.Open(_container);

            Assert.That(_view.Languages, Is.EqualTo(Localizer.AvailableLanguages()));
            Assert.That(_view.Languages, Does.Contain(GameLanguage.Japanese));
            var button = _container.Q<Button>(SettingsView.LanguageButtonName);
            Assert.That(button.text, Is.EqualTo("日本語"));
            Assert.That(button.enabledSelf, Is.EqualTo(_view.Languages.Count > 1));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ChoosingALanguageSavesItAtOnce()
        {
            _view.Open(_container);

            _view.SetLanguage(GameLanguage.English);

            Assert.That(new LanguagePreferenceStore(_languageStore.Key, null, GameLanguage.Japanese).Load(), Is.EqualTo(GameLanguage.English), "kept across a restart");
            Assert.That(PlayerPrefs.HasKey(_store.Key), Is.False, "the control settings are not saved for a language change");
            Assert.That(_view.HasUnsavedChanges, Is.False);
            Assert.That(_container.Q<Button>(SettingsView.LanguageButtonName).text, Is.EqualTo("English"));
            Assert.That(_container.Q<Label>(SettingsView.TitleName).text, Is.EqualTo("Settings"));
        }

        [Test]
        public void NextLanguageCyclesThroughTheAvailableLanguages()
        {
            var both = new[] { GameLanguage.Japanese, GameLanguage.English };
            var japaneseOnly = new[] { GameLanguage.Japanese };

            Assert.That(SettingsView.NextLanguage(GameLanguage.Japanese, both), Is.EqualTo(GameLanguage.English));
            Assert.That(SettingsView.NextLanguage(GameLanguage.English, both), Is.EqualTo(GameLanguage.Japanese));
            Assert.That(SettingsView.NextLanguage(GameLanguage.Japanese, japaneseOnly), Is.EqualTo(GameLanguage.Japanese));
            Assert.That(SettingsView.NextLanguage(GameLanguage.English, japaneseOnly), Is.EqualTo(GameLanguage.Japanese), "a language without strings moves to one with");
            Assert.That(SettingsView.NextLanguage(GameLanguage.English, Array.Empty<GameLanguage>()), Is.EqualTo(GameLanguage.English));
            Assert.Throws<ArgumentNullException>(() => SettingsView.NextLanguage(GameLanguage.Japanese, null));
        }

        [Test]
        public void ChoosingTheSameLanguageAgainDoesNotRebuildTheScreen()
        {
            _view.Open(_container);
            var root = _view.Root;
            var calls = 0;
            Action<GameLanguage> count = _ => calls++;
            Localizer.Changed += count;
            try
            {
                _view.SetLanguage(GameLanguage.Japanese);
            }
            finally
            {
                Localizer.Changed -= count;
            }

            Assert.That(calls, Is.Zero);
            Assert.That(_view.Root, Is.SameAs(root));
            Assert.That(_view.HasUnsavedChanges, Is.False);
        }

        [Test]
        public void ResetToDefaultsKeepsTheLanguage()
        {
            _view.Open(_container);
            _view.SetLanguage(GameLanguage.English);
            _view.SetValue(ControlValue.MasterVolume, 20);

            _view.ResetToDefaults();

            Assert.That(AppliedControlSettings.Current, Is.EqualTo(ControlSettings.Default));
            Assert.That(Localizer.Language, Is.EqualTo(GameLanguage.English));
            Assert.That(_container.Q<Label>(SettingsView.TitleName).text, Is.EqualTo("Settings"));
        }

        [Test]
        public void AClosedViewIgnoresLanguageChanges()
        {
            _view.Open(_container);
            _view.Close();

            Localizer.Apply(GameLanguage.English);

            Assert.That(_view.IsOpen, Is.False);
            Assert.That(_container.childCount, Is.Zero);
        }

        [Test]
        public void SetLanguageNeedsAnOpenView()
        {
            Assert.Throws<InvalidOperationException>(() => _view.SetLanguage(GameLanguage.English));
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
            var hud = Hud.Create("SettingsViewTests.Hud");
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
                Assert.That(settings.Slot.Q<Button>(SettingsView.InvertLookYButtonName), Is.Not.Null);
                Assert.That(settings.Slot.Q<SliderInt>(SettingsView.SliderName(ControlValue.StickDeadzone)), Is.Not.Null);
                Assert.That(settings.Slot.Q<Button>(SettingsView.RumbleButtonName), Is.Not.Null);
                Assert.That(settings.Slot.Q<SliderInt>(SettingsView.SliderName(ControlValue.RumbleStrength)), Is.Not.Null);

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
            var hud = Hud.Create("SettingsViewTests.HudClose");
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
