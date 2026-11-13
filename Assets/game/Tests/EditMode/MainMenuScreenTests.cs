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
        }

        [TearDown]
        public void TearDown()
        {
            _store.Delete();
            MenuNavigator.Host = null;
            MenuNavigator.TakeLobbyRequest();
            MenuNavigator.TakeNotice();
            UnityEngine.Object.DestroyImmediate(_owner);
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
            Assert.That(_screen.Notice, Does.StartWith(MainMenuScreen.LoadoutUnavailableNotice));
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
