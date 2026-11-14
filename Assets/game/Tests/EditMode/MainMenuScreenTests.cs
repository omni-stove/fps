using System;
using System.Linq;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Fps.Tests.EditMode
{
    public sealed class MainMenuScreenTests
    {
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

            // 本物の選択 (LoadoutPreferenceStore.DefaultKey) を壊さないよう、テストごとに別のキーを使う
            _store = new LoadoutPreferenceStore("Fps.Tests.MainMenuLoadout." + Guid.NewGuid().ToString("N"));
            _screen.LoadoutStore = _store;

            // 本物のセーブ (persistentDataPath) を読み書きしないよう、キャンペーンのテストはメモリの置き場所を使う
            _screen.SaveStorage = new MemorySaveStorage();
            CampaignLaunchSelection.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            _store.Delete();
            MenuNavigator.Host = null;
            MenuNavigator.TakeLobbyRequest();
            MenuNavigator.TakeTutorialRequest();
            MenuNavigator.TakeNotice();
            CampaignLaunchSelection.Clear();
            UnityEngine.Object.DestroyImmediate(_owner);
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
            // 設定とロードアウトは遷移ではないので行き先の一覧には無く、ボタンだけがある
            Assert.That(MainMenuScreen.ButtonNames.Count, Is.EqualTo(MenuRoute.Destinations.Count + 2));
            Assert.That(MainMenuScreen.ButtonNames, Has.Member(MainMenuScreen.SettingsButtonName));
            Assert.That(MainMenuScreen.ButtonNames, Has.Member(MainMenuScreen.LoadoutButtonName));
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
            _screen.MoveFocus(1);
            Assert.That(_screen.FocusIndex, Is.EqualTo(2));
            _screen.MoveFocus(-1);
            _screen.MoveFocus(-1);
            _screen.MoveFocus(-1);
            Assert.That(_screen.FocusIndex, Is.EqualTo(MainMenuScreen.ButtonNames.Count - 1), "moving up from the top wraps to quit");
        }

        [Test]
        public void ChooseGoesToTheDestinationAndDisablesTheButtons()
        {
            _screen.Bind(_root);

            Assert.That(_screen.Choose(MenuDestination.Range), Is.True);

            Assert.That(_host.Loaded, Is.EqualTo(new[] { MenuRoute.RangeScene }));
            Assert.That(_screen.Buttons.All(b => !b.enabledSelf), Is.True, "no second press while the scene loads");
            Assert.That(_screen.Choose(MenuDestination.Campaign), Is.False);
            Assert.That(_host.Loaded, Has.Count.EqualTo(1));
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
        public void ADisconnectReasonIsLookedUpByTheMenuAndFollowsTheLanguage()
        {
            var applied = AppliedControlSettings.Current;
            AppliedControlSettings.Apply(ControlSettings.Default.WithLanguage(GameLanguage.Japanese));
            try
            {
                _screen.Bind(_root);

                _screen.ShowNotice(DisconnectNotice.ServerStoppedMessage);
                Assert.That(_screen.Notice, Is.EqualTo("サーバーが止まりました。"));

                AppliedControlSettings.Apply(ControlSettings.Default.WithLanguage(GameLanguage.English));
                Assert.That(_screen.Notice, Is.EqualTo("The server stopped."));

                _screen.ShowNotice((string)null);
                AppliedControlSettings.Apply(ControlSettings.Default.WithLanguage(GameLanguage.Japanese));
                Assert.That(_screen.Notice, Is.Null, "a hidden notice stays hidden after the language changes");
            }
            finally
            {
                AppliedControlSettings.Apply(applied);
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

                var expected = new[] { LoadoutTestAssets.SmgId, WeaponAssetBuilder.HandgunId };
                Assert.That(_store.Load(), Is.EqualTo(expected));
                Assert.That(_screen.IsLoadoutOpen, Is.False);
                Assert.That(_screen.LoadoutSlot.childCount, Is.Zero);
                Assert.That(_screen.LoadoutSlot.style.display.value, Is.EqualTo(DisplayStyle.None));
                Assert.That(_screen.Notice, Is.EqualTo(LoadoutFormat.Saved));
                Assert.That(_screen.FocusIndex, Is.EqualTo(MainMenuScreen.ButtonNames.ToList().IndexOf(MainMenuScreen.LoadoutButtonName)));
                Assert.That(_screen.Buttons.All(b => b.enabledSelf), Is.True);

                // 次に開いたとき (再起動後も同じ PlayerPrefs から) 前に選んだ並びが選ばれている
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
                Assert.That(storage.Slots, Has.Count.EqualTo(2), "nothing is deleted before the answer");
                Assert.That(_host.Loaded, Is.Empty);

                // 確認は取り消しで閉じられる
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
            private static readonly string[] _scenes =
            {
                MenuRoute.MainMenuScene, MenuRoute.RangeScene, MenuRoute.CampaignScene, MenuRoute.NetScene,
            };

            public readonly System.Collections.Generic.List<string> Loaded = new System.Collections.Generic.List<string>();
            public string Missing;

            public int BuildIndexOf(string path)
            {
                return path == Missing ? -1 : Array.IndexOf(_scenes, path);
            }

            public void Load(int buildIndex)
            {
                Loaded.Add(_scenes[buildIndex]);
            }

            public void Quit()
            {
            }
        }
    }
}
