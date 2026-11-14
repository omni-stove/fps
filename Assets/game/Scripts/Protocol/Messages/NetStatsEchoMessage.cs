namespace Fps.Protocol
{
    /// <summary><see cref="NetStatsProbeMessage"/> への返事。受け取った番号と、返した時点のサーバーの tick。</summary>
    public sealed class NetStatsEchoMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.NetStatsEcho;

        public byte Version => CurrentVersion;

        public ushort Sequence { get; set; }

        public int ServerTick { get; set; }

        public void Write(MessageWriter writer)
        {
            writer.WriteUInt16(Sequence);
            writer.WriteInt32(ServerTick);
        }

        public void Read(MessageReader reader)
        {
            Sequence = reader.ReadUInt16();
            ServerTick = reader.ReadInt32AtLeast(0, "server tick");
        }
    }
}
