using System.Numerics;

namespace Shooter.Protocol
{
    public sealed class TeleportedMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.Teleported;

        public byte Version => CurrentVersion;

        public int PlayerId { get; set; }

        public int Tick { get; set; }

        public Vector3 Position { get; set; }

        public float Yaw { get; set; }

        public void Write(MessageWriter writer)
        {
            writer.WriteInt32(PlayerId);
            writer.WriteInt32(Tick);
            writer.WriteVector3(Position);
            writer.WriteSingle(Yaw);
        }

        public void Read(MessageReader reader)
        {
            PlayerId = reader.ReadInt32AtLeast(0, "player id");
            Tick = reader.ReadInt32AtLeast(0, "teleport tick");
            Position = reader.ReadFiniteVector3("teleport position");
            Yaw = reader.ReadFiniteSingle("teleport yaw");
        }
    }
}
