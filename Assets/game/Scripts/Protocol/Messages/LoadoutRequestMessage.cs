namespace Shooter.Protocol
{
    public sealed class LoadoutRequestMessage : IMessage
    {
        public const byte CurrentVersion = 2;

        public const int MaxWeapons = byte.MaxValue;

        private readonly ContentIdList _weaponIds = new ContentIdList(MaxWeapons, "loadout weapon");

        public ushort Id => MessageIds.LoadoutRequest;

        public byte Version => CurrentVersion;

        public ContentIdList WeaponIds => _weaponIds;

        public string CosmeticId { get; set; } = "";

        public void Write(MessageWriter writer)
        {
            _weaponIds.Write(writer);
            writer.WriteString(CosmeticId, MatchProtocol.MaxContentIdBytes);
        }

        public void Read(MessageReader reader)
        {
            _weaponIds.Read(reader);
            CosmeticId = reader.ReadString(MatchProtocol.MaxContentIdBytes, CosmeticId);
        }
    }
}
