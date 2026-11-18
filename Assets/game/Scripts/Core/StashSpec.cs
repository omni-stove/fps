using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class StashSpec
    {
        public const int DefaultCapacity = 60;

        // 渡す物はゲームの持ち物 (inventory.json) の Id で決まり、キットの既定の持ち物は空なので、空を既定にする。
        public static IReadOnlyList<StashStack> DefaultStartingItems { get; } = Array.Empty<StashStack>();

        public static StashSpec Default { get; } = new StashSpec();

        public StashSpec(int capacity = DefaultCapacity, IReadOnlyList<StashStack> startingItems = null)
        {
            Capacity = SpecGuard.AtLeast(capacity, 1, nameof(capacity));
            StartingItems = startingItems == null ? DefaultStartingItems : CheckStartingItems(startingItems, Capacity);
        }

        // 山ごとの重ねられる数は持ち物の maxStack がすでに決めているので、ここでは山の数だけを持つ。
        public int Capacity { get; }

        public IReadOnlyList<StashStack> StartingItems { get; }

        private static StashStack[] CheckStartingItems(IReadOnlyList<StashStack> startingItems, int capacity)
        {
            if (startingItems.Count > capacity)
            {
                throw new ArgumentException(
                    "startingItems has " + startingItems.Count + " stacks, which do not fit in a stash of capacity " + capacity, nameof(startingItems));
            }

            var copy = new StashStack[startingItems.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = startingItems[i] ?? throw new ArgumentException("startingItems must not contain null", nameof(startingItems));
            }

            return copy;
        }
    }

    public sealed class StashStack
    {
        public StashStack(string itemId, int count)
        {
            ItemId = SpecGuard.LowercaseUuid(itemId, nameof(itemId));
            Count = SpecGuard.AtLeast(count, 1, nameof(count));
        }

        public string ItemId { get; }

        public int Count { get; }
    }
}
