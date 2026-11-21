using System;
using System.Collections.Generic;
using System.Linq;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Shooter.Tests.EditMode
{
    public sealed class LoadoutSelectPanelTests
    {
        private const string _rifle = "00000000-0000-4000-8000-000000000001";
        private const string _handgun = "00000000-0000-4000-8000-000000000002";
        private const string _smg = "00000000-0000-4000-8000-000000000003";
        private const string _pistol = "00000000-0000-4000-8000-000000000004";
        private const string _beaconId = "beacon";
        private const string _grappleId = "grapple";
        private const string _beaconJa = "リスポーンビーコン";

        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();

        private VisualElement _container;
        private LoadoutSelectPanel _panel;
        private readonly List<LoadoutChoice> _confirmed = new List<LoadoutChoice>();
        private int _cancelled;

        private static WeaponSpec Spec(string id, string nameKey, WeaponCategory category)
        {
            return new WeaponSpec(id, nameKey, category, FireMode.Auto, 30, 90, 0.1f, 2f, 25, 100f, DamageFalloff.None);
        }

        internal static LoadoutSelection Selection(IReadOnlyList<string> preferred = null)
        {
            var catalog = new WeaponCatalog(new[]
            {
                Spec(_rifle, "weapon.rifle", WeaponCategory.AssaultRifle),
                Spec(_handgun, "weapon.handgun", WeaponCategory.Handgun),
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
            foreach (var created in _created)
            {
                UnityEngine.Object.DestroyImmediate(created);
            }

            _created.Clear();
            Localizer.Apply(GameLanguage.Japanese);
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

            Assert.That(_confirmed.Count(), Is.EqualTo(1));
            Assert.That(_confirmed[0].WeaponIds, Is.EqualTo(new[] { _smg, _handgun }));
            Assert.That(_confirmed[0].AbilityIds, Is.Null, "a panel opened without abilities hands over none");
        }

        // ビーコンは表示名のキーを持ち、grapple はキーを持たないので Id で出る。既定の能力は 1 つ目の枠の grapple だけ
        private AbilitySelection OpenWithAbilities(IReadOnlyList<string> preferred = null, bool withCatalog = true)
        {
            var abilities = new AbilitySelection(new[] { _beaconId, _grappleId }, preferred, new[] { _grappleId });
            _panel.Dispose();
            _panel = new LoadoutSelectPanel(_container, Selection(), null, abilities, withCatalog ? AbilityCatalog() : null);
            _panel.Confirmed += ids => _confirmed.Add(ids);
            return abilities;
        }

        private AbilityCatalogAsset AbilityCatalog()
        {
            var beacon = UnityEngine.ScriptableObject.CreateInstance<BeaconDeployDefinition>();
            beacon.Id = _beaconId;
            var grapple = UnityEngine.ScriptableObject.CreateInstance<NullAbilityDefinition>();
            grapple.Id = _grappleId;
            var catalog = UnityEngine.ScriptableObject.CreateInstance<AbilityCatalogAsset>();
            catalog.SetAbilities(new AbilityDefinition[] { beacon, grapple });
            _created.Add(beacon);
            _created.Add(grapple);
            _created.Add(catalog);
            return catalog;
        }

        private VisualElement[] AbilityRows()
        {
            return _container.Query<VisualElement>(className: LoadoutSelectPanel.AbilitySlotClass).ToList().ToArray();
        }

        private LoadoutPresetBook OpenWithPresets(int count, int maxPresets = 3)
        {
            var book = new LoadoutPresetBook(new LoadoutPresetSpec(maxPresets, 12));
            for (var i = 0; i < count; i++)
            {
                book.Add(i == 1 ? "Close" : string.Empty, new LoadoutChoice(new[] { _rifle, _handgun }));
            }

            _panel.Dispose();
            _panel = new LoadoutSelectPanel(_container, Selection(), presets: book);
            _panel.Confirmed += ids => _confirmed.Add(ids);
            return book;
        }

        [Test]
        public void WithoutPresetsThereIsNoPresetRow()
        {
            Assert.That(_panel.HasPresets, Is.False);
            Assert.That(_container.Q<VisualElement>(className: LoadoutSelectPanel.PresetsClass), Is.Null);
            Assert.That(_panel.PresetNameField, Is.Null);
            Assert.That(_panel.CyclePreset(1), Is.False);
            Assert.That(_panel.SaveAsNewPreset(), Is.False);
            Assert.That(_panel.RenamePreset(), Is.False);
        }

        [Test]
        public void ThePresetRowShowsTheActivePresetAboveTheSlots()
        {
            var book = OpenWithPresets(2);

            var row = _container.Q<VisualElement>(className: LoadoutSelectPanel.PresetsClass);
            Assert.That(row, Is.Not.Null);
            Assert.That(_container.IndexOf(row), Is.LessThan(_container.IndexOf(Rows()[0])), "the presets come before the slots");
            Assert.That(Rows(), Has.Length.EqualTo(2), "the preset row is not a weapon slot");
            Assert.That(_panel.PresetTitle, Is.EqualTo("プリセット 1/2"));
            Assert.That(_panel.PresetName, Is.EqualTo("ロードアウト 1"));
            Assert.That(_panel.PresetNameField.value, Is.Empty);
            Assert.That(_panel.PresetNameField.maxLength, Is.EqualTo(12));
            Assert.That(_panel.FocusRow, Is.EqualTo(0), "the first weapon slot still has the focus");

            book.Select(1);
            _panel.RefreshPresets();

            Assert.That(_panel.PresetTitle, Is.EqualTo("プリセット 2/2"));
            Assert.That(_panel.PresetName, Is.EqualTo("Close"));
            Assert.That(_panel.PresetNameField.value, Is.EqualTo("Close"));
        }

        [Test]
        public void MoveFocusGoesThroughThePresetRowBeforeTheSlots()
        {
            OpenWithPresets(2);

            _panel.MoveFocus(-1);
            Assert.That(_panel.FocusRow, Is.EqualTo(LoadoutSelectPanel.PresetRow));
            Assert.That(_container.Q<VisualElement>(className: LoadoutSelectPanel.PresetsClass)
                .ClassListContains(LoadoutSelectPanel.FocusedPresetsClass), Is.True);
            Assert.That(Rows()[0].ClassListContains(LoadoutSelectPanel.FocusedSlotClass), Is.False);

            _panel.MoveFocus(-1);
            Assert.That(_panel.FocusRow, Is.EqualTo(_panel.ConfirmRow), "moving up from the presets wraps to confirm");
            _panel.MoveFocus(1);
            Assert.That(_panel.FocusRow, Is.EqualTo(LoadoutSelectPanel.PresetRow), "moving down from confirm wraps to the presets");
            _panel.MoveFocus(1);
            Assert.That(_panel.FocusRow, Is.EqualTo(0));
        }

        [Test]
        public void CycleOnThePresetRowAsksForTheNextPresetAndWraps()
        {
            OpenWithPresets(3);
            var selected = new List<int>();
            _panel.PresetSelected += selected.Add;
            _panel.FocusPresets();

            Assert.That(_panel.Cycle(1), Is.True);
            Assert.That(_panel.Cycle(-1), Is.True);
            Assert.That(_panel.CyclePreset(4), Is.True);

            Assert.That(selected, Is.EqualTo(new[] { 1, 2, 1 }), "the panel asks; the caller switches the active preset");
            Assert.That(_panel.Selection.WeaponIds, Is.EqualTo(new[] { _rifle, _handgun }), "the weapons are left to the caller");
        }

        [Test]
        public void ASinglePresetHasNothingToSwitchTo()
        {
            OpenWithPresets(1);
            var selected = new List<int>();
            _panel.PresetSelected += selected.Add;

            Assert.That(_panel.CyclePreset(1), Is.False);
            Assert.That(_panel.PresetPreviousButton.enabledSelf, Is.False);
            Assert.That(_panel.PresetNextButton.enabledSelf, Is.False);
            Assert.That(selected, Is.Empty);
        }

        [Test]
        public void SaveAsNewHandsOverTheTypedNameAndTheCurrentChoice()
        {
            OpenWithPresets(1);
            var requested = new List<(string name, LoadoutChoice choice)>();
            _panel.PresetSaveAsNewRequested += (name, choice) => requested.Add((name, choice));
            _panel.Select(0, 1);
            _panel.PresetNameField.value = "Rush";

            Assert.That(_panel.SaveAsNewPreset(), Is.True);

            Assert.That(requested.Count, Is.EqualTo(1));
            Assert.That(requested[0].name, Is.EqualTo("Rush"));
            Assert.That(requested[0].choice.WeaponIds, Is.EqualTo(new[] { _smg, _handgun }));
            Assert.That(_confirmed, Is.Empty, "saving as new does not confirm");
        }

        [Test]
        public void SaveAsNewAtTheLimitShowsTheLimitInstead()
        {
            OpenWithPresets(2, 2);
            var requested = 0;
            _panel.PresetSaveAsNewRequested += (_, _) => requested++;

            Assert.That(_panel.SaveAsNewPreset(), Is.False);

            Assert.That(requested, Is.Zero);
            Assert.That(_panel.Status, Is.EqualTo("プリセットは 2 個まで保存できます"));
            Assert.That(_panel.IsStatusError, Is.True);
            Assert.That(_panel.SaveAsNewPresetButton.enabledSelf, Is.True, "the button stays so pressing it can tell the limit");
        }

        [Test]
        public void RenameHandsOverTheTypedName()
        {
            OpenWithPresets(1);
            var requested = new List<string>();
            _panel.PresetRenameRequested += requested.Add;
            _panel.PresetNameField.value = "Long";

            Assert.That(_panel.RenamePreset(), Is.True);

            Assert.That(requested, Is.EqualTo(new[] { "Long" }));
        }

        [Test]
        public void BusyStopsThePresetButtons()
        {
            OpenWithPresets(2);
            var selected = new List<int>();
            _panel.PresetSelected += selected.Add;

            _panel.SetBusy(true);

            Assert.That(_panel.PresetNextButton.enabledSelf, Is.False);
            Assert.That(_panel.RenamePresetButton.enabledSelf, Is.False);
            Assert.That(_panel.SaveAsNewPresetButton.enabledSelf, Is.False);
            Assert.That(_panel.PresetNameField.enabledSelf, Is.False);
            Assert.That(_panel.CyclePreset(1), Is.False);
            Assert.That(_panel.SaveAsNewPreset(), Is.False);
            Assert.That(_panel.RenamePreset(), Is.False);

            _panel.SetBusy(false);

            Assert.That(_panel.PresetNextButton.enabledSelf, Is.True);
            Assert.That(_panel.CyclePreset(1), Is.True);
            Assert.That(selected, Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void WithoutAbilitiesThereAreNoAbilityRows()
        {
            Assert.That(_panel.AbilitySlotCount, Is.Zero);
            Assert.That(AbilityRows(), Is.Empty);
            Assert.That(_panel.ConfirmRow, Is.EqualTo(2));
            Assert.Throws<ArgumentOutOfRangeException>(() => _panel.AbilityName(0));
            Assert.That(_panel.CycleAbility(0, 1), Is.False);
        }

        [Test]
        public void TheAbilityRowsComeBelowTheWeaponsWithTheirTitlesAndCurrentAbility()
        {
            var abilities = OpenWithAbilities();

            Assert.That(_panel.Abilities, Is.SameAs(abilities));
            Assert.That(_panel.AbilitySlotCount, Is.EqualTo(2));
            Assert.That(Rows(), Has.Length.EqualTo(4), "the ability rows share the slot look");
            var abilityRows = AbilityRows();
            Assert.That(abilityRows, Has.Length.EqualTo(2));
            Assert.That(Rows()[2], Is.SameAs(abilityRows[0]), "the abilities come after the weapons");
            Assert.That(abilityRows[0].Q<Label>(className: LoadoutSelectPanel.SlotTitleClass).text, Is.EqualTo("能力 1"));
            Assert.That(abilityRows[1].Q<Label>(className: LoadoutSelectPanel.SlotTitleClass).text, Is.EqualTo("能力 2"));
            Assert.That(_panel.AbilityRow(0), Is.EqualTo(2));
            Assert.That(_panel.AbilityRow(1), Is.EqualTo(3));
            Assert.That(_panel.ConfirmRow, Is.EqualTo(4));

            Assert.That(_panel.AbilityName(0), Is.EqualTo(_grappleId), "an ability without a name key shows its id");
            Assert.That(_panel.AbilityName(1), Is.EqualTo("なし"));
            Assert.That(abilityRows[1].Q<Label>(className: LoadoutSelectPanel.AbilityNameClass).ClassListContains(LoadoutSelectPanel.NoAbilityClass), Is.True);
            Assert.That(abilityRows[0].Q<Label>(className: LoadoutSelectPanel.AbilityNameClass).ClassListContains(LoadoutSelectPanel.NoAbilityClass), Is.False);
            Assert.That(_panel.AbilityPreviousButton(0).text, Is.EqualTo("<"));
            Assert.That(_panel.AbilityNextButton(0).text, Is.EqualTo(">"));
            Assert.That(_panel.AbilityNextButton(0).enabledSelf, Is.True);
        }

        [Test]
        public void StartsFromTheSavedAbilities()
        {
            OpenWithAbilities(new[] { AbilitySelection.None, _beaconId });

            Assert.That(_panel.AbilityName(0), Is.EqualTo("なし"));
            Assert.That(_panel.AbilityName(1), Is.EqualTo(_beaconJa));
        }

        [Test]
        public void CycleOnAnAbilityRowGoesThroughNoneAndTheAbilities()
        {
            var abilities = OpenWithAbilities();
            _panel.MoveFocus(2);
            Assert.That(_panel.FocusRow, Is.EqualTo(_panel.AbilityRow(0)));
            Assert.That(AbilityRows()[0].ClassListContains(LoadoutSelectPanel.FocusedSlotClass), Is.True);

            Assert.That(_panel.Cycle(1), Is.True);
            Assert.That(_panel.AbilityName(0), Is.EqualTo("なし"), "after the last ability comes none");
            Assert.That(_panel.Cycle(1), Is.True);
            Assert.That(_panel.AbilityName(0), Is.EqualTo(_beaconJa));
            Assert.That(_panel.Cycle(-1), Is.True);
            Assert.That(_panel.AbilityName(0), Is.EqualTo("なし"));
            Assert.That(_panel.Cycle(-1), Is.True);
            Assert.That(_panel.AbilityName(0), Is.EqualTo(_grappleId), "going back from none wraps to the last ability");

            Assert.That(abilities.AbilityIds, Is.EqualTo(new[] { _grappleId, AbilitySelection.None }));
            Assert.That(_panel.Selection.WeaponIds, Is.EqualTo(new[] { _rifle, _handgun }), "the weapons do not move");
        }

        [Test]
        public void AnAbilityChosenInTheOtherSlotIsSkipped()
        {
            OpenWithAbilities();
            _panel.MoveFocus(3);
            Assert.That(_panel.FocusRow, Is.EqualTo(_panel.AbilityRow(1)));

            Assert.That(_panel.Cycle(1), Is.True);
            Assert.That(_panel.AbilityName(1), Is.EqualTo(_beaconJa));
            Assert.That(_panel.Cycle(1), Is.True);
            Assert.That(_panel.AbilityName(1), Is.EqualTo("なし"), "grapple is in slot 1 already");
        }

        [Test]
        public void TheStepButtonsCycleTheirSlotAndFocusItsRow()
        {
            OpenWithAbilities();

            Assert.That(_panel.CycleAbility(1, 1), Is.True);

            Assert.That(_panel.FocusRow, Is.EqualTo(_panel.AbilityRow(1)));
            Assert.That(AbilityRows()[1].ClassListContains(LoadoutSelectPanel.FocusedSlotClass), Is.True);
            Assert.That(Rows()[0].ClassListContains(LoadoutSelectPanel.FocusedSlotClass), Is.False);
            Assert.That(_panel.AbilityName(1), Is.EqualTo(_beaconJa));

            Assert.That(_panel.CycleAbility(0, -1), Is.True);
            Assert.That(_panel.FocusRow, Is.EqualTo(_panel.AbilityRow(0)));
            Assert.That(_panel.AbilityName(0), Is.EqualTo("なし"));

            Assert.That(_panel.CycleAbility(2, 1), Is.False);
            Assert.That(_panel.CycleAbility(-1, 1), Is.False);
        }

        [Test]
        public void ConfirmHandsOverTheChosenAbilitiesWithTheWeapons()
        {
            OpenWithAbilities();
            _panel.Select(0, 1);
            _panel.CycleAbility(0, 1);
            _panel.CycleAbility(1, 1);

            Assert.That(_panel.Confirm(), Is.True);

            Assert.That(_confirmed, Has.Count.EqualTo(1));
            Assert.That(_confirmed[0].WeaponIds, Is.EqualTo(new[] { _smg, _handgun }));
            Assert.That(_confirmed[0].AbilityIds, Is.EqualTo(new[] { AbilitySelection.None, _beaconId }));
        }

        [Test]
        public void TheConfirmRowComesAfterTheAbilitiesAndSubmitMovesThrough()
        {
            OpenWithAbilities();

            _panel.MoveFocus(4);
            Assert.That(_panel.FocusRow, Is.EqualTo(_panel.ConfirmRow));
            Assert.That(_panel.Cycle(1), Is.False, "the confirm row has nothing to cycle");
            _panel.MoveFocus(-1);
            Assert.That(_panel.FocusRow, Is.EqualTo(_panel.AbilityRow(1)));
            _panel.MoveFocus(2);
            Assert.That(_panel.FocusRow, Is.EqualTo(0), "moving down from confirm wraps to the first weapon slot");
        }

        [Test]
        public void BusyDisablesTheAbilityButtons()
        {
            OpenWithAbilities();

            _panel.SetBusy(true);
            Assert.That(_panel.AbilityPreviousButton(0).enabledSelf, Is.False);
            Assert.That(_panel.AbilityNextButton(1).enabledSelf, Is.False);
            Assert.That(_panel.CycleAbility(0, 1), Is.False);
            Assert.That(_panel.AbilityName(0), Is.EqualTo(_grappleId));

            _panel.SetBusy(false);
            Assert.That(_panel.AbilityPreviousButton(0).enabledSelf, Is.True);
            Assert.That(_panel.AbilityNextButton(1).enabledSelf, Is.True);
            Assert.That(_panel.CycleAbility(0, 1), Is.True);
        }

        [Test]
        public void ASlotWithOnlyNoneCannotBeCycled()
        {
            var abilities = new AbilitySelection(Array.Empty<string>(), null, null);
            _panel.Dispose();
            _panel = new LoadoutSelectPanel(_container, Selection(), null, abilities);

            Assert.That(_panel.AbilityName(0), Is.EqualTo("なし"));
            Assert.That(_panel.AbilityPreviousButton(0).enabledSelf, Is.False);
            Assert.That(_panel.AbilityNextButton(0).enabledSelf, Is.False);
            Assert.That(_panel.CycleAbility(0, 1), Is.False);

            _panel.SetBusy(true);
            _panel.SetBusy(false);
            Assert.That(_panel.AbilityNextButton(0).enabledSelf, Is.False, "leaving busy keeps it disabled");
        }

        [Test]
        public void WithoutACatalogTheAbilityIdIsShown()
        {
            OpenWithAbilities(new[] { _beaconId, _grappleId }, false);

            Assert.That(_panel.AbilityCatalog, Is.Null);
            Assert.That(_panel.AbilityName(0), Is.EqualTo(_beaconId));
        }

        [Test]
        public void AnAbilityMissingFromTheCatalogShowsItsId()
        {
            var abilities = new AbilitySelection(new[] { "unknown" }, new[] { "unknown", AbilitySelection.None }, null);
            _panel.Dispose();
            _panel = new LoadoutSelectPanel(_container, Selection(), null, abilities, AbilityCatalog());

            Assert.That(_panel.AbilityName(0), Is.EqualTo("unknown"));
        }

        [Test]
        public void TheAbilityTextsFollowTheLanguage()
        {
            Localizer.Apply(GameLanguage.English);
            OpenWithAbilities(new[] { _beaconId, AbilitySelection.None });

            var abilityRows = AbilityRows();
            Assert.That(abilityRows[0].Q<Label>(className: LoadoutSelectPanel.SlotTitleClass).text, Is.EqualTo("Ability 1"));
            Assert.That(abilityRows[1].Q<Label>(className: LoadoutSelectPanel.SlotTitleClass).text, Is.EqualTo("Ability 2"));
            Assert.That(_panel.AbilityName(0), Is.EqualTo("Respawn Beacon"));
            Assert.That(_panel.AbilityName(1), Is.EqualTo("None"));
        }

        [Test]
        public void TheAbilityTextsHaveTheSameKeysInEveryLanguage()
        {
            foreach (var language in GameLanguages.All)
            {
                var table = Localizer.TableOf(language);
                foreach (var key in new[] { "loadout.abilitySlot", "loadout.abilityNone", "loadout.abilityPrevious", "loadout.abilityNext" })
                {
                    Assert.That(table.Contains(key), Is.True, $"{GameLanguages.Code(language)}: {key}");
                }
            }
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

        // SMG はレベル 4 で使える。Pistol はアンロックの表に無いが述語が断る
        private void OpenWithLocks()
        {
            var catalog = new WeaponCatalog(new[]
            {
                Spec(_rifle, "weapon.rifle", WeaponCategory.AssaultRifle),
                Spec(_handgun, "weapon.handgun", WeaponCategory.Handgun),
                Spec(_smg, "SMG", WeaponCategory.SubmachineGun),
                Spec(_pistol, "Pistol", WeaponCategory.Handgun),
            });
            var rule = new LoadoutRule(new[]
            {
                new[] { WeaponCategory.AssaultRifle, WeaponCategory.SubmachineGun },
                new[] { WeaponCategory.Handgun },
            });
            var ledger = new ProgressionLedger(new ProgressionSpec(
                100, 50, 50, 250, 500, new[] { 0, 500, 1500, 3000 }, new[] { new ProgressionUnlock(_smg, 4) }));
            var selection = new LoadoutSelection(rule, catalog, null, new[] { _rifle, _handgun }, id => id != _smg && id != _pistol);
            _panel.Dispose();
            _panel = new LoadoutSelectPanel(_container, selection, ledger);
            _panel.Confirmed += ids => _confirmed.Add(ids);
        }

        private static string LockText(Button option)
        {
            return option.Q<Label>(className: LoadoutSelectPanel.LockLevelClass)?.text;
        }

        [Test]
        public void ALockedOptionShowsALockWithItsLevelAndCannotBePressed()
        {
            OpenWithLocks();

            var smg = _panel.OptionButton(0, 1);
            Assert.That(smg.ClassListContains(LoadoutSelectPanel.LockedOptionClass), Is.True);
            Assert.That(smg.Q(className: LoadoutSelectPanel.LockIconClass), Is.Not.Null);
            Assert.That(LockText(smg), Is.EqualTo("レベル 4 で解放"));
            Assert.That(smg.enabledSelf, Is.False);

            var rifle = _panel.OptionButton(0, 0);
            Assert.That(rifle.ClassListContains(LoadoutSelectPanel.LockedOptionClass), Is.False);
            Assert.That(rifle.Q(className: LoadoutSelectPanel.OptionLockClass), Is.Null);
            Assert.That(rifle.enabledSelf, Is.True);
        }

        [Test]
        public void AnOptionLockedForAnotherReasonShowsTheLockWithoutALevel()
        {
            OpenWithLocks();

            var pistol = _panel.OptionButton(1, 1);
            Assert.That(pistol.ClassListContains(LoadoutSelectPanel.LockedOptionClass), Is.True);
            Assert.That(LockText(pistol), Is.EqualTo("ロック中"));
        }

        [Test]
        public void WithoutALedgerTheLockHasNoLevel()
        {
            var rule = new LoadoutRule(new[] { new[] { WeaponCategory.AssaultRifle, WeaponCategory.SubmachineGun } });
            var catalog = new WeaponCatalog(new[]
            {
                Spec(_rifle, "weapon.rifle", WeaponCategory.AssaultRifle),
                Spec(_smg, "SMG", WeaponCategory.SubmachineGun),
            });
            _panel.Dispose();
            _panel = new LoadoutSelectPanel(_container, new LoadoutSelection(rule, catalog, null, null, id => id != _smg));

            Assert.That(LockText(_panel.OptionButton(0, 1)), Is.EqualTo("ロック中"));
        }

        [Test]
        public void ALockedOptionCannotBeSelectedNorCycledTo()
        {
            OpenWithLocks();

            Assert.That(_panel.Select(0, 1), Is.False);
            Assert.That(_panel.Cycle(1), Is.False, "the rifle is the only weapon of slot 0 that can be used");
            Assert.That(_panel.Selection.WeaponIds, Is.EqualTo(new[] { _rifle, _handgun }));
            Assert.That(_panel.Confirm(), Is.True);
            Assert.That(_confirmed[0].WeaponIds, Is.EqualTo(new[] { _rifle, _handgun }));
        }

        [Test]
        public void LeavingBusyKeepsTheLockedOptionsDisabled()
        {
            OpenWithLocks();

            _panel.SetBusy(true);
            Assert.That(_panel.OptionButton(0, 0).enabledSelf, Is.False);

            _panel.SetBusy(false);
            Assert.That(_panel.OptionButton(0, 0).enabledSelf, Is.True);
            Assert.That(_panel.OptionButton(0, 1).enabledSelf, Is.False);
            Assert.That(_panel.OptionButton(1, 1).enabledSelf, Is.False);
        }

        [Test]
        public void RejectsNulls()
        {
            Assert.Throws<ArgumentNullException>(() => new LoadoutSelectPanel(null, Selection()));
            Assert.Throws<ArgumentNullException>(() => new LoadoutSelectPanel(new VisualElement(), null));
        }
    }
}
