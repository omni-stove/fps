using System.Numerics;

namespace Shooter.Server
{
    internal readonly struct TrackingRound
    {
        public TrackingRound(int projectileId, int shooterId, ServerLockTarget target, Vector3 position)
        {
            ProjectileId = projectileId;
            ShooterId = shooterId;
            Target = target;
            Position = position;
        }

        public int ProjectileId { get; }

        public int ShooterId { get; }

        public ServerLockTarget Target { get; }

        public Vector3 Position { get; }
    }
}
