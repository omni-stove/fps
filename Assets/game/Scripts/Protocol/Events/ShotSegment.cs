using System;
using System.Numerics;
using Shooter.Core;

namespace Shooter.Protocol
{
    public struct ShotSegment
    {
        public Vector3 Origin { get; set; }

        public Vector3 Direction { get; set; }

        public bool HasImpact { get; set; }

        public Vector3 ImpactPoint { get; set; }

        public Vector3 ImpactNormal { get; set; }

        public string ImpactMaterialId { get; set; }

        public bool ImpactIsCharacter { get; set; }

        public bool HasImpactZone { get; set; }

        public HitZone ImpactZone { get; set; }

        internal void Write(MessageWriter writer)
        {
            if (HasImpactZone && !(HasImpact && ImpactIsCharacter))
            {
                throw new InvalidOperationException("only an impact on a character has a hit zone");
            }

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

        // 前に同じ位置にあった素材の Id を渡すのは、変わっていない文字列を作り直さないため。
        internal void Read(MessageReader reader)
        {
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
