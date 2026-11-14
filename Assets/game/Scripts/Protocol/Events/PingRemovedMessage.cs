using Fps.Core;

namespace Fps.Protocol
{
    /// <summary>印が消えた知らせと、その訳。</summary>
    public sealed class PingRemovedMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.PingRemoved;

        public byte Version => CurrentVersion;

        public int PingId { get; set; }

        public PingRemoval Reason { get; set; }

        public void Write(MessageWriter writer)
        {
            writer.WriteInt32(PingId);
            writer.WriteByte((byte)Reason);
        }

        public void Read(MessageReader reader)
        {
            PingId = reader.ReadInt32AtLeast(0, "ping id");
            Reason = (PingRemoval)reader.ReadByteBelow(EnumRange<PingRemoval>.Count, "ping removal");
        }
    }
}
