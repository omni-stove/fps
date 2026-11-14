namespace Fps.Protocol
{
    /// <summary>持ち主が持っている武器のうち <see cref="WeaponId"/> への持ち替えを頼む。</summary>
    public sealed class SwitchWeaponRequestMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.SwitchWeaponRequest;

        public byte Version => CurrentVersion;

        public string WeaponId { get; set; } = "";

        public void Write(MessageWriter writer)
        {
            writer.WriteString(WeaponId, MatchProtocol.MaxContentIdBytes);
        }

        public void Read(MessageReader reader)
        {
            WeaponId = reader.ReadString(MatchProtocol.MaxContentIdBytes, WeaponId);
        }
    }
}
