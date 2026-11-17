using Shooter.Core;

namespace Shooter.Protocol
{
    /// <summary>
    /// クライアントがつないで最初に送る承認のデータ。中身は Netcode の承認データ (<see cref="ConnectionPayload"/>) と同じく、
    /// ビルドの版と戻ってきた人を見分ける鍵 (端末の Id か EOS の ProductUserId) とどれも無くてよい欄 (<see cref="ConnectionExtras"/>) に、
    /// 表示名を足したもの。
    /// </summary>
    /// <remarks>
    /// 版と鍵の文字の妥当性はサーバーが <see cref="ConnectionApproval"/> で確かめるので、ここでは長さだけを絞る。
    /// 欄は表示名の後ろに、どれかがあるときだけ書く。どれも無ければ欄を足す前と同じバイト列になり、足す前のビルドのサーバーも
    /// 読み切って版の違いで断れる (版の違う相手に「読めない」で断られると、何が違うかをプレイヤーに示せない) ため、
    /// このメッセージの版 (<see cref="CurrentVersion"/>) は上げない。
    /// </remarks>
    public sealed class ConnectRequestMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        /// <summary>表示名の最長のバイト数 (UTF-8)。</summary>
        public const int MaxDisplayNameBytes = 64;

        public ushort Id => MessageIds.ConnectRequest;

        public byte Version => CurrentVersion;

        /// <summary>クライアントの <see cref="MatchProtocol.Version"/>。</summary>
        public ushort ProtocolVersion { get; set; } = MatchProtocol.Version;

        /// <summary>クライアントのビルドの版 (<see cref="BuildVersion"/> で正規化したもの)。</summary>
        public string ClientVersion { get; set; } = "";

        /// <summary>切断して戻ってきた人を見分ける鍵 (<see cref="Shooter.Core.RejoinKey"/>)。</summary>
        public string RejoinKey { get; set; } = "";

        public string DisplayName { get; set; } = "";

        /// <summary>入っているパーティ (メンバー用ロビー) の Id。無ければ空。</summary>
        public string PartyId { get; set; } = "";

        /// <summary>参加コードで入るときのコード。無ければ空。</summary>
        public string JoinCode { get; set; } = "";

        /// <summary>実力の値。無ければ null。</summary>
        public int? SkillRating { get; set; }

        /// <summary>ボイスで使う EOS の ProductUserId。無ければ空。</summary>
        public string ProductUserId { get; set; } = "";

        /// <summary>欄 (<see cref="PartyId"/>・<see cref="JoinCode"/>・<see cref="SkillRating"/>・<see cref="ProductUserId"/>) のどれかがあるか。</summary>
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
