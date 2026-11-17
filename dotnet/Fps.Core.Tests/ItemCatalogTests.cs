using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class ItemCatalogTests
    {
        private const string _herbId = "0d3c6f1a-5b2e-4c7d-8a9f-1e2b3c4d5e6f";
        private const string _ammoId = "7a8b9c0d-1e2f-4a3b-9c4d-5e6f7a8b9c0d";

        private static readonly ItemSpec _herb = new ItemSpec(_herbId, "item.herb");
        private const string _rifleItemId = "2b3c4d5e-6f7a-4b8c-9d0e-1f2a3b4c5d6e";
        private const string _rifleId = "5e6f7a8b-9c0d-4e1f-8a3b-4c5d6e7f8a9b";

        [Test]
        public void ItemsAreFoundById()
        {
            var catalog = new ItemCatalog(new[] { _herb }, InventoryRule.Default);

            Assert.That(catalog.TryFind(_herbId, out var found), Is.True);
            Assert.That(found, Is.SameAs(_herb));
            Assert.That(catalog.IndexOf(_ammoId), Is.EqualTo(-1));
            Assert.That(catalog.IndexOf(null), Is.EqualTo(-1));
        }

        [Test]
        public void DuplicateIdsAndUnknownStartingItemsAreRefused()
        {
            Assert.Throws<ArgumentException>(() => new ItemCatalog(new[] { _herb, new ItemSpec(_herbId) }, InventoryRule.Default));
            Assert.Throws<ArgumentException>(() => new ItemCatalog(new[] { _herb }, InventoryRule.Default, new[] { new ItemCount(_ammoId, 1) }));
        }

        [Test]
        public void TheEmptyCatalogHasNoItemsAndTheDefaultRule()
        {
            Assert.That(ItemCatalog.Empty.Items, Is.Empty);
            Assert.That(ItemCatalog.Empty.Starting, Is.Empty);
            Assert.That(ItemCatalog.Empty.Rule, Is.SameAs(InventoryRule.Default));
        }

        [Test]
        public void ItemSpecChecksItsValues()
        {
            Assert.Throws<ArgumentException>(() => new ItemSpec("herb"));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ItemSpec(_herbId, maxStack: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ItemSpec(_herbId, weight: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ItemSpec(_herbId, width: ItemSpec.MaxSize + 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ItemSpec(_herbId, useHealth: -1));
        }

        [Test]
        public void ItemSpecSwapsWidthAndHeightWhenRotated()
        {
            var item = new ItemSpec(_herbId, width: 3, height: 1);

            Assert.That(item.WidthWhen(true), Is.EqualTo(1));
            Assert.That(item.HeightWhen(true), Is.EqualTo(3));
            Assert.That(item.IsUsable, Is.False);
            Assert.That(new ItemSpec(_herbId, useArmor: 5).IsUsable, Is.True);
        }

        [Test]
        public void InventoryRuleChecksItsValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new InventoryRule(maxStacks: InventoryRule.StackLimit + 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new InventoryRule(maxWeight: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new InventoryRule(gridWidth: InventoryRule.MaxGridWidth + 1));
            Assert.Throws<ArgumentException>(() => new InventoryRule(gridHeight: 4));
        }

        [Test]
        public void InventoryRuleTellsTheGridShape()
        {
            Assert.That(InventoryRule.Default.HasGrid, Is.False);
            Assert.That(InventoryRule.Default.StackCapacity, Is.EqualTo(InventoryRule.StackLimit));
            Assert.That(new InventoryRule(gridWidth: 8).HasFixedGridHeight, Is.False);
            Assert.That(new InventoryRule(gridWidth: 8, gridHeight: 4).HasFixedGridHeight, Is.True);
            Assert.That(new InventoryRule(maxStacks: 6).StackCapacity, Is.EqualTo(6));
        }

        [Test]
        public void AWeaponItemIsFoundByItsWeaponAndLeadsToItsAmmo()
        {
            var ammo = new ItemSpec(_ammoId, "item.ammo", maxStack: 30);
            var rifle = new ItemSpec(_rifleItemId, "weapon.rifle", weaponId: _rifleId, ammoItemId: _ammoId);
            var catalog = new ItemCatalog(new[] { ammo, rifle }, InventoryRule.Default);

            Assert.That(catalog.TryFindWeapon(_rifleId, out var found), Is.True);
            Assert.That(found, Is.SameAs(rifle));
            Assert.That(catalog.TryFindAmmo(_rifleId, out var foundAmmo), Is.True);
            Assert.That(foundAmmo, Is.SameAs(ammo));
            Assert.That(catalog.TryFindWeapon(_herbId, out _), Is.False);
            Assert.That(catalog.TryFindAmmo(null, out _), Is.False);
            Assert.That(rifle.IsWeapon, Is.True);
            Assert.That(ammo.IsWeapon, Is.False);
        }

        [Test]
        public void WeaponItemsThatCannotBeHeldAreRefused()
        {
            var ammo = new ItemSpec(_ammoId, "item.ammo");
            var rifle = new ItemSpec(_rifleItemId, weaponId: _rifleId, ammoItemId: _ammoId);

            Assert.Throws<ArgumentException>(() => new ItemCatalog(new[] { rifle }, InventoryRule.Default), "the ammo item is missing");
            Assert.Throws<ArgumentException>(
                () => new ItemCatalog(new[] { ammo, rifle, new ItemSpec(_herbId, weaponId: _rifleId) }, InventoryRule.Default), "the weapon is listed twice");
            Assert.Throws<ArgumentException>(
                () => new ItemCatalog(new[] { new ItemSpec(_ammoId, weaponId: "other"), rifle }, InventoryRule.Default), "the ammo is a weapon");
            Assert.Throws<ArgumentException>(
                () => new ItemCatalog(new[] { ammo, rifle }, InventoryRule.Default, new[] { new ItemCount(_rifleItemId, 1) }), "weapons are not starting items");
        }

        [Test]
        public void AWeaponItemSpecMustHoldOneWeaponAndNotHeal()
        {
            Assert.Throws<ArgumentException>(() => new ItemSpec(_rifleItemId, maxStack: 2, weaponId: _rifleId));
            Assert.Throws<ArgumentException>(() => new ItemSpec(_rifleItemId, useHealth: 10, weaponId: _rifleId));
            Assert.Throws<ArgumentException>(() => new ItemSpec(_rifleItemId, ammoItemId: _ammoId), "ammo needs a weapon");
            Assert.Throws<ArgumentException>(() => new ItemSpec(_rifleItemId, weaponId: _rifleId, ammoItemId: _rifleItemId), "its own ammo");
            Assert.Throws<ArgumentException>(() => new ItemSpec(_rifleItemId, weaponId: _rifleId, ammoItemId: "9MM"), "not a uuid");
            Assert.That(new ItemSpec(_rifleItemId, weaponId: "  " + _rifleId + " ").WeaponId, Is.EqualTo(_rifleId));
        }
    }
}
