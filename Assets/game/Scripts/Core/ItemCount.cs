using System;

namespace Shooter.Core
{
    public readonly struct ItemCount
    {
        public ItemCount(string itemId, int count)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                throw new ArgumentException("itemId must not be null or empty", nameof(itemId));
            }

            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "count must be positive");
            }

            ItemId = itemId;
            Count = count;
        }

        public string ItemId { get; }

        public int Count { get; }
    }
}
