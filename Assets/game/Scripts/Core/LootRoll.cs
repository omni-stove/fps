using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public static class LootRoll
    {
        public static IReadOnlyList<ItemCount> Roll(LootTable table, Random random)
        {
            if (table == null)
            {
                throw new ArgumentNullException(nameof(table));
            }

            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            var picked = new List<ItemCount>();
            for (var i = 0; i < table.Rolls; i++)
            {
                var entry = Pick(table, random.NextDouble());
                Merge(picked, entry.ItemId, CountOf(entry, random.NextDouble()));
            }

            return picked;
        }

        // 乱数を引く所と分けるのは、どの値でどの行が出るかをテストで決め打ちに確かめるため。
        public static LootEntry Pick(LootTable table, double fraction)
        {
            if (table == null)
            {
                throw new ArgumentNullException(nameof(table));
            }

            if (!(fraction >= 0.0) || fraction >= 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(fraction), fraction, "fraction must be in [0, 1)");
            }

            var point = fraction * table.TotalWeight;
            var reached = 0.0;
            foreach (var entry in table.Entries)
            {
                reached += entry.Weight;
                if (point < reached)
                {
                    return entry;
                }
            }

            // float の重みを double で足し直すと、合計の丸めの差で最後の行をわずかに越えることがある。
            return table.Entries[table.Entries.Count - 1];
        }

        public static int CountOf(LootEntry entry, double fraction)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            if (!(fraction >= 0.0) || fraction >= 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(fraction), fraction, "fraction must be in [0, 1)");
            }

            // MaxCount が int.MaxValue のとき MaxCount + 1 が溢れるので long で幅を取る。
            var span = (long)entry.MaxCount - entry.MinCount + 1;
            var offset = (long)(fraction * span);
            return (int)Math.Min(entry.MinCount + offset, entry.MaxCount);
        }

        private static void Merge(List<ItemCount> picked, string itemId, int count)
        {
            for (var i = 0; i < picked.Count; i++)
            {
                if (string.Equals(picked[i].ItemId, itemId, StringComparison.Ordinal))
                {
                    var total = (int)Math.Min((long)picked[i].Count + count, int.MaxValue);
                    picked[i] = new ItemCount(itemId, total);
                    return;
                }
            }

            picked.Add(new ItemCount(itemId, count));
        }
    }
}
