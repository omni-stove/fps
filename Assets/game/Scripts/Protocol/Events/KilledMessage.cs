namespace Fps.Protocol
{
    /// <summary>全員に送るキルログの 1 行 (<c>Fps.Core.KillFeedEntry</c> の中身)。自滅なら倒した人と倒された人が同じ。</summary>
    public sealed class KilledMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.Killed;

        public byte Version => CurrentVersion;

        public int KillerId { get; set; }

        public int VictimId { get; set; }

        public string WeaponId { get; set; } = "";

        public bool Headshot { get; set; }

        /// <summary>味方を倒したか。</summary>
        public bool Friendly { get; set; }

        public void Write(MessageWriter writer)
        {
            writer.WriteInt32(KillerId);
            writer.WriteInt32(VictimId);
            writer.WriteString(WeaponId, MatchProtocol.MaxContentIdBytes);
            writer.WriteBool(Headshot);
            writer.WriteBool(Friendly);
        }

        public void Read(MessageReader reader)
        {
            KillerId = reader.ReadInt32AtLeast(0, "killer");
            VictimId = reader.ReadInt32AtLeast(0, "victim");
            WeaponId = reader.ReadString(MatchProtocol.MaxContentIdBytes, WeaponId);
            Headshot = reader.ReadBool();
            Friendly = reader.ReadBool();
        }
    }
}
