using System;

namespace Shooter.Protocol
{
    // 置き場所や大きさを送らないのは、持ち物庫は並べて数を見せるだけで、格子に並べ直すのは出撃中の持ち物 (InventoryStateMessage) だけだから。
    public sealed class StashItemList
    {
        public const int MaxCount = byte.MaxValue;

        private readonly string _what;

        // 読むたびに文字列をつなぐと GC を起こすので、読み損ねたときに出す名前は先に作っておく。
        private readonly string _countName;
        private readonly string _itemName;
        private readonly string _amountName;
        private string[] _ids = Array.Empty<string>();
        private ushort[] _counts = Array.Empty<ushort>();

        public StashItemList(string what)
        {
            _what = what ?? "stash";
            _countName = _what + " item count";
            _itemName = _what + " item";
            _amountName = _what + " item amount";
        }

        public int Count { get; private set; }

        public string IdAt(int index)
        {
            Check(index);
            return _ids[index];
        }

        public int CountAt(int index)
        {
            Check(index);
            return _counts[index];
        }

        public void Clear()
        {
            Count = 0;
        }

        public void Add(string itemId, int count)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                throw new ArgumentException("a " + _what + " item needs an id", nameof(itemId));
            }

            if (count < 1 || count > ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "a " + _what + " item count must be 1 to " + ushort.MaxValue);
            }

            if (Count == MaxCount)
            {
                throw new InvalidOperationException("at most " + MaxCount + " " + _what + " items fit");
            }

            Grow(Count + 1);
            _ids[Count] = itemId;
            _counts[Count] = (ushort)count;
            Count++;
        }

        public void CopyFrom(StashItemList other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            Grow(other.Count);
            Array.Copy(other._ids, _ids, other.Count);
            Array.Copy(other._counts, _counts, other.Count);
            Count = other.Count;
        }

        internal void Write(MessageWriter writer)
        {
            writer.WriteByte((byte)Count);
            for (var i = 0; i < Count; i++)
            {
                writer.WriteString(_ids[i], MatchProtocol.MaxContentIdBytes);
                writer.WriteUInt16(_counts[i]);
            }
        }

        internal void Read(MessageReader reader)
        {
            Count = 0;
            var count = reader.ReadByteBelow(MaxCount + 1, _countName);
            Grow(count);
            for (var i = 0; i < count; i++)
            {
                var id = reader.ReadString(MatchProtocol.MaxContentIdBytes, _ids[i]);
                if (id.Length == 0)
                {
                    throw new MessageReadException("a " + _itemName + " carries no id");
                }

                var amount = reader.ReadUInt16();
                if (amount == 0)
                {
                    throw new MessageReadException(_amountName + " must be at least 1");
                }

                _ids[i] = id;
                _counts[i] = amount;
            }

            Count = count;
        }

        private void Check(int index)
        {
            if ((uint)index >= (uint)Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "index must be below " + Count);
            }
        }

        private void Grow(int count)
        {
            if (count > _ids.Length)
            {
                var size = Math.Max(count, Math.Min(MaxCount, Math.Max(4, _ids.Length * 2)));
                Array.Resize(ref _ids, size);
                Array.Resize(ref _counts, size);
            }
        }
    }
}
