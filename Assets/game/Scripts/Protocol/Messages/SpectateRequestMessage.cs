namespace Shooter.Protocol
{
    public sealed class SpectateRequestMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.SpectateRequest;

        public byte Version => CurrentVersion;

        public SpectateStep Step { get; set; }

        public void Write(MessageWriter writer)
        {
            writer.WriteByte((byte)Step);
        }

        public void Read(MessageReader reader)
        {
            Step = (SpectateStep)reader.ReadByteBelow(EnumRange<SpectateStep>.Count, "spectate step");
        }
    }
}
