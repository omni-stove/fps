using System;

namespace Shooter.Core
{
    // 位置 (HitboxHistory) と違って補間しない。しゃがみはある tick で切り替わるもので、
    // 途中の高さのカプセルはサーバーにもクライアントにも存在しないため。
    public sealed class StanceHistory
    {
        private readonly int[] _ticks;
        private readonly bool[] _crouched;
        private int _start;

        public int Capacity => _ticks.Length;

        public int Count { get; private set; }

        public StanceHistory(int capacity = HitboxHistory.DefaultCapacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "capacity must be positive");
            }

            _ticks = new int[capacity];
            _crouched = new bool[capacity];
        }

        public void Record(int tick, bool crouched)
        {
            if (Count > 0)
            {
                var newest = IndexOf(Count - 1);
                if (tick < _ticks[newest])
                {
                    throw new ArgumentOutOfRangeException(nameof(tick), tick, "tick must not be older than the newest record");
                }

                if (tick == _ticks[newest])
                {
                    _crouched[newest] = crouched;
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
            _crouched[index] = crouched;
        }

        public bool TrySample(float tick, out bool crouched)
        {
            crouched = false;
            if (Count == 0 || float.IsNaN(tick))
            {
                return false;
            }

            if (tick < _ticks[IndexOf(0)] || tick > _ticks[IndexOf(Count - 1)])
            {
                return false;
            }

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

            crouched = _crouched[IndexOf(lo)];
            return true;
        }

        private int IndexOf(int logical)
        {
            return (_start + logical) % Capacity;
        }
    }
}
