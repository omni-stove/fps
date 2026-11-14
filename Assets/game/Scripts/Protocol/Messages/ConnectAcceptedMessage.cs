namespace Fps.Protocol
{
    /// <summary>サーバーが承認したときに返す、クライアントのプレイヤーとサーバーの時計。</summary>
    public sealed class ConnectAcceptedMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.ConnectAccepted;

        public byte Version => CurrentVersion;

        /// <summary>このクライアントのプレイヤーの Id。スナップショットとイベントはこの Id で自分を指す。</summary>
        public int PlayerId { get; set; }

        public string ServerVersion { get; set; } = "";

        /// <summary>サーバーの 1 秒あたりの tick 数。</summary>
        public int TickRate { get; set; } = 1;

        /// <summary>承認した時点のサーバーの tick。</summary>
        public int ServerTick { get; set; }

        public void Write(MessageWriter writer)
        {
            writer.WriteInt32(PlayerId);
            writer.WriteString(ServerVersion, Fps.Core.BuildVersion.MaxLength);
            writer.WriteInt32(TickRate);
            writer.WriteInt32(ServerTick);
        }

        public void Read(MessageReader reader)
        {
            PlayerId = reader.ReadInt32AtLeast(0, "player id");
            ServerVersion = reader.ReadString(Fps.Core.BuildVersion.MaxLength, ServerVersion);
            TickRate = reader.ReadInt32AtLeast(1, "tick rate");
            ServerTick = reader.ReadInt32AtLeast(0, "server tick");
        }
    }
}
