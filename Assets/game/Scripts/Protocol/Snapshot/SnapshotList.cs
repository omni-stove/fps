using System;
using System.Globalization;

namespace Shooter.Protocol
{
    // 読むたびに作り直すと GC を起こすので、確保した配列を使い回し、要素は ref で直接書き換える。
    public sealed class SnapshotList<T>
        where T : struct, ISnapshotEntity<T>
    {
        private readonly string _what;

        // 読むたびに文字列をつなぐと GC を起こすので、読み損ねたときに出す名前は先に作っておく。
        private readonly string _countName;
        private readonly string _idName;
        private T[] _items = Array.Empty<T>();

        public SnapshotList(int maxCount, string what)
        {
            if (maxCount < 0 || maxCount > byte.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCount), maxCount, "max count must be 0-255");
            }

            MaxCount = maxCount;
            _what = what ?? typeof(T).Name;
            _countName = _what + " count";
            _idName = _what + " id";
        }

        public int MaxCount { get; }

        public int Count { get; private set; }

        public ref T this[int index]
        {
            get
            {
                if ((uint)index >= (uint)Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index), index, "index must be below " + Count);
                }

                return ref _items[index];
            }
        }

        public void Clear()
        {
            Count = 0;
        }

        public ref T Add()
        {
            if (Count == MaxCount)
            {
                throw new InvalidOperationException("at most " + MaxCount + " " + _what + " entries fit in a snapshot");
            }

            Grow(Count + 1);
            Count++;
            _items[Count - 1] = default;
            return ref _items[Count - 1];
        }

        public int IndexOf(int id)
        {
            for (var i = 0; i < Count; i++)
            {
                if (_items[i].EntityId == id)
                {
                    return i;
                }
            }

            return -1;
        }

        public void CopyFrom(SnapshotList<T> other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            Grow(other.Count);
            Array.Copy(other._items, _items, other.Count);
            Count = other.Count;
        }

        internal bool IsComplete
        {
            get
            {
                for (var i = 0; i < Count; i++)
                {
                    if (_items[i].PresentBits != _items[i].AllBits)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        internal void Write(MessageWriter writer, SnapshotList<T> baseline)
        {
            writer.WriteByte((byte)Count);
            for (var i = 0; i < Count; i++)
            {
                ref var item = ref _items[i];
                for (var j = 0; j < i; j++)
                {
                    if (_items[j].EntityId == item.EntityId)
                    {
                        throw new InvalidOperationException(
                            "the snapshot lists " + _what + " " + item.EntityId.ToString(CultureInfo.InvariantCulture) + " twice");
                    }
                }

                var bits = item.AllBits;
                if (baseline != null)
                {
                    var at = baseline.IndexAt(i, item.EntityId);
                    if (at >= 0)
                    {
                        bits = item.ChangedBits(in baseline._items[at]);
                    }
                }

                writer.WriteInt32(item.EntityId);
                writer.WriteUInt16((ushort)bits);
                item.WriteBits(writer, bits);
            }
        }

        internal void Read(MessageReader reader, bool hasBaseline)
        {
            Count = 0;
            var count = reader.ReadByteBelow(MaxCount + 1, _countName);
            Grow(count);
            for (var i = 0; i < count; i++)
            {
                var id = reader.ReadInt32AtLeast(0, _idName);
                for (var j = 0; j < i; j++)
                {
                    if (_items[j].EntityId == id)
                    {
                        throw new MessageReadException(
                            "the snapshot lists " + _what + " " + id.ToString(CultureInfo.InvariantCulture) + " twice");
                    }
                }

                // 前に同じ位置にあった中身を残したまま読むのは、変わっていない Id の文字列を作り直さないため。
                ref var item = ref _items[i];
                var all = item.AllBits;
                var bits = reader.ReadUInt16();
                if ((bits & ~all) != 0 || (!hasBaseline && bits != all))
                {
                    throw new MessageReadException(
                        _what + " " + id.ToString(CultureInfo.InvariantCulture) + " has unexpected parts "
                        + bits.ToString(CultureInfo.InvariantCulture));
                }

                item.ReadBits(reader, id, bits);
                Count = i + 1;
            }
        }

        internal bool CanFillFrom(SnapshotList<T> baseline)
        {
            for (var i = 0; i < Count; i++)
            {
                if (_items[i].PresentBits != _items[i].AllBits && baseline.IndexAt(i, _items[i].EntityId) < 0)
                {
                    return false;
                }
            }

            return true;
        }

        internal void FillFrom(SnapshotList<T> baseline)
        {
            for (var i = 0; i < Count; i++)
            {
                ref var item = ref _items[i];
                if (item.PresentBits != item.AllBits)
                {
                    item.FillFrom(in baseline._items[baseline.IndexAt(i, item.EntityId)]);
                }
            }
        }

        // 顔ぶれが変わらなければ同じ位置にあるので、先にそこを見る。
        private int IndexAt(int hint, int id)
        {
            if (hint < Count && _items[hint].EntityId == id)
            {
                return hint;
            }

            return IndexOf(id);
        }

        private void Grow(int count)
        {
            if (count > _items.Length)
            {
                Array.Resize(ref _items, Math.Max(count, Math.Min(MaxCount, Math.Max(4, _items.Length * 2))));
            }
        }
    }
}
