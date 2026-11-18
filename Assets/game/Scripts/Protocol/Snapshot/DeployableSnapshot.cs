using System;
using System.Numerics;
using Shooter.Core;

namespace Shooter.Protocol
{
    public struct DeployableSnapshot : ISnapshotEntity<DeployableSnapshot>
    {
        public int DeployableId { get; set; }
        public DeployableFields Present { get; set; }

        // 種類 (Kind) と別に Id を持つのは、種類でクライアントの扱い (盾は弾を止める・囮は敵に見せる) を分け、
        // 見た目と値はデータの Id で引くため。
        public DeployableKind Kind { get; set; }

        public string TypeId { get; set; }

        public Team Team { get; set; }

        public bool HasOwner { get; set; }

        public int OwnerId { get; set; }

        public Vector3 Position { get; set; }

        public float Yaw { get; set; }

        public int Health { get; set; }

        public int EntityId => DeployableId;

        public int PresentBits => (int)Present;

        public int AllBits => (int)DeployableFields.All;

        public int OptionalBits => 0;

        public int ChangedBits(in DeployableSnapshot baseline)
        {
            var bits = DeployableFields.None;
            if (Kind != baseline.Kind
                || !string.Equals(TypeId ?? "", baseline.TypeId ?? "", StringComparison.Ordinal)
                || Team != baseline.Team
                || HasOwner != baseline.HasOwner
                || (HasOwner && OwnerId != baseline.OwnerId))
            {
                bits |= DeployableFields.Identity;
            }

            if (!Same.Bits(Position, baseline.Position) || !Same.Bits(Yaw, baseline.Yaw))
            {
                bits |= DeployableFields.Transform;
            }

            if (Health != baseline.Health)
            {
                bits |= DeployableFields.Health;
            }

            return (int)bits;
        }

        public void WriteBits(MessageWriter writer, int bits)
        {
            var fields = (DeployableFields)bits;
            if ((fields & DeployableFields.Identity) != 0)
            {
                if (HasOwner && OwnerId < 0)
                {
                    throw new InvalidOperationException("deployable owner id must not be negative");
                }

                writer.WriteByte((byte)Kind);
                writer.WriteString(TypeId, MatchProtocol.MaxContentIdBytes);
                writer.WriteByte((byte)Team);
                writer.WriteInt32(HasOwner ? OwnerId : PlayerIds.None);
            }

            if ((fields & DeployableFields.Transform) != 0)
            {
                writer.WriteVector3(Position);
                writer.WriteSingle(Yaw);
            }

            if ((fields & DeployableFields.Health) != 0)
            {
                if (Health < 0)
                {
                    throw new InvalidOperationException("deployable health must not be negative");
                }

                writer.WriteInt32(Health);
            }
        }

        public void ReadBits(MessageReader reader, int id, int bits)
        {
            DeployableId = id;
            var fields = (DeployableFields)bits;
            Present = fields;
            if ((fields & DeployableFields.Identity) != 0)
            {
                Kind = (DeployableKind)reader.ReadByteBelow(EnumRange<DeployableKind>.Count, "deployable kind");
                TypeId = reader.ReadString(MatchProtocol.MaxContentIdBytes, TypeId);
                Team = (Team)reader.ReadByteBelow(EnumRange<Team>.Count, "deployable team");
                var owner = reader.ReadInt32AtLeast(PlayerIds.None, "deployable owner");
                HasOwner = owner != PlayerIds.None;
                OwnerId = HasOwner ? owner : 0;
            }

            if ((fields & DeployableFields.Transform) != 0)
            {
                Position = reader.ReadFiniteVector3("deployable position");
                Yaw = reader.ReadFiniteSingle("deployable yaw");
            }

            if ((fields & DeployableFields.Health) != 0)
            {
                Health = reader.ReadInt32AtLeast(0, "deployable health");
            }
        }

        public void FillFrom(in DeployableSnapshot baseline)
        {
            var missing = DeployableFields.All & ~Present;
            if ((missing & DeployableFields.Identity) != 0)
            {
                Kind = baseline.Kind;
                TypeId = baseline.TypeId;
                Team = baseline.Team;
                HasOwner = baseline.HasOwner;
                OwnerId = baseline.OwnerId;
            }

            if ((missing & DeployableFields.Transform) != 0)
            {
                Position = baseline.Position;
                Yaw = baseline.Yaw;
            }

            if ((missing & DeployableFields.Health) != 0)
            {
                Health = baseline.Health;
            }

            Present = DeployableFields.All;
        }
    }
}
