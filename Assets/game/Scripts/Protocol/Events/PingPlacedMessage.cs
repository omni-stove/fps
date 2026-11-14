using System.Numerics;
using Fps.Core;

namespace Fps.Protocol
{
    /// <summary>印を見せる相手 (付けた人の味方) に送る、新しい印。</summary>
    public sealed class PingPlacedMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.PingPlaced;

        public byte Version => CurrentVersion;

        public int PingId { get; set; }

        public PingKind Kind { get; set; }

        public Vector3 Position { get; set; }

        /// <summary>敵に付いて動くか。動けば <see cref="PingMovedMessage"/> で位置が届く。</summary>
        public bool Tracking { get; set; }

        /// <summary>消えるまでの秒。</summary>
        public float RemainingSeconds { get; set; }

        public void Write(MessageWriter writer)
        {
            writer.WriteInt32(PingId);
            writer.WriteByte((byte)Kind);
            writer.WriteVector3(Position);
            writer.WriteBool(Tracking);
            writer.WriteSingle(RemainingSeconds);
        }

        public void Read(MessageReader reader)
        {
            PingId = reader.ReadInt32AtLeast(0, "ping id");
            Kind = (PingKind)reader.ReadByteBelow(EnumRange<PingKind>.Count, "ping kind");
            Position = reader.ReadFiniteVector3("ping position");
            Tracking = reader.ReadBool();
            RemainingSeconds = reader.ReadFiniteSingle("ping remaining seconds");
        }
    }
}
