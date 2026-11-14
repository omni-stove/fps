namespace Fps.Protocol
{
    /// <summary>サーバーが承認したときに返す、クライアントのプレイヤーとサーバーの時計。</summary>
    /// <remarks>
    /// マップはクライアントの <c>-map</c> ではなくここで受け取る。<c>-map</c> はホスト・サーバーだけが選び、クライアントは
    /// つないだサーバーのマップへ合わせる (Netcode のシーン管理と同じ意味) ため。
    /// </remarks>
    public sealed class ConnectAcceptedMessage : IMessage
    {
        public const byte CurrentVersion = 2;

        public ushort Id => MessageIds.ConnectAccepted;

        public byte Version => CurrentVersion;

        /// <summary>このクライアントのプレイヤーの Id。スナップショットとイベントはこの Id で自分を指す。</summary>
        public int PlayerId { get; set; }

        public string ServerVersion { get; set; } = "";

        /// <summary>サーバーの 1 秒あたりの tick 数。</summary>
        public int TickRate { get; set; } = 1;

        /// <summary>承認した時点のサーバーの tick。</summary>
        public int ServerTick { get; set; }

        /// <summary>サーバーの試合のマップ (シーン名。<see cref="Fps.Core.NetLaunchOptions.IsMapName"/> に通る)。</summary>
        public string Map { get; set; } = "";

        /// <summary>試合の最大の体力。スナップショットは今の体力だけを運ぶので、HUD のゲージの満タンをここで知る。</summary>
        public int MaxHealth { get; set; } = 1;

        public void Write(MessageWriter writer)
        {
            writer.WriteInt32(PlayerId);
            writer.WriteString(ServerVersion, Fps.Core.BuildVersion.MaxLength);
            writer.WriteInt32(TickRate);
            writer.WriteInt32(ServerTick);
            writer.WriteString(Map, Fps.Core.NetLaunchOptions.MaxMapNameLength);
            writer.WriteInt32(MaxHealth);
        }

        public void Read(MessageReader reader)
        {
            PlayerId = reader.ReadInt32AtLeast(0, "player id");
            ServerVersion = reader.ReadString(Fps.Core.BuildVersion.MaxLength, ServerVersion);
            TickRate = reader.ReadInt32AtLeast(1, "tick rate");
            ServerTick = reader.ReadInt32AtLeast(0, "server tick");
            Map = reader.ReadString(Fps.Core.NetLaunchOptions.MaxMapNameLength, Map);
            if (!Fps.Core.NetLaunchOptions.IsMapName(Map))
            {
                throw new MessageReadException("map '" + Map + "' is not a scene name");
            }

            MaxHealth = reader.ReadInt32AtLeast(1, "max health");
        }
    }
}
