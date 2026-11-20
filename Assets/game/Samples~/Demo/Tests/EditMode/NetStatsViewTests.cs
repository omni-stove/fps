using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // Edit Mode では UIDocument が木を作らないことがあるので、Hud.uxml を複製した木を直接 Bind する。
    // 文は JapaneseLanguageSetUp が効かせる日本語の表 (Resources/Localization/ja/net.json) から引く。
    public sealed class NetStatsViewTests
    {
        private GameObject _host;
        private NetStatsView _view;

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
            _host = new GameObject("TestNetStats");
            _view = _host.AddComponent<NetStatsView>();
            var layout = Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath);
            Assert.That(layout, Is.Not.Null, "Assets/game/Samples~/Demo/UI/Resources/Hud/Hud.uxml should be loadable from Resources");
            _view.Bind(layout.CloneTree());
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
            GameSignals.ResetAll();
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

        private string LabelText()
        {
            return ((Label)_view.Element).text;
        }
    }
}
