using System.Collections.Generic;

namespace Fps.Core.Navigation
{
    internal sealed class MinHeap
    {
        private readonly List<(float Key, int Value)> _items = new List<(float Key, int Value)>();

        public void Clear()
        {
            _items.Clear();
        }

        public void Push(float key, int value)
        {
            _items.Add((key, value));
            var i = _items.Count - 1;
            while (i > 0)
            {
                var parent = (i - 1) / 2;
                if (_items[parent].Key <= key)
                {
                    break;
                }

                _items[i] = _items[parent];
                i = parent;
            }

            _items[i] = (key, value);
        }

        public bool TryPop(out float key, out int value)
        {
            if (_items.Count == 0)
            {
                key = 0f;
                value = -1;
                return false;
            }

            (key, value) = _items[0];
            var lastIndex = _items.Count - 1;
            var last = _items[lastIndex];
            _items.RemoveAt(lastIndex);
            if (lastIndex == 0)
            {
                return true;
            }

            var i = 0;
            while (true)
            {
                var child = i * 2 + 1;
                if (child >= lastIndex)
                {
                    break;
                }

                if (child + 1 < lastIndex && _items[child + 1].Key < _items[child].Key)
                {
                    child++;
                }

                if (_items[child].Key >= last.Key)
                {
                    break;
                }

                _items[i] = _items[child];
                i = child;
            }

            _items[i] = last;
            return true;
        }
    }
}
