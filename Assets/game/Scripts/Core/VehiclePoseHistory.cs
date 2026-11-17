using System;
using System.Numerics;

namespace Shooter.Core
{
    public sealed class VehiclePoseHistory
    {
        public const int DefaultCapacity = 64;

        private readonly int[] _ticks;
        private readonly Vector3[] _positions;
        private readonly Quaternion[] _rotations;
        private int _start;

        public VehiclePoseHistory(int capacity = DefaultCapacity)
        {
            if (capacity < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "capacity must be at least 2");
            }

            _ticks = new int[capacity];
            _positions = new Vector3[capacity];
            _rotations = new Quaternion[capacity];
        }

        public int Count { get; private set; }

        public int NewestTick => Count == 0 ? throw new InvalidOperationException("the history is empty") : _ticks[Index(Count - 1)];

        public void Clear()
        {
            _start = 0;
            Count = 0;
        }

        // 同じ tick をもう一度残すと上書きするのは、同じ tick のうちに直した位置を最後の値にするため。
        public void Record(int tick, Vector3 position, Quaternion rotation)
        {
            if (Count > 0)
            {
                var newest = _ticks[Index(Count - 1)];
                if (tick < newest)
                {
                    throw new ArgumentOutOfRangeException(nameof(tick), tick, "tick must not be older than the newest recorded tick");
                }

                if (tick == newest)
                {
                    _positions[Index(Count - 1)] = position;
                    _rotations[Index(Count - 1)] = rotation;
                    return;
                }
            }

            int slot;
            if (Count < _ticks.Length)
            {
                slot = Index(Count);
                Count++;
            }
            else
            {
                slot = _start;
                _start = (_start + 1) % _ticks.Length;
            }

            _ticks[slot] = tick;
            _positions[slot] = position;
            _rotations[slot] = rotation;
        }

        // 範囲の外は端の値にするのは、巻き戻しの上限より古い射撃も、まだ届いていない tick の表示も、知っている一番近い姿で扱うため。
        public bool TrySample(float tick, out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = Quaternion.Identity;
            if (Count == 0 || float.IsNaN(tick))
            {
                return false;
            }

            var first = Index(0);
            if (tick <= _ticks[first])
            {
                position = _positions[first];
                rotation = _rotations[first];
                return true;
            }

            for (var i = 1; i < Count; i++)
            {
                var after = Index(i);
                if (tick > _ticks[after])
                {
                    continue;
                }

                var before = Index(i - 1);
                var t = (tick - _ticks[before]) / (_ticks[after] - _ticks[before]);
                position = Vector3.Lerp(_positions[before], _positions[after], t);
                rotation = Quaternion.Slerp(_rotations[before], _rotations[after], t);
                return true;
            }

            var last = Index(Count - 1);
            position = _positions[last];
            rotation = _rotations[last];
            return true;
        }

        private int Index(int offset)
        {
            return (_start + offset) % _ticks.Length;
        }
    }
}
