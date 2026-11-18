using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class LootSpec
    {
        // 落ちる物はゲームの持ち物 (inventory.json) の Id で決まり、キットの既定の持ち物は空なので、表も空を既定にする。
        public static IReadOnlyList<LootTable> DefaultTables { get; } = Array.Empty<LootTable>();

        public static LootSpec Default { get; } = new LootSpec();

        public LootSpec(IReadOnlyList<LootTable> tables = null)
        {
            Tables = tables == null ? DefaultTables : SpecLists.UniqueById(tables, t => t.Id, nameof(tables));
        }

        public IReadOnlyList<LootTable> Tables { get; }

        public LootTable Find(string id)
        {
            return SpecLists.FindById(Tables, t => t.Id, id);
        }
    }

    public sealed class LootTable
    {
        public const int DefaultRolls = 1;

        public LootTable(string id, IReadOnlyList<LootEntry> entries, int rolls = DefaultRolls)
        {
            Id = SpecLists.Id(id, nameof(id));
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            // 引く物の無い表は、箱を開けても空のままで、書き忘れと見分けられない。
            if (entries.Count == 0)
            {
                throw new ArgumentException("entries must have at least one entry", nameof(entries));
            }

            var copy = new LootEntry[entries.Count];
            var total = 0f;
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = entries[i] ?? throw new ArgumentException("entries must not contain null", nameof(entries));
                total += copy[i].Weight;
            }

            if (float.IsInfinity(total))
            {
                throw new ArgumentException("the weights of entries add up past the float range", nameof(entries));
            }

            Entries = copy;
            TotalWeight = total;
            Rolls = SpecGuard.AtLeast(rolls, 1, nameof(rolls));
        }

        public string Id { get; }

        public IReadOnlyList<LootEntry> Entries { get; }

        public int Rolls { get; }

        public float TotalWeight { get; }
    }

    public sealed class LootEntry
    {
        public const int DefaultMinCount = 1;
        public const int DefaultMaxCount = 1;

        public LootEntry(string itemId, float weight, int minCount = DefaultMinCount, int maxCount = DefaultMaxCount)
        {
            ItemId = SpecGuard.LowercaseUuid(itemId, nameof(itemId));
            Weight = SpecGuard.PositiveFinite(weight, nameof(weight));
            MinCount = SpecGuard.AtLeast(minCount, 1, nameof(minCount));
            MaxCount = SpecGuard.AtLeast(maxCount, minCount, nameof(maxCount));
        }

        public string ItemId { get; }

        public float Weight { get; }

        public int MinCount { get; }

        public int MaxCount { get; }
    }
}
