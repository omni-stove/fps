using Fps.Core;

namespace Fps.Protocol
{
    /// <summary>
    /// クライアントがつないで最初に送る承認のデータ。中身は Netcode の承認データ (<see cref="ConnectionPayload"/>) と同じく、
    /// ビルドの版と戻ってきた人を見分ける鍵 (端末の Id か EOS の ProductUserId) に、表示名を足したもの。
    /// </summary>
    /// <remarks>
    /// 版と鍵の文字の妥当性はサーバーが <see cref="ConnectionApproval"/> で確かめるので、ここでは長さだけを絞る。
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

        /// <summary>切断して戻ってきた人を見分ける鍵 (<see cref="Fps.Core.RejoinKey"/>)。</summary>
        public string RejoinKey { get; set; } = "";

        public string DisplayName { get; set; } = "";

        public void Write(MessageWriter writer)
        {
            writer.WriteUInt16(ProtocolVersion);
            writer.WriteString(ClientVersion, BuildVersion.MaxLength);
            writer.WriteString(RejoinKey, Fps.Core.RejoinKey.MaxLength);
            writer.WriteString(DisplayName, MaxDisplayNameBytes);
        }

        public void Read(MessageReader reader)
        {
            ProtocolVersion = reader.ReadUInt16();
            ClientVersion = reader.ReadString(BuildVersion.MaxLength, ClientVersion);
            RejoinKey = reader.ReadString(Fps.Core.RejoinKey.MaxLength, RejoinKey);
            DisplayName = reader.ReadString(MaxDisplayNameBytes, DisplayName);
        }
    }
}
