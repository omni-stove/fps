using Shooter.Core;

namespace Shooter.Protocol
{
    // 版と鍵の文字の妥当性はサーバーが ConnectionApproval で確かめるので、ここでは長さだけを絞る。
    // 欄は表示名の後ろに、どれかがあるときだけ書く。どれも無ければ欄を足す前と同じバイト列になり、足す前のビルドのサーバーも
    // 読み切って版の違いで断れる (版の違う相手に「読めない」で断られると、何が違うかをプレイヤーに示せない) ため、
    // このメッセージの版 (CurrentVersion) は上げない。
    public sealed class ConnectRequestMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public const int MaxDisplayNameBytes = PlayerDisplayName.MaxBytes;

        public ushort Id => MessageIds.ConnectRequest;

        public byte Version => CurrentVersion;

        public ushort ProtocolVersion { get; set; } = MatchProtocol.Version;

        public string ClientVersion { get; set; } = "";

        public string RejoinKey { get; set; } = "";

        public string DisplayName { get; set; } = "";

        public string PartyId { get; set; } = "";

        public string JoinCode { get; set; } = "";

        public int? SkillRating { get; set; }

        public string ProductUserId { get; set; } = "";

        public bool HasExtras =>
            !string.IsNullOrEmpty(PartyId) || !string.IsNullOrEmpty(JoinCode) || SkillRating.HasValue || !string.IsNullOrEmpty(ProductUserId);

        public void Write(MessageWriter writer)
        {
            writer.WriteUInt16(ProtocolVersion);
            writer.WriteString(ClientVersion, BuildVersion.MaxLength);
            writer.WriteString(RejoinKey, Shooter.Core.RejoinKey.MaxLength);
            writer.WriteString(DisplayName, MaxDisplayNameBytes);
            if (!HasExtras)
            {
                return;
            }

            writer.WriteString(PartyId, ConnectionExtras.MaxPartyIdLength);
            writer.WriteString(JoinCode, ConnectionExtras.MaxJoinCodeLength);
            writer.WriteBool(SkillRating.HasValue);
            writer.WriteInt32(SkillRating ?? 0);
            writer.WriteString(ProductUserId, ConnectionExtras.MaxProductUserIdLength);
        }

        public void Read(MessageReader reader)
        {
            ProtocolVersion = reader.ReadUInt16();
            ClientVersion = reader.ReadString(BuildVersion.MaxLength, ClientVersion);
            RejoinKey = reader.ReadString(Shooter.Core.RejoinKey.MaxLength, RejoinKey);
            DisplayName = reader.ReadString(MaxDisplayNameBytes, DisplayName);

            // 受け手は 1 つのインスタンスを使い回すので、欄の無いメッセージでは前に読んだ欄を消す
            if (reader.IsAtEnd)
            {
                PartyId = "";
                JoinCode = "";
                SkillRating = null;
                ProductUserId = "";
                return;
            }

            PartyId = reader.ReadString(ConnectionExtras.MaxPartyIdLength, PartyId);
            JoinCode = reader.ReadString(ConnectionExtras.MaxJoinCodeLength, JoinCode);
            var hasSkill = reader.ReadBool();
            var skill = reader.ReadInt32();
            SkillRating = hasSkill ? skill : (int?)null;
            ProductUserId = reader.ReadString(ConnectionExtras.MaxProductUserIdLength, ProductUserId);
        }
    }
}
