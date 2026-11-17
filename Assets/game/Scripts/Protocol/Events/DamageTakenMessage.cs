using System.Numerics;

namespace Shooter.Protocol
{
    public sealed class DamageTakenMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.DamageTaken;

        public byte Version => CurrentVersion;

        public bool HasSource { get; set; }

        public Vector3 SourcePosition { get; set; }

        public int Amount { get; set; }

        public void Write(MessageWriter writer)
        {
            writer.WriteBool(HasSource);
            if (HasSource)
            {
                writer.WriteVector3(SourcePosition);
            }

            writer.WriteInt32(Amount);
        }

        public void Read(MessageReader reader)
        {
            HasSource = reader.ReadBool();
            SourcePosition = HasSource ? reader.ReadFiniteVector3("damage source") : default;
            Amount = reader.ReadInt32AtLeast(0, "damage amount");
        }
    }
}
