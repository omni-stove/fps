using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class StashTests
    {
        public const string Ammo = "5b0c55a4-8f0e-4a43-9c55-0b1f3ad1e001";
        public const string Bandage = "5b0c55a4-8f0e-4a43-9c55-0b1f3ad1e002";
        public const string Rifle = "5b0c55a4-8f0e-4a43-9c55-0b1f3ad1e003";
        public const string Unknown = "5b0c55a4-8f0e-4a43-9c55-0b1f3ad1e0ff";

        private static readonly StashEntry[] _nullStack = { null };

        public static ItemCatalog Catalog()
        {
            return new ItemCatalog(
                new[]
                {
                    new ItemSpec(Ammo, maxStack: 30),
                    new ItemSpec(Bandage, maxStack: 5),
                    new ItemSpec(Rifle, weaponId: "rifle"),
                },
                InventoryRule.Default);
        }

        [Test]
        public void AddFillsPartStacksFirstThenOpensNewOnesUpToTheCapacity()
        {
            var stash = new Stash(3, Catalog());

            Assert.That(stash.Add(Ammo, 70), Is.EqualTo(70));
            AssertStacks(stash, (Ammo, 30), (Ammo, 30), (Ammo, 10));
            Assert.That(stash.FreeSlots, Is.EqualTo(0));
            Assert.That(stash.RoomFor(Ammo), Is.EqualTo(20));
            Assert.That(stash.RoomFor(Bandage), Is.EqualTo(0));

            Assert.That(stash.Add(Ammo, 25), Is.EqualTo(20));
            AssertStacks(stash, (Ammo, 30), (Ammo, 30), (Ammo, 30));
            Assert.That(stash.CountOf(Ammo), Is.EqualTo(90));
        }

        [Test]
        public void WeaponsTakeOneSlotEach()
        {
            var stash = new Stash(2, Catalog());

            Assert.That(stash.Add(Rifle, 3), Is.EqualTo(2));
            AssertStacks(stash, (Rifle, 1), (Rifle, 1));
        }

        [Test]
        public void AnItemOutsideTheCatalogDoesNotGoIn()
        {
            var stash = new Stash(3, Catalog());

            Assert.That(stash.RoomFor(Unknown), Is.EqualTo(0));
            Assert.That(stash.Add(Unknown, 1), Is.EqualTo(0));
            Assert.That(stash.Stacks.Count, Is.EqualTo(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => stash.Add(Ammo, -1));
        }

        [Test]
        public void RemoveTakesFromTheLastStackFirst()
        {
            var stash = new Stash(3, Catalog());
            stash.Add(Ammo, 70);

            Assert.That(stash.Remove(Ammo, 15), Is.EqualTo(15));
            AssertStacks(stash, (Ammo, 30), (Ammo, 25));

            Assert.That(stash.Remove(Ammo, 100), Is.EqualTo(55));
            Assert.That(stash.Stacks.Count, Is.EqualTo(0));
            Assert.That(stash.Remove(Bandage, 1), Is.EqualTo(0));
        }

        [Test]
        public void TryTakeTakesNothingUnlessEverythingIsThere()
        {
            var stash = new Stash(3, Catalog());
            stash.Add(Ammo, 10);

            Assert.That(stash.TryTake(new[] { new ItemCount(Ammo, 5), new ItemCount(Bandage, 1) }), Is.False);
            Assert.That(stash.TryTake(new[] { new ItemCount(Ammo, 6), new ItemCount(Ammo, 6) }), Is.False);
            Assert.That(stash.CountOf(Ammo), Is.EqualTo(10));

            Assert.That(stash.TryTake(new[] { new ItemCount(Ammo, 4), new ItemCount(Ammo, 2) }), Is.True);
            Assert.That(stash.CountOf(Ammo), Is.EqualTo(4));
            Assert.That(stash.TryTake(Array.Empty<ItemCount>()), Is.True);
        }

        [Test]
        public void ChangedFiresOnlyWhenTheContentsChange()
        {
            var stash = new Stash(1, Catalog());
            var changes = 0;
            stash.Changed += () => changes++;

            stash.Add(Ammo, 30);
            stash.Add(Ammo, 1);
            stash.Add(Unknown, 1);
            stash.Remove(Bandage, 1);
            stash.TryTake(new[] { new ItemCount(Bandage, 1) });
            stash.TryTake(new[] { new ItemCount(Ammo, 1) });
            stash.Remove(Ammo, 1);

            Assert.That(changes, Is.EqualTo(3));
        }

        [Test]
        public void FreshStartsWithTheSpecsStartingItems()
        {
            var spec = new StashSpec(3, new[] { new StashEntry(Ammo, 40), new StashEntry(Bandage, 3) });

            var stash = Stash.Fresh(spec, Catalog());

            Assert.That(stash.Capacity, Is.EqualTo(3));
            AssertStacks(stash, (Ammo, 30), (Ammo, 10), (Bandage, 3));
            Assert.That(
                stash.Status,
                Is.EqualTo(new StashStatus(3, new[] { new StashEntry(Ammo, 30), new StashEntry(Ammo, 10), new StashEntry(Bandage, 3) })));
        }

        [Test]
        public void FreshWithTheDefaultSpecIsEmpty()
        {
            var stash = Stash.Fresh(StashSpec.Default, Catalog());

            Assert.That(stash.Capacity, Is.EqualTo(StashSpec.DefaultCapacity));
            Assert.That(stash.Stacks.Count, Is.EqualTo(0));
            Assert.That(stash.Status.FreeSlots, Is.EqualTo(60));
        }

        [Test]
        public void RestoreKeepsEveryStackEvenPastTheCapacity()
        {
            var stash = Stash.Restore(
                2,
                Catalog(),
                new[] { new StashEntry(Ammo, 20), new StashEntry(Bandage, 5), new StashEntry(Unknown, 1) });

            AssertStacks(stash, (Ammo, 20), (Bandage, 5), (Unknown, 1));
            Assert.That(stash.FreeSlots, Is.EqualTo(0));
            Assert.That(stash.Status.Capacity, Is.EqualTo(3));
            Assert.That(stash.Add(Bandage, 1), Is.EqualTo(0));
            Assert.That(stash.Add(Ammo, 15), Is.EqualTo(10));
            Assert.That(stash.Remove(Unknown, 1), Is.EqualTo(1));
        }

        [Test]
        public void RejectsBadArguments()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Stash(0, Catalog()));
            Assert.Throws<ArgumentNullException>(() => new Stash(1, null));
            Assert.Throws<ArgumentNullException>(() => Stash.Fresh(null, Catalog()));
            Assert.Throws<ArgumentNullException>(() => Stash.Restore(1, Catalog(), null));
            Assert.Throws<ArgumentException>(() => Stash.Restore(1, Catalog(), _nullStack));
            Assert.Throws<ArgumentNullException>(() => new Stash(1, Catalog()).Has(null));
        }

        public static void AssertStacks(Stash stash, params (string ItemId, int Count)[] expected)
        {
            Assert.That(stash.Stacks.Count, Is.EqualTo(expected.Length));
            for (var i = 0; i < expected.Length; i++)
            {
                Assert.That(stash.Stacks[i].ItemId, Is.EqualTo(expected[i].ItemId), "stack " + i);
                Assert.That(stash.Stacks[i].Count, Is.EqualTo(expected[i].Count), "stack " + i);
            }
        }
    }
}
