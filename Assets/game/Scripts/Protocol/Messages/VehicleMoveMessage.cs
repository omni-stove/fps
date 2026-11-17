using System.Numerics;

namespace Fps.Protocol
{
    // 運転席のクライアントは乗り物を自分の物理で動かすので、入力ではなく動かした後の姿を送り、サーバーは届く範囲かを確かめるだけにする。
    public sealed class VehicleMoveMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.VehicleMove;

        public byte Version => CurrentVersion;

        public int Vehicle { get; set; }

        public int Sequence { get; set; }

        public int CorrectionGeneration { get; set; }

        public Vector3 Position { get; set; }

        public Quaternion Rotation { get; set; }

        public Vector3 Velocity { get; set; }

        public void Write(MessageWriter writer)
        {
            if (Vehicle < 0 || Sequence < 0 || CorrectionGeneration < 0)
            {
                throw new System.InvalidOperationException("vehicle, sequence and correction generation must not be negative");
            }

            writer.WriteInt32(Vehicle);
            writer.WriteInt32(Sequence);
            writer.WriteInt32(CorrectionGeneration);
            writer.WriteVector3(Position);
            writer.WriteQuaternion(Rotation);
            writer.WriteVector3(Velocity);
        }

        public void Read(MessageReader reader)
        {
            Vehicle = reader.ReadInt32AtLeast(0, "vehicle");
            Sequence = reader.ReadInt32AtLeast(0, "vehicle move sequence");
            CorrectionGeneration = reader.ReadInt32AtLeast(0, "vehicle correction generation");
            Position = reader.ReadFiniteVector3("vehicle position");
            Rotation = reader.ReadFiniteQuaternion("vehicle rotation");
            Velocity = reader.ReadFiniteVector3("vehicle velocity");
        }
    }
}
