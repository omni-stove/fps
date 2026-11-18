using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class LootJsonTests
    {
        private const string _source = "loot.json";
        private const string _bandage = "31d5979e-6067-49d9-b70c-fdcf44dc77d1";
        private const string _medkit = "61dd226a-156c-4060-ab8f-5dbed8cb7f4f";

        private static readonly string[] _expectedItemNames = { "tables" };
        private static readonly string[] _expectedTableItemNames = { "id", "rolls", "entries" };
        private static readonly string[] _expectedEntryItemNames = { "itemId", "weight", "minCount", "maxCount" };

        [Test]
        public void ReadsEveryItem()
        {
            var spec = LootJson.Parse(
                "{\"tables\": [{\"id\": \"medical\", \"rolls\": 2, \"entries\": ["
                + "{\"itemId\": \"" + _bandage + "\", \"weight\": 3, \"minCount\": 1, \"maxCount\": 4},"
                + " {\"itemId\": \"" + _medkit + "\", \"weight\": 0.5}]}]}",
                _source);

            Assert.That(spec.Tables.Count, Is.EqualTo(1));
            var table = spec.Find("medical");
            Assert.That(table, Is.SameAs(spec.Tables[0]));
            Assert.That(table.Rolls, Is.EqualTo(2));
            Assert.That(table.TotalWeight, Is.EqualTo(3.5f));
            Assert.That(table.Entries.Count, Is.EqualTo(2));
            Assert.That(table.Entries[0].ItemId, Is.EqualTo(_bandage));
            Assert.That(table.Entries[0].Weight, Is.EqualTo(3f));
            Assert.That(table.Entries[0].MinCount, Is.EqualTo(1));
            Assert.That(table.Entries[0].MaxCount, Is.EqualTo(4));
            Assert.That(table.Entries[1].Weight, Is.EqualTo(0.5f));
            Assert.That(table.Entries[1].MinCount, Is.EqualTo(1));
            Assert.That(table.Entries[1].MaxCount, Is.EqualTo(1));
        }

        [Test]
        public void LeftOutItemsUseTheDefaults()
        {
            var spec = LootJson.Parse("{}", _source);
            var oneRoll = LootJson.Parse(
                "{\"tables\": [{\"id\": \"t\", \"entries\": [{\"itemId\": \"" + _bandage + "\", \"weight\": 1, \"minCount\": 3}]}]}",
                _source);

            Assert.That(spec.Tables, Is.Empty);
            Assert.That(LootSpec.Default.Tables, Is.Empty);
            Assert.That(oneRoll.Tables[0].Rolls, Is.EqualTo(1));
            Assert.That(oneRoll.Tables[0].Entries[0].MaxCount, Is.EqualTo(3), "a left out maxCount is the minCount");
        }

        [Test]
        public void TheItemNamesAreTheConstructorArguments()
        {
            Assert.That(LootJson.ItemNames, Is.EqualTo(_expectedItemNames));
            Assert.That(LootJson.TableItemNames, Is.EqualTo(_expectedTableItemNames));
            Assert.That(LootJson.EntryItemNames, Is.EqualTo(_expectedEntryItemNames));
            Assert.That(LootJson.ResourcePath, Is.EqualTo("Tuning/loot"));
        }

        [Test]
        public void ABadValueNamesItsItem()
        {
            var noEntries = Reject("{\"tables\": [{\"id\": \"t\"}]}");
            var emptyEntries = Reject("{\"tables\": [{\"id\": \"t\", \"entries\": []}]}");
            var rolls = Reject(Table("{\"itemId\": \"" + _bandage + "\", \"weight\": 1}", "\"rolls\": 0, "));
            var itemId = Reject(Table("{\"itemId\": \"bandage\", \"weight\": 1}"));
            var weight = Reject(Table("{\"itemId\": \"" + _bandage + "\", \"weight\": 0}"));
            var counts = Reject(Table("{\"itemId\": \"" + _bandage + "\", \"weight\": 1, \"minCount\": 3, \"maxCount\": 2}"));
            var twice = Reject("{\"tables\": [" + TableBody("a") + ", " + TableBody("a") + "]}");

            Assert.That(noEntries.Item, Is.EqualTo("tables[0].entries"));
            Assert.That(emptyEntries.Item, Is.EqualTo("tables[0].entries"));
            Assert.That(emptyEntries.InnerException, Is.InstanceOf<ArgumentException>());
            Assert.That(rolls.Item, Is.EqualTo("tables[0].rolls"));
            Assert.That(itemId.Item, Is.EqualTo("tables[0].entries[0].itemId"));
            Assert.That(itemId.Message, Does.Contain("UUID"));
            Assert.That(weight.Item, Is.EqualTo("tables[0].entries[0].weight"));
            Assert.That(counts.Item, Is.EqualTo("tables[0].entries[0].maxCount"));
            Assert.That(twice.Item, Is.EqualTo("tables"));
        }

        private static string Table(string entry, string extra = "")
        {
            return "{\"tables\": [{\"id\": \"t\", " + extra + "\"entries\": [" + entry + "]}]}";
        }

        private static string TableBody(string id)
        {
            return "{\"id\": \"" + id + "\", \"entries\": [{\"itemId\": \"" + _bandage + "\", \"weight\": 1}]}";
        }

        private static TuningJsonException Reject(string json)
        {
            return Assert.Throws<TuningJsonException>(() => LootJson.Parse(json, _source));
        }
    }
}
