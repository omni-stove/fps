using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Shooter.Tests.EditMode
{
    // 本物の設定を壊さないよう、保存先はテストごとの別のキーにする。
    public sealed class SettingsViewOnlineTests
    {
        private static readonly string[] _regions = { "asia", "europe", "north-america" };

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
            _store = new ControlSettingsStore("Shooter.Tests.SettingsViewOnline." + suffix);
            _languageStore = new LanguagePreferenceStore("Shooter.Tests.SettingsViewOnline.Language." + suffix, _store.Key, GameLanguage.Japanese);
            _voiceStore = new VoiceSettingsStore("Shooter.Tests.SettingsViewOnline.Voice." + suffix);
            _view = new SettingsView(_store, languageStore: _languageStore, voiceStore: _voiceStore, regions: _regions);
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
        public void TheRegionAndTelemetryRowsStartAtAutoAndOffInJapanese()
        {
            _view.Open(_container);

            var region = _container.Q<Button>(SettingsView.RegionButtonName);
            var telemetry = _container.Q<Button>(SettingsView.TelemetryButtonName);
            Assert.That(CaptionOf(region), Is.EqualTo("クイックマッチの地域"));
            Assert.That(region.text, Is.EqualTo("自動 (どの地域でも)"));
            Assert.That(region.enabledSelf, Is.True);
            Assert.That(HeadingOf(region), Is.EqualTo("オンライン"));
            Assert.That(CaptionOf(telemetry), Is.EqualTo("クラッシュ報告と利用状況の記録を許可"));
            Assert.That(telemetry.text, Is.EqualTo("オフ"));
            Assert.That(HeadingOf(telemetry), Is.EqualTo("プライバシー"));
        }

        [Test]
        public void TheRegionAndTelemetryRowsAreInEnglishToo()
        {
            LogAssert.Expect(LogType.Log, Localizer.LogTag + " language is now en");
            Localizer.Apply(GameLanguage.English);

            _view.Open(_container);
            _view.SetQuickMatchRegion("north-america");

            var region = _container.Q<Button>(SettingsView.RegionButtonName);
            var telemetry = _container.Q<Button>(SettingsView.TelemetryButtonName);
            Assert.That(CaptionOf(region), Is.EqualTo("Quick Match region"));
            Assert.That(region.text, Is.EqualTo("North America"));
            Assert.That(HeadingOf(region), Is.EqualTo("Online"));
            Assert.That(CaptionOf(telemetry), Is.EqualTo("Allow crash reports and usage data"));
            Assert.That(telemetry.text, Is.EqualTo("Off"));
            Assert.That(HeadingOf(telemetry), Is.EqualTo("Privacy"));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void EveryOfferedRegionHasANameInEveryLanguage()
        {
            foreach (var language in new[] { GameLanguage.Japanese, GameLanguage.English })
            {
                var table = Localizer.TableOf(language);
                Assert.That(table.Contains(SettingsView.RegionTextKeyPrefix + ControlSettings.AutoRegion), Is.True, language.ToString());
                foreach (var region in QuickMatchRegionList.Load())
                {
                    Assert.That(table.Contains(SettingsView.RegionTextKeyPrefix + region), Is.True, $"{language} {region}");
                }
            }
        }

        [Test]
        public void PressingTheRegionButtonCyclesFromAutoThroughEveryRegionAndBack()
        {
            _view.Open(_container);
            var button = _container.Q<Button>(SettingsView.RegionButtonName);
            var seen = new List<string>();

            for (var i = 0; i < _regions.Length + 1; i++)
            {
                PressRegion();
                seen.Add(AppliedControlSettings.Current.QuickMatchRegion);
            }

            Assert.That(seen, Is.EqualTo(new[] { "asia", "europe", "north-america", "auto" }));
            Assert.That(button.text, Is.EqualTo("自動 (どの地域でも)"));
        }

        [Test]
        public void ChoosingARegionAndConsentingAppliesAtOnceAndIsKeptAfterARestart()
        {
            var received = new List<ControlSettings>();
            AppliedControlSettings.Changed += received.Add;
            try
            {
                _view.Open(_container);

                _view.SetQuickMatchRegion("europe");
                _view.SetTelemetryConsent(true);

                Assert.That(AppliedControlSettings.Current.QuickMatchRegion, Is.EqualTo("europe"));
                Assert.That(AppliedControlSettings.Current.TelemetryConsent, Is.True);
                Assert.That(received.Count, Is.EqualTo(2));
                Assert.That(_container.Q<Button>(SettingsView.RegionButtonName).text, Is.EqualTo("ヨーロッパ"));
                Assert.That(_container.Q<Button>(SettingsView.TelemetryButtonName).text, Is.EqualTo(SettingsView.OnText));
                Assert.That(PlayerPrefs.HasKey(_store.Key), Is.False, "not written until it closes");

                Assert.That(_view.Close(), Is.True);

                // 起動し直したときと同じく、新しいストアで読む。
                var saved = new ControlSettingsStore(_store.Key).Load();
                Assert.That(saved.QuickMatchRegion, Is.EqualTo("europe"));
                Assert.That(saved.TelemetryConsent, Is.True);

                AppliedControlSettings.Apply(saved);
                _view.Open(_container);
                Assert.That(_container.Q<Button>(SettingsView.RegionButtonName).text, Is.EqualTo("ヨーロッパ"));
                Assert.That(_container.Q<Button>(SettingsView.TelemetryButtonName).text, Is.EqualTo(SettingsView.OnText));
            }
            finally
            {
                AppliedControlSettings.Changed -= received.Add;
            }
        }

        [Test]
        public void PressingTheTelemetryButtonTogglesTheConsent()
        {
            _view.Open(_container);
            var button = _container.Q<Button>(SettingsView.TelemetryButtonName);

            PressTelemetry();
            Assert.That(AppliedControlSettings.Current.TelemetryConsent, Is.True);
            Assert.That(button.text, Is.EqualTo(SettingsView.OnText));

            PressTelemetry();
            Assert.That(AppliedControlSettings.Current.TelemetryConsent, Is.False);
            Assert.That(button.text, Is.EqualTo(SettingsView.OffText));
        }

        [Test]
        public void ResetToDefaultsGoesBackToAutoAndWithdrawsTheConsent()
        {
            _view.Open(_container);
            _view.SetQuickMatchRegion("asia");
            _view.SetTelemetryConsent(true);

            _view.ResetToDefaults();

            Assert.That(AppliedControlSettings.Current.QuickMatchRegion, Is.EqualTo(ControlSettings.AutoRegion));
            Assert.That(AppliedControlSettings.Current.TelemetryConsent, Is.False);
            Assert.That(_container.Q<Button>(SettingsView.RegionButtonName).text, Is.EqualTo("自動 (どの地域でも)"));
            Assert.That(_container.Q<Button>(SettingsView.TelemetryButtonName).text, Is.EqualTo(SettingsView.OffText));
        }

        [Test]
        public void ASavedRegionThatIsNoLongerOfferedIsShownByItsNameAndCanBeLeft()
        {
            AppliedControlSettings.Apply(ControlSettings.Default.WithQuickMatchRegion("oceania"));

            _view.Open(_container);
            var button = _container.Q<Button>(SettingsView.RegionButtonName);

            Assert.That(button.text, Is.EqualTo("oceania"));
            Assert.That(button.enabledSelf, Is.True);
            PressRegion();
            Assert.That(AppliedControlSettings.Current.QuickMatchRegion, Is.EqualTo("asia"));
        }

        [Test]
        public void WithNoRegionsOfferedTheRegionButtonCannotBePressed()
        {
            var view = new SettingsView(_store, languageStore: _languageStore, voiceStore: _voiceStore, regions: Array.Empty<string>());
            try
            {
                view.Open(_container);

                var button = _container.Q<Button>(SettingsView.RegionButtonName);
                Assert.That(button.text, Is.EqualTo("自動 (どの地域でも)"));
                Assert.That(button.enabledSelf, Is.False);
            }
            finally
            {
                view.Close();
            }
        }

        [Test]
        public void SettingTheRegionNeedsAnOpenViewAndARegionName()
        {
            Assert.Throws<InvalidOperationException>(() => _view.SetQuickMatchRegion("asia"));
            Assert.Throws<InvalidOperationException>(() => _view.SetTelemetryConsent(true));

            _view.Open(_container);
            Assert.Throws<ArgumentException>(() => _view.SetQuickMatchRegion("Asia"));
            Assert.Throws<ArgumentNullException>(() => _view.SetQuickMatchRegion(null));
            Assert.That(AppliedControlSettings.Current.QuickMatchRegion, Is.EqualTo(ControlSettings.AutoRegion));
        }

        [Test]
        public void TheOfferedRegionsMustBeDistinctRegionNamesOtherThanAuto()
        {
            Assert.Throws<ArgumentException>(() => new SettingsView(_store, regions: new[] { "auto" }));
            Assert.Throws<ArgumentException>(() => new SettingsView(_store, regions: new[] { "Asia" }));
            Assert.Throws<ArgumentException>(() => new SettingsView(_store, regions: new string[] { null }));
            Assert.Throws<ArgumentException>(() => new SettingsView(_store, regions: new[] { "asia", "asia" }));
            Assert.That(new SettingsView(_store, regions: _regions).Regions, Is.EqualTo(_regions));
        }

        [Test]
        public void WithoutGivenRegionsTheViewOffersTheRegionsInResources()
        {
            var view = new SettingsView(_store);

            Assert.That(view.Regions, Is.EqualTo(new[] { "asia", "europe", "north-america" }));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void NextRegionPutsAutoFirst()
        {
            var two = new[] { "asia", "europe" };

            Assert.That(SettingsView.NextRegion("auto", two), Is.EqualTo("asia"));
            Assert.That(SettingsView.NextRegion("asia", two), Is.EqualTo("europe"));
            Assert.That(SettingsView.NextRegion("europe", two), Is.EqualTo("auto"));
            Assert.That(SettingsView.NextRegion("oceania", two), Is.EqualTo("asia"), "a region no longer offered moves to the first one");
            Assert.That(SettingsView.NextRegion("auto", Array.Empty<string>()), Is.EqualTo("auto"));
            Assert.That(SettingsView.NextRegion("oceania", Array.Empty<string>()), Is.EqualTo("auto"));
            Assert.Throws<ArgumentNullException>(() => SettingsView.NextRegion("auto", null));
        }

        [Test]
        public void FormatRegionUsesTheStringsAndFallsBackToTheName()
        {
            Assert.That(SettingsView.FormatRegion("auto"), Is.EqualTo("自動 (どの地域でも)"));
            Assert.That(SettingsView.FormatRegion("asia"), Is.EqualTo("アジア"));
            Assert.That(SettingsView.FormatRegion("eu-west-2"), Is.EqualTo("eu-west-2"));
            Assert.Throws<ArgumentNullException>(() => SettingsView.FormatRegion(null));
        }

        [Test]
        public void TheRegionListInResourcesIsReadWithoutWarnings()
        {
            Assert.That(Resources.Load<TextAsset>(QuickMatchRegionList.ResourcePath), Is.Not.Null);

            Assert.That(QuickMatchRegionList.Load(), Is.EqualTo(new[] { "asia", "europe", "north-america" }));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void TheRegionListSkipsOnlyTheEntriesItCannotUse()
        {
            var warnings = new List<string>();

            var regions = QuickMatchRegionList.Read(
                "test.json",
                "﻿{\"regions\":[\"asia\",\"Asia\",\"auto\",\"asia\",3,\"eu-west-2\"],\"note\":1}",
                warnings.Add);

            Assert.That(regions, Is.EqualTo(new[] { "asia", "eu-west-2" }));
            Assert.That(warnings, Has.Count.EqualTo(5));
            Assert.That(string.Join("\n", warnings), Does.Contain("'note'").And.Contain("regions[1]").And.Contain("regions[2]")
                .And.Contain("regions[3]").And.Contain("regions[4]"));
        }

        [TestCase(null)]
        [TestCase("{")]
        [TestCase("[]")]
        [TestCase("{\"regions\":\"asia\"}")]
        [TestCase("{}")]
        public void AnUnreadableRegionListFallsBackToTheBuiltInOne(string json)
        {
            var warnings = new List<string>();

            var regions = QuickMatchRegionList.Read("test.json", json, warnings.Add);

            Assert.That(regions, Is.SameAs(QuickMatchRegionList.BuiltIn));
            Assert.That(warnings, Has.Count.EqualTo(1));
            Assert.That(warnings[0], Does.StartWith("test.json: "));
        }

        [Test]
        public void AnEmptyRegionListOffersOnlyAuto()
        {
            var warnings = new List<string>();

            Assert.That(QuickMatchRegionList.Read("test.json", "{\"regions\":[]}", warnings.Add), Is.Empty);
            Assert.That(warnings, Is.Empty);
        }

        // ボタンはパネルに付いておらずクリックのイベントが届かないので、ボタンが呼ぶのと同じ操作をする。
        private void PressRegion()
        {
            _view.SetQuickMatchRegion(SettingsView.NextRegion(_view.Settings.QuickMatchRegion, _view.Regions));
        }

        private void PressTelemetry()
        {
            _view.SetTelemetryConsent(!_view.Settings.TelemetryConsent);
        }

        private static string CaptionOf(VisualElement control)
        {
            Assert.That(control, Is.Not.Null);
            return ((Label)control.parent[0]).text;
        }

        private static string HeadingOf(VisualElement control)
        {
            var rows = control.parent.parent;
            return ((Label)rows[rows.IndexOf(control.parent) - 1]).text;
        }
    }
}
