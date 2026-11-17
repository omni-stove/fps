using System;
using System.Numerics;

namespace Shooter.Protocol
{
    public sealed class FireRequestMessage : IMessage
    {
        public const byte CurrentVersion = 2;

        public ushort Id => MessageIds.FireRequest;

        public byte Version => CurrentVersion;

        public string WeaponId { get; set; } = "";

        public Vector3 Origin { get; set; }

        public Vector3 Direction { get; set; }

        public int ClientTick { get; set; }

        public float InterpolationAlpha { get; set; }

        public int ShotIndex { get; set; }

        public int ShotGeneration { get; set; }

        public float ChargeFraction { get; set; }

        public byte AmmoTypeIndex { get; set; }

        public bool HasLockTarget { get; set; }

        public int LockTargetId { get; set; }

        // ビームや火炎のように押している間ずっと出る撃ち方で、押しっぱなしの間に 1 発ずつ要求を送らずに済むよう、押した・離した知らせを運ぶ。
        public TriggerEdge Trigger { get; set; }

        public void Write(MessageWriter writer)
        {
            if (HasLockTarget && LockTargetId < 0)
            {
                throw new InvalidOperationException("lock target id must not be negative");
            }

            writer.WriteString(WeaponId, MatchProtocol.MaxContentIdBytes);
            writer.WriteVector3(Origin);
            writer.WriteVector3(Direction);
            writer.WriteInt32(ClientTick);
            writer.WriteSingle(InterpolationAlpha);
            writer.WriteInt32(ShotIndex);
            writer.WriteInt32(ShotGeneration);
            writer.WriteSingle(ChargeFraction);
            writer.WriteByte(AmmoTypeIndex);
            writer.WriteInt32(HasLockTarget ? LockTargetId : PlayerIds.None);
            writer.WriteByte((byte)Trigger);
        }

        public void Read(MessageReader reader)
        {
            WeaponId = reader.ReadString(MatchProtocol.MaxContentIdBytes, WeaponId);
            Origin = reader.ReadFiniteVector3("shot origin");
            Direction = reader.ReadFiniteVector3("shot direction");
            ClientTick = reader.ReadInt32AtLeast(0, "client tick");
            InterpolationAlpha = reader.ReadFiniteSingle("interpolation alpha");
            ShotIndex = reader.ReadInt32AtLeast(0, "shot index");
            ShotGeneration = reader.ReadInt32AtLeast(0, "shot generation");
            ChargeFraction = reader.ReadFiniteSingleBetween(0f, 1f, "charge fraction");
            AmmoTypeIndex = reader.ReadByte();
            var lockTarget = reader.ReadInt32AtLeast(PlayerIds.None, "lock target");
            HasLockTarget = lockTarget != PlayerIds.None;
            LockTargetId = HasLockTarget ? lockTarget : 0;
            Trigger = (TriggerEdge)reader.ReadByteBelow(EnumRange<TriggerEdge>.Count, "trigger edge");
        }
    }
}
