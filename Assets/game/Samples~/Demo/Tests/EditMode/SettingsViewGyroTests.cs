using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using InputGyroscope = UnityEngine.InputSystem.Gyroscope;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // 本物の設定を壊さないよう、保存先はテストごとの別のキーにする。
    // テストを走らせる機器にジャイロがあるかで結果が変わらないよう、有無は渡して決める。
    public sealed class SettingsViewGyroTests
    {
        private ControlSettings _appliedBefore;
        private GameLanguage _languageBefore;
        private ControlSettingsStore _store;
        private LanguagePreferenceStore _languageStore;
        private VoiceSettingsStore _voiceStore;
        private bool _gyroPresent;
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
            _store = new ControlSettingsStore("Shooter.Tests.SettingsViewGyro." + suffix);
            _languageStore = new LanguagePreferenceStore("Shooter.Tests.SettingsViewGyro.Language." + suffix, _store.Key, GameLanguage.Japanese);
            _voiceStore = new VoiceSettingsStore("Shooter.Tests.SettingsViewGyro.Voice." + suffix);
            _gyroPresent = true;
            _view = NewView();
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
        public void TheGyroRowsStartOffAt100PercentInTheirOwnSectionAfterTheGamepad()
        {
            _view.Open(_container);

            Assert.That(CaptionOf(Gyro), Is.EqualTo("ジャイロで視点を動かす"));
            Assert.That(Gyro.text, Is.EqualTo("オフ"));
            Assert.That(CaptionOf(Sensitivity), Is.EqualTo("ジャイロ感度"));
            Assert.That(Sensitivity.lowValue, Is.EqualTo(10));
            Assert.That(Sensitivity.highValue, Is.EqualTo(500));
            Assert.That(Sensitivity.value, Is.EqualTo(100));
            Assert.That(SensitivityLabel.text, Is.EqualTo("100%"));
            Assert.That(CaptionOf(AimOnly), Is.EqualTo("覗き込み (ADS) 中だけジャイロを使う"));
            Assert.That(AimOnly.text, Is.EqualTo("オフ"));
            Assert.That(CaptionOf(InvertY), Is.EqualTo("ジャイロの上下反転"));
            Assert.That(InvertY.text, Is.EqualTo("オフ"));
            Assert.That(Unavailable.style.display.value, Is.EqualTo(DisplayStyle.None));

            var order = _view.Root.Query<VisualElement>().ToList();
            int IndexOf(VisualElement element) => order.IndexOf(element);
            var heading = _view.Root.Query<Label>(className: "settings__section").Where(label => label.text == "ジャイロ").First();
            Assert.That(heading, Is.Not.Null);
            Assert.That(IndexOf(_view.Root.Q<SliderInt>(SettingsView.SliderName(ControlValue.RumbleStrength))), Is.LessThan(IndexOf(heading)));
            Assert.That(IndexOf(heading), Is.LessThan(IndexOf(Unavailable)));
            Assert.That(IndexOf(Unavailable), Is.LessThan(IndexOf(Gyro)));
            Assert.That(IndexOf(Gyro), Is.LessThan(IndexOf(Sensitivity)));
            Assert.That(IndexOf(Sensitivity), Is.LessThan(IndexOf(AimOnly)));
            Assert.That(IndexOf(AimOnly), Is.LessThan(IndexOf(InvertY)));
        }

        [Test]
        public void TheGyroRowsAreInEnglishToo()
        {
            LogAssert.Expect(LogType.Log, Localizer.LogTag + " language is now en");
            Localizer.Apply(GameLanguage.English);
            _gyroPresent = false;

            _view.Open(_container);

            Assert.That(CaptionOf(Gyro), Is.EqualTo("Gyro aiming"));
            Assert.That(Gyro.text, Is.EqualTo("Off"));
            Assert.That(CaptionOf(Sensitivity), Is.EqualTo("Gyro sensitivity"));
            Assert.That(CaptionOf(AimOnly), Is.EqualTo("Gyro only while aiming (ADS)"));
            Assert.That(CaptionOf(InvertY), Is.EqualTo("Invert gyro Y"));
            Assert.That(Unavailable.text, Is.EqualTo("No gyro was found on this device, so these cannot be changed."));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void EveryGyroKeyIsInEveryLanguage()
        {
            var keys = new[]
            {
                "settings.section.gyro",
                "settings.gyro",
                "settings.value.gyroSensitivity",
                "settings.gyroAimOnly",
                "settings.invertGyroY",
                "settings.gyro.unavailable",
            };
            foreach (var language in new[] { GameLanguage.Japanese, GameLanguage.English })
            {
                var table = Localizer.TableOf(language);
                foreach (var key in keys)
                {
                    Assert.That(table.Contains(key), Is.True, $"{language} {key}");
                }
            }
        }

        [Test]
        public void TheGyroRowsGoIntoTheAppliedControlSettingsAndAreKeptAfterARestart()
        {
            var received = new List<ControlSettings>();
            AppliedControlSettings.Changed += received.Add;
            try
            {
                _view.Open(_container);

                PressGyro();
                _view.SetGyroSensitivity(250);
                PressAimOnly();
                PressInvertY();

                var applied = AppliedControlSettings.Current;
                Assert.That(applied.GyroEnabled, Is.True);
                Assert.That(applied.GyroSensitivityPercent, Is.EqualTo(250));
                Assert.That(applied.GyroAimOnly, Is.True);
                Assert.That(applied.InvertGyroY, Is.True);
                Assert.That(received.Count, Is.EqualTo(4));
                Assert.That(Gyro.text, Is.EqualTo("オン"));
                Assert.That(Sensitivity.value, Is.EqualTo(250));
                Assert.That(SensitivityLabel.text, Is.EqualTo("250%"));
                Assert.That(AimOnly.text, Is.EqualTo("オン"));
                Assert.That(InvertY.text, Is.EqualTo("オン"));
                Assert.That(PlayerPrefs.HasKey(_store.Key), Is.False, "not written until it closes");

                Assert.That(_view.Close(), Is.True);

                // 起動し直したときと同じく、新しいストアで読む。
                var saved = new ControlSettingsStore(_store.Key).Load();
                Assert.That(saved.GyroEnabled, Is.True);
                Assert.That(saved.GyroSensitivityPercent, Is.EqualTo(250));
                Assert.That(saved.GyroAimOnly, Is.True);
                Assert.That(saved.InvertGyroY, Is.True);

                AppliedControlSettings.Apply(saved);
                _view.Open(_container);
                Assert.That(Gyro.text, Is.EqualTo("オン"));
                Assert.That(Sensitivity.value, Is.EqualTo(250));
                Assert.That(SensitivityLabel.text, Is.EqualTo("250%"));
            }
            finally
            {
                AppliedControlSettings.Changed -= received.Add;
            }
        }

        [Test]
        public void TheOtherGyroRowsCanOnlyBeChangedWhileTheGyroIsOn()
        {
            _view.Open(_container);

            Assert.That(Gyro.enabledSelf, Is.True);
            Assert.That(Sensitivity.enabledSelf, Is.False);
            Assert.That(AimOnly.enabledSelf, Is.False);
            Assert.That(InvertY.enabledSelf, Is.False);

            PressGyro();

            Assert.That(Sensitivity.enabledSelf, Is.True);
            Assert.That(AimOnly.enabledSelf, Is.True);
            Assert.That(InvertY.enabledSelf, Is.True);
        }

        [Test]
        public void WithoutAGyroEveryGyroRowIsDisabledAndTheReasonIsShown()
        {
            _gyroPresent = false;
            AppliedControlSettings.Apply(ControlSettings.Default.WithGyroEnabled(true).WithGyroSensitivity(150));

            _view.Open(_container);

            Assert.That(_view.GyroAvailable, Is.False);
            Assert.That(Gyro.enabledSelf, Is.False);
            Assert.That(Sensitivity.enabledSelf, Is.False);
            Assert.That(AimOnly.enabledSelf, Is.False);
            Assert.That(InvertY.enabledSelf, Is.False);
            Assert.That(Unavailable.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(Unavailable.text, Is.EqualTo("この機器にはジャイロが見つからないため、変えられません。"));
            Assert.That(Gyro.text, Is.EqualTo("オン"), "the saved values stay as they are for a device with a gyro");
            Assert.That(SensitivityLabel.text, Is.EqualTo("150%"));
            Assert.That(_view.HasUnsavedChanges, Is.False);
        }

        [Test]
        public void ConnectingAGyroWhileOpenEnablesTheRowsAndRemovingItDisablesThemAgain()
        {
            _gyroPresent = false;
            _view.Open(_container);
            Assert.That(Gyro.enabledSelf, Is.False);

            _gyroPresent = true;
            var device = InputSystem.AddDevice<InputGyroscope>();
            try
            {
                Assert.That(Gyro.enabledSelf, Is.True);
                Assert.That(Unavailable.style.display.value, Is.EqualTo(DisplayStyle.None));

                _gyroPresent = false;
                InputSystem.RemoveDevice(device);

                Assert.That(Gyro.enabledSelf, Is.False);
                Assert.That(Unavailable.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            }
            finally
            {
                if (device.added)
                {
                    InputSystem.RemoveDevice(device);
                }
            }
        }

        [Test]
        public void TheDefaultCheckLooksAtTheCurrentInputSystemGyro()
        {
            var device = InputSystem.AddDevice<InputGyroscope>();
            try
            {
                Assert.That(SettingsView.HasGyroDevice(), Is.True);
                Assert.That(new SettingsView(_store).GyroAvailable, Is.True);
            }
            finally
            {
                InputSystem.RemoveDevice(device);
            }
        }

        [Test]
        public void ResetToDefaultsTurnsTheGyroOffAt100Percent()
        {
            _view.Open(_container);
            PressGyro();
            _view.SetGyroSensitivity(40);
            PressAimOnly();
            PressInvertY();

            _view.ResetToDefaults();

            var applied = AppliedControlSettings.Current;
            Assert.That(applied.GyroEnabled, Is.False);
            Assert.That(applied.GyroSensitivityPercent, Is.EqualTo(100));
            Assert.That(applied.GyroAimOnly, Is.False);
            Assert.That(applied.InvertGyroY, Is.False);
            Assert.That(Gyro.text, Is.EqualTo("オフ"));
            Assert.That(SensitivityLabel.text, Is.EqualTo("100%"));
        }

        [Test]
        public void SettingTheGyroNeedsAnOpenViewAndASensitivityInRange()
        {
            Assert.Throws<InvalidOperationException>(() => _view.SetGyroEnabled(true));
            Assert.Throws<InvalidOperationException>(() => _view.SetGyroSensitivity(100));
            Assert.Throws<InvalidOperationException>(() => _view.SetGyroAimOnly(true));
            Assert.Throws<InvalidOperationException>(() => _view.SetInvertGyroY(true));

            _view.Open(_container);
            Assert.Throws<ArgumentOutOfRangeException>(() => _view.SetGyroSensitivity(9));
            Assert.Throws<ArgumentOutOfRangeException>(() => _view.SetGyroSensitivity(501));
            Assert.That(AppliedControlSettings.Current.GyroSensitivityPercent, Is.EqualTo(100));
        }

        [Test]
        public void TheGyroRowsKeepTheirValuesWhenTheLanguageChanges()
        {
            _view.Open(_container);
            PressGyro();
            _view.SetGyroSensitivity(220);

            LogAssert.Expect(LogType.Log, Localizer.LogTag + " language is now en");
            _view.SetLanguage(GameLanguage.English);

            Assert.That(Gyro.text, Is.EqualTo("On"));
            Assert.That(Sensitivity.value, Is.EqualTo(220));
            Assert.That(SensitivityLabel.text, Is.EqualTo("220%"));
            Assert.That(Sensitivity.enabledSelf, Is.True);
        }

        [Test]
        public void FormatGyroSensitivityIsAPercent()
        {
            Assert.That(SettingsView.FormatGyroSensitivity(10), Is.EqualTo("10%"));
            Assert.That(SettingsView.FormatGyroSensitivity(500), Is.EqualTo("500%"));
        }

        [Test]
        public void ThePauseSettingsShowTheGyroRowsAndSaveThemOnClose()
        {
            var hud = Hud.Create("SettingsViewGyroTests.Hud");
            try
            {
                var pause = hud.PauseMenu;
                var settings = hud.PauseSettings;
                settings.UseSettingsView(_view);
                var root = Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath).CloneTree();
                pause.Bind(root);
                settings.Bind(root);

                pause.Open();
                pause.OpenSettings();

                Assert.That(settings.Slot.Q<Button>(SettingsView.GyroButtonName), Is.Not.Null);
                Assert.That(settings.Slot.Q<SliderInt>(SettingsView.GyroSensitivitySliderName), Is.Not.Null);
                Assert.That(settings.Slot.Q<Button>(SettingsView.GyroAimOnlyButtonName), Is.Not.Null);
                Assert.That(settings.Slot.Q<Button>(SettingsView.InvertGyroYButtonName), Is.Not.Null);

                PressGyro();
                _view.SetGyroSensitivity(180);
                Assert.That(AppliedControlSettings.Current.GyroEnabled, Is.True, "it works as soon as the game resumes");
                settings.CloseSettings();

                var saved = _store.Load();
                Assert.That(saved.GyroEnabled, Is.True);
                Assert.That(saved.GyroSensitivityPercent, Is.EqualTo(180));
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

        private Button Gyro => _container.Q<Button>(SettingsView.GyroButtonName);

        private SliderInt Sensitivity => _container.Q<SliderInt>(SettingsView.GyroSensitivitySliderName);

        private Label SensitivityLabel => _container.Q<Label>(SettingsView.GyroSensitivityValueName);

        private Button AimOnly => _container.Q<Button>(SettingsView.GyroAimOnlyButtonName);

        private Button InvertY => _container.Q<Button>(SettingsView.InvertGyroYButtonName);

        private Label Unavailable => _container.Q<Label>(SettingsView.GyroUnavailableName);

        private SettingsView NewView()
        {
            return new SettingsView(_store, languageStore: _languageStore, voiceStore: _voiceStore, gyroAvailable: () => _gyroPresent);
        }

        // ボタンはパネルに付いておらずクリックのイベントが届かないので、ボタンが呼ぶのと同じ操作をする。
        private void PressGyro()
        {
            _view.SetGyroEnabled(!_view.Settings.GyroEnabled);
        }

        private void PressAimOnly()
        {
            _view.SetGyroAimOnly(!_view.Settings.GyroAimOnly);
        }

        private void PressInvertY()
        {
            _view.SetInvertGyroY(!_view.Settings.InvertGyroY);
        }

        private static string CaptionOf(VisualElement control)
        {
            Assert.That(control, Is.Not.Null);
            return ((Label)control.parent[0]).text;
        }
    }
}
