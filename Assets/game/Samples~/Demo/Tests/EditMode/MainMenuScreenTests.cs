using System;
using System.Linq;
using System.Text.RegularExpressions;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Shooter.Tests.EditMode
{
    public sealed class MainMenuScreenTests
    {
        // 試合 1 回で 250 XP、レベル 2 は 500 XP から。SMG はレベル 2 で使えるようになる
        private static readonly ProgressionSpec _profileSpec = new ProgressionSpec(
            100, 0, 0, 250, 0, new[] { 0, 500, 1500 }, new[] { new ProgressionUnlock(LoadoutTestAssets.SmgId, 2) });

        private GameObject _owner;
        private MainMenuScreen _screen;
        private VisualElement _root;
        private RecordingHost _host;
        private LoadoutPreferenceStore _store;

        [SetUp]
        public void SetUp()
        {
            var layout = Resources.Load<VisualTreeAsset>(MainMenuScreen.LayoutResourcePath);
            Assert.That(layout, Is.Not.Null, "Resources/" + MainMenuScreen.LayoutResourcePath + ".uxml is missing");
            _root = layout.CloneTree();
            _owner = new GameObject("MainMenuScreenTests");
            _screen = _owner.AddComponent<MainMenuScreen>();
            _host = new RecordingHost();
            MenuNavigator.Host = _host;
            MenuNavigator.Scenes = MenuScenes.Default;
            MenuNavigator.SceneRoot = KitPaths.DefaultRoot;

            // 本物の選択 (LoadoutPreferenceStore.DefaultKey) を壊さないよう、テストごとに別のキーを使う
            _store = new LoadoutPreferenceStore("Shooter.Tests.MainMenuLoadout." + Guid.NewGuid().ToString("N"));
            _screen.LoadoutStore = _store;

            // 本物のセーブ (persistentDataPath) を読み書きしないよう、キャンペーンのテストはメモリの置き場所を使う
            _screen.SaveStorage = new MemorySaveStorage();
            CampaignLaunchSelection.Clear();
            ProgressionBootstrap.Uninstall();
            CombatEvents.ClearSubscribers();

            // オフラインだけのビルドと同じく、パーティと参加コードの口が無い状態から始める
            GameSignals.PartyControl.Clear();
            GameSignals.JoinByCode.Clear();
            MainMenuPanels.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            ProgressionBootstrap.Uninstall();
            CombatEvents.ClearSubscribers();
            _store.Delete();
            MenuNavigator.Host = null;
            MenuNavigator.Scenes = null;
            MenuNavigator.SceneRoot = null;
            MenuNavigator.TakeTutorialRequest();
            MenuNavigator.TakeReturnReason();
            CampaignLaunchSelection.Clear();
            UnityEngine.Object.DestroyImmediate(_owner);
            GameSignals.ResetAll();

            // サンプルの登録 (DefaultMenuScreens) を戻し、残りのテストとエディターのメニューから画面を消したままにしない
            MainMenuPanels.Clear();
            DefaultMenuScreens.RegisterMainMenuPanels();
        }

        [Test]
        public void TheTutorialButtonSitsNextToTheRangeAndOpensTheRangeWithTheTutorial()
        {
            var names = MainMenuScreen.ButtonNames.ToList();
            Assert.That(names.IndexOf(MainMenuScreen.TutorialButtonName), Is.EqualTo(names.IndexOf(MainMenuScreen.RangeButtonName) + 1));

            _screen.Bind(_root);

            Assert.That(_screen.Choose(MenuDestination.Tutorial), Is.True);

            Assert.That(_host.Loaded, Is.EqualTo(new[] { MenuRoute.RangeScene }));
            Assert.That(MenuNavigator.TakeTutorialRequest(), Is.True);
        }

        [Test]
        public void TheLayoutAndThemeAreInResources()
        {
            Assert.That(Resources.Load<ThemeStyleSheet>(MainMenuScreen.ThemeResourcePath), Is.Not.Null);
        }

        [Test]
        public void BindFindsTheButtonsInFocusOrder()
        {
            _screen.Bind(_root);

            Assert.That(_screen.Buttons.Select(b => b.name), Is.EqualTo(MainMenuScreen.ButtonNames));
            Assert.That(_screen.Buttons.Select(b => b.tabIndex), Is.EqualTo(Enumerable.Range(0, MainMenuScreen.ButtonNames.Count)));
            Assert.That(_screen.SettingsSlot, Is.Not.Null);
            Assert.That(_screen.SettingsSlot.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(_screen.Notice, Is.Null);
            Assert.That(_screen.FocusIndex, Is.EqualTo(-1));
        }

        [Test]
        public void TheButtonsAreLaidOutInTheSameOrder()
        {
            var inLayout = _root.Query<Button>().ToList().Select(b => b.name);

            Assert.That(inLayout, Is.EqualTo(MainMenuScreen.ButtonNames), "top-to-bottom order matches the gamepad focus order");
        }

        [Test]
        public void EveryDestinationHasAButton()
        {
            // 設定・ロードアウト・プロフィール・パーティ・コードで入る・登録した画面 5 つは遷移ではないので行き先の一覧には無く、ボタンだけがある
            Assert.That(MainMenuScreen.ButtonNames.Count, Is.EqualTo(MenuRoute.Destinations.Count + 10));
            foreach (var kind in MainMenuPanels.Kinds)
            {
                Assert.That(MainMenuScreen.ButtonNames, Has.Member(MainMenuScreen.PanelButtonName(kind)));
            }

            Assert.That(MainMenuScreen.ButtonNames, Has.Member(MainMenuScreen.SettingsButtonName));
            Assert.That(MainMenuScreen.ButtonNames, Has.Member(MainMenuScreen.LoadoutButtonName));
            Assert.That(MainMenuScreen.ButtonNames, Has.Member(MainMenuScreen.ProfileButtonName));
            Assert.That(MainMenuScreen.ButtonNames, Has.Member(MainMenuScreen.PartyButtonName));
            Assert.That(MainMenuScreen.ButtonNames, Has.Member(MainMenuScreen.JoinByCodeButtonName));
            Assert.That(MainMenuScreen.ButtonNames, Is.Unique);
        }

        [Test]
        public void BindRejectsALayoutWithoutTheButtons()
        {
            var empty = new VisualElement();

            Assert.Throws<InvalidOperationException>(() => _screen.Bind(empty));
            Assert.Throws<ArgumentNullException>(() => _screen.Bind(null));
            Assert.That(_screen.Buttons, Is.Empty);
        }

        [Test]
        public void MoveFocusFollowsTheButtonOrderAndWraps()
        {
            _screen.Bind(_root);

            _screen.MoveFocus(1);
            Assert.That(_screen.FocusIndex, Is.EqualTo(0), "the first move from nothing selects the top button");
            _screen.MoveFocus(1);
            Assert.That(_screen.FocusIndex, Is.EqualTo(1));
            _screen.MoveFocus(1);
            Assert.That(_screen.FocusIndex, Is.EqualTo(4), "the hidden party and join-by-code buttons are skipped on the way to the loadout");
            Assert.That(MainMenuScreen.ButtonNames[4], Is.EqualTo(MainMenuScreen.LoadoutButtonName));
            _screen.MoveFocus(-1);
            Assert.That(_screen.FocusIndex, Is.EqualTo(1), "and on the way back to quick match");
            _screen.MoveFocus(-1);
            _screen.MoveFocus(-1);
            Assert.That(_screen.FocusIndex, Is.EqualTo(MainMenuScreen.ButtonNames.Count - 1), "moving up from the top wraps to quit");
        }

        [Test]
        public void AnOfflineOnlyBuildHidesThePartyAndJoinByCodeButtons()
        {
            _screen.Bind(_root);

            Assert.That(_screen.IsButtonShown(MainMenuScreen.PartyButtonName), Is.False);
            Assert.That(_screen.IsButtonShown(MainMenuScreen.JoinByCodeButtonName), Is.False);
            Assert.That(_root.Q<Button>(MainMenuScreen.PartyButtonName).style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(_root.Q<Button>(MainMenuScreen.JoinByCodeButtonName).style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(_screen.IsButtonShown(MainMenuScreen.QuickMatchButtonName), Is.True);

            _screen.OpenParty();
            _screen.OpenCustomMatch();

            Assert.That(_screen.IsPartyOpen, Is.False, "nothing opens without the party service");
            Assert.That(_screen.IsCustomMatchOpen, Is.False, "nothing opens without the join-by-code service");
            Assert.That(_screen.PartySlot.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(_screen.CustomMatchSlot.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(_screen.Buttons.All(b => b.enabledSelf), Is.True);
        }

        [Test]
        public void TheOnlineServicesShowTheButtonsNextToQuickMatchInFocusOrder()
        {
            GameSignals.PartyControl.Publish(new FakePartyControl());
            GameSignals.JoinByCode.Publish(new FakeJoinByCode());
            _screen.Bind(_root);

            Assert.That(_screen.IsButtonShown(MainMenuScreen.PartyButtonName), Is.True);
            Assert.That(_screen.IsButtonShown(MainMenuScreen.JoinByCodeButtonName), Is.True);

            _screen.MoveFocus(1);
            _screen.MoveFocus(1);
            _screen.MoveFocus(1);
            Assert.That(MainMenuScreen.ButtonNames[_screen.FocusIndex], Is.EqualTo(MainMenuScreen.PartyButtonName));
            _screen.MoveFocus(1);
            Assert.That(MainMenuScreen.ButtonNames[_screen.FocusIndex], Is.EqualTo(MainMenuScreen.JoinByCodeButtonName));
            _screen.MoveFocus(1);
            Assert.That(MainMenuScreen.ButtonNames[_screen.FocusIndex], Is.EqualTo(MainMenuScreen.LoadoutButtonName));
        }

        [Test]
        public void TheButtonsFollowTheServicesWhileTheMenuIsShown()
        {
            _screen.Bind(_root);
            var party = new FakePartyControl();
            var joinByCode = new FakeJoinByCode();

            GameSignals.PartyControl.Publish(party);
            GameSignals.JoinByCode.Publish(joinByCode);

            Assert.That(_screen.IsButtonShown(MainMenuScreen.PartyButtonName), Is.True, "a service published after the menu opened shows its button");
            Assert.That(_screen.IsButtonShown(MainMenuScreen.JoinByCodeButtonName), Is.True);

            _screen.MoveFocus(1);
            _screen.MoveFocus(1);
            _screen.MoveFocus(1);
            Assert.That(MainMenuScreen.ButtonNames[_screen.FocusIndex], Is.EqualTo(MainMenuScreen.PartyButtonName));

            GameSignals.PartyControl.ClearIfCurrent(party);

            Assert.That(_screen.IsButtonShown(MainMenuScreen.PartyButtonName), Is.False);
            Assert.That(_screen.IsButtonShown(MainMenuScreen.JoinByCodeButtonName), Is.True);
            Assert.That(MainMenuScreen.ButtonNames[_screen.FocusIndex], Is.EqualTo(MainMenuScreen.JoinByCodeButtonName),
                "the focus leaves the hidden button for the next shown one");

            GameSignals.JoinByCode.ClearIfCurrent(joinByCode);

            Assert.That(_screen.IsButtonShown(MainMenuScreen.JoinByCodeButtonName), Is.False);
            Assert.That(MainMenuScreen.ButtonNames[_screen.FocusIndex], Is.EqualTo(MainMenuScreen.LoadoutButtonName));
        }

        [Test]
        public void PartyOpensInItsSlotAndBackReturnsToItsButton()
        {
            var party = new FakePartyControl();
            GameSignals.PartyControl.Publish(party);
            _screen.Bind(_root);
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Party\] asked to create the party"));

            _screen.OpenParty();

            Assert.That(_screen.IsPartyOpen, Is.True);
            Assert.That(_screen.PartyPanel.Container, Is.SameAs(_screen.PartySlot));
            Assert.That(_screen.PartySlot.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(_screen.PartyPanel.Control, Is.SameAs(party));
            Assert.That(_screen.Buttons.All(b => !b.enabledSelf), Is.True);
            Assert.That(_screen.Choose(MenuDestination.Range), Is.False, "the menu does not move while the party is open");
            _screen.OpenCustomMatch();
            Assert.That(_screen.IsCustomMatchOpen, Is.False, "only one overlay at a time");
            _screen.MoveFocus(1);
            Assert.That(_screen.FocusIndex, Is.EqualTo(-1), "the gamepad moves inside the party, not the menu");

            Assert.That(_screen.PartyPanel.Create(), Is.True);
            Assert.That(party.Calls, Is.EqualTo(new[] { "Create" }));

            _screen.PartyPanel.Cancel();

            Assert.That(_screen.IsPartyOpen, Is.False);
            Assert.That(_screen.PartySlot.childCount, Is.Zero);
            Assert.That(_screen.PartySlot.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(_screen.Buttons.All(b => b.enabledSelf), Is.True);
            Assert.That(_screen.FocusIndex, Is.EqualTo(MainMenuScreen.ButtonNames.ToList().IndexOf(MainMenuScreen.PartyButtonName)));
            Assert.That(_host.Loaded, Is.Empty);
        }

        [Test]
        public void JoinByCodeOpensInItsSlotAndBackReturnsToItsButton()
        {
            GameSignals.JoinByCode.Publish(new FakeJoinByCode());
            _screen.Bind(_root);

            _screen.OpenCustomMatch();

            Assert.That(_screen.IsCustomMatchOpen, Is.True);
            Assert.That(_screen.CustomMatchPanel.Container, Is.SameAs(_screen.CustomMatchSlot));
            Assert.That(_screen.CustomMatchSlot.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(_screen.Buttons.All(b => !b.enabledSelf), Is.True);
            Assert.That(_screen.Choose(MenuDestination.Range), Is.False, "the menu does not move while join by code is open");

            _screen.CustomMatchPanel.Cancel();

            Assert.That(_screen.IsCustomMatchOpen, Is.False);
            Assert.That(_screen.CustomMatchSlot.childCount, Is.Zero);
            Assert.That(_screen.CustomMatchSlot.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(_screen.Buttons.All(b => b.enabledSelf), Is.True);
            Assert.That(_screen.FocusIndex, Is.EqualTo(MainMenuScreen.ButtonNames.ToList().IndexOf(MainMenuScreen.JoinByCodeButtonName)));
        }

        [Test]
        public void AJoinedMatchKeepsTheMenuFromGoingElsewhereUntilTheSceneChanges()
        {
            var joinByCode = new FakeJoinByCode();
            GameSignals.JoinByCode.Publish(joinByCode);
            _screen.Bind(_root);
            _screen.OpenCustomMatch();
            _screen.CustomMatchPanel.CodeField.value = "ab7-k2x";
            LogAssert.Expect(LogType.Log, new Regex(@"^\[CustomMatch\] looking for a match with join code AB7-K2X"));
            LogAssert.Expect(LogType.Log, new Regex(@"^\[CustomMatch\] join code AB7-K2X: Joined"));
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Menu\] joined a match by code"));

            Assert.That(_screen.CustomMatchPanel.Join(), Is.True);
            joinByCode.Pending(JoinCodeOutcome.Joined);

            Assert.That(joinByCode.Codes, Is.EqualTo(new[] { "AB7-K2X" }));
            Assert.That(_screen.CustomMatchPanel.HasJoined, Is.True);

            _screen.CustomMatchPanel.Cancel();

            Assert.That(_screen.IsCustomMatchOpen, Is.True, "back does nothing while the joined match loads");
            Assert.That(_screen.Buttons.All(b => !b.enabledSelf), Is.True);

            _screen.CloseCustomMatch();

            Assert.That(_screen.IsCustomMatchOpen, Is.False);
            Assert.That(_screen.Buttons.All(b => !b.enabledSelf), Is.True, "no second destination while the joined match loads");
            Assert.That(_screen.Choose(MenuDestination.Range), Is.False);
            _screen.OpenParty();
            Assert.That(_screen.IsPartyOpen, Is.False);
            Assert.That(_host.Loaded, Is.Empty);
        }

        [Test]
        public void WithoutRegisteredScreensTheirButtonsAreHiddenAndSkipped()
        {
            _screen.Bind(_root);

            foreach (var kind in MainMenuPanels.Kinds)
            {
                var buttonName = MainMenuScreen.PanelButtonName(kind);
                Assert.That(_screen.IsButtonShown(buttonName), Is.False, buttonName);
                Assert.That(_root.Q<Button>(buttonName).style.display.value, Is.EqualTo(DisplayStyle.None), buttonName);
            }

            Assert.That(_screen.PanelSlot.style.display.value, Is.EqualTo(DisplayStyle.None));

            // プロフィール (5) の次は、隠れた 5 つ (6〜10) を飛ばして射撃場 (11)
            _screen.MoveFocus(1);
            _screen.MoveFocus(1);
            _screen.MoveFocus(1);
            _screen.MoveFocus(1);
            Assert.That(_screen.FocusIndex, Is.EqualTo(5));
            Assert.That(MainMenuScreen.ButtonNames[5], Is.EqualTo(MainMenuScreen.ProfileButtonName));
            _screen.MoveFocus(1);
            Assert.That(_screen.FocusIndex, Is.EqualTo(11));
            Assert.That(MainMenuScreen.ButtonNames[11], Is.EqualTo(MainMenuScreen.RangeButtonName));

            _screen.OpenPanel(MainMenuPanelKind.Stats);

            Assert.That(_screen.IsPanelOpen, Is.False, "nothing opens without a registered screen");
            Assert.That(_screen.OpenPanelKind, Is.Null);
            Assert.That(_screen.PanelSlot.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(_screen.Buttons.All(b => b.enabledSelf), Is.True);
            Assert.That(_screen.Notice, Is.Null);
        }

        [Test]
        public void TheScreenButtonsSitAfterTheProfileInTheirOwnOrder()
        {
            var names = MainMenuScreen.ButtonNames.ToList();
            var profile = names.IndexOf(MainMenuScreen.ProfileButtonName);

            Assert.That(names.GetRange(profile + 1, 5), Is.EqualTo(new[]
            {
                "menu-stats", "menu-leaderboard", "menu-replays", "menu-cosmetics", "menu-stash",
            }));
            Assert.That(MainMenuPanels.Kinds.Select(MainMenuScreen.PanelButtonName), Is.EqualTo(names.GetRange(profile + 1, 5)));
        }

        [Test]
        public void ARegisteredScreenShowsItsButtonOpensInTheSlotAndBackReturnsToItsButton()
        {
            FakeMenuPanel opened = null;
            MainMenuPanels.Register(MainMenuPanelKind.Leaderboard, slot => opened = new FakeMenuPanel(slot));
            _screen.Bind(_root);

            Assert.That(_screen.IsButtonShown(MainMenuScreen.LeaderboardButtonName), Is.True);
            Assert.That(_screen.IsButtonShown(MainMenuScreen.StatsButtonName), Is.False, "only the registered screen gets a button");

            _screen.OpenPanel(MainMenuPanelKind.Leaderboard);

            Assert.That(_screen.IsPanelOpen, Is.True);
            Assert.That(_screen.OpenPanelKind, Is.EqualTo(MainMenuPanelKind.Leaderboard));
            Assert.That(_screen.Panel, Is.SameAs(opened));
            Assert.That(opened.Container, Is.SameAs(_screen.PanelSlot));
            Assert.That(_screen.PanelSlot.childCount, Is.EqualTo(1));
            Assert.That(_screen.PanelSlot.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(_screen.Buttons.All(b => !b.enabledSelf), Is.True);
            Assert.That(_screen.Choose(MenuDestination.Range), Is.False, "the menu does not move while the screen is open");
            _screen.OpenProfile();
            Assert.That(_screen.IsProfileOpen, Is.False, "only one overlay at a time");
            _screen.MoveFocus(1);
            Assert.That(_screen.FocusIndex, Is.EqualTo(-1), "the gamepad moves inside the screen, not the menu");

            opened.Cancel();

            Assert.That(_screen.IsPanelOpen, Is.False);
            Assert.That(_screen.OpenPanelKind, Is.Null);
            Assert.That(opened.Disposals, Is.EqualTo(1));
            Assert.That(_screen.PanelSlot.childCount, Is.Zero, "the shared slot is emptied for the next screen");
            Assert.That(_screen.PanelSlot.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(_screen.Buttons.All(b => b.enabledSelf), Is.True);
            Assert.That(_screen.FocusIndex, Is.EqualTo(MainMenuScreen.ButtonNames.ToList().IndexOf(MainMenuScreen.LeaderboardButtonName)));
            Assert.That(_host.Loaded, Is.Empty);

            opened.Cancel();
            Assert.That(opened.Disposals, Is.EqualTo(1), "a closed screen no longer closes the menu's slot");
        }

        [Test]
        public void TheScreenButtonsFollowTheRegistrationsWhileTheMenuIsShown()
        {
            _screen.Bind(_root);

            MainMenuPanels.Register(MainMenuPanelKind.Stats, slot => new FakeMenuPanel(slot));
            MainMenuPanels.Register(MainMenuPanelKind.Stash, slot => new FakeMenuPanel(slot));

            Assert.That(_screen.IsButtonShown(MainMenuScreen.StatsButtonName), Is.True, "a screen registered after the menu opened shows its button");
            Assert.That(_screen.IsButtonShown(MainMenuScreen.StashButtonName), Is.True);
            Assert.That(_screen.IsButtonShown(MainMenuScreen.ReplaysButtonName), Is.False);

            var names = MainMenuScreen.ButtonNames.ToList();
            _screen.MoveFocus(1);
            _screen.MoveFocus(1);
            _screen.MoveFocus(1);
            _screen.MoveFocus(1);
            _screen.MoveFocus(1);
            Assert.That(_screen.FocusIndex, Is.EqualTo(names.IndexOf(MainMenuScreen.StatsButtonName)));

            Assert.That(MainMenuPanels.Unregister(MainMenuPanelKind.Stats), Is.True);

            Assert.That(_screen.IsButtonShown(MainMenuScreen.StatsButtonName), Is.False);
            Assert.That(_screen.FocusIndex, Is.EqualTo(names.IndexOf(MainMenuScreen.StashButtonName)),
                "the focus leaves the hidden button for the next shown one");
            Assert.That(MainMenuPanels.Unregister(MainMenuPanelKind.Stats), Is.False, "nothing is left to unregister");
        }

        [Test]
        public void UnregisteringHidesTheDefaultScreenButton()
        {
            MainMenuPanels.UseDefault(MainMenuPanelKind.Stats, slot => new FakeMenuPanel(slot));
            _screen.Bind(_root);
            Assert.That(_screen.IsButtonShown(MainMenuScreen.StatsButtonName), Is.True);

            Assert.That(MainMenuPanels.Unregister(MainMenuPanelKind.Stats), Is.True);
            Assert.That(MainMenuPanels.IsRegistered(MainMenuPanelKind.Stats), Is.False);
            Assert.That(_screen.IsButtonShown(MainMenuScreen.StatsButtonName), Is.False);
            Assert.That(MainMenuPanels.Unregister(MainMenuPanelKind.Stats), Is.False);

            MainMenuPanels.Register(MainMenuPanelKind.Stats, slot => new FakeMenuPanel(slot));
            Assert.That(_screen.IsButtonShown(MainMenuScreen.StatsButtonName), Is.True, "a new registration shows the button again");
        }

        [Test]
        public void AScreenStaysOpenWhenItIsUnregisteredUntilBackIsPressed()
        {
            FakeMenuPanel opened = null;
            MainMenuPanels.Register(MainMenuPanelKind.Replays, slot => opened = new FakeMenuPanel(slot));
            _screen.Bind(_root);
            _screen.OpenPanel(MainMenuPanelKind.Replays);

            MainMenuPanels.Unregister(MainMenuPanelKind.Replays);

            Assert.That(_screen.IsPanelOpen, Is.True);
            Assert.That(_screen.IsButtonShown(MainMenuScreen.ReplaysButtonName), Is.False);

            opened.Cancel();

            Assert.That(_screen.IsPanelOpen, Is.False);
            Assert.That(MainMenuScreen.ButtonNames[_screen.FocusIndex], Is.EqualTo(MainMenuScreen.RangeButtonName),
                "back lands on the next shown button because the screen's own button is gone");
        }

        [Test]
        public void AScreenThatFailsToOpenOnlyShowsANotice()
        {
            var applied = Localizer.Language;
            Localizer.Apply(GameLanguage.Japanese);
            try
            {
                MainMenuPanels.Register(MainMenuPanelKind.Cosmetics, slot =>
                {
                    slot.Add(new Label("half built"));
                    throw new InvalidOperationException("no cosmetic catalog");
                });
                MainMenuPanels.Register(MainMenuPanelKind.Stash, slot => null);
                _screen.Bind(_root);
                LogAssert.Expect(LogType.Error, new Regex(@"^\[Menu\] cannot open the Cosmetics screen: no cosmetic catalog"));

                _screen.OpenPanel(MainMenuPanelKind.Cosmetics);

                Assert.That(_screen.IsPanelOpen, Is.False);
                Assert.That(_screen.Notice, Is.EqualTo("この画面を開けません: no cosmetic catalog"));
                Assert.That(_screen.PanelSlot.childCount, Is.Zero);
                Assert.That(_screen.PanelSlot.style.display.value, Is.EqualTo(DisplayStyle.None));
                Assert.That(_screen.Buttons.All(b => b.enabledSelf), Is.True);
                Assert.That(_screen.FocusIndex, Is.EqualTo(MainMenuScreen.ButtonNames.ToList().IndexOf(MainMenuScreen.CosmeticsButtonName)));

                Localizer.Apply(GameLanguage.English);
                Assert.That(_screen.Notice, Is.EqualTo("This screen cannot be opened: no cosmetic catalog"));

                LogAssert.Expect(LogType.Error, new Regex(@"^\[Menu\] cannot open the Stash screen: the registered Stash screen returned nothing"));

                _screen.OpenPanel(MainMenuPanelKind.Stash);

                Assert.That(_screen.IsPanelOpen, Is.False);
                Assert.That(_screen.Notice, Is.EqualTo("This screen cannot be opened: the registered Stash screen returned nothing"));
                Assert.That(_screen.Buttons.All(b => b.enabledSelf), Is.True);
            }
            finally
            {
                Localizer.Apply(applied);
            }
        }

        [Test]
        public void RebindingDisposesTheOpenScreen()
        {
            FakeMenuPanel opened = null;
            MainMenuPanels.Register(MainMenuPanelKind.Stats, slot => opened = new FakeMenuPanel(slot));
            _screen.Bind(_root);
            _screen.OpenPanel(MainMenuPanelKind.Stats);

            _screen.Bind(_root);

            Assert.That(opened.Disposals, Is.EqualTo(1));
            Assert.That(_screen.IsPanelOpen, Is.False);
            Assert.That(_screen.PanelSlot.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(_screen.IsButtonShown(MainMenuScreen.StatsButtonName), Is.True, "the registration outlives the layout");
        }

        [Test]
        public void TheRegistryRejectsAMissingScreenAndReplacesARepeatedOne()
        {
            Assert.Throws<ArgumentNullException>(() => MainMenuPanels.Register(MainMenuPanelKind.Stats, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => MainMenuPanels.Register((MainMenuPanelKind)99, slot => null));
            Assert.Throws<ArgumentOutOfRangeException>(() => MainMenuScreen.PanelButtonName((MainMenuPanelKind)99));
            Assert.That(MainMenuPanels.IsRegistered(MainMenuPanelKind.Stats), Is.False);

            var changed = new System.Collections.Generic.List<MainMenuPanelKind>();
            Action<MainMenuPanelKind> record = changed.Add;
            MainMenuPanels.Changed += record;
            try
            {
                Func<VisualElement, IMainMenuPanel> first = slot => new FakeMenuPanel(slot);
                Func<VisualElement, IMainMenuPanel> second = slot => new FakeMenuPanel(slot);
                MainMenuPanels.Register(MainMenuPanelKind.Stats, first);
                MainMenuPanels.Register(MainMenuPanelKind.Stats, second);

                Assert.That(MainMenuPanels.TryGet(MainMenuPanelKind.Stats, out var registered), Is.True);
                Assert.That(registered, Is.SameAs(second));

                MainMenuPanels.Clear();

                Assert.That(MainMenuPanels.IsRegistered(MainMenuPanelKind.Stats), Is.False);
                Assert.That(changed, Is.EqualTo(new[] { MainMenuPanelKind.Stats, MainMenuPanelKind.Stats, MainMenuPanelKind.Stats }));
            }
            finally
            {
                MainMenuPanels.Changed -= record;
            }
        }

        [Test]
        public void ChooseGoesToTheDestinationAndDisablesTheButtons()
        {
            _screen.Bind(_root);

            Assert.That(_screen.Choose(MenuDestination.Range), Is.True);

            Assert.That(_host.Loaded, Is.EqualTo(new[] { MenuRoute.RangeScene }));
            Assert.That(_screen.Buttons.All(b => !b.enabledSelf), Is.True, "no second press while the scene loads");
            Assert.That(_screen.Choose(MenuDestination.Campaign), Is.False);
            Assert.That(_host.Loaded.Count(), Is.EqualTo(1));
        }

        [Test]
        public void ChooseShowsANoticeAndStaysUsableWhenTheSceneIsMissing()
        {
            _screen.Bind(_root);
            _host.Missing = MenuRoute.CampaignScene;
            LogAssert.Expect(LogType.Error, new Regex(Regex.Escape(MenuRoute.CampaignScene)));

            Assert.That(_screen.Choose(MenuDestination.Campaign), Is.False);

            Assert.That(_screen.Notice, Does.Contain(MenuRoute.CampaignScene));
            Assert.That(_screen.Buttons.All(b => b.enabledSelf), Is.True);
            Assert.That(_screen.Choose(MenuDestination.Range), Is.True);
        }

        [Test]
        public void ChooseGoesToTheCampaignSceneSetByTheData()
        {
            const string chapter = "Assets/game/Scenes/Chapter1.unity";
            _host.Scenes.Add(chapter);
            MenuNavigator.Scenes = new MenuScenes(null, null, "Chapter1");
            _screen.Bind(_root);

            Assert.That(_screen.Choose(MenuDestination.Campaign), Is.True);

            Assert.That(_host.Loaded, Is.EqualTo(new[] { chapter }));
        }

        [Test]
        public void TheMissingSceneNoticeNamesTheSceneSetByTheData()
        {
            const string chapter = "Assets/game/Scenes/Chapter1.unity";
            MenuNavigator.Scenes = new MenuScenes(null, null, "Chapter1");
            _screen.Bind(_root);
            LogAssert.Expect(LogType.Error, new Regex(Regex.Escape(chapter)));

            Assert.That(_screen.Choose(MenuDestination.Campaign), Is.False);

            Assert.That(_screen.Notice, Does.Contain(chapter));
            Assert.That(_host.Loaded, Is.Empty);
        }

        [Test]
        public void AVersionMismatchNamesBothVersionsAndAFailedStartShowsItsDetail()
        {
            var applied = Localizer.Language;
            Localizer.Apply(GameLanguage.Japanese);
            try
            {
                _screen.Bind(_root);

                _screen.ShowReturnReason(MenuReturnReason.Disconnected(
                    DisconnectNotice.Classify(DisconnectNotice.VersionMismatchReason("0.2.0"), false, "0.1.0"), 5));
                Assert.That(_screen.Notice, Is.EqualTo("版が違います (サーバー 0.2.0 / このゲーム 0.1.0)。サーバーと同じ版のゲームでつないでください。"));

                _screen.ShowReturnReason(MenuReturnReason.OnlineFailed(7, "[QuickMatch] no lobby was found"));
                Assert.That(_screen.Notice, Is.EqualTo("[QuickMatch] no lobby was found"));

                _screen.ShowReturnReason(null);
                Assert.That(_screen.Notice, Is.Null);
            }
            finally
            {
                Localizer.Apply(applied);
            }
        }

        [Test]
        public void ADisconnectReasonIsLookedUpByTheMenuAndFollowsTheLanguage()
        {
            var applied = Localizer.Language;
            Localizer.Apply(GameLanguage.Japanese);
            try
            {
                _screen.Bind(_root);

                _screen.ShowReturnReason(
                    MenuReturnReason.Disconnected(DisconnectNotice.Classify(DisconnectNotice.ServerStoppedReason, true, "0.1.0"), 0));
                Assert.That(_screen.Notice, Is.EqualTo("サーバーが止まりました。"));

                Localizer.Apply(GameLanguage.English);
                Assert.That(_screen.Notice, Is.EqualTo("The server stopped."));

                _screen.ShowNotice((string)null);
                Localizer.Apply(GameLanguage.Japanese);
                Assert.That(_screen.Notice, Is.Null, "a hidden notice stays hidden after the language changes");
            }
            finally
            {
                Localizer.Apply(applied);
            }
        }

        [Test]
        public void SettingsWithoutAScreenOnlyShowsANotice()
        {
            _screen.Bind(_root);

            _screen.OpenSettings();

            Assert.That(_screen.IsSettingsOpen, Is.False);
            Assert.That(_screen.Notice, Is.EqualTo(MainMenuScreen.SettingsUnavailableNotice));
            Assert.That(_host.Loaded, Is.Empty);
        }

        [Test]
        public void SettingsHandsTheSlotToTheSettingsScreenAndClosesBackToTheMenu()
        {
            _screen.Bind(_root);
            VisualElement received = null;
            _screen.SettingsRequested += slot =>
            {
                received = slot;
                slot.Add(new Label("volume"));
            };

            _screen.OpenSettings();

            Assert.That(received, Is.SameAs(_screen.SettingsSlot));
            Assert.That(_screen.IsSettingsOpen, Is.True);
            Assert.That(_screen.SettingsSlot.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(_screen.Choose(MenuDestination.Range), Is.False, "the menu does not move while settings are open");

            _screen.CloseSettings();

            Assert.That(_screen.IsSettingsOpen, Is.False);
            Assert.That(_screen.SettingsSlot.childCount, Is.Zero);
            Assert.That(_screen.SettingsSlot.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(_screen.FocusIndex, Is.EqualTo(MainMenuScreen.ButtonNames.ToList().IndexOf(MainMenuScreen.SettingsButtonName)));
            Assert.That(_screen.Buttons.All(b => b.enabledSelf), Is.True);
        }

        [Test]
        public void LoadoutWithoutAssetsOnlyShowsANotice()
        {
            _screen.Bind(_root);
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Loadout\] cannot open the loadout screen"));

            _screen.OpenLoadout();

            Assert.That(_screen.IsLoadoutOpen, Is.False);
            Assert.That(_screen.Notice, Does.StartWith(MainMenuScreen.LoadoutUnavailableNotice(string.Empty)));
            Assert.That(_screen.LoadoutSlot.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(_screen.Buttons.All(b => b.enabledSelf), Is.True);
        }

        [Test]
        public void LoadoutShowsTheSlotsAndSavesTheChoice()
        {
            using (var assets = new LoadoutTestAssets())
            {
                _screen.Loadout = assets.Loadout;
                _screen.Catalog = assets.Catalog;
                _screen.Bind(_root);

                _screen.OpenLoadout();

                Assert.That(_screen.IsLoadoutOpen, Is.True);
                Assert.That(_screen.LoadoutPanel.Container, Is.SameAs(_screen.LoadoutSlot));
                Assert.That(_screen.LoadoutSlot.style.display.value, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(_screen.Buttons.All(b => !b.enabledSelf), Is.True);
                Assert.That(_screen.Choose(MenuDestination.Range), Is.False, "the menu does not move while the loadout is open");
                Assert.That(_host.Loaded, Is.Empty);

                _screen.LoadoutPanel.Select(0, 1);
                Assert.That(_screen.LoadoutPanel.Confirm(), Is.True);

                var expected = new[] { LoadoutTestAssets.SmgId, TestWeapons.HandgunId };
                Assert.That(_store.Load(), Is.EqualTo(expected));
                Assert.That(_screen.IsLoadoutOpen, Is.False);
                Assert.That(_screen.LoadoutSlot.childCount, Is.Zero);
                Assert.That(_screen.LoadoutSlot.style.display.value, Is.EqualTo(DisplayStyle.None));
                Assert.That(_screen.Notice, Is.EqualTo(LoadoutFormat.Saved));
                Assert.That(_screen.FocusIndex, Is.EqualTo(MainMenuScreen.ButtonNames.ToList().IndexOf(MainMenuScreen.LoadoutButtonName)));
                Assert.That(_screen.Buttons.All(b => b.enabledSelf), Is.True);

                _screen.OpenLoadout();
                Assert.That(_screen.LoadoutPanel.Selection.WeaponIds, Is.EqualTo(expected));
                Assert.That(_screen.LoadoutPanel.Selection.RestoredPreference, Is.True);
            }
        }

        [Test]
        public void ClosingTheLoadoutWithoutConfirmingSavesNothing()
        {
            using (var assets = new LoadoutTestAssets())
            {
                _screen.Loadout = assets.Loadout;
                _screen.Catalog = assets.Catalog;
                _screen.Bind(_root);
                _screen.OpenLoadout();
                _screen.LoadoutPanel.Select(0, 1);

                _screen.CloseLoadout();

                Assert.That(_store.Load(), Is.Null);
                Assert.That(_screen.IsLoadoutOpen, Is.False);
                Assert.That(_screen.Notice, Is.Null);
            }
        }

        [Test]
        public void ProfileOpensWithTheRecordedLevelAndStatsAndClosesBackToItsButton()
        {
            var storage = new MemorySaveStorage();
            new ProfileStore(storage).Save(new PlayerProfile(600, 3, 1, 2, 1));
            var recorder = ProgressionBootstrap.Install(new ProgressionRecorder(storage, _profileSpec, new OfflineParticipants()));
            _screen.Bind(_root);

            _screen.OpenProfile();

            Assert.That(_screen.IsProfileOpen, Is.True);
            Assert.That(_screen.ProfilePanel.Container, Is.SameAs(_screen.ProfileSlot));
            Assert.That(_screen.ProfileSlot.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(_screen.ProfilePanel.Profile, Is.EqualTo(recorder.Profile));
            Assert.That(_screen.ProfilePanel.Level, Is.EqualTo(2));
            Assert.That(_screen.ProfilePanel.LevelText, Is.EqualTo(ProgressionFormat.Level(2)));
            Assert.That(_screen.ProfilePanel.TotalXpText, Is.EqualTo(ProgressionFormat.TotalXp(600)));
            Assert.That(_screen.ProfilePanel.StatLines, Has.Member(ProgressionFormat.Kills(3)));
            Assert.That(_screen.ProfilePanel.StatLines, Has.Member(ProgressionFormat.Matches(2)));
            Assert.That(_screen.Buttons.All(b => !b.enabledSelf), Is.True);
            Assert.That(_screen.Choose(MenuDestination.Range), Is.False, "the menu does not move while the profile is open");
            _screen.OpenLoadout();
            Assert.That(_screen.IsLoadoutOpen, Is.False, "only one overlay at a time");

            _screen.ProfilePanel.Cancel();

            Assert.That(_screen.IsProfileOpen, Is.False);
            Assert.That(_screen.ProfileSlot.childCount, Is.Zero);
            Assert.That(_screen.ProfileSlot.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(_screen.Buttons.All(b => b.enabledSelf), Is.True);
            Assert.That(_screen.FocusIndex, Is.EqualTo(MainMenuScreen.ButtonNames.ToList().IndexOf(MainMenuScreen.ProfileButtonName)));
        }

        [Test]
        public void AProfileWithoutASaveOpensAtLevelOne()
        {
            ProgressionBootstrap.Install(new ProgressionRecorder(new MemorySaveStorage(), _profileSpec, new OfflineParticipants()));
            _screen.Bind(_root);

            _screen.OpenProfile();

            Assert.That(_screen.IsProfileOpen, Is.True);
            Assert.That(_screen.ProfilePanel.Profile, Is.EqualTo(PlayerProfile.Empty));
            Assert.That(_screen.ProfilePanel.Level, Is.EqualTo(1));
        }

        [Test]
        public void ProfileWithoutARecorderOnlyShowsANotice()
        {
            _screen.Bind(_root);
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Menu\] cannot open the profile"));

            _screen.OpenProfile();

            Assert.That(_screen.IsProfileOpen, Is.False);
            Assert.That(_screen.Notice, Is.EqualTo(MainMenuScreen.ProfileUnavailableNotice));
            Assert.That(_screen.ProfileSlot.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(_screen.Buttons.All(b => b.enabledSelf), Is.True);
        }

        [Test]
        public void ALockedWeaponCannotBeChosenInTheLoadoutUntilTheLevelIsReached()
        {
            using (var assets = new LoadoutTestAssets())
            {
                var recorder = ProgressionBootstrap.Install(new ProgressionRecorder(new MemorySaveStorage(), _profileSpec, new OfflineParticipants()));
                _screen.Loadout = assets.Loadout;
                _screen.Catalog = assets.Catalog;
                _screen.Bind(_root);

                _screen.OpenLoadout();

                var selection = _screen.LoadoutPanel.Selection;
                Assert.That(selection.OptionsAt(0)[1].Id, Is.EqualTo(LoadoutTestAssets.SmgId));
                Assert.That(selection.IsAvailableAt(0, 1), Is.False, "the SMG needs level 2");
                Assert.That(_screen.LoadoutPanel.Select(0, 1), Is.False);
                Assert.That(selection.WeaponIds, Is.EqualTo(new[] { TestWeapons.RifleId, TestWeapons.HandgunId }));
                _screen.CloseLoadout();

                // 試合 2 回 (250 XP ずつ) でレベル 2 になる
                CombatEvents.RaiseMatchFinished(new MatchResult(MatchOutcome.Lost, 0, 0, 0));
                CombatEvents.RaiseMatchFinished(new MatchResult(MatchOutcome.Lost, 0, 0, 0));
                Assert.That(recorder.Level, Is.EqualTo(2));

                _screen.OpenLoadout();

                Assert.That(_screen.LoadoutPanel.Selection.IsAvailableAt(0, 1), Is.True);
                Assert.That(_screen.LoadoutPanel.Select(0, 1), Is.True);
                Assert.That(_screen.LoadoutPanel.Confirm(), Is.True);
                Assert.That(_store.Load(), Is.EqualTo(new[] { LoadoutTestAssets.SmgId, TestWeapons.HandgunId }));
            }
        }

        [Test]
        public void TheProgressionSetOnTheScreenWinsOverTheInstalledOne()
        {
            var installed = ProgressionBootstrap.Install(new ProgressionRecorder(new MemorySaveStorage(), _profileSpec, new OfflineParticipants()));
            Assert.That(_screen.Progression, Is.SameAs(installed));

            var own = new ProgressionRecorder(new MemorySaveStorage(), _profileSpec, new OfflineParticipants());
            _screen.Progression = own;
            Assert.That(_screen.Progression, Is.SameAs(own));

            _screen.Progression = null;
            Assert.That(_screen.Progression, Is.SameAs(installed));
        }

        [Test]
        public void CampaignWithoutACatalogOnlyShowsANotice()
        {
            _screen.Bind(_root);
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Menu\] cannot open the campaign"));

            _screen.OpenCampaign();

            Assert.That(_screen.IsCampaignOpen, Is.False);
            Assert.That(_screen.Notice, Does.StartWith(CampaignMenuFormat.Unavailable(string.Empty)));
            Assert.That(_screen.CampaignSlot.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(_screen.Buttons.All(b => b.enabledSelf), Is.True);
        }

        [Test]
        public void CampaignListsTheMissionsAndStartsTheChosenOneAtTheChosenDifficulty()
        {
            using (var missions = new CampaignTestCatalog())
            {
                var storage = UseCampaign(missions);

                _screen.OpenCampaign();

                var panel = _screen.CampaignPanel;
                Assert.That(_screen.IsCampaignOpen, Is.True);
                Assert.That(panel.Container, Is.SameAs(_screen.CampaignSlot));
                Assert.That(_screen.CampaignSlot.style.display.value, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(_screen.Buttons.All(b => !b.enabledSelf), Is.True);
                Assert.That(_screen.Choose(MenuDestination.Range), Is.False, "the menu does not move while the campaign is open");
                Assert.That(panel.Menu.Missions.Select(m => m.DisplayName),
                    Is.EqualTo(new[] { CampaignTestCatalog.Mission01Name, CampaignTestCatalog.Mission02Name }));
                Assert.That(CampaignTestCatalog.Mission01Name, Is.Not.EqualTo(CampaignTestCatalog.Mission01NameKey), "the name comes from the table, not the key");
                Assert.That(panel.Menu.Difficulty, Is.EqualTo(CampaignDifficulty.Normal));
                Assert.That(panel.IsShown(CampaignMenuItemKind.Start), Is.True);
                Assert.That(panel.IsShown(CampaignMenuItemKind.Continue), Is.False);
                Assert.That(panel.HasSaveBadge(0), Is.False);

                // ゲームパッドと同じ順: ミッションで決定 → 難易度を左へ (Easy) → 決定 → 「始める」で決定
                panel.Submit();
                Assert.That(panel.CycleDifficulty(-1), Is.True);
                panel.Submit();
                Assert.That(panel.Menu.FocusedItem.Kind, Is.EqualTo(CampaignMenuItemKind.Start));
                panel.Submit();

                Assert.That(_host.Loaded, Is.EqualTo(new[] { MenuRoute.CampaignScene }));
                Assert.That(CampaignLaunchSelection.Current.MissionId, Is.EqualTo(CampaignTestCatalog.Mission01));
                Assert.That(CampaignLaunchSelection.Current.Difficulty, Is.EqualTo(CampaignDifficulty.Easy));
                Assert.That(panel.IsBusy, Is.True, "no second press while the scene loads");
                Assert.That(storage.Slots, Is.Empty);
            }
        }

        [Test]
        public void ASavedMissionOffersContinueWhichKeepsTheSave()
        {
            using (var missions = new CampaignTestCatalog())
            {
                var storage = UseCampaign(missions);
                var slot = CampaignSaveSlots.For(CampaignTestCatalog.Mission01);
                storage.Slots[slot] = CampaignTestCatalog.SaveJson(CampaignTestCatalog.Mission01, "area-a");

                _screen.OpenCampaign();

                var panel = _screen.CampaignPanel;
                Assert.That(panel.HasSaveBadge(0), Is.True);
                Assert.That(panel.HasSaveBadge(1), Is.False);
                Assert.That(panel.IsShown(CampaignMenuItemKind.Continue), Is.True);
                Assert.That(panel.IsShown(CampaignMenuItemKind.Restart), Is.True);
                Assert.That(panel.IsShown(CampaignMenuItemKind.Start), Is.False);

                panel.Press(CampaignMenuItemKind.Continue);

                Assert.That(_host.Loaded, Is.EqualTo(new[] { MenuRoute.CampaignScene }));
                Assert.That(storage.Slots.ContainsKey(slot), Is.True, "CampaignRunner restores from the save");
            }
        }

        [Test]
        public void StartOverAsksAndThenDeletesTheSaveBeforeLoading()
        {
            using (var missions = new CampaignTestCatalog())
            {
                var storage = UseCampaign(missions);
                storage.Slots[CampaignSaveSlots.For(CampaignTestCatalog.Mission01)] =
                    CampaignTestCatalog.SaveJson(CampaignTestCatalog.Mission01, "area-a");
                storage.Slots[CampaignSaveSlots.Legacy] = CampaignTestCatalog.SaveJson(CampaignTestCatalog.Mission01, "start");
                _screen.OpenCampaign();
                var panel = _screen.CampaignPanel;

                panel.Press(CampaignMenuItemKind.Restart);

                Assert.That(panel.Question, Does.Contain(CampaignTestCatalog.Mission01Name));
                Assert.That(panel.IsShown(CampaignMenuItemKind.ConfirmRestart), Is.True);
                Assert.That(panel.IsShown(CampaignMenuItemKind.Continue), Is.False);
                Assert.That(storage.Slots.Count(), Is.EqualTo(2), "nothing is deleted before the answer");
                Assert.That(_host.Loaded, Is.Empty);

                panel.Cancel();
                Assert.That(panel.Question, Is.Null);
                Assert.That(_screen.IsCampaignOpen, Is.True);

                panel.Press(CampaignMenuItemKind.Restart);
                panel.Press(CampaignMenuItemKind.ConfirmRestart);

                Assert.That(storage.Slots, Is.Empty, "the mission slot and the legacy slot are both gone");
                Assert.That(_host.Loaded, Is.EqualTo(new[] { MenuRoute.CampaignScene }));
            }
        }

        [Test]
        public void AFailedDeleteKeepsTheMenuOpenAndLoadsNothing()
        {
            using (var missions = new CampaignTestCatalog())
            {
                var storage = UseCampaign(missions);
                storage.Slots[CampaignSaveSlots.For(CampaignTestCatalog.Mission01)] =
                    CampaignTestCatalog.SaveJson(CampaignTestCatalog.Mission01, "area-a");
                storage.FailDeletes = true;
                _screen.OpenCampaign();
                var panel = _screen.CampaignPanel;
                LogAssert.Expect(LogType.Error, new Regex(@"^\[Campaign\] could not delete the save of mission=Campaign01"));

                panel.Press(CampaignMenuItemKind.Restart);
                panel.Press(CampaignMenuItemKind.ConfirmRestart);

                Assert.That(_host.Loaded, Is.Empty);
                Assert.That(CampaignLaunchSelection.Current, Is.Null);
                Assert.That(panel.IsBusy, Is.False);
                Assert.That(panel.IsStatusError, Is.True);
                Assert.That(panel.Status, Does.Contain("read-only"));
                Assert.That(panel.IsShown(CampaignMenuItemKind.Continue), Is.True, "the save is still there to continue from");
            }
        }

        [Test]
        public void AMissionOutsideTheBuildKeepsItsSave()
        {
            using (var missions = new CampaignTestCatalog())
            {
                var storage = UseCampaign(missions);
                var slot = CampaignSaveSlots.For(CampaignTestCatalog.Mission02);
                storage.Slots[slot] = CampaignTestCatalog.SaveJson(CampaignTestCatalog.Mission02, "start");
                _screen.OpenCampaign();
                var panel = _screen.CampaignPanel;
                panel.ChooseMission(1);
                LogAssert.Expect(LogType.Error, new Regex(Regex.Escape(CampaignTestCatalog.NotBuiltScene) + ".*not in the build settings"));

                panel.Press(CampaignMenuItemKind.Restart);
                panel.Press(CampaignMenuItemKind.ConfirmRestart);

                Assert.That(storage.Slots.ContainsKey(slot), Is.True, "the save is deleted only when the mission can start");
                Assert.That(_host.Loaded, Is.Empty);
                Assert.That(panel.Status, Does.Contain(CampaignTestCatalog.Mission02Name));
                Assert.That(panel.IsBusy, Is.False);
            }
        }

        [Test]
        public void ABrokenSaveIsNotOfferedToContinue()
        {
            using (var missions = new CampaignTestCatalog())
            {
                var storage = UseCampaign(missions);
                storage.Slots[CampaignSaveSlots.For(CampaignTestCatalog.Mission01)] = "{ broken";
                LogAssert.Expect(LogType.Warning, new Regex(@"^\[Campaign\] mission=Campaign01 has a save that cannot be resumed"));

                _screen.OpenCampaign();

                Assert.That(_screen.CampaignPanel.IsShown(CampaignMenuItemKind.Continue), Is.False);
                Assert.That(_screen.CampaignPanel.IsShown(CampaignMenuItemKind.Start), Is.True);
            }
        }

        [Test]
        public void ReopeningTheCampaignStartsAtTheLastLaunch()
        {
            using (var missions = new CampaignTestCatalog())
            {
                UseCampaign(missions);
                CampaignLaunchSelection.Select(new CampaignLaunch(CampaignTestCatalog.Mission02, CampaignDifficulty.Hard));

                _screen.OpenCampaign();

                Assert.That(_screen.CampaignPanel.Menu.SelectedMission.Id, Is.EqualTo(CampaignTestCatalog.Mission02));
                Assert.That(_screen.CampaignPanel.Menu.Difficulty, Is.EqualTo(CampaignDifficulty.Hard));
            }
        }

        [Test]
        public void BackClosesTheCampaignAndFocusesItsButton()
        {
            using (var missions = new CampaignTestCatalog())
            {
                UseCampaign(missions);
                _screen.OpenCampaign();

                _screen.CampaignPanel.Cancel();

                Assert.That(_screen.IsCampaignOpen, Is.False);
                Assert.That(_screen.CampaignSlot.childCount, Is.Zero);
                Assert.That(_screen.CampaignSlot.style.display.value, Is.EqualTo(DisplayStyle.None));
                Assert.That(_screen.Buttons.All(b => b.enabledSelf), Is.True);
                Assert.That(_screen.FocusIndex, Is.EqualTo(MainMenuScreen.ButtonNames.ToList().IndexOf(MainMenuScreen.CampaignButtonName)));
                Assert.That(_host.Loaded, Is.Empty);
            }
        }

        private MemorySaveStorage UseCampaign(CampaignTestCatalog missions)
        {
            var storage = new MemorySaveStorage();
            _screen.Missions = missions.Catalog;
            _screen.SaveStorage = storage;
            _screen.Bind(_root);
            return storage;
        }

        private sealed class RecordingHost : IMenuSceneHost
        {
            public readonly System.Collections.Generic.List<string> Scenes = new System.Collections.Generic.List<string>
            {
                MenuRoute.MainMenuScene, MenuRoute.RangeScene, MenuRoute.CampaignScene,
            };

            public readonly System.Collections.Generic.List<string> Loaded = new System.Collections.Generic.List<string>();
            public string Missing;

            public int BuildIndexOf(string path)
            {
                return path == Missing ? -1 : Scenes.IndexOf(path);
            }

            public void Load(int buildIndex)
            {
                Loaded.Add(Scenes[buildIndex]);
            }

            public void Quit()
            {
            }
        }

        private sealed class FakePartyControl : IPartyControl
        {
            public readonly System.Collections.Generic.List<string> Calls = new System.Collections.Generic.List<string>();

            public event Action<PartyStatus> StatusChanged
            {
                add { }
                remove { }
            }

            public PartyStatus Status => default;

            public void Create()
            {
                Calls.Add("Create");
            }

            public void Join(string inviteCode)
            {
                Calls.Add("Join:" + inviteCode);
            }

            public void Leave()
            {
                Calls.Add("Leave");
            }
        }

        private sealed class FakeMenuPanel : IMainMenuPanel
        {
            public FakeMenuPanel(VisualElement container)
            {
                Container = container;
                container.Add(new Label("screen"));
            }

            public event Action Cancelled;

            public VisualElement Container { get; }

            public int Disposals { get; private set; }

            public void Cancel()
            {
                Cancelled?.Invoke();
            }

            public void Dispose()
            {
                Disposals++;
            }
        }

        private sealed class FakeJoinByCode : IJoinByCode
        {
            public readonly System.Collections.Generic.List<string> Codes = new System.Collections.Generic.List<string>();

            public Action<JoinCodeOutcome> Pending { get; private set; }

            public void Join(string code, Action<JoinCodeOutcome> done)
            {
                Codes.Add(code);
                Pending = done;
            }
        }
    }
}
