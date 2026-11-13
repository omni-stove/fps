using System;
using System.Linq;
using System.Text.RegularExpressions;
using Fps.Core;
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
        }

        [TearDown]
        public void TearDown()
        {
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
            // 設定は遷移ではないので行き先の一覧には無く、ボタンだけがある
            Assert.That(MainMenuScreen.ButtonNames.Count, Is.EqualTo(MenuRoute.Destinations.Count + 1));
            Assert.That(MainMenuScreen.ButtonNames, Has.Member(MainMenuScreen.SettingsButtonName));
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
