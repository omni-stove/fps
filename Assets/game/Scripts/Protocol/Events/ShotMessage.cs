using System;
using System.Numerics;
using Fps.Core;

namespace Fps.Protocol
{
    /// <summary>
    /// サーバーが確定した 1 発。撃った人の近くにいるクライアントへ送り、弾道・着弾の演出を出させる。
    /// 撃った人がもう抜けていても演出は出す (<see cref="HasShooter"/> が false)。
    /// </summary>
    public sealed class ShotMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.Shot;

        public byte Version => CurrentVersion;

        public bool HasShooter { get; set; }

        public int ShooterId { get; set; }

        public string WeaponId { get; set; } = "";

        public Vector3 Origin { get; set; }

        public Vector3 Direction { get; set; }

        /// <summary>何かに当たったか。false なら着弾の値は使わない。</summary>
        public bool HasImpact { get; set; }

        public Vector3 ImpactPoint { get; set; }

        public Vector3 ImpactNormal { get; set; }

        /// <summary>当たった面の素材の Id (着弾の音と破片を選ぶ)。</summary>
        public string ImpactMaterialId { get; set; } = "";

        public bool ImpactIsCharacter { get; set; }

        /// <summary>キャラに当たった部位が分かるか。キャラ以外なら false。</summary>
        public bool HasImpactZone { get; set; }

        public HitZone ImpactZone { get; set; }

        public void Write(MessageWriter writer)
        {
            if (HasShooter && ShooterId < 0)
            {
                throw new InvalidOperationException("shooter id must not be negative");
            }

            if (HasImpactZone && !(HasImpact && ImpactIsCharacter))
            {
                throw new InvalidOperationException("only an impact on a character has a hit zone");
            }

            writer.WriteInt32(HasShooter ? ShooterId : PlayerIds.None);
            writer.WriteString(WeaponId, MatchProtocol.MaxContentIdBytes);
            writer.WriteVector3(Origin);
            writer.WriteVector3(Direction);
            writer.WriteBool(HasImpact);
            if (!HasImpact)
            {
                return;
            }

            writer.WriteVector3(ImpactPoint);
            writer.WriteVector3(ImpactNormal);
            writer.WriteString(ImpactMaterialId, MatchProtocol.MaxContentIdBytes);
            writer.WriteBool(ImpactIsCharacter);
            if (ImpactIsCharacter)
            {
                writer.WriteBool(HasImpactZone);
                writer.WriteByte(HasImpactZone ? (byte)ImpactZone : (byte)0);
            }
        }

        public void Read(MessageReader reader)
        {
            var shooter = reader.ReadInt32AtLeast(PlayerIds.None, "shooter");
            HasShooter = shooter != PlayerIds.None;
            ShooterId = HasShooter ? shooter : 0;
            WeaponId = reader.ReadString(MatchProtocol.MaxContentIdBytes, WeaponId);
            Origin = reader.ReadFiniteVector3("shot origin");
            Direction = reader.ReadFiniteVector3("shot direction");
            HasImpact = reader.ReadBool();
            ImpactIsCharacter = false;
            HasImpactZone = false;
            ImpactZone = default;
            if (!HasImpact)
            {
                ImpactPoint = default;
                ImpactNormal = default;
                return;
            }

            ImpactPoint = reader.ReadFiniteVector3("impact point");
            ImpactNormal = reader.ReadFiniteVector3("impact normal");
            ImpactMaterialId = reader.ReadString(MatchProtocol.MaxContentIdBytes, ImpactMaterialId);
            ImpactIsCharacter = reader.ReadBool();
            if (ImpactIsCharacter)
            {
                HasImpactZone = reader.ReadBool();
                ImpactZone = (HitZone)reader.ReadByteBelow(EnumRange<HitZone>.Count, "impact zone");
            }
        }
    }
}
