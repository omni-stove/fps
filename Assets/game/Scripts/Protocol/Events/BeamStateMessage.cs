using System;
using System.Numerics;

namespace Shooter.Protocol
{
    public sealed class BeamStateMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.BeamState;

        public byte Version => CurrentVersion;

        public int ShooterId { get; set; }

        // ビームの見た目と音を武器から選ぶため、撃った人の今の武器に頼らずに送る。
        public string WeaponId { get; set; } = "";

        public Vector3 Start { get; set; }

        public Vector3 End { get; set; }

        public bool Active { get; set; }

        public void Write(MessageWriter writer)
        {
            if (ShooterId < 0)
            {
                throw new InvalidOperationException("shooter id must not be negative");
            }

            writer.WriteInt32(ShooterId);
            writer.WriteString(WeaponId, MatchProtocol.MaxContentIdBytes);
            writer.WriteVector3(Start);
            writer.WriteVector3(End);
            writer.WriteBool(Active);
        }

        public void Read(MessageReader reader)
        {
            ShooterId = reader.ReadInt32AtLeast(0, "beam shooter");
            WeaponId = reader.ReadString(MatchProtocol.MaxContentIdBytes, WeaponId);
            Start = reader.ReadFiniteVector3("beam start");
            End = reader.ReadFiniteVector3("beam end");
            Active = reader.ReadBool();
        }
    }
}
