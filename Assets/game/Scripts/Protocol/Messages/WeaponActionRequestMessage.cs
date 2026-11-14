namespace Fps.Protocol
{
    /// <summary>持ち主が今の武器のリロードかアクティブリロードを頼む。</summary>
    public sealed class WeaponActionRequestMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.WeaponActionRequest;

        public byte Version => CurrentVersion;

        public WeaponAction Action { get; set; }

        public void Write(MessageWriter writer)
        {
            writer.WriteByte((byte)Action);
        }

        public void Read(MessageReader reader)
        {
            Action = (WeaponAction)reader.ReadByteBelow(EnumRange<WeaponAction>.Count, "weapon action");
        }
    }
}
