using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class InventoryJsonTests
    {
        private const string _herbId = "0d3c6f1a-5b2e-4c7d-8a9f-1e2b3c4d5e6f";

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
    }
}
