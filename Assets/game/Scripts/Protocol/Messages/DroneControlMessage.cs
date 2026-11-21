using System;
using System.Numerics;

namespace Shooter.Protocol
{
    // 動かした後の位置でなく操縦の入力を送るのは、ドローンの速さと壁をサーバーが決めるため (乗り物と違い、手元で物理を回さない)。
    // 向きは差分でなく向く角度で送り、落ちた送信の分だけ向きがずれ続けないようにする。
    public sealed class DroneControlMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.DroneControl;

        public byte Version => CurrentVersion;

        public int Deployable { get; set; }

        // 届く順が入れ替わる経路で送るので、古い入力で新しい入力を上書きしないための番号
        public int Sequence { get; set; }

        // x が右、y が前。長さ 1 までを速さの割合として受ける
        public Vector2 Move { get; set; }

        // 上へ 1、下へ -1
        public float Climb { get; set; }

        public float Yaw { get; set; }

        public bool Recall { get; set; }

        public void Write(MessageWriter writer)
        {
            if (Deployable < 1 || Sequence < 0)
            {
                throw new InvalidOperationException("a drone control needs a deployable id of at least 1 and a sequence that is not negative");
            }

            writer.WriteInt32(Deployable);
            writer.WriteInt32(Sequence);
            writer.WriteVector2(Move);
            writer.WriteSingle(Climb);
            writer.WriteSingle(Yaw);
            writer.WriteBool(Recall);
        }

        public void Read(MessageReader reader)
        {
            Deployable = reader.ReadInt32AtLeast(1, "drone deployable");
            Sequence = reader.ReadInt32AtLeast(0, "drone control sequence");
            Move = reader.ReadFiniteVector2("drone move");
            Climb = reader.ReadFiniteSingleBetween(-1f, 1f, "drone climb");
            Yaw = reader.ReadFiniteSingle("drone yaw");
            Recall = reader.ReadBool();
        }
    }
}
