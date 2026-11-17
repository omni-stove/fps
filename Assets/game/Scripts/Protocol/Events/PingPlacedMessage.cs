using System.Numerics;
using Shooter.Core;

namespace Shooter.Protocol
{
    public sealed class PingPlacedMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.PingPlaced;

        public byte Version => CurrentVersion;

        public int PingId { get; set; }

        public PingKind Kind { get; set; }

        public Vector3 Position { get; set; }

        public bool Tracking { get; set; }

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
