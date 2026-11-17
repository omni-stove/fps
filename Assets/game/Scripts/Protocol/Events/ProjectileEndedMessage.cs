using System;
using System.Numerics;

namespace Shooter.Protocol
{
    public sealed class ProjectileEndedMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.ProjectileEnded;

        public byte Version => CurrentVersion;

        public int ProjectileId { get; set; }

        public ProjectileEndReason Reason { get; set; }

        public Vector3 Position { get; set; }

        public void Write(MessageWriter writer)
        {
            if (ProjectileId < 0)
            {
                throw new InvalidOperationException("projectile id must not be negative");
            }

            writer.WriteInt32(ProjectileId);
            writer.WriteByte((byte)Reason);
            writer.WriteVector3(Position);
        }

        public void Read(MessageReader reader)
        {
            ProjectileId = reader.ReadInt32AtLeast(0, "projectile id");
            Reason = (ProjectileEndReason)reader.ReadByteBelow(EnumRange<ProjectileEndReason>.Count, "projectile end reason");
            Position = reader.ReadFiniteVector3("projectile end position");
        }
    }
}
