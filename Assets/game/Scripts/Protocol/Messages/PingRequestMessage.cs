using System.Numerics;

namespace Fps.Protocol
{
    /// <summary>持ち主が向いた先に印を付ける要求。何に当たったか・付けてよいかはサーバーが決める。</summary>
    public sealed class PingRequestMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.PingRequest;

        public byte Version => CurrentVersion;

        /// <summary>視点から見た向き。長さはサーバーが 1 にそろえる。</summary>
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
