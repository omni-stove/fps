using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // Edit Mode では UIDocument が木を作らないことがあるので、Hud.uxml を複製した木を直接 Bind する。
    // 文は JapaneseLanguageSetUp が効かせる日本語の表 (Resources/Localization/ja/net.json) から引く。
    // 行の中身を見るテストは F3 で出す側にした設定で始め、本物の設定と保存先は使わない。
    public sealed class NetStatsViewTests
    {
        private const string StoreKey = "Shooter.Tests.NetStatsView.ControlSettings";

        private GameObject _host;
        private NetStatsView _view;
        private RecordingSettingsSink _settings;
        private ControlSettingsStore _store;

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
            _store = new ControlSettingsStore(StoreKey);
            _store.Delete();
            _settings = new RecordingSettingsSink(ControlSettings.Default.WithShowNetStats(true));
            _host = new GameObject("TestNetStats");
            _view = _host.AddComponent<NetStatsView>();
            _view.Settings = _settings;
            _view.Store = _store;
            var layout = Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath);
            Assert.That(layout, Is.Not.Null, "Assets/game/Samples~/Demo/UI/Resources/Hud/Hud.uxml should be loadable from Resources");
            _view.Bind(layout.CloneTree());
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
            GameSignals.ResetAll();
            _store.Delete();
        }

        [Test]
        public void TheViewIsHiddenWithoutStats()
        {
            Assert.That(_view.Element, Is.Not.Null, "Hud.uxml has the net-stats element");
            Assert.That(_view.Element, Is.InstanceOf<Label>());
            Assert.That(_view.IsShown, Is.False);
            Assert.That(_view.Text, Is.Empty);
            Assert.That(LabelText(), Is.Empty);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void PublishedStatsBecomeTheLine()
        {
            GameSignals.NetStats.Publish(new NetStatsSignal(48, 3, 1234));

            Assert.That(_view.IsShown, Is.True);
            Assert.That(_view.Text, Is.EqualTo("Ping 48 ms  ロス 3%  tick 1234"));
            Assert.That(LabelText(), Is.EqualTo("Ping 48 ms  ロス 3%  tick 1234"));
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
        }

        [Test]
        public void UnknownValuesAreShownAsDashes()
        {
            GameSignals.NetStats.Publish(new NetStatsSignal(null, null, 50));

            Assert.That(LabelText(), Is.EqualTo("Ping -- ms  ロス --%  tick 50"));

            GameSignals.NetStats.Publish(new NetStatsSignal(null, null, null));

            Assert.That(_view.IsShown, Is.True, "all unknown still shows the line");
            Assert.That(LabelText(), Is.EqualTo("Ping -- ms  ロス --%  tick --"));
        }

        [TestCase(149, NetPingLevel.Good, false, false)]
        [TestCase(150, NetPingLevel.Warning, true, false)]
        [TestCase(299, NetPingLevel.Warning, true, false)]
        [TestCase(300, NetPingLevel.Bad, false, true)]
        public void ThePingLevelPicksTheColorClass(int ping, NetPingLevel level, bool warning, bool bad)
        {
            GameSignals.NetStats.Publish(new NetStatsSignal(ping, 0, 1));

            Assert.That(_view.Level, Is.EqualTo(level));
            Assert.That(_view.Element.ClassListContains(NetStatsView.WarningClass), Is.EqualTo(warning));
            Assert.That(_view.Element.ClassListContains(NetStatsView.BadClass), Is.EqualTo(bad));
        }

        [Test]
        public void TheColorFollowsThePingAsItChanges()
        {
            GameSignals.NetStats.Publish(new NetStatsSignal(400, 0, 1));
            GameSignals.NetStats.Publish(new NetStatsSignal(20, 0, 2));

            Assert.That(_view.Element.ClassListContains(NetStatsView.BadClass), Is.False);
            Assert.That(_view.Element.ClassListContains(NetStatsView.WarningClass), Is.False);
            Assert.That(LabelText(), Is.EqualTo("Ping 20 ms  ロス 0%  tick 2"));
        }

        [Test]
        public void AnUnknownPingIsNotColored()
        {
            GameSignals.NetStats.Publish(new NetStatsSignal(400, 0, 1));
            GameSignals.NetStats.Publish(new NetStatsSignal(null, 0, 2));

            Assert.That(_view.Level, Is.EqualTo(NetPingLevel.Good));
            Assert.That(_view.Element.ClassListContains(NetStatsView.BadClass), Is.False);
        }

        [Test]
        public void ClearingTheSignalHidesTheLineAndDropsTheColor()
        {
            GameSignals.NetStats.Publish(new NetStatsSignal(500, 10, 7));

            GameSignals.NetStats.Clear();

            Assert.That(_view.IsShown, Is.False);
            Assert.That(_view.Stats, Is.Null);
            Assert.That(_view.Text, Is.Empty);
            Assert.That(LabelText(), Is.Empty);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(_view.Element.ClassListContains(NetStatsView.BadClass), Is.False);
        }

        [Test]
        public void AValuePublishedBeforeBindingIsShownOnBind()
        {
            GameSignals.NetStats.Publish(new NetStatsSignal(160, null, 9));
            var host = new GameObject("TestNetStatsLate");
            try
            {
                var view = host.AddComponent<NetStatsView>();
                view.Settings = _settings;
                view.Store = null;
                view.Bind(Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath).CloneTree());

                Assert.That(view.IsShown, Is.True);
                Assert.That(view.Text, Is.EqualTo("Ping 160 ms  ロス --%  tick 9"));
                Assert.That(view.Element.ClassListContains(NetStatsView.WarningClass), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void UnbindingStopsFollowingTheSignal()
        {
            GameSignals.NetStats.Publish(new NetStatsSignal(400, 0, 1));
            var element = _view.Element;

            _view.Unbind();
            GameSignals.NetStats.Publish(new NetStatsSignal(20, 0, 2));

            Assert.That(((Label)element).text, Is.Empty);
            Assert.That(element.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(element.ClassListContains(NetStatsView.BadClass), Is.False);
            Assert.That(_view.Stats, Is.EqualTo((NetStatsSignal?)new NetStatsSignal(400, 0, 1)), "the signal is no longer heard");
        }

        [Test]
        public void TheEnglishLineUsesTheSameValues()
        {
            var table = Localizer.TableOf(GameLanguage.English);

            Assert.That(table.Resolve(NetStatsFormat.Text(48, 3, 1234)), Is.EqualTo("Ping 48 ms  Loss 3%  tick 1234"));
            Assert.That(table.Resolve(NetStatsFormat.Text(null, null, null)), Is.EqualTo("Ping -- ms  Loss --%  tick --"));
        }

        [Test]
        public void TheToggleIsTheNetStatsActionOnF3()
        {
            Assert.That(NetStatsView.ToggleActionPath, Is.EqualTo(InputActionPaths.NetStats));
            Assert.That(NetStatsView.ToggleActionPath, Is.EqualTo("Player/NetStats"));
        }

        [Test]
        public void TheLineIsHiddenByDefaultEvenWhileOnline()
        {
            Assert.That(ControlSettings.Default.ShowNetStats, Is.False);
            _view.Settings = new RecordingSettingsSink(ControlSettings.Default);

            GameSignals.NetStats.Publish(new NetStatsSignal(48, 3, 1234));

            Assert.That(_view.IsEnabled, Is.False);
            Assert.That(_view.IsShown, Is.False);
            Assert.That(_view.Stats, Is.EqualTo((NetStatsSignal?)new NetStatsSignal(48, 3, 1234)), "the values are still heard");
            Assert.That(LabelText(), Is.Empty);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void TogglingShowsAndHidesTheLine()
        {
            var hidden = new RecordingSettingsSink(ControlSettings.Default);
            _view.Settings = hidden;
            GameSignals.NetStats.Publish(new NetStatsSignal(400, 3, 1234));

            _view.Toggle();

            Assert.That(_view.IsEnabled, Is.True);
            Assert.That(_view.IsShown, Is.True);
            Assert.That(LabelText(), Is.EqualTo("Ping 400 ms  ロス 3%  tick 1234"));
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(_view.Element.ClassListContains(NetStatsView.BadClass), Is.True);

            _view.Toggle();

            Assert.That(_view.IsEnabled, Is.False);
            Assert.That(_view.IsShown, Is.False);
            Assert.That(LabelText(), Is.Empty);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(_view.Element.ClassListContains(NetStatsView.BadClass), Is.False);
            Assert.That(hidden.Applied, Has.Count.EqualTo(2));
            Assert.That(hidden.Applied[0].ShowNetStats, Is.True);
            Assert.That(hidden.Applied[1].ShowNetStats, Is.False);
        }

        [Test]
        public void TogglingChangesOnlyTheNetStatsFlag()
        {
            var before = ControlSettings.Default.With(ControlValue.MouseSensitivity, 200);
            var sink = new RecordingSettingsSink(before);
            _view.Settings = sink;

            _view.Toggle();

            Assert.That(sink.Current, Is.EqualTo(before.WithShowNetStats(true)));
        }

        [Test]
        public void TogglingSavesOnlyTheFlagOverTheSavedSettings()
        {
            var saved = ControlSettings.Default.With(ControlValue.FieldOfView, 80);
            _store.Save(saved);

            // 設定画面で編集中のまだ保存していない値は、F3 で保存されない。
            _view.Settings = new RecordingSettingsSink(ControlSettings.Default.With(ControlValue.FieldOfView, 90));
            _view.Toggle();

            Assert.That(_store.Load(), Is.EqualTo(saved.WithShowNetStats(true)));

            _view.Toggle();

            Assert.That(_store.Load(), Is.EqualTo(saved));
        }

        [Test]
        public void SettingTheSameFlagAppliesAndSavesNothing()
        {
            _view.SetEnabled(true);

            Assert.That(_settings.Applied, Is.Empty);
            Assert.That(PlayerPrefs.HasKey(StoreKey), Is.False);
        }

        [Test]
        public void WithoutAStoreTheToggleIsAppliedButNotSaved()
        {
            _view.Store = null;

            _view.Toggle();

            Assert.That(_view.IsEnabled, Is.False);
            Assert.That(_settings.Applied, Has.Count.EqualTo(1));
            Assert.That(PlayerPrefs.HasKey(StoreKey), Is.False);
        }

        [Test]
        public void OfflineTheLineStaysHiddenEvenWhenEnabled()
        {
            Assert.That(_view.IsEnabled, Is.True);
            Assert.That(GameSignals.NetStats.HasValue, Is.False, "nothing publishes the net stats offline");

            _view.SetEnabled(true);
            _view.Refresh();

            Assert.That(_view.IsShown, Is.False);
            Assert.That(LabelText(), Is.Empty);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void ChangingTheSettingsSinkRedrawsTheLine()
        {
            GameSignals.NetStats.Publish(new NetStatsSignal(20, 0, 1));
            Assert.That(_view.IsShown, Is.True);

            _view.Settings = new RecordingSettingsSink(ControlSettings.Default);

            Assert.That(_view.IsShown, Is.False);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        private string LabelText()
        {
            return ((Label)_view.Element).text;
        }
    }
}
