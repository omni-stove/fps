using System;
using System.Collections.Generic;

namespace Fps.Core
{
    // 消すのは呼ぶ側 (Unity の物を壊す) なので、ここは消す物を選んで一覧から外し、返すだけにする。
    public sealed class DeployRoster<T>
        where T : class
    {
        private readonly List<T> _items = new List<T>();

        public DeployRoster(int maxActive, DeployOverflow overflow)
        {
            Validate(maxActive, overflow);
            MaxActive = maxActive;
            Overflow = overflow;
        }

        public int MaxActive { get; private set; }

        public DeployOverflow Overflow { get; private set; }

        public IReadOnlyList<T> Items => _items;

        public int Count => _items.Count;

        public bool CanPlace => Overflow == DeployOverflow.ReplaceOldest || _items.Count < MaxActive;

        public IReadOnlyList<T> Configure(int maxActive, DeployOverflow overflow)
        {
            Validate(maxActive, overflow);
            MaxActive = maxActive;
            Overflow = overflow;
            return TrimTo(maxActive);
        }

        public IReadOnlyList<T> Add(T item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            if (_items.Contains(item))
            {
                throw new ArgumentException("the item is already in the roster", nameof(item));
            }

            if (!CanPlace)
            {
                throw new InvalidOperationException($"the roster is full ({_items.Count}/{MaxActive}) and refuses new items");
            }

            var evicted = TrimTo(MaxActive - 1);
            _items.Add(item);
            return evicted;
        }

        public bool Remove(T item)
        {
            return item != null && _items.Remove(item);
        }

        public IReadOnlyList<T> Clear()
        {
            var all = _items.ToArray();
            _items.Clear();
            return all;
        }

        private IReadOnlyList<T> TrimTo(int count)
        {
            var excess = _items.Count - count;
            if (excess <= 0)
            {
                return Array.Empty<T>();
            }

            var evicted = _items.GetRange(0, excess).ToArray();
            _items.RemoveRange(0, excess);
            return evicted;
        }

        private static void Validate(int maxActive, DeployOverflow overflow)
        {
            if (maxActive < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxActive), maxActive, "maxActive must be at least 1");
            }

            if (!Enum.IsDefined(typeof(DeployOverflow), overflow))
            {
                throw new ArgumentOutOfRangeException(nameof(overflow), overflow, "overflow is not a defined DeployOverflow");
            }
        }
    }
}
