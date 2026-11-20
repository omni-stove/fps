using System.Collections.Generic;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class LoadoutSelectionAssetsTests
    {
        private readonly List<Object> _created = new List<Object>();
        private LoadoutTestAssets _assets;

        [SetUp]
        public void SetUp()
        {
            _assets = new LoadoutTestAssets();
        }

        [TearDown]
        public void TearDown()
        {
            _assets.Dispose();
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        private RecordingAbilityDefinition Ability(string id)
        {
            var ability = ScriptableObject.CreateInstance<RecordingAbilityDefinition>();
            ability.name = id + "Asset";
            ability.Id = id;
            _created.Add(ability);
            return ability;
        }

        private AbilityCatalogAsset AbilityCatalog(params AbilityDefinition[] abilities)
        {
            var catalog = ScriptableObject.CreateInstance<AbilityCatalogAsset>();
            catalog.name = "TestAbilities";
            catalog.SetAbilities(abilities);
            _created.Add(catalog);
            return catalog;
        }

        [Test]
        public void AbilitiesStartFromTheLoadoutsAbilitiesAndListTheCatalogInEverySlot()
        {
            var beacon = Ability("beacon");
            var grapple = Ability("grapple");
            var mine = Ability("mine");
            _assets.Loadout.SetAbilities(new AbilityDefinition[] { grapple, null });

            Assert.That(
                LoadoutSelectionAssets.TryCreateAbilities(
                    _assets.Loadout, AbilityCatalog(beacon, grapple, mine), null, out var selection, out var error),
                Is.True, error);

            Assert.That(selection.SlotCount, Is.EqualTo(AbilityHost.SlotCount));
            Assert.That(selection.OptionsAt(0), Is.EqualTo(new[] { AbilitySelection.None, "beacon", "grapple", "mine" }));
            Assert.That(selection.OptionsAt(1), Is.EqualTo(new[] { AbilitySelection.None, "beacon", "grapple", "mine" }));
            Assert.That(selection.AbilityIds, Is.EqualTo(new[] { "grapple", AbilitySelection.None }), "the loadout's empty slot stays empty");
            Assert.That(selection.RestoredPreference, Is.False);
        }

        [Test]
        public void AbilitiesStartFromThePreferenceWhenEverySlotCanBeRestored()
        {
            var beacon = Ability("beacon");
            var grapple = Ability("grapple");
            _assets.Loadout.SetAbilities(new AbilityDefinition[] { grapple });
            var catalog = AbilityCatalog(beacon, grapple);

            LoadoutSelectionAssets.TryCreateAbilities(_assets.Loadout, catalog, new[] { "beacon", "grapple" }, out var restored, out _);
            Assert.That(restored.RestoredPreference, Is.True);
            Assert.That(restored.AbilityIds, Is.EqualTo(new[] { "beacon", "grapple" }));

            LoadoutSelectionAssets.TryCreateAbilities(_assets.Loadout, catalog, new[] { "missing", "beacon" }, out var partly, out _);
            Assert.That(partly.RestoredPreference, Is.False);
            Assert.That(partly.AbilityIds, Is.EqualTo(new[] { "grapple", "beacon" }), "the slot that cannot be restored takes the loadout's ability");
        }

        [Test]
        public void ALockedAbilityIsListedButNotRestored()
        {
            var beacon = Ability("beacon");
            var grapple = Ability("grapple");

            Assert.That(
                LoadoutSelectionAssets.TryCreateAbilities(
                    _assets.Loadout, AbilityCatalog(beacon, grapple), new[] { "beacon", AbilitySelection.None },
                    out var selection, out var error, id => id != "beacon"),
                Is.True, error);

            Assert.That(selection.OptionsAt(0)[1], Is.EqualTo("beacon"));
            Assert.That(selection.IsAvailableAt(0, 1), Is.False);
            Assert.That(selection.RestoredPreference, Is.False);
            Assert.That(selection.AbilityIds, Is.EqualTo(new[] { AbilitySelection.None, AbilitySelection.None }));
        }

        [Test]
        public void WithoutAnAbilityCatalogNoAbilitiesAreOffered()
        {
            Assert.That(
                LoadoutSelectionAssets.TryCreateAbilities(_assets.Loadout, null, new[] { "beacon" }, out var selection, out var error),
                Is.True);
            Assert.That(selection, Is.Null);
            Assert.That(error, Is.Null);
        }

        [Test]
        public void AbilitiesFailWithAReasonWhenTheLoadoutIsMissingOrTheCatalogIsBroken()
        {
            var beacon = Ability("beacon");

            Assert.That(LoadoutSelectionAssets.TryCreateAbilities(null, AbilityCatalog(beacon), null, out var selection, out var error), Is.False);
            Assert.That(selection, Is.Null);
            Assert.That(error, Is.EqualTo("no loadout is assigned"));

            Assert.That(LoadoutSelectionAssets.TryCreateAbilities(_assets.Loadout, AbilityCatalog(), null, out selection, out error), Is.False);
            Assert.That(selection, Is.Null);
            Assert.That(error, Is.EqualTo("ability catalog 'TestAbilities' has no abilities"));

            Assert.That(
                LoadoutSelectionAssets.TryCreateAbilities(_assets.Loadout, AbilityCatalog(beacon, null), null, out _, out error), Is.False);
            Assert.That(error, Is.EqualTo("ability catalog 'TestAbilities' ability #1 is empty"));
        }

        [Test]
        public void DefaultAbilityIdsFollowTheLoadoutWithNoneForEmptySlots()
        {
            _assets.Loadout.SetAbilities(new AbilityDefinition[] { null, Ability("mine") });

            Assert.That(LoadoutSelectionAssets.DefaultAbilityIds(_assets.Loadout), Is.EqualTo(new[] { AbilitySelection.None, "mine" }));

            _assets.Loadout.SetAbilities(null);
            Assert.That(LoadoutSelectionAssets.DefaultAbilityIds(_assets.Loadout), Is.Empty);
            Assert.Throws<System.ArgumentNullException>(() => LoadoutSelectionAssets.DefaultAbilityIds(null));
        }

        [Test]
        public void ListsTheCatalogBySlotAndStartsFromTheDefaultLoadout()
        {
            Assert.That(
                LoadoutSelectionAssets.TryCreate(_assets.Loadout, _assets.Catalog, null, null, out var selection, out var error),
                Is.True, error);

            Assert.That(selection.SlotCount, Is.EqualTo(2));
            Assert.That(selection.OptionsAt(0).Count, Is.EqualTo(2));
            Assert.That(selection.OptionsAt(0)[0].Id, Is.EqualTo(TestWeapons.RifleId));
            Assert.That(selection.OptionsAt(0)[1].Id, Is.EqualTo(LoadoutTestAssets.SmgId));
            Assert.That(selection.OptionsAt(1).Count, Is.EqualTo(1));
            Assert.That(selection.OptionsAt(1)[0].Id, Is.EqualTo(TestWeapons.HandgunId));
            Assert.That(selection.WeaponIds, Is.EqualTo(new[] { TestWeapons.RifleId, TestWeapons.HandgunId }));
            Assert.That(selection.RestoredPreference, Is.False);
        }

        [Test]
        public void StartsFromThePreferenceThenTheFallback()
        {
            var saved = new[] { LoadoutTestAssets.SmgId, TestWeapons.HandgunId };

            LoadoutSelectionAssets.TryCreate(_assets.Loadout, _assets.Catalog, saved, null, out var restored, out _);
            Assert.That(restored.RestoredPreference, Is.True);
            Assert.That(restored.WeaponIds, Is.EqualTo(saved));

            LoadoutSelectionAssets.TryCreate(_assets.Loadout, _assets.Catalog, null, saved, out var fromFallback, out _);
            Assert.That(fromFallback.RestoredPreference, Is.False);
            Assert.That(fromFallback.WeaponIds, Is.EqualTo(saved));
        }

        [Test]
        public void AWeaponThatIsNotAvailableIsListedButCannotBeChosen()
        {
            var saved = new[] { LoadoutTestAssets.SmgId, TestWeapons.HandgunId };

            Assert.That(
                LoadoutSelectionAssets.TryCreate(
                    _assets.Loadout, _assets.Catalog, saved, null, out var selection, out var error, id => id != LoadoutTestAssets.SmgId),
                Is.True, error);

            Assert.That(selection.OptionsAt(0)[1].Id, Is.EqualTo(LoadoutTestAssets.SmgId), "the locked weapon stays in the list");
            Assert.That(selection.IsAvailableAt(0, 0), Is.True);
            Assert.That(selection.IsAvailableAt(0, 1), Is.False);
            Assert.That(selection.RestoredPreference, Is.False, "the saved locked weapon is not restored");
            Assert.That(selection.WeaponIds, Is.EqualTo(new[] { TestWeapons.RifleId, TestWeapons.HandgunId }));
            Assert.That(selection.Select(0, 1), Is.False);
            Assert.That(selection.WeaponIds, Is.EqualTo(new[] { TestWeapons.RifleId, TestWeapons.HandgunId }));
            Assert.That(selection.Validate(), Is.Null);
        }

        [Test]
        public void AWeaponBecomesAvailableWhenThePredicateSaysSo()
        {
            var unlocked = false;
            LoadoutSelectionAssets.TryCreate(
                _assets.Loadout, _assets.Catalog, null, null, out var selection, out _, id => unlocked || id != LoadoutTestAssets.SmgId);
            Assert.That(selection.Select(0, 1), Is.False);

            unlocked = true;

            Assert.That(selection.IsAvailableAt(0, 1), Is.True);
            Assert.That(selection.Select(0, 1), Is.True);
            Assert.That(selection.WeaponIds, Is.EqualTo(new[] { LoadoutTestAssets.SmgId, TestWeapons.HandgunId }));
        }

        [Test]
        public void FailsWithAReasonWhenAnAssetIsMissingOrBroken()
        {
            Assert.That(LoadoutSelectionAssets.TryCreate(null, _assets.Catalog, null, null, out var selection, out var error), Is.False);
            Assert.That(selection, Is.Null);
            Assert.That(error, Does.Contain("loadout"));

            Assert.That(LoadoutSelectionAssets.TryCreate(_assets.Loadout, null, null, null, out _, out error), Is.False);
            Assert.That(error, Does.Contain("catalog"));

            var empty = ScriptableObject.CreateInstance<WeaponCatalogAsset>();
            try
            {
                Assert.That(LoadoutSelectionAssets.TryCreate(_assets.Loadout, empty, null, null, out _, out error), Is.False);
                Assert.That(error, Does.Contain("no weapons"));
            }
            finally
            {
                Object.DestroyImmediate(empty);
            }
        }
    }
}
