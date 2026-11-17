using System;
using System.Numerics;

namespace Shooter.Core
{
    /// <summary>
    /// エンティティ 1 体分の位置とリーンの傾きの履歴。tick ごとの値を固定長のリングバッファに記録し、
    /// 小数の tick を渡すと前後の記録から線形補間した値を返す。容量を超えたら古い記録から上書きする。
    /// </summary>
    /// <remarks>
    /// 傾きは量と向きの角度ではなく、目の横のずれ (<see cref="LeanStep.EyeOffset(float, float, MotorSpec)"/>) を世界の向きで持つ。
    /// 向きの角度を補間すると -π と π の境で逆に回るが、ずれのベクトルなら位置と同じく線形に補間できるため。
    /// </remarks>
    public sealed class HitboxHistory
    {
        /// <summary>既定の容量。60Hz で約 1 秒分。</summary>
        public const int DefaultCapacity = 64;

        private readonly int[] _ticks;
        private readonly Vector3[] _positions;
        private readonly Vector3[] _leans;
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
            _leans = new Vector3[capacity];
        }

        /// <summary>
        /// tick の位置を、傾けていないものとして記録する。tick は前回以上でなければならない。前回と同じ tick なら上書きする。
        /// tick が飛んでもよく、間は前後の記録から補間される。
        /// </summary>
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

        /// <summary>
        /// tick 時点の位置を返す。記録がちょうどその tick にあればその値、間なら前後の記録から線形補間した値。
        /// 最も古い記録より前・最も新しい記録より後・記録が無い・NaN のときは false。
        /// </summary>
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
                lean = _leans[before];
                return true;
            }

            var after = IndexOf(lo + 1);
            var t = (float)((tick - (double)_ticks[before]) / ((double)_ticks[after] - _ticks[before]));
            position = Vector3.Lerp(_positions[before], _positions[after], t);
            lean = Vector3.Lerp(_leans[before], _leans[after], t);
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

        private static bool IsFinite(Vector3 v)
        {
            return !float.IsNaN(v.X) && !float.IsInfinity(v.X)
                && !float.IsNaN(v.Y) && !float.IsInfinity(v.Y)
                && !float.IsNaN(v.Z) && !float.IsInfinity(v.Z);
        }
    }
}
