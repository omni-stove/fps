using System;
using System.Collections.Generic;

namespace Fps.Core
{
    public sealed class ItemCatalog
    {
        // 持ち物を送る電文がアイテムを表の番号 (byte) で指す
        public const int MaxItems = byte.MaxValue;

        public static readonly ItemCatalog Empty = new ItemCatalog(Array.Empty<ItemSpec>(), InventoryRule.Default);

        public ItemCatalog(IEnumerable<ItemSpec> items, InventoryRule rule, IEnumerable<ItemCount> starting = null)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            Rule = rule ?? throw new ArgumentNullException(nameof(rule));
            var list = new List<ItemSpec>();
            foreach (var item in items)
            {
                if (item == null)
                {
                    throw new ArgumentException("items must not contain null", nameof(items));
                }

                if (IndexIn(list, item.Id) >= 0)
                {
                    throw new ArgumentException("item " + item.Id + " is listed twice", nameof(items));
                }

                list.Add(item);
            }

            if (list.Count > MaxItems)
            {
                throw new ArgumentException("at most " + MaxItems + " items can be listed", nameof(items));
            }

            Items = list.AsReadOnly();
            var start = new List<ItemCount>();
            if (starting != null)
            {
                foreach (var entry in starting)
                {
                    if (entry.ItemId == null)
                    {
                        throw new ArgumentException("starting must not contain an empty entry", nameof(starting));
                    }

                    if (IndexIn(list, entry.ItemId) < 0)
                    {
                        throw new ArgumentException("starting item " + entry.ItemId + " is not one of the items", nameof(starting));
                    }

                    start.Add(entry);
                }
            }

            Starting = start.AsReadOnly();
        }

        public IReadOnlyList<ItemSpec> Items { get; }

        public InventoryRule Rule { get; }

        public IReadOnlyList<ItemCount> Starting { get; }

        public bool TryFind(string id, out ItemSpec item)
        {
            var at = IndexOf(id);
            item = at >= 0 ? Items[at] : null;
            return item != null;
        }

        public int IndexOf(string id)
        {
            return string.IsNullOrEmpty(id) ? -1 : IndexIn(Items, id);
        }

        private static int IndexIn(IReadOnlyList<ItemSpec> items, string id)
        {
            for (var i = 0; i < items.Count; i++)
            {
                if (string.Equals(items[i].Id, id, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
