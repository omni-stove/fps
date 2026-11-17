namespace Shooter.Protocol
{
    public sealed class IdleWarningMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.IdleWarning;

        public byte Version => CurrentVersion;

        public bool Active { get; set; }

        public float RemainingSeconds { get; set; }

        public void Write(MessageWriter writer)
        {
            writer.WriteBool(Active);
            writer.WriteSingle(Active ? RemainingSeconds : 0f);
        }

        public void Read(MessageReader reader)
        {
            Active = reader.ReadBool();
            RemainingSeconds = reader.ReadFiniteSingle("idle remaining seconds");
        }
    }
}
