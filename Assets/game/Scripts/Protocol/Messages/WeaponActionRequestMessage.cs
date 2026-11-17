namespace Shooter.Protocol
{
    /// <summary>持ち主が今の武器のリロード・アクティブリロード・弾薬の種類の切り替えを頼む。</summary>
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
