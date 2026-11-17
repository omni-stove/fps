using System;
using System.Numerics;

namespace Shooter.Protocol
{
    /// <remarks>
    /// 発射の知らせ (<see cref="ProjectileSpawnedMessage"/>) が落ちても、後から見えるようになった人にも弾が見えるよう、スナップショットにも並べる。
    /// </remarks>
    public struct ProjectileSnapshot : ISnapshotEntity<ProjectileSnapshot>
    {
        public int ProjectileId { get; set; }

        public ProjectileFields Present { get; set; }

        public bool HasShooter { get; set; }

        public int ShooterId { get; set; }

        public string WeaponId { get; set; }

        public Vector3 Position { get; set; }

        public Vector3 Velocity { get; set; }

        public int EntityId => ProjectileId;

        public int PresentBits => (int)Present;

        public int AllBits => (int)ProjectileFields.All;

        public int ChangedBits(in ProjectileSnapshot baseline)
        {
            var bits = ProjectileFields.None;
            if (HasShooter != baseline.HasShooter
                || (HasShooter && ShooterId != baseline.ShooterId)
                || !string.Equals(WeaponId ?? "", baseline.WeaponId ?? "", StringComparison.Ordinal))
            {
                bits |= ProjectileFields.Identity;
            }

            if (!Same.Bits(Position, baseline.Position) || !Same.Bits(Velocity, baseline.Velocity))
            {
                bits |= ProjectileFields.Motion;
            }

            return (int)bits;
        }

        public void WriteBits(MessageWriter writer, int bits)
        {
            var fields = (ProjectileFields)bits;
            if ((fields & ProjectileFields.Identity) != 0)
            {
                if (HasShooter && ShooterId < 0)
                {
                    throw new InvalidOperationException("shooter id must not be negative");
                }

                writer.WriteInt32(HasShooter ? ShooterId : PlayerIds.None);
                writer.WriteString(WeaponId, MatchProtocol.MaxContentIdBytes);
            }

            if ((fields & ProjectileFields.Motion) != 0)
            {
                writer.WriteVector3(Position);
                writer.WriteVector3(Velocity);
            }
        }

        public void ReadBits(MessageReader reader, int id, int bits)
        {
            ProjectileId = id;
            var fields = (ProjectileFields)bits;
            Present = fields;
            if ((fields & ProjectileFields.Identity) != 0)
            {
                var shooter = reader.ReadInt32AtLeast(PlayerIds.None, "projectile shooter");
                HasShooter = shooter != PlayerIds.None;
                ShooterId = HasShooter ? shooter : 0;
                WeaponId = reader.ReadString(MatchProtocol.MaxContentIdBytes, WeaponId);
            }

            if ((fields & ProjectileFields.Motion) != 0)
            {
                Position = reader.ReadFiniteVector3("projectile position");
                Velocity = reader.ReadFiniteVector3("projectile velocity");
            }
        }

        public void FillFrom(in ProjectileSnapshot baseline)
        {
            var missing = ProjectileFields.All & ~Present;
            if ((missing & ProjectileFields.Identity) != 0)
            {
                HasShooter = baseline.HasShooter;
                ShooterId = baseline.ShooterId;
                WeaponId = baseline.WeaponId;
            }

            if ((missing & ProjectileFields.Motion) != 0)
            {
                Position = baseline.Position;
                Velocity = baseline.Velocity;
            }

            Present = ProjectileFields.All;
        }
    }
}
