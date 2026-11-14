using System;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// エンティティ 1 体分の位置の履歴。tick ごとの位置を固定長のリングバッファに記録し、
    /// 小数の tick を渡すと前後の記録から線形補間した位置を返す。容量を超えたら古い記録から上書きする。
    /// </summary>
    public sealed class HitboxHistory
    {
        /// <summary>既定の容量。60Hz で約 1 秒分。</summary>
        public const int DefaultCapacity = 64;

        private readonly int[] _ticks;
        private readonly Vector3[] _positions;
        private int _start;

        /// <summary>保持できる記録の数。</summary>
        public int Capacity => _ticks.Length;

        /// <summary>今保持している記録の数。</summary>
        public int Count { get; private set; }

        /// <param name="capacity">保持する記録の数。1 以上。tick が飛んで記録された場合も 1 件と数える。</param>
        public HitboxHistory(int capacity = DefaultCapacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "capacity must be positive");
            }

            _ticks = new int[capacity];
            _positions = new Vector3[capacity];
        }

        /// <summary>
        /// tick の位置を記録する。tick は前回以上でなければならない。前回と同じ tick なら上書きする。
        /// tick が飛んでもよく、間は前後の記録から補間される。
        /// </summary>
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

        /// <summary>
        /// tick 時点の位置を返す。記録がちょうどその tick にあればその値、間なら前後の記録から線形補間した値。
        /// 最も古い記録より前・最も新しい記録より後・記録が無い・NaN のときは false。
        /// </summary>
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

            // tick 以下で最も新しい記録を二分探索で探す。範囲内なので必ず見つかる。
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

        /// <summary>最も古い記録と最も新しい記録の tick。記録が無ければ false。</summary>
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
