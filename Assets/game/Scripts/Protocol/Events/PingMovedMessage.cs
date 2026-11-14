using System.Numerics;

namespace Fps.Protocol
{
    /// <summary>敵に付いた印の新しい位置。次の知らせで上書きされるので、落ちてもよい届け方で送る。</summary>
    public sealed class PingMovedMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.PingMoved;

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
