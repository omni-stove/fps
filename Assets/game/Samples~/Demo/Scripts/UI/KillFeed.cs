using System;
using System.Collections.Generic;

namespace Shooter.Runtime
{
    public sealed class KillFeed
    {
        public const int DefaultCapacity = 5;

        public const float DefaultLineSeconds = 5f;

        private readonly List<KillFeedLine> _lines = new List<KillFeedLine>();
        private readonly List<float> _remaining = new List<float>();

        public event Action Changed;

        public KillFeed(int capacity = DefaultCapacity, float lineSeconds = DefaultLineSeconds)
        {
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "capacity must be at least 1");
            }

            if (!(lineSeconds > 0f) || float.IsInfinity(lineSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(lineSeconds), lineSeconds, "lineSeconds must be a positive finite number");
            }

            Capacity = capacity;
            LineSeconds = lineSeconds;
            Lines = _lines.AsReadOnly();
        }

        public int Capacity { get; }

        public float LineSeconds { get; }

        public IReadOnlyList<KillFeedLine> Lines { get; }

        public int Count => _lines.Count;

        public float RemainingSeconds(int index)
        {
            if (index < 0 || index >= _remaining.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "index must be within the lines");
            }

            return _remaining[index];
        }

        public void Add(KillFeedLine line)
        {
            while (_lines.Count >= Capacity)
            {
                RemoveOldest();
            }

            _lines.Add(line);
            _remaining.Add(LineSeconds);
            Changed?.Invoke();
        }

        public void Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            if (_lines.Count == 0 || deltaSeconds == 0f)
            {
                return;
            }

            var removed = false;
            for (var i = _remaining.Count - 1; i >= 0; i--)
            {
                var left = _remaining[i] - deltaSeconds;
                if (left > 0f)
                {
                    _remaining[i] = left;
                    continue;
                }

                _remaining.RemoveAt(i);
                _lines.RemoveAt(i);
                removed = true;
            }

            if (removed)
            {
                Changed?.Invoke();
            }
        }

        public void Clear()
        {
            if (_lines.Count == 0)
            {
                return;
            }

            _lines.Clear();
            _remaining.Clear();
            Changed?.Invoke();
        }

        private void RemoveOldest()
        {
            _lines.RemoveAt(0);
            _remaining.RemoveAt(0);
        }
    }
}
