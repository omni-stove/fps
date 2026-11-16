using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class InventoryPickupTests
    {
        private const string _herbId = "0d3c6f1a-5b2e-4c7d-8a9f-1e2b3c4d5e6f";
        private const string _plateId = "5c6d7e8f-9a0b-4c1d-8e2f-3a4b5c6d7e8f";
        private const string _keyId = "1a2b3c4d-5e6f-4a7b-8c9d-0e1f2a3b4c5d";

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
    }
}
