using System.Numerics;

namespace Shooter.Protocol
{
    public sealed class PingRequestMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.PingRequest;

        public byte Version => CurrentVersion;

        public Vector3 Direction { get; set; }

        public void Write(MessageWriter writer)
        {
            writer.WriteVector3(Direction);
        }

        public void Read(MessageReader reader)
        {
            Direction = reader.ReadFiniteVector3("ping direction");
        }
    }
}
