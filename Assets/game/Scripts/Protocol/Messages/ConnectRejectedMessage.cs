namespace Shooter.Protocol
{
    public sealed class ConnectRejectedMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.ConnectRejected;

        public byte Version => CurrentVersion;

        public string Reason { get; set; } = "";

        public void Write(MessageWriter writer)
        {
            writer.WriteString(Reason, MatchProtocol.MaxReasonBytes);
        }

        public void Read(MessageReader reader)
        {
            Reason = reader.ReadString(MatchProtocol.MaxReasonBytes, Reason);
        }
    }
}
