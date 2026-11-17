using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class InventoryWeaponTests
    {
        private const string _ammoId = "1a2b3c4d-5e6f-4a7b-8c9d-0e1f2a3b4c5d";
        private const string _rifleItemId = "2b3c4d5e-6f7a-4b8c-9d0e-1f2a3b4c5d6e";
        private const string _pistolItemId = "3c4d5e6f-7a8b-4c9d-8e1f-2a3b4c5d6e7f";
        private const string _bandageId = "4d5e6f7a-8b9c-4d0e-9f2a-3b4c5d6e7f8a";
        private const string _rifleId = "5e6f7a8b-9c0d-4e1f-8a3b-4c5d6e7f8a9b";
        private const string _pistolId = "6f7a8b9c-0d1e-4f2a-9b4c-5d6e7f8a9b0c";
        private const string _knifeId = "7a8b9c0d-1e2f-4a3b-8c5d-6e7f8a9b0c1d";

        // 9mm: 1x1・30 発で 1 山・1 発 0.01 kg
        private static readonly ItemSpec _ammo = new ItemSpec(_ammoId, "item.ammo9mm", maxStack: 30, weight: 0.01f);

        // 小銃: 4x2、拳銃: 2x1。どちらも 9mm を撃つ。重さは武器の値を使う
        private static readonly ItemSpec _rifleItem = new ItemSpec(_rifleItemId, "weapon.rifle", width: 4, height: 2, weaponId: _rifleId, ammoItemId: _ammoId);
        private static readonly ItemSpec _pistolItem = new ItemSpec(_pistolItemId, "weapon.handgun", width: 2, height: 1, weaponId: _pistolId, ammoItemId: _ammoId);
        private static readonly ItemSpec _bandage = new ItemSpec(_bandageId, "item.bandage", maxStack: 5, weight: 0.1f, useHealth: 15);

        // 弾倉 10・予備 40・3 kg / 弾倉 6・予備 20・1 kg / 持ち物に無いナイフ
        private static readonly WeaponSpec _rifle = Spec(_rifleId, 10, 40, 3f);
        private static readonly WeaponSpec _pistol = Spec(_pistolId, 6, 20, 1f);
        private static readonly WeaponSpec _knife = Spec(_knifeId, 1, 0, 0.5f);

        private static int[] Ints(params int[] values) => values;

        private static WeaponSpec Spec(string id, int magazineSize, int reserveAmmo, float weight, ReloadStyle style = ReloadStyle.Whole)
        {
            var ammo = new AmmoSpec(magazineSize, reserveAmmo, 1f, reloadStyle: style, reloadSecondsPerRound: style == ReloadStyle.PerRound ? 0.5f : 0f);
            return new WeaponSpec(id, null, WeaponCategory.AssaultRifle, ammo: ammo, weight: weight);
        }

        private static Inventory Make(InventoryRule rule, params ItemCount[] starting)
        {
            return new Inventory(new ItemCatalog(new[] { _ammo, _rifleItem, _pistolItem, _bandage }, rule, starting));
        }

        private static Inventory Grid(int width, int height, float maxWeight = 0f, params ItemCount[] starting)
        {
            return Make(new InventoryRule(maxWeight: maxWeight, gridWidth: width, gridHeight: height), starting);
        }

        [Test]
        public void HeldWeaponsBecomeStacksWeighingWhatTheWeaponWeighs()
        {
            var inventory = Grid(6, 2);

            var missing = inventory.HoldWeapons(new[] { _rifle, _pistol });

            Assert.That(missing, Is.Empty);
            Assert.That(inventory.Stacks.Select(s => (s.ItemId, s.X, s.Y)), Is.EqualTo(new[] { (_rifleItemId, 0, 0), (_pistolItemId, 4, 0) }));
            Assert.That(inventory.TotalWeight, Is.EqualTo(4f).Within(1e-4f));
            Assert.That(inventory.WeaponWeight, Is.EqualTo(4f).Within(1e-4f));
            Assert.That(inventory.HoldsWeapon(_rifleId), Is.True);
            Assert.That(inventory.WeaponIdOf(inventory.Stacks[1].Handle), Is.EqualTo(_pistolId));
        }

        [Test]
        public void AWeaponNoLongerHeldLeavesTheInventory()
        {
            var inventory = Grid(6, 2);
            inventory.HoldWeapons(new[] { _rifle, _pistol });
            var changes = 0;
            inventory.Changed += () => changes++;

            inventory.HoldWeapons(new[] { _pistol });

            Assert.That(inventory.Stacks.Select(s => s.ItemId), Is.EqualTo(new[] { _pistolItemId }));
            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void HoldingTheSameWeaponsAgainChangesNothing()
        {
            var inventory = Grid(6, 2);
            inventory.HoldWeapons(new[] { _rifle, _pistol });
            var changes = 0;
            inventory.Changed += () => changes++;

            inventory.HoldWeapons(new[] { _rifle, _pistol });

            Assert.That(changes, Is.EqualTo(0));
        }

        [Test]
        public void AWeaponThatDoesNotFitIsReportedAndAWeaponWithoutAnItemIsLeftOut()
        {
            var inventory = Grid(4, 2);

            var missing = inventory.HoldWeapons(new[] { _rifle, _pistol, _knife });

            Assert.That(missing, Is.EqualTo(new[] { _pistolId }));
            Assert.That(inventory.Stacks.Select(s => s.ItemId), Is.EqualTo(new[] { _rifleItemId }));
        }

        [Test]
        public void AWeaponHeavierThanTheWeightLeftIsReported()
        {
            var inventory = Grid(6, 2, maxWeight: 3.5f);

            var missing = inventory.HoldWeapons(new[] { _rifle, _pistol });

            Assert.That(missing, Is.EqualTo(new[] { _pistolId }));
        }

        [Test]
        public void WeaponItemsCannotBeAddedAsItems()
        {
            var inventory = Grid(6, 2);

            Assert.That(inventory.Add(_rifleItemId, 1), Is.EqualTo(0));
            Assert.That(inventory.RoomFor(_rifleItemId), Is.EqualTo(0));
            Assert.That(inventory.Stacks, Is.Empty);
        }

        [Test]
        public void FillKeepsTheHeldWeaponsAndPutsThemFirst()
        {
            var inventory = Grid(6, 2, 0f, new ItemCount(_bandageId, 2));
            inventory.Add(_ammoId, 10);
            inventory.HoldWeapons(new[] { _pistol });

            inventory.Fill();

            Assert.That(inventory.Stacks.Select(s => (s.ItemId, s.Count, s.X, s.Y)), Is.EqualTo(new[] { (_pistolItemId, 1, 0, 0), (_bandageId, 2, 2, 0) }));
        }

        [Test]
        public void CanHoldWeaponLooksAtTheRoomAndTheWeaponsWeight()
        {
            var inventory = Grid(6, 2, maxWeight: 5f);
            inventory.HoldWeapons(new[] { _rifle });

            Assert.That(inventory.CanHoldWeapon(_pistolId, 1f), Is.True);
            Assert.That(inventory.CanHoldWeapon(_pistolId, 2.5f), Is.False, "3 + 2.5 kg is over 5 kg");
            Assert.That(inventory.CanHoldWeapon(_rifleId, 3f), Is.True, "already held");
            Assert.That(inventory.CanHoldWeapon(_knifeId, 100f), Is.True, "not an inventory weapon");
        }

        [Test]
        public void CanHoldWeaponCountsTheRoomOfTheWeaponItReplaces()
        {
            var inventory = Grid(4, 2);
            inventory.HoldWeapons(new[] { _rifle });

            Assert.That(inventory.CanHoldWeapon(_pistolId, 1f), Is.False);
            Assert.That(inventory.CanHoldWeapon(_pistolId, 1f, _rifleId), Is.True);
            Assert.That(inventory.Stacks.Select(s => s.ItemId), Is.EqualTo(new[] { _rifleItemId }), "only asks");
        }

        [Test]
        public void APooledWeaponMovesItsReserveIntoTheInventoryAndReadsItBack()
        {
            var inventory = Grid(8, 4);
            var weapon = new Weapon(_rifle);

            Assert.That(weapon.UseAmmoPool(InventoryWeapons.PoolFor(inventory, _rifle)), Is.True);

            Assert.That(inventory.CountOf(_ammoId), Is.EqualTo(40));
            Assert.That(inventory.Stacks.Select(s => s.Count), Is.EqualTo(Ints(30, 10)));
            Assert.That(weapon.ReserveAmmo, Is.EqualTo(40));
            Assert.That(weapon.StartingReserveAmmo, Is.EqualTo(40));
        }

        [Test]
        public void ReloadingTakesRoundsFromTheInventory()
        {
            var inventory = Grid(8, 4);
            var weapon = new Weapon(_rifle);
            weapon.UseAmmoPool(InventoryWeapons.PoolFor(inventory, _rifle));
            for (var i = 0; i < 7; i++)
            {
                weapon.TryFire();
                weapon.Tick(1f);
            }

            Assert.That(weapon.TryStartReload(), Is.True);
            weapon.Tick(1f);

            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(10));
            Assert.That(inventory.CountOf(_ammoId), Is.EqualTo(33));
            Assert.That(weapon.ReserveAmmo, Is.EqualTo(33));
        }

        [Test]
        public void ReloadingWithLessInTheInventoryLoadsWhatIsThere()
        {
            var inventory = Grid(8, 4);
            var weapon = new Weapon(_rifle, 0, 0, GameRules.Default);
            weapon.UseAmmoPool(InventoryWeapons.PoolFor(inventory, _rifle));
            inventory.Add(_ammoId, 4);

            Assert.That(weapon.TryStartReload(), Is.True);
            weapon.Tick(1f);

            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(4));
            Assert.That(inventory.CountOf(_ammoId), Is.EqualTo(0));
            Assert.That(weapon.TryStartReload(), Is.False, "nothing left to load");
        }

        [Test]
        public void ALoadOneRoundAtATimeWeaponTakesEachRoundFromTheInventory()
        {
            var shotgun = Spec(_rifleId, 4, 10, 3f, ReloadStyle.PerRound);
            var inventory = Grid(8, 4);
            var weapon = new Weapon(shotgun, 0, 0, GameRules.Default);
            weapon.UseAmmoPool(InventoryWeapons.PoolFor(inventory, shotgun));
            inventory.Add(_ammoId, 3);

            Assert.That(weapon.TryStartReload(), Is.True);
            weapon.Tick(0.5f);
            weapon.Tick(0.5f);

            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(2));
            Assert.That(inventory.CountOf(_ammoId), Is.EqualTo(1));
        }

        [Test]
        public void PickedUpAmmoGoesIntoTheInventoryUpToItsRoom()
        {
            var inventory = Grid(2, 1);
            var weapon = new Weapon(_pistol, 6, 0, GameRules.Default);
            weapon.UseAmmoPool(InventoryWeapons.PoolFor(inventory, _pistol));

            Assert.That(weapon.ReserveAmmoRoom, Is.EqualTo(60));
            Assert.That(weapon.AddReserveAmmo(100), Is.EqualTo(60));
            Assert.That(inventory.CountOf(_ammoId), Is.EqualTo(60));
            Assert.That(weapon.MaxReserveAmmo, Is.EqualTo(60));
        }

        [Test]
        public void ChangingTheInventoryTellsThePooledWeapon()
        {
            var inventory = Grid(8, 4);
            var weapon = new Weapon(_pistol, 6, 0, GameRules.Default);
            weapon.UseAmmoPool(InventoryWeapons.PoolFor(inventory, _pistol));
            var changes = 0;
            weapon.ReserveAmmoChanged += () => changes++;

            inventory.Add(_ammoId, 5);
            weapon.UseAmmoPool(null);
            inventory.Add(_ammoId, 5);

            Assert.That(changes, Is.EqualTo(1));
            Assert.That(weapon.AmmoPool, Is.Null);
        }

        [Test]
        public void ARebuiltWeaponWithTheSameReserveAddsNoAmmo()
        {
            var inventory = Grid(8, 4);
            new Weapon(_rifle).UseAmmoPool(InventoryWeapons.PoolFor(inventory, _rifle));
            inventory.Remove(_ammoId, 15);

            var rebuilt = new Weapon(_rifle, 10, 25, GameRules.Default);
            rebuilt.UseAmmoPool(InventoryWeapons.PoolFor(inventory, _rifle));

            Assert.That(inventory.CountOf(_ammoId), Is.EqualTo(25));
        }

        [Test]
        public void AnInfiniteReserveWeaponKeepsCountingItsOwnReserve()
        {
            var inventory = Grid(8, 4);
            var weapon = new Weapon(_rifle, GameRules.Default.WithInfiniteReserve(true));

            Assert.That(weapon.UseAmmoPool(InventoryWeapons.PoolFor(inventory, _rifle)), Is.False);
            Assert.That(weapon.AmmoPool, Is.Null);
            Assert.That(inventory.Stacks, Is.Empty);
            Assert.That(weapon.ReserveAmmo, Is.EqualTo(40));
        }

        [Test]
        public void AWeaponWithoutAnAmmoItemHasNoPool()
        {
            var inventory = Grid(8, 4);

            Assert.That(InventoryWeapons.PoolFor(inventory, _knife), Is.Null);
        }

        [Test]
        public void SwitchingAmmoTypesPutsTheMagazineBackIntoTheInventory()
        {
            var inventory = Grid(8, 4);
            var magazine = new MagazineAmmo(_rifle.Ammo, 7, 0, GameRules.Default);
            magazine.UsePool(new InventoryAmmoPool(inventory, _ammoId));

            magazine.Unload();

            Assert.That(magazine.AmmoInMagazine, Is.EqualTo(0));
            Assert.That(inventory.CountOf(_ammoId), Is.EqualTo(7));
        }

        [Test]
        public void ADroppedPooledWeaponLeavesItsReserveInTheInventory()
        {
            var inventory = Grid(8, 4);
            var pooled = new Weapon(_rifle);
            pooled.UseAmmoPool(InventoryWeapons.PoolFor(inventory, _rifle));
            var own = new Weapon(_rifle);

            var leaving = WeaponAmmo.Leaving(pooled);

            Assert.That((leaving.AmmoInMagazine, leaving.ReserveAmmo), Is.EqualTo((10, 0)));
            Assert.That(WeaponAmmo.Leaving(own).ReserveAmmo, Is.EqualTo(40));
            Assert.That(WeaponAmmo.Of(pooled).ReserveAmmo, Is.EqualTo(40), "a save still records what the inventory holds");
        }

        [Test]
        public void RestockTopsUpToTheLargestStartingReserveOfTheWeaponsSharingTheAmmo()
        {
            var inventory = Grid(8, 4);
            inventory.Add(_ammoId, 25);
            var weapons = new List<(string, int)> { (_rifleId, 40), (_pistolId, 20), (_knifeId, 50) };

            Assert.That(InventoryWeapons.Restock(inventory, weapons), Is.EqualTo(15));
            Assert.That(inventory.CountOf(_ammoId), Is.EqualTo(40));
            Assert.That(InventoryWeapons.Restock(inventory, weapons), Is.EqualTo(0), "already stocked");
        }

        [Test]
        public void AServerWeaponKeepsItsPoolWhenRefilled()
        {
            var inventory = Grid(8, 4);
            var weapon = new ServerWeapon(_rifle);
            weapon.UseAmmoPool(InventoryWeapons.PoolFor(inventory, _rifle));
            inventory.Remove(_ammoId, 40);

            weapon.Refill();

            Assert.That(weapon.HasAmmoPool, Is.True);
            Assert.That(weapon.ReserveAmmo, Is.EqualTo(40));
            Assert.That(inventory.CountOf(_ammoId), Is.EqualTo(40));
        }

        [Test]
        public void TheWeaponsWeightComesFromTheWeaponOnceHeld()
        {
            var inventory = Grid(6, 2);

            Assert.That(inventory.WeightOf(_rifleItem), Is.EqualTo(0f));
            inventory.HoldWeapons(new[] { _rifle });
            Assert.That(inventory.WeightOf(_rifleItem), Is.EqualTo(3f));
        }

        [Test]
        public void HoldWeaponsNeedsAList()
        {
            Assert.Throws<ArgumentNullException>(() => Grid(6, 2).HoldWeapons(null));
        }
    }
}
