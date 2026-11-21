using System;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Shooter.Tests.EditMode
{
    // サンプルが登録する 7 つの画面を、本物のメニューのレイアウトでボタンから開いて戻る
    public sealed class DefaultMainMenuPanelsTests
    {
        private GameObject _owner;
        private MainMenuScreen _screen;
        private VisualElement _root;

        [SetUp]
        public void SetUp()
        {
            var layout = Resources.Load<VisualTreeAsset>(MainMenuScreen.LayoutResourcePath);
            Assert.That(layout, Is.Not.Null, "Resources/" + MainMenuScreen.LayoutResourcePath + ".uxml is missing");
            _root = layout.CloneTree();
            _owner = new GameObject("DefaultMainMenuPanelsTests");
            _screen = _owner.AddComponent<MainMenuScreen>();
            _screen.SaveStorage = new MemorySaveStorage();
            ProgressionBootstrap.Uninstall();
            CombatEvents.ClearSubscribers();
            GameSignals.ResetAll();
            MainMenuPanels.Clear();
            DefaultMenuScreens.RegisterMainMenuPanels();
        }

        [TearDown]
        public void TearDown()
        {
            ProgressionBootstrap.Uninstall();
            CombatEvents.ClearSubscribers();
            UnityEngine.Object.DestroyImmediate(_owner);
            GameSignals.ResetAll();
            var runner = UnityEngine.Object.FindAnyObjectByType<PlaylistRunner>();
            if (runner != null)
            {
                UnityEngine.Object.DestroyImmediate(runner.gameObject);
            }

            // Edit Mode の残りのテストとエディターのメニューでも、サンプルの登録があるままにする
            MainMenuPanels.Clear();
            DefaultMenuScreens.RegisterMainMenuPanels();
        }

        [Test]
        public void TheSampleRegistersEveryScreenSoEveryButtonIsShown()
        {
            _screen.Bind(_root);

            foreach (var kind in MainMenuPanels.Kinds)
            {
                Assert.That(MainMenuPanels.IsRegistered(kind), Is.True, kind.ToString());
                Assert.That(_screen.IsButtonShown(MainMenuScreen.PanelButtonName(kind)), Is.True, kind.ToString());
            }
        }

        [TestCase(MainMenuPanelKind.Leaderboard, typeof(LeaderboardPanel))]
        [TestCase(MainMenuPanelKind.Replays, typeof(ReplayPanel))]
        [TestCase(MainMenuPanelKind.ServerBrowser, typeof(ServerBrowserPanel))]
        public void AScreenWithoutItsServiceStillOpensAndBackReturnsToItsButton(MainMenuPanelKind kind, Type expected)
        {
            _screen.Bind(_root);

            _screen.OpenPanel(kind);

            Assert.That(_screen.OpenPanelKind, Is.EqualTo(kind));
            Assert.That(_screen.Panel, Is.InstanceOf(expected));
            Assert.That(_screen.PanelSlot.childCount, Is.GreaterThan(0));

            _screen.ClosePanel();

            Assert.That(_screen.IsPanelOpen, Is.False);
            Assert.That(_screen.PanelSlot.childCount, Is.Zero);
            Assert.That(MainMenuScreen.ButtonNames[_screen.FocusIndex], Is.EqualTo(MainMenuScreen.PanelButtonName(kind)));
        }

        [Test]
        public void ThePlaylistsOpenWithTheTunedListAndBackReturnsToTheirButton()
        {
            _screen.Bind(_root);

            _screen.OpenPanel(MainMenuPanelKind.Playlists);

            Assert.That(_screen.OpenPanelKind, Is.EqualTo(MainMenuPanelKind.Playlists));
            var playlists = (PlaylistPanel)_screen.Panel;
            Assert.That(playlists.Container, Is.SameAs(_screen.PanelSlot));
            Assert.That(playlists.Playlists, Is.SameAs(KitTuning.Playlists));
            Assert.That(playlists.Count, Is.EqualTo(KitTuning.Playlists.Count));
            Assert.That(playlists.Runner, Is.Not.Null);
            Assert.That(playlists.Runner, Is.SameAs(PlaylistPanel.MenuRunner()), "the runner is reused rather than added again");
            Assert.That(playlists.Runner.IsRunning, Is.False, "opening the list does not start a playlist");

            playlists.Cancel();

            Assert.That(_screen.IsPanelOpen, Is.False);
            Assert.That(_screen.PanelSlot.childCount, Is.Zero);
            Assert.That(MainMenuScreen.ButtonNames[_screen.FocusIndex], Is.EqualTo(MainMenuScreen.PlaylistsButtonName));
        }

        [Test]
        public void TheStatsAndCosmeticsOpenFromTheInstalledProfile()
        {
            var recorder = ProgressionBootstrap.Install(
                new ProgressionRecorder(new MemorySaveStorage(), ProgressionRecorder.LoadSpec(), new OfflineParticipants()));
            _screen.Bind(_root);

            _screen.OpenPanel(MainMenuPanelKind.Stats);

            var stats = (StatsPanel)_screen.Panel;
            Assert.That(stats.Profile, Is.SameAs(recorder.Profile));
            Assert.That(stats.StatLines, Has.Count.EqualTo(8));
            _screen.ClosePanel();

            _screen.OpenPanel(MainMenuPanelKind.Cosmetics);

            var cosmetics = (CosmeticsPanel)_screen.Panel;
            Assert.That(cosmetics.Locker, Is.SameAs(ProgressionBootstrap.Cosmetics));
            _screen.ClosePanel();
            Assert.That(_screen.IsPanelOpen, Is.False);
        }

        [Test]
        public void WithoutAProfileTheStatsAndCosmeticsShowANotice()
        {
            _screen.Bind(_root);
            LogAssert.Expect(LogType.Error, new Regex(@"^\[Menu\] cannot open the Stats screen: no progression recorder is installed \(ProgressionBootstrap\)"));

            _screen.OpenPanel(MainMenuPanelKind.Stats);

            Assert.That(_screen.IsPanelOpen, Is.False);
            Assert.That(_screen.Notice, Does.Contain("no progression recorder is installed (ProgressionBootstrap)"));

            LogAssert.Expect(LogType.Error, new Regex(@"^\[Menu\] cannot open the Cosmetics screen: no cosmetic locker is published \(GameSignals\.Cosmetics\)"));

            _screen.OpenPanel(MainMenuPanelKind.Cosmetics);

            Assert.That(_screen.IsPanelOpen, Is.False);
            Assert.That(_screen.Buttons.All(b => b.enabledSelf), Is.True);
        }

        [Test]
        public void ARegistrationFromTheGameSurvivesTheSampleDefaultsBeingRegisteredAgain()
        {
            Func<VisualElement, IMainMenuPanel> custom = slot => null;
            MainMenuPanels.Register(MainMenuPanelKind.Stats, custom);

            DefaultMenuScreens.RegisterMainMenuPanels();

            Assert.That(MainMenuPanels.TryGet(MainMenuPanelKind.Stats, out var open), Is.True);
            Assert.That(open, Is.SameAs(custom));
        }

        [Test]
        public void TheStashShowsWhatTheMatchPublished()
        {
            GameSignals.Stash.Publish(new StashStatus(12, Array.Empty<StashEntry>()));
            _screen.Bind(_root);

            _screen.OpenPanel(MainMenuPanelKind.Stash);

            var stash = (StashPanel)_screen.Panel;
            Assert.That(stash.Status.Capacity, Is.EqualTo(12));
            Assert.That(stash.SlotsText, Is.EqualTo(StashPanel.Slots(0, 12)));
            Assert.That(stash.ItemLines, Is.EqualTo(new[] { StashPanel.Empty }));
        }
    }
}
