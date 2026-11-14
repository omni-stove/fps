using System;
using Fps.Protocol;

namespace Fps.NetV2
{
    /// <summary>
    /// 組み立て終えたスナップショットを tick ごとに取っておき、次に届く差分の基準にする。
    /// サーバーが基準に選ぶのは、こちらが入力で知らせた最新の tick から、サーバーが覚えている数 (32) までなので、それより多く取っておく。
    /// </summary>
    /// <remarks>
    /// 毎 tick 届くので、入れ物は tick の剰余の位置に作り置いて使い回す (GC を起こさないため)。
    /// </remarks>
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

        /// <summary>組み立てられた中で一番新しいスナップショット。まだ無ければ null。</summary>
        public SnapshotMessage Latest => _latest;

        /// <summary>入力で知らせる、組み立てられた最新の tick。まだ無ければ <see cref="InputMessage.NoSnapshot"/>。</summary>
        public int LatestTick => _latest != null ? _latest.Tick : InputMessage.NoSnapshot;

        /// <summary>差分の基準が見つからず組み立てられなかった数。</summary>
        public int Unresolved { get; private set; }

        /// <summary>
        /// 読んだばかりのスナップショットを基準で埋めて取っておく。received は表 (<see cref="MessageTable"/>) が使い回す入れ物でよい。
        /// </summary>
        /// <returns>
        /// これまでで一番新しい tick として取っておけたら、その写し (<see cref="Latest"/>)。
        /// 基準が無くて組み立てられない、または前に届いたものより古いなら null (古いものも基準としては取っておく)。
        /// </returns>
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
