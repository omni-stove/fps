using System;
using System.Collections.Generic;
using System.Globalization;

namespace Shooter.Core
{
    public readonly struct StashStatus : IEquatable<StashStatus>
    {
        // 渡された一覧を後から書き換えられても変わらないよう写しを持つ。default ではコンストラクタを通らず null になるので、読むときに空へ寄せる。
        private readonly StashEntry[] _stacks;

        public StashStatus(int capacity, IReadOnlyList<StashEntry> stacks)
        {
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "capacity must be at least 1");
            }

            var copy = new StashEntry[stacks?.Count ?? 0];
            if (copy.Length > capacity)
            {
                throw new ArgumentException("stacks has " + copy.Length + " stacks, more than the capacity " + capacity, nameof(stacks));
            }

            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = stacks[i] ?? throw new ArgumentException("stacks must not contain null", nameof(stacks));
            }

            Capacity = capacity;
            _stacks = copy;
        }

        public int Capacity { get; }

        public IReadOnlyList<StashEntry> Stacks => _stacks ?? Array.Empty<StashEntry>();

        public int FreeSlots => Capacity - Stacks.Count;

        public bool Equals(StashStatus other)
        {
            if (Capacity != other.Capacity)
            {
                return false;
            }

            var mine = Stacks;
            var theirs = other.Stacks;
            if (mine.Count != theirs.Count)
            {
                return false;
            }

            for (var i = 0; i < mine.Count; i++)
            {
                if (!string.Equals(mine[i].ItemId, theirs[i].ItemId, StringComparison.Ordinal) || mine[i].Count != theirs[i].Count)
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return obj is StashStatus other && Equals(other);
        }

        public static bool operator ==(StashStatus left, StashStatus right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(StashStatus left, StashStatus right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            var hash = Capacity;
            foreach (var stack in Stacks)
            {
                hash = HashCode.Combine(hash, StringComparer.Ordinal.GetHashCode(stack.ItemId), stack.Count);
            }

            return hash;
        }

        public override string ToString()
        {
            return "stash " + Stacks.Count.ToString(CultureInfo.InvariantCulture) + "/" + Capacity.ToString(CultureInfo.InvariantCulture);
        }
    }
}
