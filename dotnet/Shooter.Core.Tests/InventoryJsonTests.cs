using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class InventoryJsonTests
    {
        private const string _herbId = "0d3c6f1a-5b2e-4c7d-8a9f-1e2b3c4d5e6f";
        private const string _ammoId = "7a8b9c0d-1e2f-4a3b-9c4d-5e6f7a8b9c0d";
        private const string _rifleItemId = "2b3c4d5e-6f7a-4b8c-9d0e-1f2a3b4c5d6e";
        private const string _rifleId = "5e6f7a8b-9c0d-4e1f-8a3b-4c5d6e7f8a9b";

        [Test]
        public void ReadsTheRuleItemsAndStartingItems()
        {
            var catalog = InventoryJson.Parse(
                "{ \"rule\": { \"maxStacks\": 12, \"maxWeight\": 30.5, \"gridWidth\": 8, \"gridHeight\": 5, \"allowRotation\": false, \"countWeightInCarryLoad\": true },"
                + " \"items\": [ { \"id\": \"" + _herbId + "\", \"nameKey\": \"item.herb\", \"iconId\": \"herb\", \"maxStack\": 3, \"maxCarried\": 9,"
                + " \"weight\": 0.25, \"width\": 1, \"height\": 2, \"useHealth\": 40, \"useArmor\": 5 } ],"
                + " \"starting\": [ { \"id\": \"" + _herbId + "\", \"count\": 2 } ] }",
                "inventory.json");

            var rule = catalog.Rule;
            Assert.That(rule.MaxStacks, Is.EqualTo(12));
            Assert.That(rule.MaxWeight, Is.EqualTo(30.5f));
            Assert.That(rule.GridWidth, Is.EqualTo(8));
            Assert.That(rule.GridHeight, Is.EqualTo(5));
            Assert.That(rule.AllowRotation, Is.False);
            Assert.That(rule.CountWeightInCarryLoad, Is.True);

            var item = catalog.Items[0];
            Assert.That(item.Id, Is.EqualTo(_herbId));
            Assert.That(item.NameKey, Is.EqualTo("item.herb"));
            Assert.That(item.IconId, Is.EqualTo("herb"));
            Assert.That(item.MaxStack, Is.EqualTo(3));
            Assert.That(item.MaxCarried, Is.EqualTo(9));
            Assert.That(item.Weight, Is.EqualTo(0.25f));
            Assert.That(item.Width, Is.EqualTo(1));
            Assert.That(item.Height, Is.EqualTo(2));
            Assert.That(item.UseHealth, Is.EqualTo(40));
            Assert.That(item.UseArmor, Is.EqualTo(5));

            Assert.That(catalog.Starting[0].ItemId, Is.EqualTo(_herbId));
            Assert.That(catalog.Starting[0].Count, Is.EqualTo(2));
        }

        [Test]
        public void LeftOutItemsUseTheDefaults()
        {
            var catalog = InventoryJson.Parse("{ \"items\": [ { \"id\": \"" + _herbId + "\" } ] }", "inventory.json");

            Assert.That(catalog.Rule.HasGrid, Is.False);
            Assert.That(catalog.Rule.MaxWeight, Is.EqualTo(InventoryRule.DefaultMaxWeight));
            Assert.That(catalog.Rule.AllowRotation, Is.EqualTo(InventoryRule.DefaultAllowRotation));
            Assert.That(catalog.Items[0].MaxStack, Is.EqualTo(ItemSpec.DefaultMaxStack));
            Assert.That(catalog.Items[0].Width, Is.EqualTo(ItemSpec.DefaultWidth));
            Assert.That(catalog.Starting, Is.Empty);
        }

        [Test]
        public void AnEmptyObjectIsAnEmptyCatalog()
        {
            var catalog = InventoryJson.Parse("{}", "inventory.json");

            Assert.That(catalog.Items, Is.Empty);
            Assert.That(catalog.Rule.HasGrid, Is.False);
        }

        [Test]
        public void UnknownItemsAreRefusedWithTheirPath()
        {
            var e = Assert.Throws<TuningJsonException>(() => InventoryJson.Parse("{ \"rule\": { \"slots\": 3 } }", "inventory.json"));

            Assert.That(e.Item, Is.EqualTo("rule.slots"));
        }

        [Test]
        public void BadValuesNameTheItem()
        {
            var e = Assert.Throws<TuningJsonException>(() => InventoryJson.Parse(
                "{ \"items\": [ { \"id\": \"" + _herbId + "\", \"maxStack\": 0 } ] }", "inventory.json"));

            Assert.That(e.Item, Is.EqualTo("items[0].maxStack"));
        }

        [Test]
        public void ARuleWithAHeightButNoWidthIsRefused()
        {
            var e = Assert.Throws<TuningJsonException>(() => InventoryJson.Parse("{ \"rule\": { \"gridHeight\": 4 } }", "inventory.json"));

            Assert.That(e.Item, Is.EqualTo("rule.gridHeight"));
        }

        [Test]
        public void AStartingItemThatIsNotListedIsRefused()
        {
            var e = Assert.Throws<TuningJsonException>(() => InventoryJson.Parse(
                "{ \"starting\": [ { \"id\": \"" + _herbId + "\" } ] }", "inventory.json"));

            Assert.That(e.Item, Is.EqualTo("starting"));
        }

        [Test]
        public void ReadsTheWeaponAndItsAmmoItem()
        {
            var catalog = InventoryJson.Parse(
                "{ \"items\": [ { \"id\": \"" + _ammoId + "\", \"maxStack\": 30, \"weight\": 0.01 },"
                + " { \"id\": \"" + _rifleItemId + "\", \"nameKey\": \"weapon.rifle\", \"weaponId\": \"" + _rifleId + "\", \"ammoItemId\": \"" + _ammoId + "\","
                + " \"width\": 4, \"height\": 2 } ] }",
                "inventory.json");

            var rifle = catalog.Items[1];
            Assert.That(rifle.WeaponId, Is.EqualTo(_rifleId));
            Assert.That(rifle.AmmoItemId, Is.EqualTo(_ammoId));
            Assert.That(rifle.Weight, Is.EqualTo(0f));
            Assert.That(catalog.Items[0].WeaponId, Is.Null);
        }

        [Test]
        public void AWeaponItemWithItsOwnWeightIsRefused()
        {
            var e = Assert.Throws<TuningJsonException>(() => InventoryJson.Parse(
                "{ \"items\": [ { \"id\": \"" + _rifleItemId + "\", \"weaponId\": \"" + _rifleId + "\", \"weight\": 3 } ] }", "inventory.json"));

            Assert.That(e.Item, Is.EqualTo("items[0].weight"));
        }

        [Test]
        public void AnAmmoItemThatIsNotListedIsRefused()
        {
            var e = Assert.Throws<TuningJsonException>(() => InventoryJson.Parse(
                "{ \"items\": [ { \"id\": \"" + _rifleItemId + "\", \"weaponId\": \"" + _rifleId + "\", \"ammoItemId\": \"" + _ammoId + "\" } ] }",
                "inventory.json"));

            Assert.That(e.Item, Is.EqualTo("items"));
        }

        [Test]
        public void ReadsTheSightOfAnAttachment()
        {
            var catalog = InventoryJson.Parse(
                "{ \"items\": [ { \"id\": \"" + _herbId + "\", \"attachment\": { \"slot\": \"optic\", \"zoomLevels\": [25],"
                + " \"sight\": { \"reticleId\": \"scope4x\", \"scopeOverlay\": true, \"eyeViewInThirdPerson\": true, \"aimedInProgress\": 0.8 } } } ] }",
                "inventory.json");

            var attachment = catalog.Items[0].Attachment;
            Assert.That(attachment.IsOptic, Is.True);
            Assert.That(attachment.Sight.ReticleId, Is.EqualTo("scope4x"));
            Assert.That(attachment.Sight.ScopeOverlay, Is.True);
            Assert.That(attachment.Sight.EyeViewInThirdPerson, Is.True);
            Assert.That(attachment.Sight.AimedInProgress, Is.EqualTo(0.8f));
        }

        [Test]
        public void AnAttachmentWithoutASightKeepsTheWeaponsOne()
        {
            var catalog = InventoryJson.Parse(
                "{ \"items\": [ { \"id\": \"" + _herbId + "\", \"attachment\": { \"slot\": \"muzzle\" } } ] }", "inventory.json");

            Assert.That(catalog.Items[0].Attachment.Sight, Is.Null);
            Assert.That(catalog.Items[0].Attachment.IsOptic, Is.False);
        }

        [Test]
        public void ABadSightNamesTheItem()
        {
            var e = Assert.Throws<TuningJsonException>(() => InventoryJson.Parse(
                "{ \"items\": [ { \"id\": \"" + _herbId + "\", \"attachment\": { \"slot\": \"optic\", \"sight\": { \"aimedInProgress\": 0 } } } ] }",
                "inventory.json"));

            Assert.That(e.Item, Is.EqualTo("items[0].attachment.sight.aimedInProgress"));

            var unknown = Assert.Throws<TuningJsonException>(() => InventoryJson.Parse(
                "{ \"items\": [ { \"id\": \"" + _herbId + "\", \"attachment\": { \"slot\": \"optic\", \"sight\": { \"color\": 1 } } } ] }",
                "inventory.json"));

            Assert.That(unknown.Item, Is.EqualTo("items[0].attachment.sight.color"));
        }
    }
}
