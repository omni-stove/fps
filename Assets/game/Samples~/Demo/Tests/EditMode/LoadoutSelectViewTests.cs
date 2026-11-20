using System;
using System.Collections.Generic;
using System.Linq;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // Edit Mode では UIDocument が木を作らないことがあるので、Hud.uxml を複製した木を直接 Bind する。
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
            Assert.That(layout, Is.Not.Null, "Assets/game/Samples~/Demo/UI/Resources/Hud/Hud.uxml should be loadable from Resources");
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
            Assert.That(_view.Element.Query<VisualElement>(className: LoadoutSelectPanel.SlotClass).ToList().Count(), Is.EqualTo(2));

            _view.Close();

            Assert.That(_view.IsOpen, Is.False);
            Assert.That(IsShown, Is.False);
            Assert.That(_view.Element.childCount, Is.Zero);
        }

        [Test]
        public void ConfirmIsPassedOnAndTheAnswerIsShown()
        {
            var received = new List<LoadoutChoice>();
            _view.Confirmed += choice => received.Add(choice);
            _view.Open(LoadoutSelectPanelTests.Selection());

            _view.Panel.Select(1, 1);
            Assert.That(_view.Panel.Confirm(), Is.True);
            Assert.That(received.Count(), Is.EqualTo(1));
            Assert.That(received[0].WeaponIds, Is.EqualTo(_view.Selection.WeaponIds));
            Assert.That(received[0].AbilityIds, Is.Null);

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

        [Test]
        public void TheLastBoundViewIsTheActiveOne()
        {
            Assert.That(LoadoutSelectView.Active, Is.SameAs(_view));

            var other = new GameObject("OtherLoadoutSelect");
            try
            {
                var otherView = other.AddComponent<LoadoutSelectView>();
                otherView.Bind(Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath).CloneTree());
                Assert.That(LoadoutSelectView.Active, Is.SameAs(otherView));

                otherView.Unbind();
                Assert.That(LoadoutSelectView.Active, Is.SameAs(_view), "an unbound view gives the screen back");
            }
            finally
            {
                Object.DestroyImmediate(other);
            }

            _view.Unbind();
            Assert.That(LoadoutSelectView.Active, Is.Not.SameAs(_view));
        }

        [Test]
        public void TheDefaultLoadoutSelectDrivesTheActiveView()
        {
            var screen = GameUi.LoadoutSelect;
            var received = new List<LoadoutChoice>();
            Action<LoadoutChoice> receive = choice => received.Add(choice);
            var abilities = new AbilitySelection(new[] { "beacon" }, new[] { "beacon", AbilitySelection.None }, null);
            screen.Confirmed += receive;
            try
            {
                Assert.That(screen.IsOpen, Is.False);

                screen.Open(LoadoutSelectPanelTests.Selection(), abilities);
                Assert.That(_view.IsOpen, Is.True);
                Assert.That(screen.IsOpen, Is.True);
                Assert.That(_view.Abilities, Is.SameAs(abilities));
                Assert.That(_view.Panel.Abilities, Is.SameAs(abilities));

                _view.Panel.Select(1, 1);
                Assert.That(_view.Panel.Confirm(), Is.True);
                Assert.That(received.Count, Is.EqualTo(1));
                Assert.That(received[0].WeaponIds, Is.EqualTo(_view.Selection.WeaponIds));
                Assert.That(received[0].AbilityIds, Is.EqualTo(new[] { "beacon", AbilitySelection.None }));

                screen.ShowSending();
                Assert.That(_view.Panel.Status, Is.EqualTo(LoadoutFormat.Sending));
                screen.ShowAnswer(false, "the match is over");
                Assert.That(_view.Panel.Status, Is.EqualTo(LoadoutFormat.Rejected("the match is over")));

                screen.Close();
                Assert.That(_view.IsOpen, Is.False);
                Assert.That(screen.IsOpen, Is.False);
                Assert.That(_view.Abilities, Is.Null, "closing forgets the abilities with the weapons");
            }
            finally
            {
                screen.Confirmed -= receive;
            }
        }

        [Test]
        public void TheAbilitySlotsAreNamedFromTheViewsCatalogAndFollowTheLanguage()
        {
            var beacon = ScriptableObject.CreateInstance<BeaconDeployDefinition>();
            var catalog = ScriptableObject.CreateInstance<AbilityCatalogAsset>();
            beacon.Id = "beacon";
            catalog.SetAbilities(new AbilityDefinition[] { beacon });
            try
            {
                _view.AbilityCatalog = catalog;
                Assert.That(_view.EffectiveAbilityCatalog, Is.SameAs(catalog));
                var received = new List<LoadoutChoice>();
                _view.Confirmed += choice => received.Add(choice);
                _view.Open(LoadoutSelectPanelTests.Selection(), new AbilitySelection(new[] { "beacon" }, null, null));

                Assert.That(_view.Panel.AbilityCatalog, Is.SameAs(catalog));
                Assert.That(_view.Panel.AbilitySlotCount, Is.EqualTo(2));
                Assert.That(_view.Panel.AbilityName(0), Is.EqualTo("なし"));
                Assert.That(_view.Panel.CycleAbility(0, 1), Is.True);
                Assert.That(_view.Panel.AbilityName(0), Is.EqualTo("リスポーンビーコン"));

                Localizer.Apply(GameLanguage.English);

                Assert.That(_view.Panel.AbilityName(0), Is.EqualTo("Respawn Beacon"), "the rebuilt screen keeps the chosen ability");
                Assert.That(_view.Panel.AbilityName(1), Is.EqualTo("None"));
                Assert.That(
                    _view.Element.Query<Label>(className: LoadoutSelectPanel.SlotTitleClass).ToList().Select(l => l.text),
                    Is.EqualTo(new[] { "Slot 1", "Slot 2", "Ability 1", "Ability 2" }));

                Assert.That(_view.Panel.Confirm(), Is.True);
                Assert.That(received[0].AbilityIds, Is.EqualTo(new[] { "beacon", AbilitySelection.None }));
            }
            finally
            {
                Localizer.Apply(GameLanguage.Japanese);
                Object.DestroyImmediate(beacon);
                Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void ASelectionWithoutAbilitiesShowsNoAbilitySlots()
        {
            _view.Open(LoadoutSelectPanelTests.Selection());

            Assert.That(_view.Panel.AbilitySlotCount, Is.Zero);
            Assert.That(_view.Panel.AbilityCatalog, Is.Null);
            Assert.That(_view.Element.Query<VisualElement>(className: LoadoutSelectPanel.AbilitySlotClass).ToList(), Is.Empty);
        }

        [Test]
        public void TheDefaultLoadoutSelectDoesNothingWithoutAView()
        {
            _view.Unbind();
            var screen = GameUi.LoadoutSelect;

            screen.Open(LoadoutSelectPanelTests.Selection(), null);

            Assert.That(_view.IsOpen, Is.False);
            Assert.DoesNotThrow(() => screen.ShowAnswer(true, null));
        }
    }
}
