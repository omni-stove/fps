using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class LoadoutPreferenceApplierTests
    {
        private const string Beacon = "beacon";
        private const string Grapple = "grapple";
        private const string Mine = "mine";

        private readonly List<Object> _created = new List<Object>();
        private readonly List<string> _warnings = new List<string>();
        private readonly List<string> _infos = new List<string>();
        private LoadoutTestAssets _assets;
        private AbilityDefinition _beacon;
        private AbilityDefinition _grapple;
        private AbilityDefinition _mine;
        private AbilityCatalogAsset _abilityCatalog;
        private WeaponDefinition[] _defaultWeapons;
        private AbilityDefinition[] _defaultAbilities;

        [SetUp]
        public void SetUp()
        {
            _assets = new LoadoutTestAssets();
            _beacon = Ability(Beacon);
            _grapple = Ability(Grapple);
            _mine = Ability(Mine);
            _abilityCatalog = ScriptableObject.CreateInstance<AbilityCatalogAsset>();
            _abilityCatalog.name = "TestAbilities";
            _abilityCatalog.SetAbilities(new[] { _beacon, _grapple, _mine });
            _created.Add(_abilityCatalog);
            _assets.Loadout.SetAbilities(new[] { _grapple });

            _defaultWeapons = new[] { _assets.Rifle, _assets.Handgun };
            _defaultAbilities = new[] { _grapple };
            Application.logMessageReceived += Capture;
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= Capture;
            _warnings.Clear();
            _infos.Clear();
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

        [Test]
        public void AFittingSaveReturnsTheSavedWeaponsAndAbilitiesInOrder()
        {
            var saved = new LoadoutChoice(
                new[] { LoadoutTestAssets.SmgId, TestWeapons.HandgunId }, new[] { Mine, Beacon });

            var applied = Apply(saved);

            Assert.That(applied.Weapons, Is.EqualTo(new[] { _assets.Smg, _assets.Handgun }));
            Assert.That(applied.Abilities, Is.EqualTo(new[] { _mine, _beacon }));
            Assert.That(applied.RestoredWeapons, Is.True);
            Assert.That(applied.RestoredAbilities, Is.True);
            Assert.That(_warnings, Is.Empty);
            Assert.That(_infos, Is.EqualTo(new[]
            {
                "[Loadout] using the saved loadout weapons=" + LoadoutTestAssets.SmgId + "," + TestWeapons.HandgunId
                + " abilities=mine,beacon",
            }));
        }

        [Test]
        public void ASavedEmptyAbilitySlotStaysEmpty()
        {
            var saved = new LoadoutChoice(
                new[] { TestWeapons.RifleId, TestWeapons.HandgunId }, new[] { AbilitySelection.None, Beacon });

            var applied = Apply(saved);

            Assert.That(applied.Abilities, Is.EqualTo(new[] { null, _beacon }));
            Assert.That(applied.RestoredAbilities, Is.True);
            Assert.That(_warnings, Is.Empty);
            Assert.That(_infos, Is.EqualTo(new[]
            {
                "[Loadout] using the saved loadout weapons=" + TestWeapons.RifleId + "," + TestWeapons.HandgunId
                + " abilities=-,beacon",
            }));
        }

        [Test]
        public void AWeaponMissingFromTheCatalogFallsBackToTheDefaultWeaponsWithAWarning()
        {
            var saved = new LoadoutChoice(new[] { "missing-weapon", TestWeapons.HandgunId }, new[] { Mine, Beacon });

            var applied = Apply(saved);

            Assert.That(applied.Weapons, Is.EqualTo(_defaultWeapons));
            Assert.That(applied.RestoredWeapons, Is.False);
            Assert.That(applied.Abilities, Is.EqualTo(new[] { _mine, _beacon }), "the abilities still fit, so they are used");
            Assert.That(applied.RestoredAbilities, Is.True);
            Assert.That(_warnings, Is.EqualTo(new[]
            {
                "[Loadout] the saved weapons are not used: weapon 'missing-weapon' in slot 0 is not in the weapon catalog;"
                + " using the scene's weapons",
            }));
            Assert.That(_infos, Is.EqualTo(new[] { "[Loadout] using the saved loadout weapons=default abilities=mine,beacon" }));
        }

        [Test]
        public void AnAbilityMissingFromTheCatalogFallsBackToTheDefaultAbilitiesWithAWarning()
        {
            var saved = new LoadoutChoice(new[] { LoadoutTestAssets.SmgId, TestWeapons.HandgunId }, new[] { Beacon, "missing-ability" });

            var applied = Apply(saved);

            Assert.That(applied.Weapons, Is.EqualTo(new[] { _assets.Smg, _assets.Handgun }), "the weapons still fit, so they are used");
            Assert.That(applied.RestoredWeapons, Is.True);
            Assert.That(applied.Abilities, Is.EqualTo(_defaultAbilities));
            Assert.That(applied.RestoredAbilities, Is.False);
            Assert.That(_warnings, Is.EqualTo(new[]
            {
                "[Loadout] the saved abilities are not used: ability 'missing-ability' in slot 1 is not in the ability catalog;"
                + " using the scene's abilities",
            }));
        }

        [Test]
        public void IdsMissingFromBothCatalogsFallBackToEverythingDefaultWithAWarningEach()
        {
            var saved = new LoadoutChoice(new[] { TestWeapons.RifleId, "missing-weapon" }, new[] { "missing-ability", AbilitySelection.None });

            var applied = Apply(saved);

            Assert.That(applied.Weapons, Is.EqualTo(_defaultWeapons));
            Assert.That(applied.Abilities, Is.EqualTo(_defaultAbilities));
            Assert.That(applied.RestoredWeapons, Is.False);
            Assert.That(applied.RestoredAbilities, Is.False);
            Assert.That(_warnings, Is.EqualTo(new[]
            {
                "[Loadout] the saved weapons are not used: weapon 'missing-weapon' in slot 1 is not in the weapon catalog;"
                + " using the scene's weapons",
                "[Loadout] the saved abilities are not used: ability 'missing-ability' in slot 0 is not in the ability catalog;"
                + " using the scene's abilities",
            }));
            Assert.That(_infos, Is.Empty);
        }

        [Test]
        public void AWeaponTheSlotDoesNotAcceptFallsBackWithTheSlotInTheWarning()
        {
            var applied = Apply(new LoadoutChoice(new[] { TestWeapons.HandgunId, TestWeapons.HandgunId }));

            Assert.That(applied.Weapons, Is.EqualTo(_defaultWeapons));
            Assert.That(_warnings, Is.EqualTo(new[]
            {
                "[Loadout] the saved weapons are not used: slot 0 does not accept weapon '" + TestWeapons.HandgunId + "';"
                + " using the scene's weapons",
            }));
        }

        [Test]
        public void ASaveWithADifferentNumberOfSlotsFallsBack()
        {
            var applied = Apply(new LoadoutChoice(new[] { TestWeapons.RifleId }, new[] { Beacon }));

            Assert.That(applied.Weapons, Is.EqualTo(_defaultWeapons));
            Assert.That(applied.Abilities, Is.EqualTo(_defaultAbilities));
            Assert.That(_warnings, Is.EqualTo(new[]
            {
                "[Loadout] the saved weapons are not used: it has 1 weapons but the loadout has 2 slots; using the scene's weapons",
                "[Loadout] the saved abilities are not used: it has 1 abilities but there are 2 ability slots; using the scene's abilities",
            }));
        }

        [Test]
        public void TheSameAbilityInTwoSlotsFallsBack()
        {
            var applied = Apply(new LoadoutChoice(new[] { TestWeapons.RifleId, TestWeapons.HandgunId }, new[] { Beacon, Beacon }));

            Assert.That(applied.Abilities, Is.EqualTo(_defaultAbilities));
            Assert.That(applied.RestoredWeapons, Is.True);
            Assert.That(_warnings, Is.EqualTo(new[]
            {
                "[Loadout] the saved abilities are not used: ability 'beacon' in slot 1 is already chosen in another slot;"
                + " using the scene's abilities",
            }));
        }

        [Test]
        public void ALockedWeaponOrAbilityFallsBack()
        {
            var saved = new LoadoutChoice(new[] { LoadoutTestAssets.SmgId, TestWeapons.HandgunId }, new[] { Mine, AbilitySelection.None });

            var applied = LoadoutPreferenceApplier.Apply(
                saved, _assets.Loadout, _assets.Catalog, _abilityCatalog, _defaultWeapons, _defaultAbilities, null,
                id => id != LoadoutTestAssets.SmgId, id => id != Mine);

            Assert.That(applied.Weapons, Is.EqualTo(_defaultWeapons));
            Assert.That(applied.Abilities, Is.EqualTo(_defaultAbilities));
            Assert.That(_warnings, Is.EqualTo(new[]
            {
                "[Loadout] the saved weapons are not used: weapon '" + LoadoutTestAssets.SmgId + "' in slot 0 is not available;"
                + " using the scene's weapons",
                "[Loadout] the saved abilities are not used: ability 'mine' in slot 0 is not available; using the scene's abilities",
            }));
        }

        [Test]
        public void ASaveWithoutAbilitiesUsesTheDefaultAbilitiesWithoutAWarning()
        {
            var applied = Apply(new LoadoutChoice(new[] { LoadoutTestAssets.SmgId, TestWeapons.HandgunId }));

            Assert.That(applied.Weapons, Is.EqualTo(new[] { _assets.Smg, _assets.Handgun }));
            Assert.That(applied.Abilities, Is.EqualTo(_defaultAbilities));
            Assert.That(applied.RestoredAbilities, Is.False);
            Assert.That(_warnings, Is.Empty);
            Assert.That(_infos, Is.EqualTo(new[]
            {
                "[Loadout] using the saved loadout weapons=" + LoadoutTestAssets.SmgId + "," + TestWeapons.HandgunId + " abilities=default",
            }));
        }

        [Test]
        public void NoSaveUsesTheDefaultsWithoutAnyLog()
        {
            var applied = Apply(null);

            Assert.That(applied.Weapons, Is.EqualTo(_defaultWeapons));
            Assert.That(applied.Abilities, Is.EqualTo(_defaultAbilities));
            Assert.That(applied.RestoredWeapons, Is.False);
            Assert.That(applied.RestoredAbilities, Is.False);
            Assert.That(_warnings, Is.Empty);
            Assert.That(_infos, Is.Empty);
        }

        [Test]
        public void SavedAbilitiesWithoutAnAbilityCatalogFallBack()
        {
            var saved = new LoadoutChoice(new[] { TestWeapons.RifleId, TestWeapons.HandgunId }, new[] { Beacon, AbilitySelection.None });

            var applied = LoadoutPreferenceApplier.Apply(saved, _assets.Loadout, _assets.Catalog, null, _defaultWeapons, _defaultAbilities);

            Assert.That(applied.RestoredWeapons, Is.True);
            Assert.That(applied.Abilities, Is.EqualTo(_defaultAbilities));
            Assert.That(_warnings, Is.EqualTo(new[]
            {
                "[Loadout] the saved abilities are not used: no ability catalog is assigned, so abilities cannot be chosen;"
                + " using the scene's abilities",
            }));
        }

        [Test]
        public void AMissingLoadoutOrWeaponCatalogFallsBack()
        {
            var saved = new LoadoutChoice(new[] { TestWeapons.RifleId, TestWeapons.HandgunId }, new[] { Beacon, AbilitySelection.None });

            var withoutLoadout = LoadoutPreferenceApplier.Apply(saved, null, _assets.Catalog, _abilityCatalog, _defaultWeapons, _defaultAbilities);

            Assert.That(withoutLoadout.Weapons, Is.EqualTo(_defaultWeapons));
            Assert.That(withoutLoadout.Abilities, Is.EqualTo(_defaultAbilities));
            Assert.That(_warnings, Is.EqualTo(new[]
            {
                "[Loadout] the saved loadout is not used: no loadout is assigned; using the scene's loadout",
            }));

            _warnings.Clear();
            var withoutCatalog = LoadoutPreferenceApplier.Apply(saved, _assets.Loadout, null, _abilityCatalog, _defaultWeapons, _defaultAbilities);

            Assert.That(withoutCatalog.Weapons, Is.EqualTo(_defaultWeapons));
            Assert.That(withoutCatalog.Abilities, Is.EqualTo(new[] { _beacon, null }));
            Assert.That(_warnings, Is.EqualTo(new[]
            {
                "[Loadout] the saved weapons are not used: no weapon catalog is assigned, so the loadout cannot be changed;"
                + " using the scene's weapons",
            }));
        }

        [Test]
        public void TheResultIsACopyOfTheDefaults()
        {
            var applied = Apply(null);
            _defaultWeapons[0] = _assets.Smg;

            Assert.That(applied.Weapons, Is.EqualTo(new[] { _assets.Rifle, _assets.Handgun }));
        }

        [Test]
        public void MissingDefaultsAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() =>
                LoadoutPreferenceApplier.Apply(null, _assets.Loadout, _assets.Catalog, _abilityCatalog, null, _defaultAbilities));
            Assert.Throws<ArgumentNullException>(() =>
                LoadoutPreferenceApplier.Apply(null, _assets.Loadout, _assets.Catalog, _abilityCatalog, _defaultWeapons, null));
        }

        private AppliedLoadout Apply(LoadoutChoice saved)
        {
            return LoadoutPreferenceApplier.Apply(
                saved, _assets.Loadout, _assets.Catalog, _abilityCatalog, _defaultWeapons, _defaultAbilities);
        }

        private AbilityDefinition Ability(string id)
        {
            var ability = ScriptableObject.CreateInstance<RecordingAbilityDefinition>();
            ability.name = id + "Asset";
            ability.Id = id;
            _created.Add(ability);
            return ability;
        }

        private void Capture(string message, string stackTrace, LogType type)
        {
            if (!message.StartsWith(LoadoutPreferenceLog.LogTag, StringComparison.Ordinal))
            {
                return;
            }

            if (type == LogType.Warning)
            {
                _warnings.Add(message);
            }
            else if (type == LogType.Log)
            {
                _infos.Add(message);
            }
        }
    }
}
