using System;
using System.Numerics;
using Shooter.Core;

namespace Shooter.Protocol
{
    // 誰が座っているかは乗り物ではなくプレイヤーの行 (PlayerSnapshot.Vehicle) に載せる。座席の数だけの並びを構造体に持たせると、差分の基準と中身を共有してしまうため。
    public struct VehicleSnapshot : ISnapshotEntity<VehicleSnapshot>
    {
        public int Index { get; set; }
        public VehicleFields Present { get; set; }

        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; }
        public Vector3 Velocity { get; set; }

        public int Health { get; set; }
        public VehicleDamageStage Stage { get; set; }

        public int EntityId => Index;

        public int PresentBits => (int)Present;

        public int AllBits => (int)VehicleFields.All;

        public int ChangedBits(in VehicleSnapshot baseline)
        {
            var bits = VehicleFields.None;
            if (!Same.Bits(Position, baseline.Position) || !Same.Bits(Rotation, baseline.Rotation) || !Same.Bits(Velocity, baseline.Velocity))
            {
                bits |= VehicleFields.Transform;
            }

            if (Health != baseline.Health || Stage != baseline.Stage)
            {
                bits |= VehicleFields.State;
            }

            return (int)bits;
        }

        public void WriteBits(MessageWriter writer, int bits)
        {
            var fields = (VehicleFields)bits;
            if ((fields & VehicleFields.Transform) != 0)
            {
                writer.WriteVector3(Position);
                writer.WriteQuaternion(Rotation);
                writer.WriteVector3(Velocity);
            }

            if ((fields & VehicleFields.State) != 0)
            {
                if (Health < 0)
                {
                    throw new InvalidOperationException("vehicle health must not be negative");
                }

                writer.WriteInt32(Health);
                writer.WriteByte((byte)Stage);
            }
        }

        public void ReadBits(MessageReader reader, int id, int bits)
        {
            Index = id;
            var fields = (VehicleFields)bits;
            Present = fields;
            if ((fields & VehicleFields.Transform) != 0)
            {
                Position = reader.ReadFiniteVector3("vehicle position");
                Rotation = reader.ReadFiniteQuaternion("vehicle rotation");
                Velocity = reader.ReadFiniteVector3("vehicle velocity");
            }

            if ((fields & VehicleFields.State) != 0)
            {
                Health = reader.ReadInt32AtLeast(0, "vehicle health");
                Stage = (VehicleDamageStage)reader.ReadByteBelow(EnumRange<VehicleDamageStage>.Count, "vehicle damage stage");
            }
        }

        public void FillFrom(in VehicleSnapshot baseline)
        {
            var missing = VehicleFields.All & ~Present;
            if ((missing & VehicleFields.Transform) != 0)
            {
                Position = baseline.Position;
                Rotation = baseline.Rotation;
                Velocity = baseline.Velocity;
            }

            if ((missing & VehicleFields.State) != 0)
            {
                Health = baseline.Health;
                Stage = baseline.Stage;
            }

            Present = VehicleFields.All;
        }
    }
}
