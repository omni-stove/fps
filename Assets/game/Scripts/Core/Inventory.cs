using System;
using System.Collections.Generic;

namespace Fps.Core
{
    public sealed class Inventory
    {
        // 持ち物を送る電文が山の番号を ushort で送る
        public const int MaxHandle = ushort.MaxValue;

        // 重さの比べで、足した重さの丸めの誤差だけで上限を超えたことにしない
        private const float _weightTolerance = 1e-4f;

        private readonly List<InventoryEntry> _stacks = new List<InventoryEntry>();
        private int _nextHandle = 1;

        public Inventory(ItemCatalog catalog)
        {
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            Stacks = _stacks.AsReadOnly();
        }

        public event Action Changed;

        public ItemCatalog Catalog { get; }

        public InventoryRule Rule => Catalog.Rule;

        public IReadOnlyList<InventoryEntry> Stacks { get; }

        public float TotalWeight => WeightOf(_stacks);

        public int GridRows => !Rule.HasGrid ? 0 : Rule.HasFixedGridHeight ? Rule.GridHeight : Bottom(_stacks);

        public int CountOf(string itemId)
        {
            return CountIn(_stacks, itemId);
        }

        public int IndexOfHandle(int handle)
        {
            for (var i = 0; i < _stacks.Count; i++)
            {
                if (_stacks[i].Handle == handle)
                {
                    return i;
                }
            }

            return -1;
        }

        public bool TryGetStack(int handle, out InventoryEntry stack)
        {
            var at = IndexOfHandle(handle);
            stack = at >= 0 ? _stacks[at] : default;
            return at >= 0;
        }

        public int RoomFor(string itemId)
        {
            if (!Catalog.TryFind(itemId, out var item))
            {
                return 0;
            }

            var copy = new List<InventoryEntry>(_stacks);
            var next = _nextHandle;
            return Place(copy, item, int.MaxValue, ref next);
        }

        public int Add(string itemId, int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "count must not be negative");
            }

            if (count == 0 || !Catalog.TryFind(itemId, out var item))
            {
                return 0;
            }

            var added = Place(_stacks, item, count, ref _nextHandle);
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

            var remaining = count;
            for (var i = _stacks.Count - 1; i >= 0 && remaining > 0; i--)
            {
                if (!string.Equals(_stacks[i].ItemId, itemId, StringComparison.Ordinal))
                {
                    continue;
                }

                var taken = Math.Min(remaining, _stacks[i].Count);
                remaining -= taken;
                TakeAt(i, taken);
            }

            var removed = count - remaining;
            if (removed > 0)
            {
                Changed?.Invoke();
            }

            return removed;
        }

        public bool TryTake(int handle, int count, out ItemCount taken)
        {
            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "count must be positive");
            }

            var at = IndexOfHandle(handle);
            if (at < 0)
            {
                taken = default;
                return false;
            }

            var amount = Math.Min(count, _stacks[at].Count);
            taken = new ItemCount(_stacks[at].ItemId, amount);
            TakeAt(at, amount);
            Changed?.Invoke();
            return true;
        }

        public InventoryMoveResult TryMove(int handle, int x, int y, bool rotated)
        {
            var result = Check(handle, x, y, rotated);
            if (result != InventoryMoveResult.Moved)
            {
                return result;
            }

            var at = IndexOfHandle(handle);
            var stack = _stacks[at];
            if (stack.X == x && stack.Y == y && stack.Rotated == rotated)
            {
                return InventoryMoveResult.Moved;
            }

            _stacks[at] = stack.MovedTo(x, y, rotated);
            Changed?.Invoke();
            return InventoryMoveResult.Moved;
        }

        public InventoryMoveResult Check(int handle, int x, int y, bool rotated)
        {
            var at = IndexOfHandle(handle);
            if (at < 0)
            {
                return InventoryMoveResult.NoStack;
            }

            if (!Rule.HasGrid)
            {
                return InventoryMoveResult.NoGrid;
            }

            var stack = _stacks[at];
            if (rotated && !Rule.AllowRotation && !stack.Rotated)
            {
                return InventoryMoveResult.RotationNotAllowed;
            }

            var width = WidthOf(stack.ItemId, rotated);
            var height = HeightOf(stack.ItemId, rotated);
            if (x < 0 || y < 0 || x + width > Rule.GridWidth || (Rule.HasFixedGridHeight && y + height > Rule.GridHeight))
            {
                return InventoryMoveResult.OutOfGrid;
            }

            return IsFree(_stacks, x, y, width, height, handle) ? InventoryMoveResult.Moved : InventoryMoveResult.Blocked;
        }

        public void Fill()
        {
            _stacks.Clear();
            _nextHandle = 1;
            foreach (var entry in Catalog.Starting)
            {
                if (Catalog.TryFind(entry.ItemId, out var item))
                {
                    Place(_stacks, item, entry.Count, ref _nextHandle);
                }
            }

            Changed?.Invoke();
        }

        public void Clear()
        {
            if (_stacks.Count == 0)
            {
                return;
            }

            _stacks.Clear();
            _nextHandle = 1;
            Changed?.Invoke();
        }

        public void Replace(IEnumerable<InventoryEntry> stacks)
        {
            if (stacks == null)
            {
                throw new ArgumentNullException(nameof(stacks));
            }

            var list = new List<InventoryEntry>();
            var maxHandle = 0;
            foreach (var stack in stacks)
            {
                if (stack.Handle <= 0)
                {
                    throw new ArgumentException("stacks must not contain an empty stack", nameof(stacks));
                }

                foreach (var other in list)
                {
                    if (other.Handle == stack.Handle)
                    {
                        throw new ArgumentException("two stacks share handle " + stack.Handle, nameof(stacks));
                    }
                }

                list.Add(stack);
                maxHandle = Math.Max(maxHandle, stack.Handle);
            }

            if (SameAs(list))
            {
                return;
            }

            _stacks.Clear();
            _stacks.AddRange(list);
            _nextHandle = maxHandle + 1;
            Changed?.Invoke();
        }

        public bool SameAs(IReadOnlyList<InventoryEntry> stacks)
        {
            if (stacks == null || stacks.Count != _stacks.Count)
            {
                return false;
            }

            for (var i = 0; i < stacks.Count; i++)
            {
                if (!stacks[i].Equals(_stacks[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public int WidthOf(InventoryEntry stack) => WidthOf(stack.ItemId, stack.Rotated);

        public int HeightOf(InventoryEntry stack) => HeightOf(stack.ItemId, stack.Rotated);

        private int Place(List<InventoryEntry> stacks, ItemSpec item, int count, ref int nextHandle)
        {
            var remaining = Math.Min(count, Allowed(stacks, item));
            var wanted = remaining;
            for (var i = 0; i < stacks.Count && remaining > 0; i++)
            {
                var stack = stacks[i];
                if (!string.Equals(stack.ItemId, item.Id, StringComparison.Ordinal) || stack.Count >= item.MaxStack)
                {
                    continue;
                }

                var put = Math.Min(remaining, item.MaxStack - stack.Count);
                stacks[i] = stack.WithCount(stack.Count + put);
                remaining -= put;
            }

            while (remaining > 0 && stacks.Count < Rule.StackCapacity)
            {
                if (!TryFindSpot(stacks, item, out var x, out var y, out var rotated))
                {
                    break;
                }

                var handle = NextHandle(stacks, ref nextHandle);
                if (handle == 0)
                {
                    break;
                }

                var put = Math.Min(remaining, item.MaxStack);
                stacks.Add(new InventoryEntry(handle, item.Id, put, x, y, rotated));
                remaining -= put;
            }

            return wanted - remaining;
        }

        private int Allowed(List<InventoryEntry> stacks, ItemSpec item)
        {
            var allowed = int.MaxValue;
            if (item.MaxCarried > 0)
            {
                allowed = Math.Max(0, item.MaxCarried - CountIn(stacks, item.Id));
            }

            if (Rule.MaxWeight > 0f && item.Weight > 0f)
            {
                var spare = Rule.MaxWeight - WeightOf(stacks);
                var byWeight = spare <= 0f ? 0 : (int)Math.Floor((spare + _weightTolerance) / item.Weight);
                allowed = Math.Min(allowed, Math.Max(0, byWeight));
            }

            return allowed;
        }

        private bool TryFindSpot(List<InventoryEntry> stacks, ItemSpec item, out int x, out int y, out bool rotated)
        {
            x = 0;
            y = 0;
            rotated = false;
            if (!Rule.HasGrid)
            {
                return true;
            }

            if (TryFindSpot(stacks, item, false, out x, out y))
            {
                return true;
            }

            if (Rule.AllowRotation && item.Width != item.Height && TryFindSpot(stacks, item, true, out x, out y))
            {
                rotated = true;
                return true;
            }

            return false;
        }

        private bool TryFindSpot(List<InventoryEntry> stacks, ItemSpec item, bool rotated, out int x, out int y)
        {
            var width = item.WidthWhen(rotated);
            var height = item.HeightWhen(rotated);
            x = 0;
            y = 0;
            if (width > Rule.GridWidth)
            {
                return false;
            }

            // 下に広げていけるなら、置いてある山の一番下の次の行は必ず空いている
            var lastY = Rule.HasFixedGridHeight ? Rule.GridHeight - height : Bottom(stacks);
            for (var row = 0; row <= lastY; row++)
            {
                for (var column = 0; column + width <= Rule.GridWidth; column++)
                {
                    if (IsFree(stacks, column, row, width, height, 0))
                    {
                        x = column;
                        y = row;
                        return true;
                    }
                }
            }

            return false;
        }

        private bool IsFree(List<InventoryEntry> stacks, int x, int y, int width, int height, int ignoreHandle)
        {
            foreach (var other in stacks)
            {
                if (other.Handle == ignoreHandle)
                {
                    continue;
                }

                var otherWidth = WidthOf(other);
                var otherHeight = HeightOf(other);
                if (x < other.X + otherWidth && other.X < x + width && y < other.Y + otherHeight && other.Y < y + height)
                {
                    return false;
                }
            }

            return true;
        }

        private int Bottom(List<InventoryEntry> stacks)
        {
            var bottom = 0;
            foreach (var stack in stacks)
            {
                bottom = Math.Max(bottom, stack.Y + HeightOf(stack));
            }

            return bottom;
        }

        private void TakeAt(int index, int amount)
        {
            var stack = _stacks[index];
            if (amount >= stack.Count)
            {
                _stacks.RemoveAt(index);
            }
            else
            {
                _stacks[index] = stack.WithCount(stack.Count - amount);
            }
        }

        // 番号を使い切ったら、今使っていない一番小さい番号を使う。山は StackLimit までなので必ず見つかる
        private static int NextHandle(List<InventoryEntry> stacks, ref int nextHandle)
        {
            if (nextHandle <= MaxHandle)
            {
                return nextHandle++;
            }

            for (var candidate = 1; candidate <= MaxHandle; candidate++)
            {
                var used = false;
                foreach (var stack in stacks)
                {
                    if (stack.Handle == candidate)
                    {
                        used = true;
                        break;
                    }
                }

                if (!used)
                {
                    return candidate;
                }
            }

            return 0;
        }

        private int WidthOf(string itemId, bool rotated)
        {
            return Catalog.TryFind(itemId, out var item) ? item.WidthWhen(rotated) : 1;
        }

        private int HeightOf(string itemId, bool rotated)
        {
            return Catalog.TryFind(itemId, out var item) ? item.HeightWhen(rotated) : 1;
        }

        private float WeightOf(List<InventoryEntry> stacks)
        {
            var total = 0f;
            foreach (var stack in stacks)
            {
                if (Catalog.TryFind(stack.ItemId, out var item))
                {
                    total += item.Weight * stack.Count;
                }
            }

            return total;
        }

        private static int CountIn(List<InventoryEntry> stacks, string itemId)
        {
            var total = 0;
            foreach (var stack in stacks)
            {
                if (string.Equals(stack.ItemId, itemId, StringComparison.Ordinal))
                {
                    total += stack.Count;
                }
            }

            return total;
        }
    }
}
