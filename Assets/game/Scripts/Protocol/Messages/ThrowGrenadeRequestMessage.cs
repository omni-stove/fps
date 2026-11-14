using System.Numerics;

namespace Fps.Protocol
{
    /// <summary>持ち主がグレネードを投げる要求。投げてよいか (持っている数・間隔) はサーバーが決める。</summary>
    public sealed class ThrowGrenadeRequestMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.ThrowGrenadeRequest;

        public byte Version => CurrentVersion;

        /// <summary>視点から見た向き。長さはサーバーが 1 にそろえる。</summary>
        public Vector3 Direction { get; set; }

        public void Write(MessageWriter writer)
        {
            writer.WriteVector3(Direction);
        }

        public void Read(MessageReader reader)
        {
            Direction = reader.ReadFiniteVector3("throw direction");
        }
    }
}
