using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class StashJsonTests
    {
        private const string _source = "stash.json";
        private const string _bandage = "31d5979e-6067-49d9-b70c-fdcf44dc77d1";

        private static readonly string[] _expectedItemNames = { "capacity", "startingItems" };
        private static readonly string[] _expectedStackItemNames = { "itemId", "count" };

        [Test]
        public void ReadsEveryItem()
        {
            var spec = StashJson.Parse(
                "{\"capacity\": 2, \"startingItems\": [{\"itemId\": \"" + _bandage + "\", \"count\": 5}, {\"itemId\": \"" + _bandage + "\", \"count\": 1}]}",
                _source);

            Assert.That(spec.Capacity, Is.EqualTo(2));
            Assert.That(spec.StartingItems.Count, Is.EqualTo(2));
            Assert.That(spec.StartingItems[0].ItemId, Is.EqualTo(_bandage));
            Assert.That(spec.StartingItems[0].Count, Is.EqualTo(5));
            Assert.That(spec.StartingItems[1].Count, Is.EqualTo(1), "the same item may fill more than one stack");
        }

        [Test]
        public void LeftOutItemsUseTheDefaults()
        {
            var spec = StashJson.Parse("{}", _source);

            Assert.That(spec.Capacity, Is.EqualTo(60));
            Assert.That(spec.StartingItems, Is.Empty);
            Assert.That(StashSpec.Default.Capacity, Is.EqualTo(60));
        }

        [Test]
        public void TheItemNamesAreTheConstructorArguments()
        {
            Assert.That(StashJson.ItemNames, Is.EqualTo(_expectedItemNames));
            Assert.That(StashJson.StackItemNames, Is.EqualTo(_expectedStackItemNames));
            Assert.That(StashJson.ResourcePath, Is.EqualTo("Tuning/stash"));
        }

        [Test]
        public void ABadValueNamesItsItem()
        {
            var capacity = Reject("{\"capacity\": 0}");
            var overflow = Reject("{\"capacity\": 1, \"startingItems\": [" + Stack(1) + ", " + Stack(2) + "]}");
            var count = Reject("{\"startingItems\": [" + Stack(0) + "]}");
            var noCount = Reject("{\"startingItems\": [{\"itemId\": \"" + _bandage + "\"}]}");
            var itemId = Reject("{\"startingItems\": [{\"itemId\": \"BANDAGE\", \"count\": 1}]}");

            Assert.That(capacity.Item, Is.EqualTo("capacity"));
            Assert.That(capacity.InnerException, Is.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(overflow.Item, Is.EqualTo("startingItems"));
            Assert.That(overflow.Message, Does.Contain("capacity 1"));
            Assert.That(count.Item, Is.EqualTo("startingItems[0].count"));
            Assert.That(noCount.Item, Is.EqualTo("startingItems[0].count"));
            Assert.That(itemId.Item, Is.EqualTo("startingItems[0].itemId"));
        }

        [Test]
        public void TheStatusKeepsACopyAndChecksTheCapacity()
        {
            var stacks = new[] { new StashStack(_bandage, 3) };
            var status = new StashStatus(4, stacks);
            stacks[0] = new StashStack(_bandage, 9);

            Assert.That(status.Stacks[0].Count, Is.EqualTo(3));
            Assert.That(status.FreeSlots, Is.EqualTo(3));
            Assert.That(status, Is.EqualTo(new StashStatus(4, new[] { new StashStack(_bandage, 3) })));
            Assert.That(status, Is.Not.EqualTo(new StashStatus(5, new[] { new StashStack(_bandage, 3) })));
            Assert.That(default(StashStatus).Stacks, Is.Empty);
            Assert.That(status.ToString(), Is.EqualTo("stash 1/4"));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StashStatus(0, null));
            Assert.Throws<ArgumentException>(() => new StashStatus(1, new[] { stacks[0], stacks[0] }));
            Assert.Throws<ArgumentException>(() => new StashStatus(1, new StashStack[] { null }));
        }

        private static string Stack(int count)
        {
            return "{\"itemId\": \"" + _bandage + "\", \"count\": " + count + "}";
        }

        private static TuningJsonException Reject(string json)
        {
            return Assert.Throws<TuningJsonException>(() => StashJson.Parse(json, _source));
        }
    }
}
