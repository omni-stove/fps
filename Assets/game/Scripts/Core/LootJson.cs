using System.Collections.Generic;

namespace Shooter.Core
{
    public static class LootJson
    {
        public const string ResourcePath = "Tuning/loot";

        public const string TablesItem = "tables";

        public const string IdItem = "id";
        public const string RollsItem = "rolls";
        public const string EntriesItem = "entries";

        public const string ItemIdItem = "itemId";
        public const string WeightItem = "weight";
        public const string MinCountItem = "minCount";
        public const string MaxCountItem = "maxCount";

        private const string _kind = "loot JSON";

        public static IReadOnlyList<string> ItemNames { get; } = new[] { TablesItem };

        public static IReadOnlyList<string> TableItemNames { get; } = new[] { IdItem, RollsItem, EntriesItem };

        public static IReadOnlyList<string> EntryItemNames { get; } = new[] { ItemIdItem, WeightItem, MinCountItem, MaxCountItem };

        public static LootSpec Parse(string text, string sourceName)
        {
            return SpecJsonReader.Read(text, sourceName, _kind, ItemNames, fields =>
            {
                var tables = SpecJsonReader.ReadList(fields, TablesItem, TableItemNames, ReadTable);
                return SpecJsonReader.Build(fields, ItemNames, () => new LootSpec(tables));
            });
        }

        private static LootTable ReadTable(ServerDataJson.Fields table)
        {
            var id = SpecJsonReader.RequireText(table, IdItem);
            var rolls = table.ReadInt(RollsItem, LootTable.DefaultRolls);
            table.Require(EntriesItem);
            var entries = SpecJsonReader.ReadList(table, EntriesItem, EntryItemNames, ReadEntry);
            return new LootTable(id, entries, rolls);
        }

        private static LootEntry ReadEntry(ServerDataJson.Fields entry)
        {
            var itemId = SpecJsonReader.RequireText(entry, ItemIdItem);
            var weight = entry.ToFloat(entry.ItemPath(WeightItem), entry.Require(WeightItem));
            var minCount = entry.ReadInt(MinCountItem, LootEntry.DefaultMinCount);
            var maxCount = entry.ReadInt(MaxCountItem, minCount);
            return new LootEntry(itemId, weight, minCount, maxCount);
        }
    }
}
