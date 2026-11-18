using System.Collections.Generic;

namespace Shooter.Core
{
    public static class StashJson
    {
        public const string ResourcePath = "Tuning/stash";

        public const string CapacityItem = "capacity";
        public const string StartingItemsItem = "startingItems";

        public const string ItemIdItem = "itemId";
        public const string CountItem = "count";

        private const string _kind = "stash JSON";

        public static IReadOnlyList<string> ItemNames { get; } = new[] { CapacityItem, StartingItemsItem };

        public static IReadOnlyList<string> StackItemNames { get; } = new[] { ItemIdItem, CountItem };

        public static StashSpec Parse(string text, string sourceName)
        {
            return SpecJsonReader.Read(text, sourceName, _kind, ItemNames, fields =>
            {
                var capacity = fields.ReadInt(CapacityItem, StashSpec.DefaultCapacity);
                var startingItems = SpecJsonReader.ReadList(fields, StartingItemsItem, StackItemNames, ReadStack);
                return SpecJsonReader.Build(fields, ItemNames, () => new StashSpec(capacity, startingItems));
            });
        }

        private static StashEntry ReadStack(ServerDataJson.Fields entry)
        {
            var itemId = SpecJsonReader.RequireText(entry, ItemIdItem);
            var count = entry.ToInt(entry.ItemPath(CountItem), entry.Require(CountItem));
            return new StashEntry(itemId, count);
        }
    }
}
