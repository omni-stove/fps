using System;

namespace Shooter.Core
{
    public readonly struct InventoryEntry : IEquatable<InventoryEntry>
    {
        public InventoryEntry(int handle, string itemId, int count, int x, int y, bool rotated)
        {
            if (handle <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(handle), handle, "handle must be positive");
            }

            if (string.IsNullOrEmpty(itemId))
            {
                throw new ArgumentException("itemId must not be null or empty", nameof(itemId));
            }

            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "count must be positive");
            }

            if (x < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(x), x, "x must not be negative");
            }

            if (y < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(y), y, "y must not be negative");
            }

            Handle = handle;
            ItemId = itemId;
            Count = count;
            X = x;
            Y = y;
            Rotated = rotated;
        }

        public int Handle { get; }

        public string ItemId { get; }

        public int Count { get; }

        public int X { get; }

        public int Y { get; }

        public bool Rotated { get; }

        public InventoryEntry WithCount(int count) => new InventoryEntry(Handle, ItemId, count, X, Y, Rotated);

        public InventoryEntry MovedTo(int x, int y, bool rotated) => new InventoryEntry(Handle, ItemId, Count, x, y, rotated);

        public bool Equals(InventoryEntry other)
        {
            return Handle == other.Handle && string.Equals(ItemId, other.ItemId, StringComparison.Ordinal) && Count == other.Count
                && X == other.X && Y == other.Y && Rotated == other.Rotated;
        }

        public static bool operator ==(InventoryEntry left, InventoryEntry right) => left.Equals(right);

        public static bool operator !=(InventoryEntry left, InventoryEntry right) => !left.Equals(right);

        public override bool Equals(object obj) => obj is InventoryEntry other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Handle;
                hash = (hash * 397) ^ (ItemId != null ? StringComparer.Ordinal.GetHashCode(ItemId) : 0);
                hash = (hash * 397) ^ Count;
                hash = (hash * 397) ^ X;
                hash = (hash * 397) ^ Y;
                return (hash * 397) ^ (Rotated ? 1 : 0);
            }
        }

        public override string ToString()
        {
            return "#" + Handle + " " + ItemId + " x" + Count + " @" + X + "," + Y + (Rotated ? " rotated" : "");
        }
    }
}
