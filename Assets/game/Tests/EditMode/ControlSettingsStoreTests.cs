using System;
using System.Text.RegularExpressions;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Shooter.Tests.EditMode
{
    public sealed class ControlSettingsStoreTests
    {
        private const string DefaultGyro = ",\"gyro\":false,\"gyroSensitivity\":100,\"gyroAimOnly\":false,\"invertGyroY\":false";

        private ControlSettingsStore _store;

        [SetUp]
        public void SetUp()
        {
            // 本物の設定 (DefaultKey) を壊さないよう、テストごとに別のキーを使う。
            _store = new ControlSettingsStore("Shooter.Tests.ControlSettings." + Guid.NewGuid().ToString("N"));
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
            Assert.That(_store.Load(), Is.EqualTo(_store.Defaults));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void TheDefaultsAreTheKitDefaults()
        {
            Assert.That(_store.Defaults, Is.EqualTo(ControlSettings.Default));
        }

        [Test]
        public void VersionFiveDataLoadsSilently()
        {
            var versionFive = OldVersion(WithoutPresentation(ControlSettings.Default.WithShowSubtitles(true).ToJson()), 5, null);
            PlayerPrefs.SetString(_store.Key, versionFive);

            var loaded = _store.Load();

            Assert.That(loaded, Is.EqualTo(ControlSettings.Default.WithShowSubtitles(true)));
            LogAssert.NoUnexpectedReceived();
        }

        [TestCase("ja")]
        [TestCase("fr")]
        public void TheLanguageOfVersionSevenDataIsSkippedSilentlyAndDroppedOnTheNextSave(string code)
        {
            // 言語を game 側に移す前のゲームが書いた形式。言語の読み移しは game 側 (サンプルの LanguagePreferenceStore) がする。
            var saved = ControlSettings.Default.WithPalette(ColorPalette.RedGreen).With(ControlValue.MasterVolume, 30);
            PlayerPrefs.SetString(_store.Key, OldVersion(saved.ToJson(), 7, code));

            var loaded = _store.Load();
            _store.Save(loaded);

            Assert.That(loaded, Is.EqualTo(saved), "every other value is kept");
            Assert.That(PlayerPrefs.GetString(_store.Key), Does.Contain("\"version\":12").And.Not.Contain("language"));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ALanguageInTheCurrentVersionIsAnUnknownKey()
        {
            PlayerPrefs.SetString(_store.Key, OldVersion(ControlSettings.Default.ToJson(), ControlSettings.CurrentVersion, "ja"));
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Input\] .*ignored unknown key 'language'"));

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
            Assert.That(_store.Load(), Is.EqualTo(_store.Defaults));
        }

        [Test]
        public void BrokenTextFallsBackToDefaultsWithAWarning()
        {
            PlayerPrefs.SetString(_store.Key, "{not json");
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Input\] .*not valid JSON"));

            Assert.That(_store.Load(), Is.EqualTo(_store.Defaults));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ValueOfAnotherTypeFallsBackToDefaultsWithAWarning()
        {
            PlayerPrefs.SetInt(_store.Key, 1);
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Input\] "));

            Assert.That(_store.Load(), Is.EqualTo(_store.Defaults));
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
        public void SavesTheValuesAndLoadsThemBackAfterARestart()
        {
            var settings = ControlSettings.Default
                .With(ControlValue.MouseSensitivity, 175)
                .With(ControlValue.GamepadSensitivity, 60)
                .With(ControlValue.AdsSensitivity, 80)
                .With(ControlValue.FieldOfView, 72)
                .With(ControlValue.MasterVolume, 30)
                .With(ControlValue.EffectsVolume, 0)
                .With(InputDeviceKind.Gamepad, HoldAction.Crouch, HoldMode.Hold)
                .WithShowNetStats(true)
                .WithPalette(ColorPalette.RedGreen)
                .WithShowSubtitles(true);

            _store.Save(settings);

            // 起動し直したときと同じく、新しいストアで読む。
            var loaded = new ControlSettingsStore(_store.Key).Load();
            Assert.That(loaded, Is.EqualTo(settings));
            Assert.That(loaded.ShowNetStats, Is.True);
            Assert.That(loaded.Palette, Is.EqualTo(ColorPalette.RedGreen));
            Assert.That(loaded.ShowSubtitles, Is.True);
            Assert.That(PlayerPrefs.GetString(_store.Key), Does.Contain("\"version\":12"));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void SavesTheAimAssistAndLoadsItBackAfterARestart()
        {
            var settings = ControlSettings.Default.WithAimAssist(false);

            _store.Save(settings);

            var loaded = new ControlSettingsStore(_store.Key).Load();
            Assert.That(loaded, Is.EqualTo(settings));
            Assert.That(loaded.AimAssist, Is.False);
            Assert.That(PlayerPrefs.GetString(_store.Key), Does.Contain("\"aimAssist\":false"));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void VersionEightDataSilentlyGetsTheAimAssistOnAndIsRewrittenInTheCurrentVersion()
        {
            // エイムアシストを足す前のゲームが書いた形式。
            var saved = ControlSettings.Default.WithShowBlood(false).With(ControlValue.GamepadSensitivity, 140);
            var versionEight = OldVersion(saved.ToJson(), 8, null);
            Assert.That(versionEight, Does.Contain("\"version\":8").And.Not.Contain("aimAssist"));
            PlayerPrefs.SetString(_store.Key, versionEight);

            var loaded = _store.Load();
            _store.Save(loaded);

            Assert.That(loaded.AimAssist, Is.EqualTo(ControlSettings.Default.AimAssist));
            Assert.That(loaded, Is.EqualTo(saved), "every other value is kept");
            Assert.That(PlayerPrefs.GetString(_store.Key), Does.Contain("\"version\":12").And.Contain("\"aimAssist\":true"));
            LogAssert.NoUnexpectedReceived();
        }

        // 視点の上下反転・デッドゾーン・振動を変えて閉じ、起動し直しても残っている
        [Test]
        public void SavesTheGamepadFeelAndLoadsItBackAfterARestart()
        {
            var settings = ControlSettings.Default
                .WithInvertLookY(true)
                .WithStickDeadzone(200)
                .WithRumbleEnabled(false)
                .WithRumbleStrength(60);

            _store.Save(settings);

            var loaded = new ControlSettingsStore(_store.Key).Load();
            Assert.That(loaded, Is.EqualTo(settings));
            Assert.That(loaded.InvertLookY, Is.True);
            Assert.That(loaded.StickDeadzone, Is.EqualTo(0.2f).Within(1e-6f));
            Assert.That(loaded.RumbleEnabled, Is.False);
            Assert.That(loaded.RumbleStrength, Is.EqualTo(0.6f).Within(1e-6f));
            Assert.That(
                PlayerPrefs.GetString(_store.Key),
                Does.Contain("\"invertLookY\":true,\"stickDeadzone\":200,\"rumble\":false,\"rumbleStrength\":60"));
            LogAssert.NoUnexpectedReceived();
        }

        // 前の版の保存がある状態で起動しても、ほかの設定が消えない
        [Test]
        public void VersionNineDataSilentlyGetsTheGamepadFeelDefaultsAndKeepsEverythingElse()
        {
            var saved = ControlSettings.Default
                .WithAimAssist(false)
                .WithShowBlood(false)
                .WithPalette(ColorPalette.BlueYellow)
                .With(ControlValue.MouseSensitivity, 220)
                .With(InputDeviceKind.Gamepad, HoldAction.Crouch, HoldMode.Hold);
            var versionNine = OldVersion(saved.ToJson(), 9, null);
            Assert.That(versionNine, Does.Contain("\"version\":9").And.Not.Contain("invertLookY").And.Not.Contain("rumble"));
            PlayerPrefs.SetString(_store.Key, versionNine);

            var loaded = _store.Load();
            _store.Save(loaded);

            Assert.That(loaded, Is.EqualTo(saved), "every other value is kept");
            Assert.That(loaded.InvertLookY, Is.EqualTo(ControlSettings.Default.InvertLookY));
            Assert.That(loaded.StickDeadzone, Is.EqualTo(ControlSettings.Default.StickDeadzone));
            Assert.That(loaded.RumbleEnabled, Is.EqualTo(ControlSettings.Default.RumbleEnabled));
            Assert.That(loaded.RumbleStrength, Is.EqualTo(ControlSettings.Default.RumbleStrength));
            Assert.That(
                PlayerPrefs.GetString(_store.Key),
                Does.Contain("\"version\":12").And.Contain("\"invertLookY\":false,\"stickDeadzone\":125,\"rumble\":true,\"rumbleStrength\":100"));
            LogAssert.NoUnexpectedReceived();
        }

        // 地域とテレメトリの同意を変えて閉じ、起動し直しても残っている
        [Test]
        public void SavesTheRegionAndTheTelemetryConsentAndLoadsThemBackAfterARestart()
        {
            var settings = ControlSettings.Default.WithQuickMatchRegion("europe").WithTelemetryConsent(true);

            _store.Save(settings);

            var loaded = new ControlSettingsStore(_store.Key).Load();
            Assert.That(loaded, Is.EqualTo(settings));
            Assert.That(loaded.QuickMatchRegion, Is.EqualTo("europe"));
            Assert.That(loaded.TelemetryConsent, Is.True);
            Assert.That(
                PlayerPrefs.GetString(_store.Key),
                Does.Contain("\"version\":12").And.EndWith(",\"quickMatchRegion\":\"europe\",\"telemetryConsent\":true" + DefaultGyro + "}"));
            LogAssert.NoUnexpectedReceived();
        }

        // 版 10 の保存がある状態で起動すると、地域は Auto・同意は無しになり、ほかの設定は消えずに今の版で書き直される
        [Test]
        public void VersionTenDataSilentlyGetsTheOnlineDefaultsAndIsRewrittenInTheCurrentVersion()
        {
            var saved = ControlSettings.Default
                .WithInvertLookY(true)
                .WithRumbleEnabled(false)
                .WithAimAssist(false)
                .WithPalette(ColorPalette.RedGreen)
                .With(ControlValue.FieldOfView, 80);
            var versionTen = OldVersion(saved.ToJson(), 10, null);
            Assert.That(versionTen, Does.Contain("\"version\":10").And.Not.Contain("quickMatchRegion").And.Not.Contain("telemetryConsent"));
            PlayerPrefs.SetString(_store.Key, versionTen);

            var loaded = _store.Load();
            _store.Save(loaded);

            Assert.That(loaded, Is.EqualTo(saved), "every other value is kept");
            Assert.That(loaded.QuickMatchRegion, Is.EqualTo(ControlSettings.AutoRegion));
            Assert.That(loaded.TelemetryConsent, Is.False);
            Assert.That(
                PlayerPrefs.GetString(_store.Key),
                Does.Contain("\"version\":12").And.EndWith(",\"quickMatchRegion\":\"auto\",\"telemetryConsent\":false" + DefaultGyro + "}"));
            LogAssert.NoUnexpectedReceived();
        }

        // 設定ファイルでジャイロを入れて閉じ、起動し直しても残っている
        [Test]
        public void SavesTheGyroAndLoadsItBackAfterARestart()
        {
            var settings = ControlSettings.Default
                .WithGyroEnabled(true)
                .WithGyroSensitivity(180)
                .WithGyroAimOnly(true)
                .WithInvertGyroY(true);

            _store.Save(settings);

            var loaded = new ControlSettingsStore(_store.Key).Load();
            Assert.That(loaded, Is.EqualTo(settings));
            Assert.That(loaded.GyroEnabled, Is.True);
            Assert.That(loaded.GyroSensitivity, Is.EqualTo(1.8f).Within(1e-6f));
            Assert.That(loaded.GyroAimOnly, Is.True);
            Assert.That(loaded.InvertGyroY, Is.True);
            Assert.That(
                PlayerPrefs.GetString(_store.Key),
                Does.Contain("\"version\":12")
                    .And.EndWith(",\"gyro\":true,\"gyroSensitivity\":180,\"gyroAimOnly\":true,\"invertGyroY\":true}"));
            LogAssert.NoUnexpectedReceived();
        }

        // 版 11 の保存がある状態で起動すると、ジャイロは切ったままになり、ほかの設定は消えずに版 12 で書き直される
        [Test]
        public void VersionElevenDataSilentlyGetsTheGyroOffAndIsRewrittenInTheCurrentVersion()
        {
            var saved = ControlSettings.Default
                .WithInvertLookY(true)
                .WithQuickMatchRegion("europe")
                .WithTelemetryConsent(true)
                .With(ControlValue.GamepadSensitivity, 140);
            var versionEleven = OldVersion(saved.ToJson(), 11, null);
            Assert.That(versionEleven, Does.Contain("\"version\":11").And.Not.Contain("gyro"));
            PlayerPrefs.SetString(_store.Key, versionEleven);

            var loaded = _store.Load();
            _store.Save(loaded);

            Assert.That(loaded, Is.EqualTo(saved), "every other value is kept");
            Assert.That(loaded.GyroEnabled, Is.False);
            Assert.That(loaded.GyroSensitivityPercent, Is.EqualTo(ControlSettings.GyroSensitivityRange.Default));
            Assert.That(loaded.InvertGyroY, Is.False, "the stick invert does not carry over to the gyro");
            Assert.That(
                PlayerPrefs.GetString(_store.Key),
                Does.Contain("\"version\":12").And.EndWith(",\"quickMatchRegion\":\"europe\",\"telemetryConsent\":true" + DefaultGyro + "}"));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ABrokenGyroSensitivityIsClampedWithAWarning()
        {
            PlayerPrefs.SetString(
                _store.Key,
                ControlSettings.Default.WithGyroEnabled(true).ToJson().Replace("\"gyroSensitivity\":100", "\"gyroSensitivity\":9000"));
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Input\] .*'gyroSensitivity' 9000 is outside 10 to 500; using 500"));

            var loaded = _store.Load();

            Assert.That(loaded.GyroSensitivityPercent, Is.EqualTo(500));
            Assert.That(loaded.GyroEnabled, Is.True, "the saved value");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ABrokenRegionFallsBackToAutoWithAWarning()
        {
            PlayerPrefs.SetString(
                _store.Key,
                ControlSettings.Default.WithQuickMatchRegion("asia").WithTelemetryConsent(true).ToJson()
                    .Replace("\"quickMatchRegion\":\"asia\"", "\"quickMatchRegion\":\"Asia Pacific\""));
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Input\] .*'quickMatchRegion'"));

            var loaded = _store.Load();

            Assert.That(loaded.QuickMatchRegion, Is.EqualTo(ControlSettings.AutoRegion));
            Assert.That(loaded.TelemetryConsent, Is.True, "the saved value");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ABrokenRumbleFlagFallsBackToOnWithAWarning()
        {
            PlayerPrefs.SetString(
                _store.Key,
                ControlSettings.Default.WithRumbleEnabled(false).WithInvertLookY(true).ToJson()
                    .Replace("\"rumble\":false", "\"rumble\":\"no\""));
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Input\] .*'rumble'"));

            var loaded = _store.Load();

            Assert.That(loaded.RumbleEnabled, Is.True);
            Assert.That(loaded.InvertLookY, Is.True, "the saved value");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ADeadzoneOutsideTheRangeIsClampedWithAWarning()
        {
            PlayerPrefs.SetString(
                _store.Key,
                ControlSettings.Default.ToJson().Replace("\"stickDeadzone\":125", "\"stickDeadzone\":800"));
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Input\] .*stickDeadzone"));

            var loaded = _store.Load();

            Assert.That(loaded.Get(ControlValue.StickDeadzone), Is.EqualTo(ControlSettings.RangeOf(ControlValue.StickDeadzone).Max));
            Assert.That(loaded.StickDeadzone, Is.EqualTo(0.5f));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ABrokenAimAssistFlagFallsBackToOnWithAWarning()
        {
            PlayerPrefs.SetString(
                _store.Key,
                ControlSettings.Default.WithShowBlood(false).ToJson().Replace("\"aimAssist\":true", "\"aimAssist\":\"off\""));
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Input\] .*aimAssist"));

            var loaded = _store.Load();

            Assert.That(loaded.AimAssist, Is.True);
            Assert.That(loaded.ShowBlood, Is.False, "the saved value");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void SavesThePresentationSettingsAndLoadsThemBackAfterARestart()
        {
            var settings = ControlSettings.Default
                .With(ControlValue.CameraShakeIntensity, 0)
                .With(ControlValue.ViewmodelMotionIntensity, 45)
                .WithShowBlood(false);

            _store.Save(settings);

            var loaded = new ControlSettingsStore(_store.Key).Load();
            Assert.That(loaded, Is.EqualTo(settings));
            Assert.That(loaded.CameraShakeIntensity, Is.EqualTo(0f));
            Assert.That(loaded.ViewmodelMotionIntensity, Is.EqualTo(0.45f).Within(1e-6f));
            Assert.That(loaded.ShowBlood, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void VersionSixDataSilentlyGetsThePresentationDefaults()
        {
            // 揺れの強さと血の表示を足す前のゲームが書いた形式。
            var versionSix = OldVersion(WithoutPresentation(ControlSettings.Default.WithPalette(ColorPalette.BlueYellow).ToJson()), 6, "en");
            Assert.That(versionSix, Does.Not.Contain("showBlood"));
            PlayerPrefs.SetString(_store.Key, versionSix);

            var loaded = _store.Load();

            Assert.That(loaded.Get(ControlValue.CameraShakeIntensity), Is.EqualTo(ControlSettings.RangeOf(ControlValue.CameraShakeIntensity).Default));
            Assert.That(loaded.Get(ControlValue.ViewmodelMotionIntensity), Is.EqualTo(ControlSettings.RangeOf(ControlValue.ViewmodelMotionIntensity).Default));
            Assert.That(loaded.ShowBlood, Is.EqualTo(ControlSettings.Default.ShowBlood));
            Assert.That(loaded.Palette, Is.EqualTo(ColorPalette.BlueYellow), "the saved value");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ABrokenBloodFlagFallsBackToShowingBloodWithAWarning()
        {
            PlayerPrefs.SetString(
                _store.Key,
                ControlSettings.Default.With(ControlValue.CameraShakeIntensity, 30).ToJson().Replace("\"showBlood\":true", "\"showBlood\":\"no\""));
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Input\] .*showBlood"));

            var loaded = _store.Load();

            Assert.That(loaded.ShowBlood, Is.True);
            Assert.That(loaded.Get(ControlValue.CameraShakeIntensity), Is.EqualTo(30), "the saved value");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ReadsVersionOneDataSilentlyWithDefaultValues()
        {
            // 数値の設定を足す前のゲームが書いた形式。
            PlayerPrefs.SetString(
                _store.Key,
                "{\"version\":1,\"keyboardMouse\":{\"sprint\":\"toggle\",\"crouch\":\"hold\",\"aim\":\"toggle\"}," +
                "\"gamepad\":{\"sprint\":\"hold\",\"crouch\":\"toggle\",\"aim\":\"hold\"}}");

            var loaded = _store.Load();

            Assert.That(loaded.Get(InputDeviceKind.KeyboardMouse, HoldAction.Sprint), Is.EqualTo(HoldMode.Toggle));
            Assert.That(loaded.Get(InputDeviceKind.KeyboardMouse, HoldAction.Aim), Is.EqualTo(HoldMode.Toggle));
            Assert.That(loaded.Get(InputDeviceKind.Gamepad, HoldAction.Sprint), Is.EqualTo(HoldMode.Hold));
            foreach (var value in ControlSettings.Values)
            {
                Assert.That(loaded.Get(value), Is.EqualTo(ControlSettings.RangeOf(value).Default), value.ToString());
            }

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void VersionOneDataIsRewrittenInTheCurrentFormatOnSave()
        {
            PlayerPrefs.SetString(
                _store.Key,
                "{\"version\":1,\"keyboardMouse\":{\"sprint\":\"toggle\",\"crouch\":\"hold\",\"aim\":\"hold\"}," +
                "\"gamepad\":{\"sprint\":\"toggle\",\"crouch\":\"toggle\",\"aim\":\"hold\"}}");
            var loaded = _store.Load();

            _store.Save(loaded.With(ControlValue.FieldOfView, 80));

            var reloaded = _store.Load();
            Assert.That(reloaded.Get(ControlValue.FieldOfView), Is.EqualTo(80));
            Assert.That(reloaded.Get(InputDeviceKind.KeyboardMouse, HoldAction.Sprint), Is.EqualTo(HoldMode.Toggle), "the old mode survives");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ValueOutsideTheRangeIsClampedWithAWarning()
        {
            PlayerPrefs.SetString(_store.Key, ControlSettings.Default.ToJson().Replace("\"fieldOfView\":60", "\"fieldOfView\":150"));
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Input\] .*fieldOfView"));

            var loaded = _store.Load();

            Assert.That(loaded.Get(ControlValue.FieldOfView), Is.EqualTo(ControlSettings.RangeOf(ControlValue.FieldOfView).Max));
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

        // 今の形式から、バージョン 7 で足した揺れの強さと血の表示を除く (初期値のときだけ)。
        private static string WithoutPresentation(string json)
        {
            return json.Replace(",\"cameraShake\":100,\"viewmodelMotion\":100,\"showBlood\":true", string.Empty);
        }

        // 今の形式の json を古い版の形にする。版 6・7 は言語 (language) を持っていた。版 8 までは版 9 で足したエイムアシストを持たない (初期値のときだけ除ける)。
        // 版 9 までは版 10 で足した視点の上下反転・デッドゾーン・振動・振動の強さを持たない (初期値のときだけ除ける)。
        // 版 10 までは版 11 で足した地域とテレメトリの同意を持たない (初期値のときだけ除ける)。
        // 版 11 までは版 12 で足したジャイロの欄を持たない (初期値のときだけ除ける)。
        private static string OldVersion(string json, int version, string language)
        {
            var old = json.Replace("\"version\":" + ControlSettings.CurrentVersion, "\"version\":" + version);
            if (version < 12)
            {
                old = old.Replace(DefaultGyro, string.Empty);
            }

            if (version < 11)
            {
                old = old.Replace(",\"quickMatchRegion\":\"auto\",\"telemetryConsent\":false", string.Empty);
            }

            if (version < 10)
            {
                old = old.Replace(",\"invertLookY\":false,\"stickDeadzone\":125,\"rumble\":true,\"rumbleStrength\":100", string.Empty);
            }

            if (version < 9)
            {
                old = old.Replace(",\"aimAssist\":true", string.Empty);
            }

            return language == null ? old : old.Insert(old.Length - 1, ",\"language\":\"" + language + "\"");
        }
    }
}
