using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class Stash
    {
        private readonly List<StashEntry> _stacks = new List<StashEntry>();

        public Stash(int capacity, ItemCatalog catalog)
        {
            Capacity = SpecGuard.AtLeast(capacity, 1, nameof(capacity));
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            Stacks = _stacks.AsReadOnly();
        }

        public event Action Changed;

        public int Capacity { get; }

        public ItemCatalog Catalog { get; }

        public IReadOnlyList<StashEntry> Stacks { get; }

        public int FreeSlots => Math.Max(0, Capacity - _stacks.Count);

        // 保存した後に広さを狭めたり持ち物を表から外したりしても、プレイヤーの物を黙って消さないよう広さを越えた山も持ち続ける。そのときは山の数を広さとして見せる。
        public StashStatus Status => new StashStatus(Math.Max(Capacity, _stacks.Count), _stacks);

        public static Stash Fresh(StashSpec spec, ItemCatalog catalog)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            var stash = new Stash(spec.Capacity, catalog);
            foreach (var entry in spec.StartingItems)
            {
                stash.Place(entry.ItemId, entry.Count);
            }

            return stash;
        }

        public static Stash Restore(int capacity, ItemCatalog catalog, IReadOnlyList<StashEntry> stacks)
        {
            if (stacks == null)
            {
                throw new ArgumentNullException(nameof(stacks));
            }

            var stash = new Stash(capacity, catalog);
            foreach (var stack in stacks)
            {
                if (stack == null)
                {
                    throw new ArgumentException("stacks must not contain null", nameof(stacks));
                }

                stash._stacks.Add(stack);
            }

            return stash;
        }

        public int CountOf(string itemId)
        {
            var total = 0L;
            foreach (var stack in _stacks)
            {
                if (string.Equals(stack.ItemId, itemId, StringComparison.Ordinal))
                {
                    total += stack.Count;
                }
            }

            return (int)Math.Min(total, int.MaxValue);
        }

        public bool Has(IReadOnlyList<ItemCount> items)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            foreach (var group in Totals(items))
            {
                if (CountOf(group.ItemId) < group.Count)
                {
                    return false;
                }
            }

            return true;
        }

        // 表に無い持ち物は重ねられる数が分からないので入れない。
        public int RoomFor(string itemId)
        {
            if (!Catalog.TryFind(itemId, out var item))
            {
                return 0;
            }

            var room = (long)FreeSlots * item.MaxStack;
            foreach (var stack in _stacks)
            {
                if (string.Equals(stack.ItemId, item.Id, StringComparison.Ordinal) && stack.Count < item.MaxStack)
                {
                    room += item.MaxStack - stack.Count;
                }
            }

            return (int)Math.Min(room, int.MaxValue);
        }

        public int Add(string itemId, int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "count must not be negative");
            }

            var added = Place(itemId, count);
            if (added > 0)
            {
                Changed?.Invoke();
            }

            return added;
        }

        public int Remove(string itemId, int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "count must not be negative");
            }

            var removed = Take(itemId, count);
            if (removed > 0)
            {
                Changed?.Invoke();
            }

            return removed;
        }

        // 持ち込む物を一部だけ出して出撃させないよう、全部そろうときだけまとめて出す。
        public bool TryTake(IReadOnlyList<ItemCount> items)
        {
            if (!Has(items))
            {
                return false;
            }

            var removed = 0;
            foreach (var group in Totals(items))
            {
                removed += Take(group.ItemId, group.Count);
            }

            if (removed > 0)
            {
                Changed?.Invoke();
            }

            return true;
        }

        private int Place(string itemId, int count)
        {
            if (count == 0 || !Catalog.TryFind(itemId, out var item))
            {
                return 0;
            }

            var remaining = count;
            for (var i = 0; i < _stacks.Count && remaining > 0; i++)
            {
                var stack = _stacks[i];
                if (!string.Equals(stack.ItemId, item.Id, StringComparison.Ordinal) || stack.Count >= item.MaxStack)
                {
                    continue;
                }

                var put = Math.Min(remaining, item.MaxStack - stack.Count);
                _stacks[i] = new StashEntry(item.Id, stack.Count + put);
                remaining -= put;
            }

            while (remaining > 0 && _stacks.Count < Capacity)
            {
                var put = Math.Min(remaining, item.MaxStack);
                _stacks.Add(new StashEntry(item.Id, put));
                remaining -= put;
            }

            return count - remaining;
        }

        // 後ろの山から減らすのは、Inventory.Remove と同じく、前の満杯の山を崩さないため。
        private int Take(string itemId, int count)
        {
            var remaining = count;
            for (var i = _stacks.Count - 1; i >= 0 && remaining > 0; i--)
            {
                var stack = _stacks[i];
                if (!string.Equals(stack.ItemId, itemId, StringComparison.Ordinal))
                {
                    continue;
                }

                var taken = Math.Min(remaining, stack.Count);
                remaining -= taken;
                if (taken == stack.Count)
                {
                    _stacks.RemoveAt(i);
                }
                else
                {
                    _stacks[i] = new StashEntry(stack.ItemId, stack.Count - taken);
                }
            }

            return count - remaining;
        }

        // 同じ持ち物が一覧に何度出ても、足りるかは合計で比べる。
        private static List<ItemCount> Totals(IReadOnlyList<ItemCount> items)
        {
            var totals = new List<ItemCount>();
            foreach (var item in items)
            {
                if (item.ItemId == null)
                {
                    throw new ArgumentException("items must not contain an empty count", nameof(items));
                }

                var merged = false;
                for (var i = 0; i < totals.Count; i++)
                {
                    if (string.Equals(totals[i].ItemId, item.ItemId, StringComparison.Ordinal))
                    {
                        totals[i] = new ItemCount(item.ItemId, (int)Math.Min((long)totals[i].Count + item.Count, int.MaxValue));
                        merged = true;
                        break;
                    }
                }

                if (!merged)
                {
                    totals.Add(item);
                }
            }

            return totals;
        }
    }
}
