using System;
using System.Collections.Generic;
using System.Linq;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// <see cref="LoadoutSelectPanel"/> がスロットごとに選べる武器を武器名と種別で並べ、行の移動・武器の切り替え・決定を受けること。
    /// </summary>
    public sealed class LoadoutSelectPanelTests
    {
        private const string _rifle = "00000000-0000-4000-8000-000000000001";
        private const string _handgun = "00000000-0000-4000-8000-000000000002";
        private const string _smg = "00000000-0000-4000-8000-000000000003";
        private const string _pistol = "00000000-0000-4000-8000-000000000004";

        private VisualElement _container;
        private LoadoutSelectPanel _panel;
        private readonly List<IReadOnlyList<string>> _confirmed = new List<IReadOnlyList<string>>();
        private int _cancelled;

        private static WeaponSpec Spec(string id, string displayName, WeaponCategory category)
        {
            return new WeaponSpec(id, displayName, category, FireMode.Auto, 30, 90, 0.1f, 2f, 25, 100f, DamageFalloff.None);
        }

        internal static LoadoutSelection Selection(IReadOnlyList<string> preferred = null)
        {
            var catalog = new WeaponCatalog(new[]
            {
                Spec(_rifle, "Rifle", WeaponCategory.AssaultRifle),
                Spec(_handgun, "Handgun", WeaponCategory.Handgun),
                Spec(_smg, "SMG", WeaponCategory.SubmachineGun),
                Spec(_pistol, "Pistol", WeaponCategory.Handgun),
            });
            var rule = new LoadoutRule(new[]
            {
                new[] { WeaponCategory.AssaultRifle, WeaponCategory.SubmachineGun },
                new[] { WeaponCategory.Handgun },
            });
            return new LoadoutSelection(rule, catalog, preferred, new[] { _rifle, _handgun });
        }

        [SetUp]
        public void SetUp()
        {
            _container = new VisualElement();
            _confirmed.Clear();
            _cancelled = 0;
            _panel = new LoadoutSelectPanel(_container, Selection());
            _panel.Confirmed += ids => _confirmed.Add(ids);
            _panel.Cancelled += () => _cancelled++;
        }

        [TearDown]
        public void TearDown()
        {
            _panel.Dispose();
        }

        private VisualElement[] Rows()
        {
            return _container.Query<VisualElement>(className: LoadoutSelectPanel.SlotClass).ToList().ToArray();
        }

        private static string[] OptionTexts(VisualElement row)
        {
            return row.Query<Button>(className: LoadoutSelectPanel.OptionClass).ToList()
                .Select(b => b.Q<Label>(className: LoadoutSelectPanel.OptionNameClass).text + " / "
                    + b.Q<Label>(className: LoadoutSelectPanel.OptionCategoryClass).text)
                .ToArray();
        }

        private static int[] SelectedIndices(VisualElement row)
        {
            var buttons = row.Query<Button>(className: LoadoutSelectPanel.OptionClass).ToList();
            return Enumerable.Range(0, buttons.Count)
                .Where(i => buttons[i].ClassListContains(LoadoutSelectPanel.SelectedOptionClass))
                .ToArray();
        }

        [Test]
        public void ListsEachSlotsWeaponsWithNameAndCategoryInCatalogOrder()
        {
            var rows = Rows();

            Assert.That(rows, Has.Length.EqualTo(2));
            Assert.That(rows[0].Q<Label>(className: LoadoutSelectPanel.SlotTitleClass).text, Is.EqualTo(LoadoutFormat.SlotTitle(0)));
            Assert.That(OptionTexts(rows[0]), Is.EqualTo(new[] { "Rifle / アサルトライフル", "SMG / サブマシンガン" }));
            Assert.That(OptionTexts(rows[1]), Is.EqualTo(new[] { "Handgun / ハンドガン", "Pistol / ハンドガン" }));
            Assert.That(SelectedIndices(rows[0]), Is.EqualTo(new[] { 0 }));
            Assert.That(SelectedIndices(rows[1]), Is.EqualTo(new[] { 0 }));
            Assert.That(_container.ClassListContains(LoadoutSelectPanel.PanelClass), Is.True);
            Assert.That(_container.Q<Button>(LoadoutSelectPanel.ConfirmButtonName), Is.SameAs(_panel.ConfirmButton));
            Assert.That(_panel.Status, Is.Null);
        }

        [Test]
        public void StartsFromTheGivenPreference()
        {
            _panel.Dispose();
            _panel = new LoadoutSelectPanel(_container, Selection(new[] { _smg, _pistol }));

            Assert.That(SelectedIndices(Rows()[0]), Is.EqualTo(new[] { 1 }));
            Assert.That(SelectedIndices(Rows()[1]), Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void MoveFocusGoesThroughTheSlotsAndConfirmAndWraps()
        {
            Assert.That(_panel.FocusRow, Is.EqualTo(0));
            Assert.That(Rows()[0].ClassListContains(LoadoutSelectPanel.FocusedSlotClass), Is.True);

            _panel.MoveFocus(1);
            Assert.That(_panel.FocusRow, Is.EqualTo(1));
            Assert.That(Rows()[0].ClassListContains(LoadoutSelectPanel.FocusedSlotClass), Is.False);
            Assert.That(Rows()[1].ClassListContains(LoadoutSelectPanel.FocusedSlotClass), Is.True);

            _panel.MoveFocus(1);
            Assert.That(_panel.FocusRow, Is.EqualTo(_panel.ConfirmRow));
            Assert.That(_panel.ConfirmButton.ClassListContains(LoadoutSelectPanel.FocusedConfirmClass), Is.True);

            _panel.MoveFocus(1);
            Assert.That(_panel.FocusRow, Is.EqualTo(0), "moving down from confirm wraps to the first slot");
            _panel.MoveFocus(-1);
            Assert.That(_panel.FocusRow, Is.EqualTo(_panel.ConfirmRow), "moving up from the first slot wraps to confirm");
        }

        [Test]
        public void CycleChangesTheWeaponOfTheFocusedSlotOnly()
        {
            _panel.MoveFocus(1);

            Assert.That(_panel.Cycle(1), Is.True);

            Assert.That(_panel.Selection.WeaponIds, Is.EqualTo(new[] { _rifle, _pistol }));
            Assert.That(SelectedIndices(Rows()[1]), Is.EqualTo(new[] { 1 }));
            Assert.That(SelectedIndices(Rows()[0]), Is.EqualTo(new[] { 0 }));

            _panel.MoveFocus(1);
            Assert.That(_panel.Cycle(1), Is.False, "the confirm row has no weapons to cycle");
        }

        [Test]
        public void SelectPicksTheWeaponAndFocusesItsRow()
        {
            Assert.That(_panel.Select(1, 1), Is.True);

            Assert.That(_panel.FocusRow, Is.EqualTo(1));
            Assert.That(_panel.Selection.WeaponIds, Is.EqualTo(new[] { _rifle, _pistol }));
            Assert.That(_panel.Select(1, 5), Is.False);
            Assert.That(_panel.Select(9, 0), Is.False);
        }

        [Test]
        public void ConfirmHandsOverTheChosenIds()
        {
            _panel.Select(0, 1);

            Assert.That(_panel.Confirm(), Is.True);

            Assert.That(_confirmed, Has.Count.EqualTo(1));
            Assert.That(_confirmed[0], Is.EqualTo(new[] { _smg, _handgun }));
        }

        [Test]
        public void ConfirmShowsWhyAChoiceThatBreaksTheRuleCannotBeSent()
        {
            var rule = new LoadoutRule(new[] { new[] { WeaponCategory.Handgun }, new[] { WeaponCategory.Handgun } });
            var catalog = new WeaponCatalog(new[] { Spec(_handgun, "Handgun", WeaponCategory.Handgun) });
            _panel.Dispose();
            _panel = new LoadoutSelectPanel(_container, new LoadoutSelection(rule, catalog, null, null));
            _panel.Confirmed += ids => _confirmed.Add(ids);

            Assert.That(_panel.Confirm(), Is.False);

            Assert.That(_confirmed, Is.Empty);
            Assert.That(_panel.IsStatusError, Is.True);
            Assert.That(_panel.Status, Does.StartWith(LoadoutFormat.Rejected(null)));
        }

        [Test]
        public void BusyBlocksChangesUntilTheAnswerArrives()
        {
            _panel.SetBusy(true);

            Assert.That(_panel.Confirm(), Is.False);
            Assert.That(_panel.Select(0, 1), Is.False);
            _panel.MoveFocus(1);
            Assert.That(_panel.FocusRow, Is.EqualTo(0));
            Assert.That(_panel.ConfirmButton.enabledSelf, Is.False);
            Assert.That(_confirmed, Is.Empty);

            _panel.SetBusy(false);
            Assert.That(_panel.ConfirmButton.enabledSelf, Is.True);
            Assert.That(_panel.Confirm(), Is.True);
        }

        [Test]
        public void ShowStatusShowsAndClearsTheMessage()
        {
            _panel.ShowStatus(LoadoutFormat.Accepted, false);
            Assert.That(_panel.Status, Is.EqualTo(LoadoutFormat.Accepted));
            Assert.That(_panel.IsStatusError, Is.False);

            _panel.ShowStatus(null, false);
            Assert.That(_panel.Status, Is.Null);
        }

        [Test]
        public void DisposeEmptiesTheContainer()
        {
            _panel.Dispose();

            Assert.That(_container.childCount, Is.Zero);
            Assert.That(_container.ClassListContains(LoadoutSelectPanel.PanelClass), Is.False);
            Assert.DoesNotThrow(() => _panel.Dispose());
        }

        [Test]
        public void RejectsNulls()
        {
            Assert.Throws<ArgumentNullException>(() => new LoadoutSelectPanel(null, Selection()));
            Assert.Throws<ArgumentNullException>(() => new LoadoutSelectPanel(new VisualElement(), null));
        }
    }
}
