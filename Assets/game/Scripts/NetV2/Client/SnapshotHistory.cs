using System;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    // サーバーが差分の基準に選ぶのは、こちらが入力で知らせた最新の tick から、サーバーが覚えている数 (32) までなので、それより多く取っておく。
    // 毎 tick 届くので、GC を起こさないよう、入れ物は tick の剰余の位置に作り置いて使い回す。
    public sealed class SnapshotHistory
    {
        public const int DefaultCapacity = 64;

        private readonly SnapshotMessage[] _slots;
        private readonly bool[] _filled;
        private SnapshotMessage _latest;

        public SnapshotHistory(int capacity = DefaultCapacity)
        {
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "capacity must be at least 1");
            }

            _slots = new SnapshotMessage[capacity];
            _filled = new bool[capacity];
        }

        public int Capacity => _slots.Length;

        public SnapshotMessage Latest => _latest;

        public int LatestTick => _latest != null ? _latest.Tick : InputMessage.NoSnapshot;

        public int Unresolved { get; private set; }

        // 前に届いたものより古いスナップショットも、後の差分の基準になり得るので取っておく。
        public SnapshotMessage Add(SnapshotMessage received)
        {
            if (received == null)
            {
                throw new ArgumentNullException(nameof(received));
            }

            // 覚えている幅より古いものを入れると、同じ位置の新しいものを潰してしまう
            if (_latest != null && received.Tick <= _latest.Tick - Capacity)
            {
                return null;
            }

            if (!received.IsComplete)
            {
                TryGet(received.BaselineTick, out var baseline);
                if (!received.TryResolve(baseline))
                {
                    Unresolved++;
                    return null;
                }
            }

            var slot = received.Tick % Capacity;
            var kept = _slots[slot] ??= new SnapshotMessage();
            kept.CopyFrom(received);
            _filled[slot] = true;
            if (_latest != null && kept.Tick <= _latest.Tick)
            {
                return null;
            }

            _latest = kept;
            return kept;
        }

        public bool TryGet(int tick, out SnapshotMessage snapshot)
        {
            snapshot = null;
            if (tick < 0)
            {
                return false;
            }

            var slot = tick % Capacity;
            if (!_filled[slot] || _slots[slot].Tick != tick)
            {
                return false;
            }

            snapshot = _slots[slot];
            return true;
        }
    }
}
