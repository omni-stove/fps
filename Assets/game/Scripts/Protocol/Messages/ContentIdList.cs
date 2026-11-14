using System;

namespace Fps.Protocol
{
    /// <summary>
    /// 武器などの Id の並び。読むたびに配列や同じ Id の文字列を作り直さず、前に読んだものを使い回す。
    /// </summary>
    public sealed class ContentIdList
    {
        private readonly string _what;

        // 読むたびに文字列をつなぐと GC を起こすので、読み損ねたときに出す名前は先に作っておく。
        private readonly string _countName;
        private string[] _ids = Array.Empty<string>();

        public ContentIdList(int maxCount, string what)
        {
            if (maxCount < 0 || maxCount > byte.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCount), maxCount, "max count must be 0-255");
            }

            MaxCount = maxCount;
            _what = what ?? "id";
            _countName = _what + " count";
        }

        public int MaxCount { get; }

        public int Count { get; private set; }

        public string this[int index]
        {
            get
            {
                if ((uint)index >= (uint)Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index), index, "index must be below " + Count);
                }

                return _ids[index];
            }
        }

        public void Clear()
        {
            Count = 0;
        }

        /// <exception cref="InvalidOperationException">もう <see cref="MaxCount"/> 個ある。</exception>
        public void Add(string id)
        {
            if (Count == MaxCount)
            {
                throw new InvalidOperationException("at most " + MaxCount + " " + _what + " ids fit");
            }

            Grow(Count + 1);
            _ids[Count] = id ?? "";
            Count++;
        }

        public void CopyFrom(ContentIdList other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            Grow(other.Count);
            Array.Copy(other._ids, _ids, other.Count);
            Count = other.Count;
        }

        public bool SameAs(ContentIdList other)
        {
            if (other == null || other.Count != Count)
            {
                return false;
            }

            for (var i = 0; i < Count; i++)
            {
                if (!string.Equals(_ids[i], other._ids[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        internal void Write(MessageWriter writer)
        {
            writer.WriteByte((byte)Count);
            for (var i = 0; i < Count; i++)
            {
                writer.WriteString(_ids[i], MatchProtocol.MaxContentIdBytes);
            }
        }

        internal void Read(MessageReader reader)
        {
            Count = 0;
            var count = reader.ReadByteBelow(MaxCount + 1, _countName);
            Grow(count);
            for (var i = 0; i < count; i++)
            {
                _ids[i] = reader.ReadString(MatchProtocol.MaxContentIdBytes, _ids[i]);
            }

            Count = count;
        }

        private void Grow(int count)
        {
            if (count > _ids.Length)
            {
                Array.Resize(ref _ids, Math.Max(count, Math.Min(MaxCount, Math.Max(4, _ids.Length * 2))));
            }
        }
    }
}
