using System;
using System.Numerics;

namespace Fps.NetV2
{
    /// <summary>
    /// 他のプレイヤー 1 人の、tick ごとの位置・向き・しゃがみの記録。表示する tick (<see cref="InterpolationClock.RenderTick"/>) の前後から補間する。
    /// </summary>
    public sealed class RemoteTrack
    {
        /// <summary>60Hz で 1 秒分。表示は数 tick 遅れなので足りる。</summary>
        public const int DefaultCapacity = 64;

        private readonly int[] _ticks;
        private readonly RemotePose[] _poses;
        private int _start;
        private int _count;

        public RemoteTrack(int capacity = DefaultCapacity)
        {
            if (capacity < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "capacity must be at least 2");
            }

            _ticks = new int[capacity];
            _poses = new RemotePose[capacity];
        }

        public int Count => _count;

        /// <summary>記録の一番新しい tick。無ければ -1。</summary>
        public int NewestTick => _count > 0 ? _ticks[IndexOf(_count - 1)] : -1;

        /// <summary>tick の姿を足す。記録より古い tick・同じ tick は捨てる (順が入れ替わって届いたもの)。</summary>
        public void Record(int tick, RemotePose pose)
        {
            if (_count > 0 && tick <= NewestTick)
            {
                return;
            }

            if (_count == _ticks.Length)
            {
                _start = (_start + 1) % _ticks.Length;
                _count--;
            }

            var index = IndexOf(_count);
            _ticks[index] = tick;
            _poses[index] = pose;
            _count++;
        }

        /// <summary>記録を捨てる (置き直されて、前の位置から補間すると滑って見えるとき)。</summary>
        public void Clear()
        {
            _start = 0;
            _count = 0;
        }

        /// <summary>
        /// tick の姿を前後の記録から補間する。記録より前なら一番古い姿、後なら一番新しい姿 (先を読まない) を返す。
        /// しゃがみは切り替わらないので、前の記録の値を使う。
        /// </summary>
        /// <returns>記録が無ければ false。</returns>
        public bool TrySample(float tick, out RemotePose pose)
        {
            pose = default;
            if (_count == 0)
            {
                return false;
            }

            if (tick <= _ticks[IndexOf(0)])
            {
                pose = _poses[IndexOf(0)];
                return true;
            }

            for (var i = _count - 1; i >= 0; i--)
            {
                var index = IndexOf(i);
                if (_ticks[index] > tick)
                {
                    continue;
                }

                var before = _poses[index];
                if (i == _count - 1)
                {
                    pose = before;
                    return true;
                }

                var nextIndex = IndexOf(i + 1);
                var t = (tick - _ticks[index]) / (_ticks[nextIndex] - _ticks[index]);
                var after = _poses[nextIndex];
                pose = new RemotePose(
                    Vector3.Lerp(before.Position, after.Position, t),
                    SnapshotMotion.LerpYaw(before.Yaw, after.Yaw, t),
                    before.IsCrouched);
                return true;
            }

            pose = _poses[IndexOf(0)];
            return true;
        }

        private int IndexOf(int logical)
        {
            return (_start + logical) % _ticks.Length;
        }
    }
}
