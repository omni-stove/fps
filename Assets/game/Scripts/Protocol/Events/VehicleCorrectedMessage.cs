using System.Numerics;

namespace Fps.Protocol
{
    public sealed class VehicleCorrectedMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.VehicleCorrected;

        public byte Version => CurrentVersion;

        public int Vehicle { get; set; }

        public int Generation { get; set; }

        public Vector3 Position { get; set; }

        public Quaternion Rotation { get; set; }

        public Vector3 Velocity { get; set; }

        public void Write(MessageWriter writer)
        {
            if (Vehicle < 0 || Generation < 0)
            {
                throw new System.InvalidOperationException("vehicle and generation must not be negative");
            }

            writer.WriteInt32(Vehicle);
            writer.WriteInt32(Generation);
            writer.WriteVector3(Position);
            writer.WriteQuaternion(Rotation);
            writer.WriteVector3(Velocity);
        }

        public void Read(MessageReader reader)
        {
            Vehicle = reader.ReadInt32AtLeast(0, "vehicle");
            Generation = reader.ReadInt32AtLeast(0, "vehicle correction generation");
            Position = reader.ReadFiniteVector3("vehicle position");
            Rotation = reader.ReadFiniteQuaternion("vehicle rotation");
            Velocity = reader.ReadFiniteVector3("vehicle velocity");
        }
    }
}
