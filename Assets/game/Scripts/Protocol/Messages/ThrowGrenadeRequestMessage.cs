using System.Numerics;

namespace Shooter.Protocol
{
    /// <summary>持ち主がグレネードを投げる要求。投げてよいか (持っている数・間隔) はサーバーが決める。</summary>
    public sealed class ThrowGrenadeRequestMessage : IMessage
    {
        public const byte CurrentVersion = 2;

        public ushort Id => MessageIds.ThrowGrenadeRequest;

        public byte Version => CurrentVersion;

        /// <summary>視点から見た向き。長さはサーバーが 1 にそろえる。</summary>
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
