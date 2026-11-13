using System;

namespace Fps.Core
{
    /// <summary>
    /// エンティティ 1 体分のしゃがみの履歴。tick ごとのしゃがみを固定長のリングバッファに記録し、
    /// 小数の tick を渡すとその tick 以下で最も新しい記録を返す。容量を超えたら古い記録から上書きする。
    /// </summary>
    /// <remarks>
    /// 位置 (<see cref="HitboxHistory"/>) と違って補間しない。しゃがみはある tick で切り替わるもので、
    /// 途中の高さのカプセルはサーバーにもクライアントにも存在しないため。
    /// </remarks>
    public sealed class StanceHistory
    {
        private readonly int[] _ticks;
        private readonly bool[] _crouched;
        private int _start;

        /// <summary>保持できる記録の数。</summary>
        public int Capacity => _ticks.Length;

        /// <summary>今保持している記録の数。</summary>
        public int Count { get; private set; }

        /// <param name="capacity">保持する記録の数。1 以上。</param>
        public StanceHistory(int capacity = HitboxHistory.DefaultCapacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "capacity must be positive");
            }

            _ticks = new int[capacity];
            _crouched = new bool[capacity];
        }

        /// <summary>tick のしゃがみを記録する。tick は前回以上でなければならない。前回と同じ tick なら上書きする。</summary>
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

        /// <summary>
        /// tick 時点のしゃがみ。tick 以下で最も新しい記録の値を返す。
        /// 最も古い記録より前・最も新しい記録より後・記録が無い・NaN のときは false を返す (<see cref="HitboxHistory.TrySample"/> と同じ範囲)。
        /// </summary>
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
