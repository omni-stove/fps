namespace Fps.Protocol
{
    /// <summary>持ち主が次の出現で持つ武器の Id (スロット順) を頼む。返事は <see cref="LoadoutAnswerMessage"/>。</summary>
    public sealed class LoadoutRequestMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        /// <summary>頼める武器の最大数。</summary>
        public const int MaxWeapons = byte.MaxValue;

        private readonly ContentIdList _weaponIds = new ContentIdList(MaxWeapons, "loadout weapon");

        public ushort Id => MessageIds.LoadoutRequest;

        public byte Version => CurrentVersion;

        /// <summary>武器の Id (スロット順)。</summary>
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
