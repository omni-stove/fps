namespace Shooter.Protocol
{
    public sealed class LoadoutRequestMessage : IMessage, IReadsOlderVersions
    {
        public const byte CurrentVersion = 3;

        // 能力の枠を持たない前の版のクライアントからの要求も、武器の選択は受け付けられるよう読む
        public const byte OldestVersion = 1;

        public const int MaxWeapons = byte.MaxValue;

        public const int MaxAbilities = byte.MaxValue;

        private readonly ContentIdList _weaponIds = new ContentIdList(MaxWeapons, "loadout weapon");
        private readonly ContentIdList _abilityIds = new ContentIdList(MaxAbilities, "loadout ability");

        public ushort Id => MessageIds.LoadoutRequest;

        public byte Version => CurrentVersion;

        public byte OldestReadableVersion => OldestVersion;

        public ContentIdList WeaponIds => _weaponIds;

        public string CosmeticId { get; set; } = "";

        // 能力を選んでいないことを別の欄にせず空の並びで表すのは、前の版の要求を読んだときと同じに扱えるようにするため。枠を「なし」にするのは空の Id で送る
        public ContentIdList AbilityIds => _abilityIds;

        public void Write(MessageWriter writer)
        {
            _weaponIds.Write(writer);
            writer.WriteString(CosmeticId, MatchProtocol.MaxContentIdBytes);
            _abilityIds.Write(writer);
        }

        public void Read(MessageReader reader)
        {
            ReadOlder(reader, CurrentVersion);
        }

        public void ReadOlder(MessageReader reader, byte version)
        {
            _weaponIds.Read(reader);
            CosmeticId = version >= 2 ? reader.ReadString(MatchProtocol.MaxContentIdBytes, CosmeticId) : "";
            if (version >= 3)
            {
                _abilityIds.Read(reader);
            }
            else
            {
                _abilityIds.Clear();
            }
        }
    }
}
