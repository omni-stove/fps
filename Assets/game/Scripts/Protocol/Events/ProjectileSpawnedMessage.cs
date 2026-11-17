using System;
using System.Numerics;

namespace Shooter.Protocol
{
    public sealed class ProjectileSpawnedMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.ProjectileSpawned;

        public byte Version => CurrentVersion;

        public int ProjectileId { get; set; }

        public string WeaponId { get; set; } = "";

        public bool HasShooter { get; set; }

        public int ShooterId { get; set; }

        public Vector3 Position { get; set; }

        public Vector3 Velocity { get; set; }

        public void Write(MessageWriter writer)
        {
            if (ProjectileId < 0)
            {
                throw new InvalidOperationException("projectile id must not be negative");
            }

            if (HasShooter && ShooterId < 0)
            {
                throw new InvalidOperationException("shooter id must not be negative");
            }

            writer.WriteInt32(ProjectileId);
            writer.WriteString(WeaponId, MatchProtocol.MaxContentIdBytes);
            writer.WriteInt32(HasShooter ? ShooterId : PlayerIds.None);
            writer.WriteVector3(Position);
            writer.WriteVector3(Velocity);
        }

        public void Read(MessageReader reader)
        {
            ProjectileId = reader.ReadInt32AtLeast(0, "projectile id");
            WeaponId = reader.ReadString(MatchProtocol.MaxContentIdBytes, WeaponId);
            var shooter = reader.ReadInt32AtLeast(PlayerIds.None, "projectile shooter");
            HasShooter = shooter != PlayerIds.None;
            ShooterId = HasShooter ? shooter : 0;
            Position = reader.ReadFiniteVector3("projectile position");
            Velocity = reader.ReadFiniteVector3("projectile velocity");
        }
    }
}
