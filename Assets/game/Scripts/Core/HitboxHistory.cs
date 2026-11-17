using System;
using System.Numerics;

namespace Shooter.Core
{
    // 傾きを量と向きの角度ではなく目の横のずれ (LeanStep.EyeOffset) を世界の向きで持つのは、
    // 向きの角度を補間すると -π と π の境で逆に回るが、ずれのベクトルなら位置と同じく線形に補間できるため。
    public sealed class HitboxHistory
    {
        // 60Hz で約 1 秒分を持てる大きさにしてある。
        public const int DefaultCapacity = 64;

        private readonly int[] _ticks;
        private readonly Vector3[] _positions;
        private readonly Vector3[] _leans;
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
            _leans = new Vector3[capacity];
        }

        public void Record(int tick, Vector3 position)
        {
            Record(tick, position, Vector3.Zero);
        }

        /// <summary>
        /// tick の位置とリーンの傾き (目の横のずれ。傾けていなければ zero) を記録する。tick の決まりは <see cref="Record(int, Vector3)"/> と同じ。
        /// </summary>
        public void Record(int tick, Vector3 position, Vector3 lean)
        {
            if (!IsFinite(lean))
            {
                throw new ArgumentOutOfRangeException(nameof(lean), lean, "lean must be finite");
            }

            if (Count > 0)
            {
                var newest = IndexOf(Count - 1);
                if (tick < _ticks[newest])
                {
                    throw new ArgumentOutOfRangeException(nameof(tick), tick, "tick must not be older than the newest record");
                }

                if (tick == _ticks[newest])
                {
                    _positions[newest] = position;
                    _leans[newest] = lean;
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
            _leans[index] = lean;
        }

        public bool TrySample(float tick, out Vector3 position)
        {
            return TrySample(tick, out position, out _);
        }

        /// <summary>tick 時点の位置とリーンの傾き。補間のしかたと false を返すときは <see cref="TrySample(float, out Vector3)"/> と同じ。</summary>
        public bool TrySample(float tick, out Vector3 position, out Vector3 lean)
        {
            position = default;
            lean = default;
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
                lean = _leans[before];
                return true;
            }

            var after = IndexOf(lo + 1);
            var t = (float)((tick - (double)_ticks[before]) / ((double)_ticks[after] - _ticks[before]));
            position = Vector3.Lerp(_positions[before], _positions[after], t);
            lean = Vector3.Lerp(_leans[before], _leans[after], t);
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

        private static bool IsFinite(Vector3 v)
        {
            return !float.IsNaN(v.X) && !float.IsInfinity(v.X)
                && !float.IsNaN(v.Y) && !float.IsInfinity(v.Y)
                && !float.IsNaN(v.Z) && !float.IsInfinity(v.Z);
        }
    }
}
