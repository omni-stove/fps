using System;
using System.Numerics;

namespace Fps.Protocol
{
    /// <summary>全員に送る爆発。投げた人がもう抜けていても演出は出す (<see cref="HasThrower"/> が false)。</summary>
    public sealed class ExplosionMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.Explosion;

        public byte Version => CurrentVersion;

        public Vector3 Center { get; set; }

        public bool HasThrower { get; set; }

        public int ThrowerId { get; set; }

        /// <summary>爆発した物の Id (爆発の見た目と音を選ぶ)。</summary>
        public string WeaponId { get; set; } = "";

        public void Write(MessageWriter writer)
        {
            if (HasThrower && ThrowerId < 0)
            {
                throw new InvalidOperationException("thrower id must not be negative");
            }

            writer.WriteVector3(Center);
            writer.WriteInt32(HasThrower ? ThrowerId : PlayerIds.None);
            writer.WriteString(WeaponId, MatchProtocol.MaxContentIdBytes);
        }

        public void Read(MessageReader reader)
        {
            Center = reader.ReadFiniteVector3("explosion center");
            var thrower = reader.ReadInt32AtLeast(PlayerIds.None, "thrower");
            HasThrower = thrower != PlayerIds.None;
            ThrowerId = HasThrower ? thrower : 0;
            WeaponId = reader.ReadString(MatchProtocol.MaxContentIdBytes, WeaponId);
        }
    }
}
