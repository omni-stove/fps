using System.Numerics;

namespace Fps.Protocol
{
    /// <summary>敵に付いた印が敵を見失い、最後の位置 (<see cref="Position"/>) に止まった知らせ。</summary>
    public sealed class PingLostMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.PingLost;

        public byte Version => CurrentVersion;

        public int PingId { get; set; }

        public Vector3 Position { get; set; }

        public void Write(MessageWriter writer)
        {
            writer.WriteInt32(PingId);
            writer.WriteVector3(Position);
        }

        public void Read(MessageReader reader)
        {
            PingId = reader.ReadInt32AtLeast(0, "ping id");
            Position = reader.ReadFiniteVector3("ping position");
        }
    }
}
