using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class LoadoutSelectionTests
    {
        private const string _rifle = "00000000-0000-4000-8000-000000000001";
        private const string _handgun = "00000000-0000-4000-8000-000000000002";
        private const string _smg = "00000000-0000-4000-8000-000000000003";
        private const string _pistol = "00000000-0000-4000-8000-000000000004";
        private const string _shotgun = "00000000-0000-4000-8000-000000000005";

        private static WeaponSpec Spec(string id, string displayName, WeaponCategory category)
        {
            return new WeaponSpec(id, displayName, category, FireMode.Auto, 30, 90, 0.1f, 2f, 25, 100f, DamageFalloff.None);
        }

        // 一覧の順: smg, handgun, rifle, pistol, shotgun (種別が混ざった順のまま並ぶことを確かめるため)
        private static WeaponCatalog Catalog()
        {
            return new WeaponCatalog(new[]
            {
                Spec(_smg, "SMG", WeaponCategory.SubmachineGun),
                Spec(_handgun, "Handgun", WeaponCategory.Handgun),
                Spec(_rifle, "Rifle", WeaponCategory.AssaultRifle),
                Spec(_pistol, "Pistol", WeaponCategory.Handgun),
                Spec(_shotgun, "Shotgun", WeaponCategory.Shotgun),
            });
        }

        private static LoadoutRule PrimaryAndSecondary()
        {
            return new LoadoutRule(new[]
            {
                new[] { WeaponCategory.AssaultRifle, WeaponCategory.SubmachineGun, WeaponCategory.Shotgun },
                new[] { WeaponCategory.Handgun },
            });
        }

        private static List<string> Ids(IReadOnlyList<WeaponSpec> weapons)
        {
            var ids = new List<string>();
            foreach (var weapon in weapons)
            {
                ids.Add(weapon.Id);
            }

            return ids;
        }

        [Test]
        public void ListsTheCatalogWeaponsEachSlotAcceptsInCatalogOrder()
        {
            var options = LoadoutSelection.ListOptions(PrimaryAndSecondary(), Catalog());

            Assert.That(options.Count, Is.EqualTo(2));
            Assert.That(Ids(options[0]), Is.EqualTo(new[] { _smg, _rifle, _shotgun }));
            Assert.That(Ids(options[1]), Is.EqualTo(new[] { _handgun, _pistol }));
        }

        [Test]
        public void ASlotThatAcceptsNothingInTheCatalogHasNoOptions()
        {
            var rule = new LoadoutRule(new[]
            {
                new[] { WeaponCategory.AssaultRifle },
                new[] { WeaponCategory.Launcher },
            });

            var selection = new LoadoutSelection(rule, Catalog(), null, null);

            Assert.That(selection.OptionsAt(1), Is.Empty);
            Assert.That(selection.SelectedIndexAt(1), Is.EqualTo(-1));
            Assert.That(selection.SelectedAt(1), Is.Null);
            Assert.That(selection.WeaponIds, Is.EqualTo(new[] { _rifle, string.Empty }));
            Assert.That(selection.Validate(), Does.Contain("slot 1"));
            Assert.That(selection.Cycle(1, 1), Is.False);
        }

        [Test]
        public void ListOptionsRejectsNulls()
        {
            Assert.Throws<ArgumentNullException>(() => LoadoutSelection.ListOptions(null, Catalog()));
            Assert.Throws<ArgumentNullException>(() => LoadoutSelection.ListOptions(PrimaryAndSecondary(), null));
            Assert.Throws<ArgumentNullException>(() => new LoadoutSelection(null, Catalog(), null, null));
            Assert.Throws<ArgumentNullException>(() => new LoadoutSelection(PrimaryAndSecondary(), null, null, null));
        }

        [Test]
        public void StartsFromThePreferredLoadout()
        {
            var selection = new LoadoutSelection(PrimaryAndSecondary(), Catalog(), new[] { _shotgun, _pistol }, new[] { _rifle, _handgun });

            Assert.That(selection.RestoredPreference, Is.True);
            Assert.That(selection.WeaponIds, Is.EqualTo(new[] { _shotgun, _pistol }));
            Assert.That(selection.SelectedIndexAt(0), Is.EqualTo(2));
            Assert.That(selection.SelectedIndexAt(1), Is.EqualTo(1));
            Assert.That(selection.Validate(), Is.Null);
        }

        [Test]
        public void FallsBackPerSlotWhenThePreferredWeaponIsNotOffered()
        {
            var selection = new LoadoutSelection(
                PrimaryAndSecondary(), Catalog(), new[] { "removed-weapon", _pistol }, new[] { _rifle, _handgun });

            Assert.That(selection.RestoredPreference, Is.False);
            Assert.That(selection.WeaponIds, Is.EqualTo(new[] { _rifle, _pistol }));
        }

        [Test]
        public void IgnoresAPreferenceWithADifferentSlotCount()
        {
            var selection = new LoadoutSelection(PrimaryAndSecondary(), Catalog(), new[] { _shotgun }, new[] { _rifle, _handgun });

            Assert.That(selection.RestoredPreference, Is.False);
            Assert.That(selection.WeaponIds, Is.EqualTo(new[] { _rifle, _handgun }));
        }

        [Test]
        public void WithoutPreferenceOrFallbackPicksTheFirstOptionNotUsedByAnEarlierSlot()
        {
            var rule = new LoadoutRule(new[]
            {
                new[] { WeaponCategory.Handgun },
                new[] { WeaponCategory.Handgun },
            });

            var selection = new LoadoutSelection(rule, Catalog(), null, null);

            Assert.That(selection.WeaponIds, Is.EqualTo(new[] { _handgun, _pistol }));
            Assert.That(selection.Validate(), Is.Null);
        }

        [Test]
        public void SelectAndCycleChangeOnlyThatSlotAndWrap()
        {
            var selection = new LoadoutSelection(PrimaryAndSecondary(), Catalog(), null, new[] { _rifle, _handgun });

            Assert.That(selection.Cycle(0, 1), Is.True);
            Assert.That(selection.SelectedAt(0).Id, Is.EqualTo(_shotgun));
            Assert.That(selection.Cycle(0, 1), Is.True);
            Assert.That(selection.SelectedAt(0).Id, Is.EqualTo(_smg), "moving past the last option wraps to the first");
            Assert.That(selection.Cycle(0, -1), Is.True);
            Assert.That(selection.SelectedAt(0).Id, Is.EqualTo(_shotgun), "moving before the first option wraps to the last");
            Assert.That(selection.SelectedAt(1).Id, Is.EqualTo(_handgun));

            Assert.That(selection.Select(1, 1), Is.True);
            Assert.That(selection.WeaponIds, Is.EqualTo(new[] { _shotgun, _pistol }));
            Assert.That(selection.Select(1, 2), Is.False);
            Assert.That(selection.Select(1, -1), Is.False);
            Assert.That(selection.SelectedAt(1).Id, Is.EqualTo(_pistol));
            Assert.Throws<ArgumentOutOfRangeException>(() => selection.Select(2, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => selection.OptionsAt(-1));
        }

        [Test]
        public void ValidateRejectsTheSameWeaponInTwoSlots()
        {
            var rule = new LoadoutRule(new[]
            {
                new[] { WeaponCategory.Handgun },
                new[] { WeaponCategory.Handgun },
            });
            var selection = new LoadoutSelection(rule, Catalog(), null, null);

            selection.Select(1, 0);

            Assert.That(selection.WeaponIds, Is.EqualTo(new[] { _handgun, _handgun }));
            Assert.That(selection.Validate(), Does.Contain("already in slot 0"));
        }

        private static Func<string, bool> AllBut(HashSet<string> locked)
        {
            return id => !locked.Contains(id);
        }

        [Test]
        public void LockedWeaponsStayListedAndAreReportedAsLocked()
        {
            var selection = new LoadoutSelection(
                PrimaryAndSecondary(), Catalog(), null, new[] { _smg, _handgun }, AllBut(new HashSet<string> { _rifle }));

            Assert.That(Ids(selection.OptionsAt(0)), Is.EqualTo(new[] { _smg, _rifle, _shotgun }));
            Assert.That(selection.IsAvailableAt(0, 0), Is.True);
            Assert.That(selection.IsAvailableAt(0, 1), Is.False);
            Assert.That(selection.IsAvailableAt(0, 2), Is.True);
            Assert.That(selection.IsAvailableAt(1, 1), Is.True);
            Assert.Throws<ArgumentOutOfRangeException>(() => selection.IsAvailableAt(0, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => selection.IsAvailableAt(0, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => selection.IsAvailableAt(2, 0));
        }

        [Test]
        public void WithoutAnAvailabilityCheckEveryWeaponIsAvailable()
        {
            var selection = new LoadoutSelection(PrimaryAndSecondary(), Catalog(), null, null);

            for (var slot = 0; slot < selection.SlotCount; slot++)
            {
                for (var option = 0; option < selection.OptionsAt(slot).Count; option++)
                {
                    Assert.That(selection.IsAvailableAt(slot, option), Is.True, "slot " + slot + " option " + option);
                }
            }
        }

        [Test]
        public void SelectRefusesALockedWeapon()
        {
            var selection = new LoadoutSelection(
                PrimaryAndSecondary(), Catalog(), null, new[] { _smg, _handgun }, AllBut(new HashSet<string> { _rifle }));

            Assert.That(selection.Select(0, 1), Is.False);
            Assert.That(selection.SelectedAt(0).Id, Is.EqualTo(_smg));
            Assert.That(selection.Select(0, 2), Is.True);
            Assert.That(selection.SelectedAt(0).Id, Is.EqualTo(_shotgun));
        }

        [Test]
        public void CycleSkipsLockedWeapons()
        {
            var selection = new LoadoutSelection(
                PrimaryAndSecondary(), Catalog(), null, new[] { _smg, _handgun }, AllBut(new HashSet<string> { _rifle }));

            Assert.That(selection.Cycle(0, 1), Is.True);
            Assert.That(selection.SelectedAt(0).Id, Is.EqualTo(_shotgun), "the locked rifle between them is skipped");
            Assert.That(selection.Cycle(0, 1), Is.True);
            Assert.That(selection.SelectedAt(0).Id, Is.EqualTo(_smg));
            Assert.That(selection.Cycle(0, -1), Is.True);
            Assert.That(selection.SelectedAt(0).Id, Is.EqualTo(_shotgun));
            Assert.That(selection.Cycle(0, 3), Is.True, "a step counts only the available weapons");
            Assert.That(selection.SelectedAt(0).Id, Is.EqualTo(_smg));
            Assert.That(selection.Cycle(0, 2), Is.False, "two steps over two available weapons comes back to the same one");
            Assert.That(selection.SelectedAt(0).Id, Is.EqualTo(_smg));
            Assert.That(selection.Cycle(0, int.MaxValue), Is.True);
            Assert.That(selection.SelectedAt(0).Id, Is.EqualTo(_shotgun));
            Assert.That(selection.Cycle(0, int.MinValue), Is.False);
            Assert.That(selection.SelectedAt(0).Id, Is.EqualTo(_shotgun));
        }

        [Test]
        public void CycleDoesNothingWhenNoOtherWeaponIsAvailable()
        {
            var selection = new LoadoutSelection(
                PrimaryAndSecondary(), Catalog(), null, null, AllBut(new HashSet<string> { _handgun }));

            Assert.That(selection.SelectedAt(1).Id, Is.EqualTo(_pistol));
            Assert.That(selection.Cycle(1, 1), Is.False);
            Assert.That(selection.Cycle(1, -1), Is.False);
            Assert.That(selection.SelectedAt(1).Id, Is.EqualTo(_pistol));
            Assert.That(selection.Validate(), Is.Null);
        }

        [Test]
        public void ALockedPreferenceOrFallbackIsNotSelected()
        {
            var lockedPreference = new LoadoutSelection(
                PrimaryAndSecondary(), Catalog(), new[] { _rifle, _pistol }, new[] { _smg, _handgun }, AllBut(new HashSet<string> { _rifle }));

            Assert.That(lockedPreference.RestoredPreference, Is.False);
            Assert.That(lockedPreference.WeaponIds, Is.EqualTo(new[] { _smg, _pistol }));

            var lockedFallback = new LoadoutSelection(
                PrimaryAndSecondary(), Catalog(), null, new[] { _rifle, _handgun }, AllBut(new HashSet<string> { _smg, _rifle, _handgun }));

            Assert.That(lockedFallback.WeaponIds, Is.EqualTo(new[] { _shotgun, _pistol }));
            Assert.That(lockedFallback.Validate(), Is.Null);
        }

        [Test]
        public void WithoutPreferenceTheFirstAvailableUnusedOptionIsPicked()
        {
            var rule = new LoadoutRule(new[]
            {
                new[] { WeaponCategory.Handgun },
                new[] { WeaponCategory.Handgun },
            });

            var selection = new LoadoutSelection(rule, Catalog(), null, null, AllBut(new HashSet<string> { _handgun }));

            Assert.That(selection.WeaponIds, Is.EqualTo(new[] { _pistol, _pistol }));
            Assert.That(selection.Validate(), Does.Contain("already in slot 0"));
        }

        [Test]
        public void ValidateRejectsASlotWhoseWeaponsAreAllLocked()
        {
            var selection = new LoadoutSelection(
                PrimaryAndSecondary(), Catalog(), null, new[] { _rifle, _handgun }, AllBut(new HashSet<string> { _handgun, _pistol }));

            Assert.That(selection.SelectedIndexAt(1), Is.EqualTo(0), "the first option is shown even though it is locked");
            Assert.That(selection.SelectedAt(1).Id, Is.EqualTo(_handgun));
            Assert.That(selection.Validate(), Does.Contain("slot 1").And.Contain(_handgun).And.Contain("not available"));
            Assert.That(selection.Select(1, 1), Is.False);
            Assert.That(selection.Cycle(1, 1), Is.False);
        }

        [Test]
        public void AvailabilityIsAskedEachTimeSoAnUnlockDuringTheScreenTakesEffect()
        {
            var locked = new HashSet<string> { _rifle };
            var selection = new LoadoutSelection(PrimaryAndSecondary(), Catalog(), null, new[] { _smg, _handgun }, AllBut(locked));

            Assert.That(selection.Select(0, 1), Is.False);

            locked.Remove(_rifle);

            Assert.That(selection.IsAvailableAt(0, 1), Is.True);
            Assert.That(selection.Select(0, 1), Is.True);
            Assert.That(selection.SelectedAt(0).Id, Is.EqualTo(_rifle));

            locked.Add(_rifle);

            Assert.That(selection.Validate(), Does.Contain("not available"));
            Assert.That(selection.Cycle(0, 1), Is.True, "cycling away from a weapon that became locked lands on the next available one");
            Assert.That(selection.SelectedAt(0).Id, Is.EqualTo(_shotgun));
        }

        [Test]
        public void TheChosenIdsAreWhatTheServerAccepts()
        {
            var selection = new LoadoutSelection(PrimaryAndSecondary(), Catalog(), new[] { _smg, _pistol }, null);
            var server = new ServerLoadout(
                PrimaryAndSecondary(), new[] { Catalog().Get(_rifle), Catalog().Get(_handgun) }, Catalog(), 0.5f);

            Assert.That(server.TryChoose(1, selection.WeaponIds, out var reason), Is.True, reason);
            Assert.That(Ids(server.WeaponsFor(1)), Is.EqualTo(new[] { _smg, _pistol }));
        }
    }
}
