using Shooter.Core;

namespace Shooter.Protocol
{
    // 版と鍵の文字の妥当性はサーバーが ConnectionApproval で確かめるので、ここでは長さだけを絞る。
    public sealed class ConnectRequestMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public const int MaxDisplayNameBytes = 64;

        public ushort Id => MessageIds.ConnectRequest;

        public byte Version => CurrentVersion;

        public ushort ProtocolVersion { get; set; } = MatchProtocol.Version;

        public string ClientVersion { get; set; } = "";

        public string RejoinKey { get; set; } = "";

        public string DisplayName { get; set; } = "";

        public void Write(MessageWriter writer)
        {
            writer.WriteUInt16(ProtocolVersion);
            writer.WriteString(ClientVersion, BuildVersion.MaxLength);
            writer.WriteString(RejoinKey, Shooter.Core.RejoinKey.MaxLength);
            writer.WriteString(DisplayName, MaxDisplayNameBytes);
        }

        public void Read(MessageReader reader)
        {
            ProtocolVersion = reader.ReadUInt16();
            ClientVersion = reader.ReadString(BuildVersion.MaxLength, ClientVersion);
            RejoinKey = reader.ReadString(Shooter.Core.RejoinKey.MaxLength, RejoinKey);
            DisplayName = reader.ReadString(MaxDisplayNameBytes, DisplayName);
        }
    }
}
