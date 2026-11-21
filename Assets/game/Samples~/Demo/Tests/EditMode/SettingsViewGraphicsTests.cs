using System;
using System.Collections.Generic;
using System.Linq;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // 本物の設定を壊さないよう、保存先はテストごとの別のキーにし、画質や fps は記録するだけの受け手に入れる。
    public sealed class SettingsViewGraphicsTests
    {
        private static readonly ScreenResolution _small = new ScreenResolution(1280, 720);
        private static readonly ScreenResolution _large = new ScreenResolution(1920, 1080);
        private static readonly string[] _qualityNames = { "Mobile", "PC" };
        private static readonly int[] _frameRates = { 30, 60, 144 };

        private ControlSettings _appliedBefore;
        private GameLanguage _languageBefore;
        private ControlSettingsStore _store;
        private LanguagePreferenceStore _languageStore;
        private VoiceSettingsStore _voiceStore;
        private GraphicsSettingsStore _graphicsStore;
        private RecordingGraphicsSink _sink;
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
            _store = new ControlSettingsStore("Shooter.Tests.SettingsViewGraphics." + suffix);
            _languageStore = new LanguagePreferenceStore("Shooter.Tests.SettingsViewGraphics.Language." + suffix, _store.Key, GameLanguage.Japanese);
            _voiceStore = new VoiceSettingsStore("Shooter.Tests.SettingsViewGraphics.Voice." + suffix);
            _graphicsStore = new GraphicsSettingsStore("Shooter.Tests.SettingsViewGraphics.Graphics." + suffix, GraphicsSettings.Default);
            _sink = new RecordingGraphicsSink(GraphicsSettings.Default);
            _view = NewView(_graphicsStore, _sink);
            _container = new VisualElement();
        }

        [TearDown]
        public void TearDown()
        {
            _view.Close();
            PlayerPrefs.DeleteKey(_store.Key);
            PlayerPrefs.DeleteKey(_languageStore.Key);
            PlayerPrefs.DeleteKey(_voiceStore.Key);
            PlayerPrefs.DeleteKey(_graphicsStore.Key);
            PlayerPrefs.Save();
            AppliedControlSettings.Apply(_appliedBefore);
            Localizer.Apply(_languageBefore);
        }

        [Test]
        public void TheGraphicsRowsStartAtTheDefaultsInJapanese()
        {
            _view.Open(_container);

            Assert.That(_view.GraphicsStore, Is.SameAs(_graphicsStore));
            Assert.That(_view.Graphics, Is.EqualTo(GraphicsSettings.Default));
            Assert.That(LabelTexts(), Does.Contain("グラフィック"));
            Assert.That(CaptionOf(ResolutionButton()), Is.EqualTo("解像度"));
            Assert.That(CaptionOf(QualityButton()), Is.EqualTo("画質"));
            Assert.That(CaptionOf(VSyncButton()), Is.EqualTo("垂直同期 (VSync)"));
            Assert.That(CaptionOf(FrameRateButton()), Is.EqualTo("fps の上限"));
            Assert.That(ResolutionButton().text, Is.EqualTo("画面に合わせる"));
            Assert.That(QualityButton().text, Is.EqualTo("既定"));
            Assert.That(VSyncButton().text, Is.EqualTo("オフ"));
            Assert.That(FrameRateButton().text, Is.EqualTo("無制限"));
            Assert.That(_view.HasUnsavedChanges, Is.False);
            Assert.That(_sink.Applied, Is.Empty, "opening does not apply anything");
        }

        [Test]
        public void TheGraphicsSectionComesRightAfterViewAndSound()
        {
            _view.Open(_container);

            var sections = _view.Root.Query<Label>(className: "settings__section").ToList().Select(label => label.text).ToList();
            Assert.That(sections.IndexOf("グラフィック"), Is.EqualTo(sections.IndexOf("視点と音") + 1));

            var rows = ResolutionButton().parent.parent;
            var heading = rows[rows.IndexOf(ResolutionButton().parent) - 1] as Label;
            Assert.That(heading, Is.Not.Null);
            Assert.That(heading.text, Is.EqualTo("グラフィック"));
            Assert.That(rows.IndexOf(QualityButton().parent), Is.EqualTo(rows.IndexOf(ResolutionButton().parent) + 1));
            Assert.That(rows.IndexOf(VSyncButton().parent), Is.EqualTo(rows.IndexOf(ResolutionButton().parent) + 2));
            Assert.That(rows.IndexOf(FrameRateButton().parent), Is.EqualTo(rows.IndexOf(ResolutionButton().parent) + 3));
        }

        [Test]
        public void ChangingTheGraphicsAppliesAtOnceButIsSavedOnlyOnClose()
        {
            var saved = new List<KeyValuePair<string, GraphicsSettings>>();
            Action<string, GraphicsSettings> record = (key, settings) => saved.Add(new KeyValuePair<string, GraphicsSettings>(key, settings));
            GraphicsSettingsStore.Saved += record;
            try
            {
                _view.Open(_container);

                _view.SetResolution(_large);
                _view.SetQualityLevel(1);
                _view.SetVSync(true);
                _view.SetFrameRateLimit(60);

                var expected = new GraphicsSettings(_large, 1, true, 60);
                Assert.That(_sink.Applied, Has.Count.EqualTo(4), "every change is applied at once");
                Assert.That(_sink.Current, Is.EqualTo(expected));
                Assert.That(ResolutionButton().text, Is.EqualTo("1920 × 1080"));
                Assert.That(QualityButton().text, Is.EqualTo("高 (PC 向け)"));
                Assert.That(VSyncButton().text, Is.EqualTo("オン"));
                Assert.That(FrameRateButton().text, Is.EqualTo("60 fps"));
                Assert.That(_view.HasUnsavedChanges, Is.True);
                Assert.That(PlayerPrefs.HasKey(_graphicsStore.Key), Is.False, "not written until it closes");
                Assert.That(saved, Is.Empty);

                Assert.That(_view.Close(), Is.True);

                Assert.That(saved, Has.Count.EqualTo(1));
                Assert.That(saved[0].Key, Is.EqualTo(_graphicsStore.Key));
                Assert.That(saved[0].Value, Is.EqualTo(expected));
                Assert.That(new GraphicsSettingsStore(_graphicsStore.Key, GraphicsSettings.Default).Load(), Is.EqualTo(expected));
                Assert.That(PlayerPrefs.HasKey(_store.Key), Is.False, "the control settings did not change");
            }
            finally
            {
                GraphicsSettingsStore.Saved -= record;
            }
        }

        [Test]
        public void TheSavedGraphicsAreShownAfterARestart()
        {
            var chosen = new GraphicsSettings(_small, 0, true, 144);
            _graphicsStore.Save(chosen);

            // 起動し直したときと同じく、保存した値を読んだ受け手から新しい画面が開く。
            var restarted = new RecordingGraphicsSink(new GraphicsSettingsStore(_graphicsStore.Key, GraphicsSettings.Default).Load());
            var view = NewView(new GraphicsSettingsStore(_graphicsStore.Key, GraphicsSettings.Default), restarted);
            try
            {
                view.Open(_container);

                Assert.That(view.Graphics, Is.EqualTo(chosen));
                Assert.That(ResolutionButton().text, Is.EqualTo("1280 × 720"));
                Assert.That(QualityButton().text, Is.EqualTo("低 (モバイル向け)"));
                Assert.That(VSyncButton().text, Is.EqualTo("オン"));
                Assert.That(FrameRateButton().text, Is.EqualTo("144 fps"));
                Assert.That(view.HasUnsavedChanges, Is.False);
            }
            finally
            {
                view.Close();
            }
        }

        [Test]
        public void PressingTheButtonsCyclesThroughTheOfferedValues()
        {
            _view.Open(_container);

            PressResolution();
            Assert.That(_view.Graphics.Resolution, Is.EqualTo(_small));
            PressResolution();
            Assert.That(_view.Graphics.Resolution, Is.EqualTo(_large));
            PressResolution();
            Assert.That(_view.Graphics.Resolution, Is.EqualTo(ScreenResolution.Native));

            PressQuality();
            Assert.That(_view.Graphics.QualityLevel, Is.EqualTo(0));
            PressQuality();
            Assert.That(_view.Graphics.QualityLevel, Is.EqualTo(1));
            PressQuality();
            Assert.That(_view.Graphics.QualityLevel, Is.EqualTo(GraphicsSettings.ProjectQualityLevel));

            _view.SetVSync(!_view.Graphics.VSync);
            Assert.That(_view.Graphics.VSync, Is.True);
            _view.SetVSync(!_view.Graphics.VSync);
            Assert.That(_view.Graphics.VSync, Is.False);

            var limits = new List<int>();
            for (var i = 0; i < 4; i++)
            {
                PressFrameRate();
                limits.Add(_view.Graphics.FrameRateLimit);
            }

            Assert.That(limits, Is.EqualTo(new[] { 30, 60, 144, 0 }));
        }

        [Test]
        public void ResetToDefaultsRestoresTheGraphicsFromTheStore()
        {
            var gameDefaults = new GraphicsSettings(ScreenResolution.Native, 1, true, 60);
            var store = new GraphicsSettingsStore(_graphicsStore.Key, gameDefaults);
            var sink = new RecordingGraphicsSink(new GraphicsSettings(_large, 0, false, 30));
            var view = NewView(store, sink);
            try
            {
                view.Open(_container);

                view.ResetToDefaults();

                Assert.That(view.Graphics, Is.EqualTo(gameDefaults), "the defaults come from the data, not from code");
                Assert.That(sink.Current, Is.EqualTo(gameDefaults));
                Assert.That(VSyncButton().text, Is.EqualTo("オン"));
                Assert.That(FrameRateButton().text, Is.EqualTo("60 fps"));
                view.Close();
                Assert.That(store.Load(), Is.EqualTo(gameDefaults));
            }
            finally
            {
                view.Close();
            }
        }

        [Test]
        public void ClosingWithoutGraphicsChangesDoesNotWriteTheStore()
        {
            _view.Open(_container);
            _view.SetVSync(true);
            _view.SetVSync(false);

            Assert.That(_view.Close(), Is.False);
            Assert.That(PlayerPrefs.HasKey(_graphicsStore.Key), Is.False);
        }

        [Test]
        public void RejectsValuesOutsideTheRangeAndEditsWhileClosed()
        {
            Assert.Throws<InvalidOperationException>(() => _view.SetVSync(true));
            Assert.Throws<InvalidOperationException>(() => _view.SetFrameRateLimit(60));
            Assert.That(_view.Graphics, Is.Null);

            _view.Open(_container);
            Assert.Throws<ArgumentOutOfRangeException>(() => _view.SetFrameRateLimit(14));
            Assert.Throws<ArgumentOutOfRangeException>(() => _view.SetFrameRateLimit(1001));
            Assert.Throws<ArgumentOutOfRangeException>(() => _view.SetQualityLevel(-2));
            Assert.Throws<ArgumentOutOfRangeException>(() => _view.SetQualityLevel(64));
            Assert.That(_view.Graphics, Is.EqualTo(GraphicsSettings.Default));
            Assert.That(_sink.Applied, Is.Empty);
        }

        [Test]
        public void AFailingApplyIsLoggedAndTheChoiceIsStillSaved()
        {
            var view = NewView(_graphicsStore, new ThrowingGraphicsSink());
            try
            {
                view.Open(_container);

                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(@"^\[Graphics\] could not apply the graphics settings .*: no display$"));
                LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("no display"));
                view.SetVSync(true);

                Assert.That(VSyncButton().text, Is.EqualTo("オン"));
                view.Close();
                Assert.That(_graphicsStore.Load().VSync, Is.True);
            }
            finally
            {
                view.Close();
            }
        }

        [Test]
        public void SwitchingTheLanguageKeepsTheGraphicsEditsAndRenamesTheRows()
        {
            _view.Open(_container);
            _view.SetResolution(_small);
            _view.SetFrameRateLimit(30);

            LogAssert.Expect(LogType.Log, Localizer.LogTag + " language is now en");
            _view.SetLanguage(GameLanguage.English);

            Assert.That(LabelTexts(), Does.Contain("Graphics"));
            Assert.That(CaptionOf(ResolutionButton()), Is.EqualTo("Resolution"));
            Assert.That(CaptionOf(QualityButton()), Is.EqualTo("Graphics quality"));
            Assert.That(CaptionOf(FrameRateButton()), Is.EqualTo("Frame rate limit"));
            Assert.That(ResolutionButton().text, Is.EqualTo("1280 × 720"));
            Assert.That(QualityButton().text, Is.EqualTo("Default"));
            Assert.That(VSyncButton().text, Is.EqualTo("Off"));
            Assert.That(FrameRateButton().text, Is.EqualTo("30 fps"));
        }

        [Test]
        public void ButtonsWithNothingToChooseAreDisabled()
        {
            var view = new SettingsView(
                _store,
                languageStore: _languageStore,
                voiceStore: _voiceStore,
                graphicsStore: _graphicsStore,
                graphicsSink: _sink,
                resolutions: Array.Empty<ScreenResolution>(),
                qualityLevelNames: Array.Empty<string>(),
                frameRateChoices: Array.Empty<int>());
            try
            {
                view.Open(_container);

                Assert.That(ResolutionButton().enabledSelf, Is.False);
                Assert.That(QualityButton().enabledSelf, Is.False);
                Assert.That(FrameRateButton().enabledSelf, Is.False);
                Assert.That(VSyncButton().enabledSelf, Is.True);
            }
            finally
            {
                view.Close();
            }
        }

        [Test]
        public void TheOfferedListsAreChecked()
        {
            Assert.Throws<ArgumentException>(() => new SettingsView(resolutions: new[] { ScreenResolution.Native }));
            Assert.Throws<ArgumentException>(() => new SettingsView(resolutions: new[] { _small, _small }));
            Assert.Throws<ArgumentException>(() => new SettingsView(frameRateChoices: new[] { 60, 30 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SettingsView(frameRateChoices: new[] { 0 }));
        }

        [Test]
        public void NextResolutionGoesThroughTheListAndBackToTheDisplay()
        {
            var list = new[] { _small, _large };

            Assert.That(SettingsView.NextResolution(ScreenResolution.Native, list), Is.EqualTo(_small));
            Assert.That(SettingsView.NextResolution(_small, list), Is.EqualTo(_large));
            Assert.That(SettingsView.NextResolution(_large, list), Is.EqualTo(ScreenResolution.Native));
            Assert.That(SettingsView.NextResolution(new ScreenResolution(800, 600), list), Is.EqualTo(_small), "a size no longer offered moves into the list");
            Assert.That(SettingsView.NextResolution(ScreenResolution.Native, Array.Empty<ScreenResolution>()), Is.EqualTo(ScreenResolution.Native));
            Assert.Throws<ArgumentNullException>(() => SettingsView.NextResolution(ScreenResolution.Native, null));
        }

        [Test]
        public void NextQualityLevelGoesThroughTheLevelsAndBackToTheDefault()
        {
            Assert.That(SettingsView.NextQualityLevel(GraphicsSettings.ProjectQualityLevel, 3), Is.EqualTo(0));
            Assert.That(SettingsView.NextQualityLevel(0, 3), Is.EqualTo(1));
            Assert.That(SettingsView.NextQualityLevel(2, 3), Is.EqualTo(GraphicsSettings.ProjectQualityLevel));
            Assert.That(SettingsView.NextQualityLevel(7, 3), Is.EqualTo(GraphicsSettings.ProjectQualityLevel), "a level the project lost goes back to the default");
            Assert.That(SettingsView.NextQualityLevel(0, 0), Is.EqualTo(GraphicsSettings.ProjectQualityLevel));
        }

        [Test]
        public void NextFrameRateLimitGoesUpAndBackToUnlimited()
        {
            Assert.That(SettingsView.NextFrameRateLimit(0, _frameRates), Is.EqualTo(30));
            Assert.That(SettingsView.NextFrameRateLimit(30, _frameRates), Is.EqualTo(60));
            Assert.That(SettingsView.NextFrameRateLimit(144, _frameRates), Is.EqualTo(0));
            Assert.That(SettingsView.NextFrameRateLimit(75, _frameRates), Is.EqualTo(144), "a limit not in the list moves to the next larger one");
            Assert.That(SettingsView.NextFrameRateLimit(0, Array.Empty<int>()), Is.EqualTo(0));
            Assert.Throws<ArgumentNullException>(() => SettingsView.NextFrameRateLimit(0, null));
        }

        [Test]
        public void TheQualityNameFallsBackToTheProjectsOwnName()
        {
            Assert.That(SettingsView.FormatQualityLevel(0, new[] { "Ultra" }), Is.EqualTo("Ultra"));
            Assert.That(SettingsView.FormatQualityLevel(5, _qualityNames), Is.EqualTo("高 (PC 向け)"), "a level past the end shows the level it is lowered to");
            Assert.That(SettingsView.FormatQualityLevel(2, Array.Empty<string>()), Is.EqualTo("3"));
            Assert.That(SettingsView.FormatQualityLevel(GraphicsSettings.ProjectQualityLevel, _qualityNames), Is.EqualTo("既定"));
        }

        [Test]
        public void ThePauseMenuShowsTheGraphicsRowsAndClosingItSavesThem()
        {
            var hud = Hud.Create("SettingsViewGraphicsTests.Hud");
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

                Assert.That(settings.Slot.Q<Button>(SettingsView.GraphicsResolutionButtonName), Is.Not.Null);
                Assert.That(settings.Slot.Q<Button>(SettingsView.GraphicsQualityButtonName), Is.Not.Null);
                Assert.That(settings.Slot.Q<Button>(SettingsView.GraphicsVSyncButtonName), Is.Not.Null);
                Assert.That(settings.Slot.Q<Button>(SettingsView.GraphicsFrameRateButtonName), Is.Not.Null);
                _view.SetFrameRateLimit(60);
                Assert.That(_sink.Current.FrameRateLimit, Is.EqualTo(60), "the limit applies while the match is paused");

                pause.Close();

                Assert.That(settings.IsSettingsOpen, Is.False);
                Assert.That(_graphicsStore.Load().FrameRateLimit, Is.EqualTo(60));
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
        public void TheMainMenuShowsTheGraphicsRowsAndSavesThemOnClose()
        {
            var layout = Resources.Load<VisualTreeAsset>(MainMenuScreen.LayoutResourcePath);
            var owner = new GameObject("SettingsViewGraphicsTests.Menu");
            try
            {
                var screen = owner.AddComponent<MainMenuScreen>();
                screen.Bind(layout.CloneTree());
                screen.InstallSettingsView(_view);
                screen.OpenSettings();

                Assert.That(screen.SettingsSlot.Q<Button>(SettingsView.GraphicsVSyncButtonName), Is.Not.Null);
                _view.SetVSync(true);
                screen.CloseSettings();

                Assert.That(_graphicsStore.Load().VSync, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void EveryGraphicsKeyIsInBothLanguages()
        {
            var keys = new[]
            {
                "settings.section.graphics",
                "settings.graphics.resolution",
                "settings.graphics.resolution.native",
                "settings.graphics.resolution.size",
                "settings.graphics.quality",
                "settings.graphics.quality.project",
                SettingsView.QualityTextKeyPrefix + "Mobile",
                SettingsView.QualityTextKeyPrefix + "PC",
                "settings.graphics.vSync",
                "settings.graphics.frameRate",
                "settings.graphics.frameRate.unlimited",
                "settings.graphics.frameRate.value",
            };
            foreach (var language in new[] { GameLanguage.Japanese, GameLanguage.English })
            {
                var table = Localizer.TableOf(language);
                foreach (var key in keys)
                {
                    Assert.That(table.Get(key), Is.Not.EqualTo(key), language + " " + key);
                }
            }
        }

        private SettingsView NewView(GraphicsSettingsStore store, IGraphicsSettingsSink sink)
        {
            return new SettingsView(
                _store,
                languageStore: _languageStore,
                voiceStore: _voiceStore,
                graphicsStore: store,
                graphicsSink: sink,
                resolutions: new[] { _small, _large },
                qualityLevelNames: _qualityNames,
                frameRateChoices: _frameRates);
        }

        // ボタンはパネルに付いておらずクリックのイベントが届かないので、ボタンが呼ぶのと同じ操作をする。
        private void PressResolution()
        {
            _view.SetResolution(SettingsView.NextResolution(_view.Graphics.Resolution, _view.Resolutions));
        }

        private void PressQuality()
        {
            _view.SetQualityLevel(SettingsView.NextQualityLevel(_view.Graphics.QualityLevel, _view.QualityLevelNames.Count));
        }

        private void PressFrameRate()
        {
            _view.SetFrameRateLimit(SettingsView.NextFrameRateLimit(_view.Graphics.FrameRateLimit, _view.FrameRateChoices));
        }

        private Button ResolutionButton()
        {
            return Require(SettingsView.GraphicsResolutionButtonName);
        }

        private Button QualityButton()
        {
            return Require(SettingsView.GraphicsQualityButtonName);
        }

        private Button VSyncButton()
        {
            return Require(SettingsView.GraphicsVSyncButtonName);
        }

        private Button FrameRateButton()
        {
            return Require(SettingsView.GraphicsFrameRateButtonName);
        }

        private Button Require(string name)
        {
            var button = _container.Q<Button>(name);
            Assert.That(button, Is.Not.Null, name);
            return button;
        }

        private static string CaptionOf(VisualElement control)
        {
            return ((Label)control.parent[0]).text;
        }

        private List<string> LabelTexts()
        {
            return _container.Query<Label>().ToList().Select(label => label.text).ToList();
        }

        private sealed class ThrowingGraphicsSink : IGraphicsSettingsSink
        {
            public GraphicsSettings Current => GraphicsSettings.Default;

            public void Apply(GraphicsSettings settings)
            {
                throw new InvalidOperationException("no display");
            }
        }
    }
}
