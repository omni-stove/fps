using System.Collections.Generic;
using System.Linq;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// <see cref="LoadoutSelectView"/> が開いている間だけ Hud.uxml の枠に選択の画面を出し、決定・答え・取り消しを扱うこと。
    /// Edit Mode では UIDocument が木を作らないことがあるので、Hud.uxml を複製した木を直接 Bind する。
    /// </summary>
    public sealed class LoadoutSelectViewTests
    {
        private GameObject _host;
        private LoadoutSelectView _view;
        private VisualElement _root;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("TestLoadoutSelect");
            _view = _host.AddComponent<LoadoutSelectView>();
            var layout = Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath);
            Assert.That(layout, Is.Not.Null, "Assets/game/UI/Resources/Hud/Hud.uxml should be loadable from Resources");
            _root = layout.CloneTree();
            _view.Bind(_root);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
        }

        private bool IsShown => _view.Element.style.display.value != DisplayStyle.None;

        [Test]
        public void TheStyleSheetIsInResources()
        {
            Assert.That(Resources.Load<StyleSheet>(LoadoutSelectPanel.StyleSheetResourcePath), Is.Not.Null);
        }

        [Test]
        public void StartsClosedAndHidden()
        {
            Assert.That(_view.IsOpen, Is.False);
            Assert.That(_view.Panel, Is.Null);
            Assert.That(IsShown, Is.False);
            Assert.That(_view.Element.childCount, Is.Zero);
        }

        [Test]
        public void OpenShowsTheSlotsAndCloseHidesThem()
        {
            _view.Open(LoadoutSelectPanelTests.Selection());

            Assert.That(_view.IsOpen, Is.True);
            Assert.That(IsShown, Is.True);
            Assert.That(_view.Element.Query<VisualElement>(className: LoadoutSelectPanel.SlotClass).ToList(), Has.Count.EqualTo(2));

            _view.Close();

            Assert.That(_view.IsOpen, Is.False);
            Assert.That(IsShown, Is.False);
            Assert.That(_view.Element.childCount, Is.Zero);
        }

        [Test]
        public void ConfirmIsPassedOnAndTheAnswerIsShown()
        {
            var received = new List<IReadOnlyList<string>>();
            _view.Confirmed += ids => received.Add(ids);
            _view.Open(LoadoutSelectPanelTests.Selection());

            _view.Panel.Select(1, 1);
            Assert.That(_view.Panel.Confirm(), Is.True);
            Assert.That(received, Has.Count.EqualTo(1));
            Assert.That(received[0], Is.EqualTo(_view.Selection.WeaponIds));

            _view.ShowSending();
            Assert.That(_view.Panel.IsBusy, Is.True);
            Assert.That(_view.Panel.Status, Is.EqualTo(LoadoutFormat.Sending));

            _view.ShowAnswer(false, "the match is over");
            Assert.That(_view.Panel.IsBusy, Is.False);
            Assert.That(_view.Panel.IsStatusError, Is.True);
            Assert.That(_view.Panel.Status, Is.EqualTo(LoadoutFormat.Rejected("the match is over")));

            _view.ShowAnswer(true, null);
            Assert.That(_view.Panel.Status, Is.EqualTo(LoadoutFormat.Accepted));
            Assert.That(_view.IsOpen, Is.True, "the screen stays until the player respawns");
        }

        [Test]
        public void RebindingTheHudKeepsTheOpenScreenAndItsState()
        {
            _view.Open(LoadoutSelectPanelTests.Selection());
            _view.Panel.Select(0, 1);
            _view.ShowSending();
            var newRoot = Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath).CloneTree();

            _view.Bind(newRoot);

            Assert.That(_root.Q<VisualElement>(LoadoutSelectView.UxmlName).childCount, Is.Zero, "the old tree is emptied");
            Assert.That(IsShown, Is.True);
            Assert.That(_view.Panel.Container, Is.SameAs(_view.Element));
            Assert.That(_view.Panel.Selection.SelectedIndexAt(0), Is.EqualTo(1));
            Assert.That(_view.Panel.IsBusy, Is.True);
            Assert.That(_view.Panel.Status, Is.EqualTo(LoadoutFormat.Sending));
        }

        [Test]
        public void OpeningBeforeTheHudIsBoundShowsItOnBind()
        {
            _view.Unbind();
            _view.Open(LoadoutSelectPanelTests.Selection());
            Assert.That(_view.Panel, Is.Null);

            _view.Bind(_root);

            Assert.That(_view.Panel, Is.Not.Null);
            Assert.That(IsShown, Is.True);
        }

        [Test]
        public void TheHudExposesTheViewAndTheLayoutPlacesIt()
        {
            var hud = _host.GetComponent<Hud>();

            Assert.That(hud.LoadoutSelect, Is.SameAs(_view));
            Assert.That(_root.Q<VisualElement>(LoadoutSelectView.UxmlName).ClassListContains("hud__loadout"), Is.True);
        }
    }
}
