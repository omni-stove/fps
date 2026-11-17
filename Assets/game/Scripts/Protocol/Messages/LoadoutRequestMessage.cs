namespace Shooter.Protocol
{
    public sealed class LoadoutRequestMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public const int MaxWeapons = byte.MaxValue;

        private readonly ContentIdList _weaponIds = new ContentIdList(MaxWeapons, "loadout weapon");

        public ushort Id => MessageIds.LoadoutRequest;

        public byte Version => CurrentVersion;

        public ContentIdList WeaponIds => _weaponIds;

        public void Write(MessageWriter writer)
        {
            _weaponIds.Write(writer);
        }

        public void Read(MessageReader reader)
        {
            _weaponIds.Read(reader);
        }
    }
}
