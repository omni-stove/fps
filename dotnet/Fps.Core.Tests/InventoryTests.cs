using System;
using System.Linq;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class InventoryTests
    {
        private const string _herbId = "0d3c6f1a-5b2e-4c7d-8a9f-1e2b3c4d5e6f";
        private const string _ammoId = "7a8b9c0d-1e2f-4a3b-9c4d-5e6f7a8b9c0d";
        private const string _rifleCaseId = "2f4e6d8c-0b1a-4c3e-8d5f-7a9b1c3d5e7f";
        private const string _unknownId = "99999999-9999-4999-8999-999999999999";

        // 1x1・5 個まで重なる・0.5 kg・体力 25
        private static readonly ItemSpec _herb = new ItemSpec(_herbId, "item.herb", maxStack: 5, weight: 0.5f, useHealth: 25);

        // 1x1・30 個まで重なる・全体で 60 個まで・1 個 0.1 kg
        private static readonly ItemSpec _ammo = new ItemSpec(_ammoId, "item.ammo", maxStack: 30, maxCarried: 60, weight: 0.1f);

        // 3x1・重ならない・4 kg
        private static readonly ItemSpec _rifleCase = new ItemSpec(_rifleCaseId, "item.case", weight: 4f, width: 3, height: 1);

        private static int[] Ints(params int[] values) => values;

        private static Inventory Make(InventoryRule rule, params ItemCount[] starting)
        {
            return new Inventory(new ItemCatalog(new[] { _herb, _ammo, _rifleCase }, rule, starting));
        }

        [Test]
        public void WithoutLimitsItemsStackUpToTheirMaxStackAndOverflowIntoNewStacks()
        {
            var inventory = Make(InventoryRule.Default);

            Assert.That(inventory.Add(_herbId, 12), Is.EqualTo(12));

            Assert.That(inventory.Stacks.Select(s => s.Count), Is.EqualTo(Ints(5, 5, 2)));
            Assert.That(inventory.CountOf(_herbId), Is.EqualTo(12));
            Assert.That(inventory.TotalWeight, Is.EqualTo(6f).Within(1e-4f));
        }

        [Test]
        public void AddingFillsTheExistingStackBeforeMakingANewOne()
        {
            var inventory = Make(InventoryRule.Default);
            inventory.Add(_herbId, 3);

            inventory.Add(_herbId, 4);

            Assert.That(inventory.Stacks.Select(s => s.Count), Is.EqualTo(Ints(5, 2)));
        }

        [Test]
        public void TheItemsMaxCarriedCapsTheTotal()
        {
            var inventory = Make(InventoryRule.Default);

            Assert.That(inventory.Add(_ammoId, 100), Is.EqualTo(60));
            Assert.That(inventory.RoomFor(_ammoId), Is.EqualTo(0));
        }

        [Test]
        public void TheStackLimitStopsNewStacks()
        {
            var inventory = Make(new InventoryRule(maxStacks: 2));

            Assert.That(inventory.Add(_herbId, 12), Is.EqualTo(10));
            Assert.That(inventory.Add(_rifleCaseId, 1), Is.EqualTo(0));
            Assert.That(inventory.RoomFor(_herbId), Is.EqualTo(0));
        }

        [Test]
        public void TheWeightLimitCountsWholeItemsOnly()
        {
            // 10 kg − ケース 2 個 (8 kg) = 2 kg → 0.5 kg のハーブは 4 個
            var inventory = Make(new InventoryRule(maxWeight: 10f));
            Assert.That(inventory.Add(_rifleCaseId, 2), Is.EqualTo(2));

            Assert.That(inventory.RoomFor(_herbId), Is.EqualTo(4));
            Assert.That(inventory.Add(_herbId, 10), Is.EqualTo(4));
            Assert.That(inventory.RoomFor(_ammoId), Is.EqualTo(0));
        }

        [Test]
        public void AFixedGridPlacesFromTheTopLeftAndRefusesWhatDoesNotFit()
        {
            // 4x2 のマス目: ケース (3x1) が 2 つで、残りは 1 列目の右端 2 マス
            var inventory = Make(new InventoryRule(gridWidth: 4, gridHeight: 2, allowRotation: false));

            Assert.That(inventory.Add(_rifleCaseId, 3), Is.EqualTo(2));
            Assert.That(inventory.Stacks[0].X, Is.EqualTo(0));
            Assert.That(inventory.Stacks[0].Y, Is.EqualTo(0));
            Assert.That(inventory.Stacks[1].X, Is.EqualTo(0));
            Assert.That(inventory.Stacks[1].Y, Is.EqualTo(1));

            Assert.That(inventory.Add(_herbId, 12), Is.EqualTo(10));
            Assert.That(inventory.Stacks[2].X, Is.EqualTo(3));
            Assert.That(inventory.Stacks[2].Y, Is.EqualTo(0));
            Assert.That(inventory.Stacks[3].X, Is.EqualTo(3));
            Assert.That(inventory.Stacks[3].Y, Is.EqualTo(1));
        }

        [Test]
        public void AnItemWiderThanTheGridIsPlacedRotatedWhenRotationIsAllowed()
        {
            var inventory = Make(new InventoryRule(gridWidth: 2, gridHeight: 3));

            Assert.That(inventory.Add(_rifleCaseId, 1), Is.EqualTo(1));

            Assert.That(inventory.Stacks[0].Rotated, Is.True);
            Assert.That(inventory.WidthOf(inventory.Stacks[0]), Is.EqualTo(1));
            Assert.That(inventory.HeightOf(inventory.Stacks[0]), Is.EqualTo(3));
        }

        [Test]
        public void AnItemWiderThanTheGridIsRefusedWithoutRotation()
        {
            var inventory = Make(new InventoryRule(gridWidth: 2, gridHeight: 3, allowRotation: false));

            Assert.That(inventory.Add(_rifleCaseId, 1), Is.EqualTo(0));
        }

        [Test]
        public void AGrowingGridAddsRowsBelowSoOnlyWeightLimitsIt()
        {
            var inventory = Make(new InventoryRule(maxWeight: 12f, gridWidth: 3));

            Assert.That(inventory.Add(_rifleCaseId, 5), Is.EqualTo(3));
            Assert.That(inventory.Stacks.Select(s => s.Y), Is.EqualTo(Ints(0, 1, 2)));
            Assert.That(inventory.GridRows, Is.EqualTo(3));
        }

        [Test]
        public void MovingChecksTheGridBoundsAndOtherStacks()
        {
            var inventory = Make(new InventoryRule(gridWidth: 4, gridHeight: 2));
            inventory.Add(_rifleCaseId, 1);
            inventory.Add(_herbId, 1);
            var caseHandle = inventory.Stacks[0].Handle;
            var herbHandle = inventory.Stacks[1].Handle;

            Assert.That(inventory.TryMove(herbHandle, 3, 1, false), Is.EqualTo(InventoryMoveResult.Moved));
            Assert.That(inventory.TryMove(caseHandle, 1, 0, false), Is.EqualTo(InventoryMoveResult.Moved));
            Assert.That(inventory.TryMove(caseHandle, 2, 0, false), Is.EqualTo(InventoryMoveResult.OutOfGrid));
            Assert.That(inventory.TryMove(caseHandle, 1, 1, false), Is.EqualTo(InventoryMoveResult.Blocked));
            Assert.That(inventory.TryMove(caseHandle, 0, 0, true), Is.EqualTo(InventoryMoveResult.OutOfGrid));
            Assert.That(inventory.TryMove(999, 0, 0, false), Is.EqualTo(InventoryMoveResult.NoStack));

            Assert.That(inventory.TryGetStack(caseHandle, out var moved), Is.True);
            Assert.That(moved.X, Is.EqualTo(1));
            Assert.That(moved.Y, Is.EqualTo(0));
        }

        [Test]
        public void CheckingAPlaceLeavesTheStackWhereItIs()
        {
            var inventory = Make(new InventoryRule(gridWidth: 4, gridHeight: 2));
            inventory.Add(_rifleCaseId, 1);
            var handle = inventory.Stacks[0].Handle;
            var changes = 0;
            inventory.Changed += () => changes++;

            Assert.That(inventory.Check(handle, 1, 1, false), Is.EqualTo(InventoryMoveResult.Moved));
            Assert.That(inventory.Check(handle, 2, 1, false), Is.EqualTo(InventoryMoveResult.OutOfGrid));

            Assert.That(inventory.Stacks[0].X, Is.Zero);
            Assert.That(inventory.Stacks[0].Y, Is.Zero);
            Assert.That(changes, Is.Zero);
        }

        [Test]
        public void MovingIsRefusedWithoutAGridOrWhenRotationIsNotAllowed()
        {
            var listOnly = Make(InventoryRule.Default);
            listOnly.Add(_herbId, 1);
            Assert.That(listOnly.TryMove(listOnly.Stacks[0].Handle, 0, 0, false), Is.EqualTo(InventoryMoveResult.NoGrid));

            var noRotation = Make(new InventoryRule(gridWidth: 4, gridHeight: 4, allowRotation: false));
            noRotation.Add(_rifleCaseId, 1);
            Assert.That(noRotation.TryMove(noRotation.Stacks[0].Handle, 0, 0, true), Is.EqualTo(InventoryMoveResult.RotationNotAllowed));
        }

        [Test]
        public void TakingFromAStackKeepsItsHandleAndRemovesItWhenEmpty()
        {
            var inventory = Make(InventoryRule.Default);
            inventory.Add(_herbId, 3);
            var handle = inventory.Stacks[0].Handle;

            Assert.That(inventory.TryTake(handle, 1, out var taken), Is.True);
            Assert.That(taken.ItemId, Is.EqualTo(_herbId));
            Assert.That(taken.Count, Is.EqualTo(1));
            Assert.That(inventory.Stacks[0].Handle, Is.EqualTo(handle));
            Assert.That(inventory.Stacks[0].Count, Is.EqualTo(2));

            Assert.That(inventory.TryTake(handle, 10, out taken), Is.True);
            Assert.That(taken.Count, Is.EqualTo(2));
            Assert.That(inventory.Stacks, Is.Empty);
            Assert.That(inventory.TryTake(handle, 1, out _), Is.False);
        }

        [Test]
        public void RemovingTakesFromTheLatestStacksFirst()
        {
            var inventory = Make(InventoryRule.Default);
            inventory.Add(_herbId, 7);

            Assert.That(inventory.Remove(_herbId, 3), Is.EqualTo(3));

            Assert.That(inventory.Stacks.Select(s => s.Count), Is.EqualTo(Ints(4)));
            Assert.That(inventory.Remove(_herbId, 10), Is.EqualTo(4));
        }

        [Test]
        public void UnknownItemsAreNeitherAddedNorGivenRoom()
        {
            var inventory = Make(InventoryRule.Default);

            Assert.That(inventory.Add(_unknownId, 1), Is.EqualTo(0));
            Assert.That(inventory.RoomFor(_unknownId), Is.EqualTo(0));
        }

        [Test]
        public void FillEmptiesThenGivesTheStartingItems()
        {
            var inventory = Make(InventoryRule.Default, new ItemCount(_herbId, 2), new ItemCount(_ammoId, 40));
            inventory.Add(_rifleCaseId, 1);

            inventory.Fill();

            Assert.That(inventory.CountOf(_rifleCaseId), Is.EqualTo(0));
            Assert.That(inventory.CountOf(_herbId), Is.EqualTo(2));
            Assert.That(inventory.Stacks.Select(s => s.Count), Is.EqualTo(Ints(2, 30, 10)));
            Assert.That(inventory.Stacks.Select(s => s.Handle), Is.EqualTo(Ints(1, 2, 3)));
        }

        [Test]
        public void ReplaceCopiesStacksAndContinuesHandlesAfterTheLargest()
        {
            var inventory = Make(InventoryRule.Default);
            var changes = 0;
            inventory.Changed += () => changes++;

            inventory.Replace(new[] { new InventoryEntry(7, _herbId, 2, 0, 0, false) });
            inventory.Replace(new[] { new InventoryEntry(7, _herbId, 2, 0, 0, false) });
            inventory.Add(_ammoId, 1);

            Assert.That(changes, Is.EqualTo(2));
            Assert.That(inventory.Stacks.Select(s => s.Handle), Is.EqualTo(Ints(7, 8)));
        }

        [Test]
        public void ReplaceRefusesTwoStacksWithOneHandle()
        {
            var inventory = Make(InventoryRule.Default);

            Assert.Throws<ArgumentException>(() => inventory.Replace(new[]
            {
                new InventoryEntry(1, _herbId, 1, 0, 0, false),
                new InventoryEntry(1, _ammoId, 1, 0, 0, false),
            }));
        }

        [Test]
        public void ChangedIsRaisedOnlyWhenTheContentsChange()
        {
            var inventory = Make(new InventoryRule(gridWidth: 2, gridHeight: 1));
            var changes = 0;
            inventory.Changed += () => changes++;

            inventory.Add(_herbId, 1);
            inventory.Add(_rifleCaseId, 1);
            inventory.TryMove(inventory.Stacks[0].Handle, 0, 0, false);
            inventory.Remove(_ammoId, 1);

            Assert.That(changes, Is.EqualTo(1));
        }
    }
}
