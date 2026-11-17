using System;
using System.Numerics;

namespace Shooter.Core
{
    public sealed class HitboxHistory
    {
        // 60Hz で約 1 秒分を持てる大きさにしてある。
        public const int DefaultCapacity = 64;

        private readonly int[] _ticks;
        private readonly Vector3[] _positions;
        private int _start;

        public int Capacity => _ticks.Length;

        public int Count { get; private set; }

        public HitboxHistory(int capacity = DefaultCapacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "capacity must be positive");
            }

            _ticks = new int[capacity];
            _positions = new Vector3[capacity];
        }

        public void Record(int tick, Vector3 position)
        {
            if (Count > 0)
            {
                var newest = _ticks[IndexOf(Count - 1)];
                if (tick < newest)
                {
                    throw new ArgumentOutOfRangeException(nameof(tick), tick, "tick must not be older than the newest record");
                }

                if (tick == newest)
                {
                    _positions[IndexOf(Count - 1)] = position;
                    return;
                }
            }

            int index;
            if (Count < Capacity)
            {
                index = IndexOf(Count);
                Count++;
            }
            else
            {
                index = _start;
                _start = (_start + 1) % Capacity;
            }

            _ticks[index] = tick;
            _positions[index] = position;
        }

        public bool TrySample(float tick, out Vector3 position)
        {
            position = default;
            if (Count == 0 || float.IsNaN(tick))
            {
                return false;
            }

            if (tick < _ticks[IndexOf(0)] || tick > _ticks[IndexOf(Count - 1)])
            {
                return false;
            }

            // 直前で範囲内と確かめてあり必ず見つかるので、見つからない場合は扱わない。
            var lo = 0;
            var hi = Count - 1;
            while (lo < hi)
            {
                var mid = (lo + hi + 1) / 2;
                if (_ticks[IndexOf(mid)] <= tick)
                {
                    lo = mid;
                }
                else
                {
                    hi = mid - 1;
                }
            }

            var before = IndexOf(lo);
            if (_ticks[before] == tick || lo == Count - 1)
            {
                position = _positions[before];
                return true;
            }

            var after = IndexOf(lo + 1);
            var t = (float)((tick - (double)_ticks[before]) / ((double)_ticks[after] - _ticks[before]));
            position = Vector3.Lerp(_positions[before], _positions[after], t);
            return true;
        }

        public bool TryGetRange(out int oldestTick, out int newestTick)
        {
            if (Count == 0)
            {
                oldestTick = 0;
                newestTick = 0;
                return false;
            }

            oldestTick = _ticks[IndexOf(0)];
            newestTick = _ticks[IndexOf(Count - 1)];
            return true;
        }

        private int IndexOf(int logical)
        {
            return (_start + logical) % Capacity;
        }
    }
}
