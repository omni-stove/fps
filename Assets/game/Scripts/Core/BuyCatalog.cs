using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class BuyCatalog
    {
        public static BuyCatalog Empty { get; } = new BuyCatalog(Array.Empty<BuyItem>());

        private readonly List<BuyItem> _items;

        public BuyCatalog(IEnumerable<BuyItem> items)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            _items = new List<BuyItem>();
            foreach (var item in items)
            {
                if (item == null)
                {
                    throw new ArgumentException("items must not contain null", nameof(items));
                }

                if (TryFind(item.Id, out _))
                {
                    throw new ArgumentException("item " + item.Id + " is listed twice", nameof(items));
                }

                _items.Add(item);
            }
        }

        public IReadOnlyList<BuyItem> Items => _items;

        public int Count => _items.Count;

        public bool TryFind(string id, out BuyItem item)
        {
            if (id != null)
            {
                foreach (var candidate in _items)
                {
                    if (string.Equals(candidate.Id, id, StringComparison.Ordinal))
                    {
                        item = candidate;
                        return true;
                    }
                }
            }

            item = null;
            return false;
        }
    }
}
