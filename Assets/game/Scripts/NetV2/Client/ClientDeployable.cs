using System;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    public readonly struct ClientDeployable
    {
        public ClientDeployable(int id, DeployableKind kind, string typeId, Team team, int? ownerId, Vector3 position, float yaw, int health)
        {
            Id = id;
            Kind = kind;
            TypeId = typeId ?? "";
            Team = team;
            OwnerId = ownerId;
            Position = position;
            Yaw = yaw;
            Health = health;
        }

        public int Id { get; }

        public DeployableKind Kind { get; }

        public string TypeId { get; }

        public Team Team { get; }

        public int? OwnerId { get; }

        public Vector3 Position { get; }

        public float Yaw { get; }

        public int Health { get; }

        public bool SameIdentity(ClientDeployable other)
        {
            return Kind == other.Kind
                && string.Equals(TypeId, other.TypeId, StringComparison.Ordinal)
                && Team == other.Team
                && OwnerId == other.OwnerId;
        }
    }
}
