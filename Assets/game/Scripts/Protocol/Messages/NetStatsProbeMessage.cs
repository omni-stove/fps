namespace Fps.Protocol
{
    /// <summary>クライアントが往復時間を測るために送る番号。サーバーは <see cref="NetStatsEchoMessage"/> で返す。</summary>
    public sealed class NetStatsProbeMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.NetStatsProbe;

        public byte Version => CurrentVersion;

        public ushort Sequence { get; set; }

        public void Write(MessageWriter writer)
        {
            writer.WriteUInt16(Sequence);
        }

        public void Read(MessageReader reader)
        {
            Sequence = reader.ReadUInt16();
        }
    }
}
