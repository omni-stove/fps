namespace Shooter.Protocol
{
    public sealed class ReviveRequestMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.ReviveRequest;

        public byte Version => CurrentVersion;

        public int TargetPlayerId { get; set; }

        public void Write(MessageWriter writer)
        {
            writer.WriteInt32(TargetPlayerId);
        }

        public void Read(MessageReader reader)
        {
            TargetPlayerId = reader.ReadInt32AtLeast(0, "revive target");
        }
    }
}
