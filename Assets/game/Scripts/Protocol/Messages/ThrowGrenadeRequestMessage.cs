using System.Numerics;

namespace Shooter.Protocol
{
    public sealed class ThrowGrenadeRequestMessage : IMessage
    {
        public const byte CurrentVersion = 2;

        public ushort Id => MessageIds.ThrowGrenadeRequest;

        public byte Version => CurrentVersion;

        public Vector3 Direction { get; set; }

        public string GrenadeTypeId { get; set; } = "";

        public void Write(MessageWriter writer)
        {
            writer.WriteVector3(Direction);
            writer.WriteString(GrenadeTypeId, MatchProtocol.MaxContentIdBytes);
        }

        public void Read(MessageReader reader)
        {
            Direction = reader.ReadFiniteVector3("throw direction");
            GrenadeTypeId = reader.ReadString(MatchProtocol.MaxContentIdBytes, GrenadeTypeId);
        }
    }
}
