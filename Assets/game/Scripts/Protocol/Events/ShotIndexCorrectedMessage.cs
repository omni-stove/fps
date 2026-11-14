namespace Fps.Protocol
{
    /// <summary>
    /// 持ち主が数えた何発目かがサーバーとずれていたときに、サーバーの番号に合わせ直させる (ばらつきの乱数をそろえるため)。
    /// </summary>
    public sealed class ShotIndexCorrectedMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.ShotIndexCorrected;

        public byte Version => CurrentVersion;

        /// <summary>持ち主が付けた番号の世代 (持ち替え・リロードで変わる)。</summary>
        public int Generation { get; set; }

        public int ClientIndex { get; set; }

        public int ServerIndex { get; set; }

        public void Write(MessageWriter writer)
        {
            writer.WriteInt32(Generation);
            writer.WriteInt32(ClientIndex);
            writer.WriteInt32(ServerIndex);
        }

        public void Read(MessageReader reader)
        {
            Generation = reader.ReadInt32AtLeast(0, "shot generation");
            ClientIndex = reader.ReadInt32AtLeast(0, "client shot index");
            ServerIndex = reader.ReadInt32AtLeast(0, "server shot index");
        }
    }
}
