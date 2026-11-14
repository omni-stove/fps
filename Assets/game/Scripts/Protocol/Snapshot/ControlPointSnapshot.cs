using System;
using Fps.Core;

namespace Fps.Protocol
{
    /// <summary>
    /// 拠点 1 つの占領の状態 (Netcode の <c>NetControlPointState</c> の中身)。拠点はマップの番号 (<see cref="Index"/>、HUD では 0 = A) で指す。
    /// </summary>
    public struct ControlPointSnapshot : ISnapshotEntity<ControlPointSnapshot>
    {
        /// <summary><see cref="ControlPercent"/> の絶対値の最大。</summary>
        public const int FullPercent = 100;

        public int Index { get; set; }
        public ControlPointFields Present { get; set; }

        public bool HasOwner { get; set; }

        /// <summary>占領しているチーム。<see cref="HasOwner"/> が false なら使わない。</summary>
        public Team Owner { get; set; }

        /// <summary>占領の進み (-100〜100)。正なら Alpha、負なら Bravo に傾いている。</summary>
        public sbyte ControlPercent { get; set; }

        /// <summary>両チームがいて押し合っているか。</summary>
        public bool Contested { get; set; }

        public int EntityId => Index;

        public int PresentBits => (int)Present;

        public int AllBits => (int)ControlPointFields.All;

        public int ChangedBits(in ControlPointSnapshot baseline)
        {
            var same = HasOwner == baseline.HasOwner
                && (!HasOwner || Owner == baseline.Owner)
                && ControlPercent == baseline.ControlPercent
                && Contested == baseline.Contested;
            return same ? 0 : (int)ControlPointFields.State;
        }

        public void WriteBits(MessageWriter writer, int bits)
        {
            if ((bits & (int)ControlPointFields.State) == 0)
            {
                return;
            }

            if (ControlPercent < -FullPercent || ControlPercent > FullPercent)
            {
                throw new InvalidOperationException("control percent must be -100 to 100");
            }

            writer.WriteBool(HasOwner);
            writer.WriteByte(HasOwner ? (byte)Owner : (byte)0);
            writer.WriteByte(unchecked((byte)ControlPercent));
            writer.WriteBool(Contested);
        }

        public void ReadBits(MessageReader reader, int id, int bits)
        {
            Index = id;
            Present = (ControlPointFields)bits;
            if ((bits & (int)ControlPointFields.State) == 0)
            {
                return;
            }

            HasOwner = reader.ReadBool();
            Owner = (Team)reader.ReadByteBelow(EnumRange<Team>.Count, "control point owner");
            var percent = unchecked((sbyte)reader.ReadByte());
            if (percent < -FullPercent || percent > FullPercent)
            {
                throw new MessageReadException("control percent " + percent + " is outside -100 to 100");
            }

            ControlPercent = percent;
            Contested = reader.ReadBool();
        }

        public void FillFrom(in ControlPointSnapshot baseline)
        {
            if ((Present & ControlPointFields.State) == 0)
            {
                HasOwner = baseline.HasOwner;
                Owner = baseline.Owner;
                ControlPercent = baseline.ControlPercent;
                Contested = baseline.Contested;
            }

            Present = ControlPointFields.All;
        }
    }
}
