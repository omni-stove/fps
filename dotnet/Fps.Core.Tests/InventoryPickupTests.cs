using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class InventoryPickupTests
    {
        private const string _herbId = "0d3c6f1a-5b2e-4c7d-8a9f-1e2b3c4d5e6f";
        private const string _plateId = "5c6d7e8f-9a0b-4c1d-8e2f-3a4b5c6d7e8f";
        private const string _keyId = "1a2b3c4d-5e6f-4a7b-8c9d-0e1f2a3b4c5d";
        private const string _rifleItemId = "2b3c4d5e-6f7a-4b8c-9d0e-1f2a3b4c5d6e";
        private const string _rifleId = "5e6f7a8b-9c0d-4e1f-8a3b-4c5d6e7f8a9b";
        private const string _pistolId = "6f7a8b9c-0d1e-4f2a-9b4c-5d6e7f8a9b0c";

        private static readonly ItemSpec _herb = new ItemSpec(_herbId, "item.herb", maxStack: 3, maxCarried: 4, weight: 0.5f, useHealth: 40);
        private static readonly ItemSpec _plate = new ItemSpec(_plateId, "item.plate", useArmor: 50);
        private static readonly ItemSpec _key = new ItemSpec(_keyId, "item.key");

        private static Inventory Make(InventoryRule rule = null)
        {
            return new Inventory(new ItemCatalog(new[] { _herb, _plate, _key }, rule ?? InventoryRule.Default));
        }

        [Test]
        public void PickingUpItemsTakesWhatFits()
        {
            var inventory = Make();
            inventory.Add(_herbId, 3);
            var collector = new PickupCollector(null, 100, 100).WithInventory(inventory);

            var outcome = PickupJudge.Judge(PickupSpec.Item(_herbId, 5), collector);

            Assert.That(outcome.IsCollected, Is.True);
            Assert.That(outcome.Kind, Is.EqualTo(PickupKind.Item));
            Assert.That(outcome.ItemId, Is.EqualTo(_herbId));
            Assert.That(outcome.Items, Is.EqualTo(1));
        }

        [Test]
        public void PickingUpIsRefusedWhenNothingFits()
        {
            var inventory = Make();
            inventory.Add(_herbId, 4);
            var collector = new PickupCollector(null, 100, 100).WithInventory(inventory);

            var outcome = PickupJudge.Judge(PickupSpec.Item(_herbId, 1), collector);

            Assert.That(outcome.Rejection, Is.EqualTo(PickupRejection.InventoryFull));
            Assert.That(outcome.Items, Is.EqualTo(0));
        }

        [Test]
        public void PickingUpWithoutAnInventoryIsRefused()
        {
            var outcome = PickupJudge.Judge(PickupSpec.Item(_herbId, 1), new PickupCollector(null, 100, 100));

            Assert.That(outcome.Rejection, Is.EqualTo(PickupRejection.NoInventory));
        }

        [Test]
        public void TheInventorySurvivesAddingWeaponsAndArmorToTheCollector()
        {
            var inventory = Make();
            var collector = new PickupCollector(null, 100, 100).WithInventory(inventory).WithWeapons(null, null).WithArmor(0, 50);

            Assert.That(collector.Inventory, Is.SameAs(inventory));
        }

        [Test]
        public void ThePromptNamesTheItemAndTheCount()
        {
            var inventory = Make();
            var outcome = PickupJudge.Judge(PickupSpec.Item(_herbId, 2), new PickupCollector(null, 100, 100).WithInventory(inventory));

            Assert.That(PickupPrompt.TryDescribe(outcome, "E", new LocalizedText(_herb.NameKey), out var text), Is.True);
            Assert.That(text.Key, Is.EqualTo(PickupPrompt.ItemKey));
        }

        [Test]
        public void TheLogNamesTheItemAndTheCount()
        {
            var outcome = PickupJudge.Judge(PickupSpec.Item(_herbId, 2), new PickupCollector(null, 100, 100).WithInventory(Make()));

            Assert.That(PickupLog.Collected("Pickup-Herb", "Player", outcome), Is.EqualTo(
                "[Pickup] collected pickup=Pickup-Herb by=Player kind=Item item=" + _herbId + " items=+2"));
            Assert.That(PickupLog.Describe(PickupSpec.Item(_herbId, 2)), Does.Contain("kind=Item item=" + _herbId + " amount=2"));
        }

        [Test]
        public void UsingAnItemHealsUpToTheMissingHealth()
        {
            var outcome = ItemUseJudge.Judge(_herb, new PickupCollector(null, 70, 100));

            Assert.That(outcome.IsUsed, Is.True);
            Assert.That(outcome.Health, Is.EqualTo(30));
            Assert.That(outcome.Armor, Is.EqualTo(0));
        }

        [Test]
        public void UsingAnItemAddsArmorOnlyToSomeoneWhoCanWearIt()
        {
            Assert.That(ItemUseJudge.Judge(_plate, new PickupCollector(null, 100, 100)).Rejection, Is.EqualTo(ItemUseRejection.Full));

            var outcome = ItemUseJudge.Judge(_plate, new PickupCollector(null, 100, 100).WithArmor(20, 50));
            Assert.That(outcome.Armor, Is.EqualTo(30));
        }

        [Test]
        public void UsingIsRefusedForUselessItemsFullOrDownedUsers()
        {
            Assert.That(ItemUseJudge.Judge(null, new PickupCollector(null, 50, 100)).Rejection, Is.EqualTo(ItemUseRejection.NoItem));
            Assert.That(ItemUseJudge.Judge(_key, new PickupCollector(null, 50, 100)).Rejection, Is.EqualTo(ItemUseRejection.NotUsable));
            Assert.That(ItemUseJudge.Judge(_herb, new PickupCollector(null, 0, 100)).Rejection, Is.EqualTo(ItemUseRejection.UserDown));
            Assert.That(ItemUseJudge.Judge(_herb, new PickupCollector(null, 100, 100)).Rejection, Is.EqualTo(ItemUseRejection.Full));
        }

        [Test]
        public void TheCarryLoadCountsExtraWeightWhateverTheWeightSource()
        {
            var carry = new CarryLoadSpec(CarrySource.None, CarrySource.None, 2f, 0.1f, 0.5f, CarryGaits.All);

            // 6 kg − 2 kg = 4 kg 超え → 1 − 0.4
            Assert.That(carry.SpeedMultiplier(null, null, 6f), Is.EqualTo(0.6f).Within(1e-5f));
            // 下限で止まる
            Assert.That(carry.SpeedMultiplier(null, null, 20f), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(carry.SpeedMultiplier(null, null, float.NaN), Is.EqualTo(1f).Within(1e-5f));
        }

        private static Inventory WeaponGrid(int width, int height, float maxWeight = 0f)
        {
            var rifle = new ItemSpec(_rifleItemId, "weapon.rifle", width: 4, height: 2, weaponId: _rifleId);
            var pistol = new ItemSpec(_plateId, "weapon.handgun", width: 2, height: 1, weaponId: _pistolId);
            return new Inventory(new ItemCatalog(new[] { rifle, pistol }, new InventoryRule(maxWeight: maxWeight, gridWidth: width, gridHeight: height)));
        }

        private static PickupCollector Armed(Inventory inventory, int maxWeapons, params string[] weaponIds)
        {
            var weapons = new PickupWeaponState[weaponIds.Length];
            for (var i = 0; i < weaponIds.Length; i++)
            {
                weapons[i] = new PickupWeaponState(weaponIds[i], 0, 0, false);
            }

            return new PickupCollector(null, 100, 100).WithWeapons(weapons, weaponIds.Length > 0 ? weaponIds[0] : null, maxWeapons).WithInventory(inventory);
        }

        [Test]
        public void AWeaponThatDoesNotFitInTheInventoryIsLeftOnTheGround()
        {
            var inventory = WeaponGrid(4, 2);
            inventory.HoldWeapons(new[] { new WeaponSpec(_rifleId, null, WeaponCategory.AssaultRifle, weight: 3f) });

            var outcome = PickupJudge.Judge(PickupSpec.Weapon(_pistolId, 0, default, 1f), Armed(inventory, 0, _rifleId));

            Assert.That(outcome.Rejection, Is.EqualTo(PickupRejection.InventoryFull));
        }

        [Test]
        public void AWeaponTooHeavyForTheInventoryIsLeftOnTheGround()
        {
            var inventory = WeaponGrid(8, 4, maxWeight: 2f);

            var outcome = PickupJudge.Judge(PickupSpec.Weapon(_rifleId, 0, default, 3f), Armed(inventory, 0));

            Assert.That(outcome.Rejection, Is.EqualTo(PickupRejection.InventoryFull));
        }

        [Test]
        public void SwappingCountsTheRoomOfTheWeaponLeft()
        {
            var inventory = WeaponGrid(4, 2);
            inventory.HoldWeapons(new[] { new WeaponSpec(_rifleId, null, WeaponCategory.AssaultRifle, weight: 3f) });

            var outcome = PickupJudge.Judge(PickupSpec.Weapon(_pistolId, 0, default, 1f), Armed(inventory, 1, _rifleId));

            Assert.That(outcome.IsCollected, Is.True);
            Assert.That(outcome.DroppedWeaponId, Is.EqualTo(_rifleId));
        }

        [Test]
        public void AWeaponThatFitsIsAdded()
        {
            var inventory = WeaponGrid(8, 4);

            var outcome = PickupJudge.Judge(PickupSpec.Weapon(_rifleId, 0, default, 3f), Armed(inventory, 0));

            Assert.That(outcome.IsCollected, Is.True);
            Assert.That(outcome.AddsWeapon, Is.True);
        }
    }
}
