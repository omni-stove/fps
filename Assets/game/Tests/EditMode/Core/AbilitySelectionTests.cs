using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class AbilitySelectionTests
    {
        private const string _beacon = "00000000-0000-4000-8000-0000000000b1";
        private const string _mine = "00000000-0000-4000-8000-0000000000b2";
        private const string _drone = "00000000-0000-4000-8000-0000000000b3";

        private static readonly string[] _all = { _beacon, _mine, _drone };

        private static AbilitySelection Select(string[] preferred = null, string[] fallback = null, Func<string, bool> isAvailable = null)
        {
            return new AbilitySelection(_all, preferred, fallback, isAvailable);
        }

        [Test]
        public void EachSlotListsNoneFirstThenTheAbilities()
        {
            var selection = Select();

            Assert.That(AbilitySelection.DefaultSlotCount, Is.EqualTo(2));
            Assert.That(AbilitySelection.None, Is.Empty);
            Assert.That(AbilitySelection.NoneIndex, Is.Zero);
            Assert.That(selection.SlotCount, Is.EqualTo(2));
            Assert.That(selection.OptionsAt(0), Is.EqualTo(new[] { string.Empty, _beacon, _mine, _drone }));
            Assert.That(selection.OptionsAt(1), Is.EqualTo(new[] { string.Empty, _beacon, _mine, _drone }));
        }

        [Test]
        public void WithNothingToRestoreEverySlotIsNone()
        {
            var selection = Select();

            Assert.That(selection.AbilityIds, Is.EqualTo(new[] { string.Empty, string.Empty }));
            Assert.That(selection.SelectedIndexAt(0), Is.EqualTo(AbilitySelection.NoneIndex));
            Assert.That(selection.RestoredPreference, Is.False);
        }

        [Test]
        public void ADefaultInAnEarlierSlotDoesNotDisplaceASavedChoiceInALaterSlot()
        {
            var selection = Select(new[] { "gone", _beacon }, new[] { _beacon, _mine });

            Assert.That(selection.AbilityIds, Is.EqualTo(new[] { string.Empty, _beacon }));
            Assert.That(selection.RestoredPreference, Is.False);
        }

        [Test]
        public void TheSavedChoiceIsRestored()
        {
            var selection = Select(new[] { _drone, string.Empty }, new[] { _beacon, _mine });

            Assert.That(selection.AbilityIds, Is.EqualTo(new[] { _drone, string.Empty }));
            Assert.That(selection.SelectedIndexAt(0), Is.EqualTo(3));
            Assert.That(selection.RestoredPreference, Is.True);
        }

        [Test]
        public void ADefaultShorterThanTheSlotsLeavesTheRestNone()
        {
            var selection = Select(null, new[] { _beacon });

            Assert.That(selection.AbilityIds, Is.EqualTo(new[] { _beacon, string.Empty }));
            Assert.That(selection.RestoredPreference, Is.False);
        }

        [Test]
        public void ASavedChoiceOfTheWrongLengthFallsBackToTheDefault()
        {
            var selection = Select(new[] { _drone }, new[] { _beacon, _mine });

            Assert.That(selection.AbilityIds, Is.EqualTo(new[] { _beacon, _mine }));
            Assert.That(selection.RestoredPreference, Is.False);
        }

        [Test]
        public void AnUnknownOrLockedSavedAbilityFallsBackOnlyInItsSlot()
        {
            var selection = Select(new[] { "gone", _drone }, new[] { _beacon, _mine });

            Assert.That(selection.AbilityIds, Is.EqualTo(new[] { _beacon, _drone }));
            Assert.That(selection.RestoredPreference, Is.False);

            var locked = Select(new[] { _mine, _drone }, new[] { _beacon, string.Empty }, id => id != _mine);

            Assert.That(locked.AbilityIds, Is.EqualTo(new[] { _beacon, _drone }));
            Assert.That(locked.RestoredPreference, Is.False);
        }

        [Test]
        public void TheSameAbilityIsNotRestoredInTwoSlots()
        {
            var selection = Select(new[] { _mine, _mine }, new[] { _beacon, _drone });

            Assert.That(selection.AbilityIds, Is.EqualTo(new[] { _mine, _drone }));
            Assert.That(selection.RestoredPreference, Is.False);

            var clash = Select(null, new[] { _mine, _mine });

            Assert.That(clash.AbilityIds, Is.EqualTo(new[] { _mine, string.Empty }));
        }

        [Test]
        public void SelectTakesAnAvailableOptionOnly()
        {
            var selection = Select(null, new[] { _beacon }, id => id != _drone);

            Assert.That(selection.Select(1, 2), Is.True);
            Assert.That(selection.AbilityIds, Is.EqualTo(new[] { _beacon, _mine }));
            Assert.That(selection.Select(1, 1), Is.False, "the beacon is already in slot 0");
            Assert.That(selection.Select(1, 3), Is.False, "the drone is locked");
            Assert.That(selection.Select(1, 4), Is.False);
            Assert.That(selection.Select(1, -1), Is.False);
            Assert.That(selection.AbilityIds, Is.EqualTo(new[] { _beacon, _mine }));
            Assert.That(selection.Select(0, AbilitySelection.NoneIndex), Is.True);
            Assert.That(selection.Select(1, 1), Is.True, "the beacon is free once slot 0 is none");
            Assert.That(selection.AbilityIds, Is.EqualTo(new[] { string.Empty, _beacon }));
        }

        [Test]
        public void IsAvailableAtReportsLockedAndTakenOptions()
        {
            var selection = Select(null, new[] { _beacon }, id => id != _drone);

            Assert.That(selection.IsAvailableAt(1, AbilitySelection.NoneIndex), Is.True);
            Assert.That(selection.IsAvailableAt(1, 1), Is.False);
            Assert.That(selection.IsAvailableAt(1, 2), Is.True);
            Assert.That(selection.IsAvailableAt(1, 3), Is.False);
            Assert.That(selection.IsAvailableAt(0, 1), Is.True, "a slot's own ability stays available to it");
            Assert.Throws<ArgumentOutOfRangeException>(() => selection.IsAvailableAt(1, 4));
        }

        [Test]
        public void CycleSkipsTakenAndLockedOptionsAndWrapsThroughNone()
        {
            var selection = Select(null, new[] { _mine }, id => id != _drone);

            Assert.That(selection.Cycle(1, 1), Is.True);
            Assert.That(selection.SelectedAt(1), Is.EqualTo(_beacon));
            Assert.That(selection.Cycle(1, 1), Is.True);
            Assert.That(selection.SelectedAt(1), Is.EqualTo(string.Empty), "mine is taken and the drone is locked");
            Assert.That(selection.Cycle(1, -1), Is.True);
            Assert.That(selection.SelectedAt(1), Is.EqualTo(_beacon));
            Assert.That(selection.Cycle(1, 0), Is.False);
        }

        [Test]
        public void ALargeStepMovesByTheRemainderOfTheAvailableCount()
        {
            var selection = Select();

            // 選べるのは「なし」と 3 つの能力の 4 つ
            Assert.That(selection.Cycle(0, 4), Is.False);
            Assert.That(selection.SelectedAt(0), Is.EqualTo(string.Empty));
            Assert.That(selection.Cycle(0, int.MaxValue), Is.True);
            Assert.That(selection.SelectedIndexAt(0), Is.EqualTo(int.MaxValue % 4));
            Assert.That(selection.Cycle(0, int.MinValue), Is.False, "int.MinValue is a multiple of 4");
        }

        [Test]
        public void ASlotWithOnlyNoneCannotCycle()
        {
            var selection = new AbilitySelection(Array.Empty<string>(), null, null);

            Assert.That(selection.OptionsAt(0), Is.EqualTo(new[] { string.Empty }));
            Assert.That(selection.Cycle(0, 1), Is.False);
            Assert.That(selection.AbilityIds, Is.EqualTo(new[] { string.Empty, string.Empty }));
        }

        [Test]
        public void SlotsCanHaveTheirOwnOptionsAndCount()
        {
            var options = new[] { new[] { _beacon }, new[] { _mine, _drone }, System.Array.Empty<string>() };
            var selection = new AbilitySelection(options, new[] { _beacon, _drone, string.Empty }, null);

            Assert.That(selection.SlotCount, Is.EqualTo(3));
            Assert.That(selection.OptionsAt(1), Is.EqualTo(new[] { string.Empty, _mine, _drone }));
            Assert.That(selection.AbilityIds, Is.EqualTo(new[] { _beacon, _drone, string.Empty }));
            Assert.That(selection.RestoredPreference, Is.True);
            Assert.That(new AbilitySelection(_all, null, null, null, 1).SlotCount, Is.EqualTo(1));
        }

        [Test]
        public void TheOptionsAreCopied()
        {
            var ids = new[] { _beacon, _mine };
            var selection = new AbilitySelection(ids, null, null);

            ids[0] = _drone;

            Assert.That(selection.OptionsAt(0), Is.EqualTo(new[] { string.Empty, _beacon, _mine }));
        }

        [Test]
        public void BadArgumentsAreRefused()
        {
            Assert.Throws<ArgumentNullException>(() => new AbilitySelection((string[])null, null, null));
            Assert.Throws<ArgumentNullException>(() => new AbilitySelection((string[][])null, null, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AbilitySelection(_all, null, null, null, -1));
            Assert.Throws<ArgumentException>(() => new AbilitySelection(new[] { _beacon, string.Empty }, null, null));
            Assert.Throws<ArgumentException>(() => new AbilitySelection(new[] { _beacon, null }, null, null));
            Assert.Throws<ArgumentException>(() => new AbilitySelection(new[] { _beacon, _beacon }, null, null));
            Assert.Throws<ArgumentException>(() => new AbilitySelection(new[] { new[] { _beacon }, null }, null, null));

            var selection = Select();
            Assert.Throws<ArgumentOutOfRangeException>(() => selection.OptionsAt(2));
            Assert.Throws<ArgumentOutOfRangeException>(() => selection.SelectedAt(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => selection.Select(2, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => selection.Cycle(2, 1));
        }
    }
}
