using System;
using System.Globalization;

namespace Shooter.Protocol
{
    /// <summary>持ち主にだけ送るグレネードの残り (Netcode の <c>NetGrenadeState</c> の中身)。</summary>
    /// <remarks>
    /// 種類ごとの残りを構造体の配列で持たずにクラスにしたのは、代入で配列が共有され、基準のスナップショットまで書き換わらないようにするため。
    /// 読むたびに配列や同じ Id の文字列を作り直さず、前に読んだものを使い回す。
    /// </remarks>
    public sealed class GrenadePouchSync
    {
        public const int MaxTypes = 16;

        private readonly string[] _typeIds = new string[MaxTypes];
        private readonly int[] _remaining = new int[MaxTypes];

        /// <summary>試合のルールで投げられるか。</summary>
        public bool Enabled { get; set; }

        public int Count { get; private set; }

        public string TypeIdAt(int index)
        {
            Check(index);
            return _typeIds[index];
        }

        public int RemainingAt(int index)
        {
            Check(index);
            return _remaining[index];
        }

        public int RemainingOf(string typeId)
        {
            var at = IndexOf(typeId);
            return at < 0 ? 0 : _remaining[at];
        }

        public int TotalRemaining
        {
            get
            {
                var total = 0;
                for (var i = 0; i < Count; i++)
                {
                    total += _remaining[i];
                }

                return total;
            }
        }

        public void Clear()
        {
            Enabled = false;
            Count = 0;
        }

        public void Add(string typeId, int remaining)
        {
            if (remaining < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(remaining), remaining, "remaining must not be negative");
            }

            if (Count == MaxTypes)
            {
                throw new InvalidOperationException("at most " + Text(MaxTypes) + " grenade types fit");
            }

            if (IndexOf(typeId) >= 0)
            {
                throw new ArgumentException("grenade type " + typeId + " is listed twice", nameof(typeId));
            }

            _typeIds[Count] = typeId ?? "";
            _remaining[Count] = remaining;
            Count++;
        }

        public void CopyFrom(GrenadePouchSync other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            Enabled = other.Enabled;
            Array.Copy(other._typeIds, _typeIds, other.Count);
            Array.Copy(other._remaining, _remaining, other.Count);
            Count = other.Count;
        }

        public bool SameAs(GrenadePouchSync other)
        {
            if (other == null || other.Enabled != Enabled || other.Count != Count)
            {
                return false;
            }

            for (var i = 0; i < Count; i++)
            {
                if (_remaining[i] != other._remaining[i] || !string.Equals(_typeIds[i], other._typeIds[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        internal void Write(MessageWriter writer)
        {
            writer.WriteBool(Enabled);
            writer.WriteByte((byte)Count);
            for (var i = 0; i < Count; i++)
            {
                writer.WriteString(_typeIds[i], MatchProtocol.MaxContentIdBytes);
                writer.WriteInt32(_remaining[i]);
            }
        }

        internal void Read(MessageReader reader)
        {
            Count = 0;
            Enabled = reader.ReadBool();
            var count = reader.ReadByteBelow(MaxTypes + 1, "grenade type count");
            for (var i = 0; i < count; i++)
            {
                var typeId = reader.ReadString(MatchProtocol.MaxContentIdBytes, _typeIds[i]);
                for (var j = 0; j < i; j++)
                {
                    if (string.Equals(_typeIds[j], typeId, StringComparison.Ordinal))
                    {
                        throw new MessageReadException("grenade type " + typeId + " is listed twice");
                    }
                }

                _typeIds[i] = typeId;
                _remaining[i] = reader.ReadInt32AtLeast(0, "grenades remaining");
            }

            Count = count;
        }

        private int IndexOf(string typeId)
        {
            var id = typeId ?? "";
            for (var i = 0; i < Count; i++)
            {
                if (string.Equals(_typeIds[i], id, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private void Check(int index)
        {
            if ((uint)index >= (uint)Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "index must be below " + Text(Count));
            }
        }

        private static string Text(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
