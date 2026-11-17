namespace Shooter.Protocol
{
    public sealed class LoadoutAnswerMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public const int MaxReasonBytes = 512;

        public ushort Id => MessageIds.LoadoutAnswer;

        public byte Version => CurrentVersion;

        public bool Accepted { get; set; }

        public string Reason { get; set; } = "";

        public void Write(MessageWriter writer)
        {
            writer.WriteBool(Accepted);
            writer.WriteString(Accepted ? "" : Reason, MaxReasonBytes);
        }

        public void Read(MessageReader reader)
        {
            Accepted = reader.ReadBool();
            Reason = reader.ReadString(MaxReasonBytes, Reason);
        }
    }
}
