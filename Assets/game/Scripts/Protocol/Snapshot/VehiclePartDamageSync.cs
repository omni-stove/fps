using System;
using System.Globalization;

namespace Shooter.Protocol
{
    // 構造体の VehicleSnapshot に載せるので、代入で差分の基準と中身を共有しても基準が書き換わらないよう、作った後は変えない。
    // 残りでなく受けた量で持つのは、サーバーが部位の耐久 (機体のアセットの Damage の欄) を持たず、残りは運転する人の側が自分の耐久から出すため。
    public sealed class VehiclePartDamageSync
    {
        public const int MaxParts = 16;

        private readonly string[] _partIds;
        private readonly int[] _taken;

        private VehiclePartDamageSync(string[] partIds, int[] taken)
        {
            _partIds = partIds;
            _taken = taken;
        }

        public static VehiclePartDamageSync None { get; } = new VehiclePartDamageSync(Array.Empty<string>(), Array.Empty<int>());

        public int Count => _partIds.Length;

        public bool IsFull => Count == MaxParts;

        public string PartIdAt(int index)
        {
            Check(index);
            return _partIds[index];
        }

        public int TakenAt(int index)
        {
            Check(index);
            return _taken[index];
        }

        // 撃たれていない部位は並びに無いので 0。
        public int TakenOf(string partId)
        {
            var at = IndexOf(partId);
            return at < 0 ? 0 : _taken[at];
        }

        public bool Contains(string partId)
        {
            return IndexOf(partId) >= 0;
        }

        // 足した後のものを新しく返す。並びが満ちていて新しい部位なら足さずに false を返す。
        public bool TryAdd(string partId, int damage, out VehiclePartDamageSync added)
        {
            if (string.IsNullOrEmpty(partId))
            {
                throw new ArgumentException("part id must not be empty", nameof(partId));
            }

            if (damage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(damage), damage, "damage must not be negative");
            }

            var at = IndexOf(partId);
            if (at < 0 && IsFull)
            {
                added = this;
                return false;
            }

            if (damage == 0 && at >= 0)
            {
                added = this;
                return true;
            }

            var count = at < 0 ? Count + 1 : Count;
            var ids = new string[count];
            var taken = new int[count];
            Array.Copy(_partIds, ids, Count);
            Array.Copy(_taken, taken, Count);
            if (at < 0)
            {
                at = Count;
                ids[at] = partId;
            }

            taken[at] = (int)Math.Min(int.MaxValue, (long)taken[at] + damage);
            added = new VehiclePartDamageSync(ids, taken);
            return true;
        }

        public bool SameAs(VehiclePartDamageSync other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (other == null || other.Count != Count)
            {
                return false;
            }

            for (var i = 0; i < Count; i++)
            {
                if (_taken[i] != other._taken[i] || !string.Equals(_partIds[i], other._partIds[i], StringComparison.Ordinal))
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
                writer.WriteString(_partIds[i], MatchProtocol.MaxContentIdBytes);
                writer.WriteInt32(_taken[i]);
            }
        }

        internal static VehiclePartDamageSync Read(MessageReader reader)
        {
            var count = reader.ReadByteBelow(MaxParts + 1, "vehicle part count");
            if (count == 0)
            {
                return None;
            }

            var ids = new string[count];
            var taken = new int[count];
            for (var i = 0; i < count; i++)
            {
                var partId = reader.ReadString(MatchProtocol.MaxContentIdBytes, null);
                if (string.IsNullOrEmpty(partId))
                {
                    throw new MessageReadException("vehicle part " + Text(i) + " has no id");
                }

                for (var j = 0; j < i; j++)
                {
                    if (string.Equals(ids[j], partId, StringComparison.Ordinal))
                    {
                        throw new MessageReadException("vehicle part " + partId + " is listed twice");
                    }
                }

                ids[i] = partId;
                taken[i] = reader.ReadInt32AtLeast(0, "vehicle part damage");
            }

            return new VehiclePartDamageSync(ids, taken);
        }

        private int IndexOf(string partId)
        {
            if (partId == null)
            {
                return -1;
            }

            for (var i = 0; i < _partIds.Length; i++)
            {
                if (string.Equals(_partIds[i], partId, StringComparison.Ordinal))
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
