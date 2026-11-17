namespace Shooter.Core
{
    public sealed class ProjectileSpec
    {
        public ProjectileSpec(
            float speed,
            float gravityMultiplier,
            float lifetimeSeconds,
            WorldHitResponse onWorldHit = WorldHitResponse.Stop,
            string visualId = null)
        {
            Speed = SpecGuard.PositiveFinite(speed, nameof(speed));
            GravityMultiplier = SpecGuard.NonNegativeFinite(gravityMultiplier, nameof(gravityMultiplier));
            LifetimeSeconds = SpecGuard.PositiveFinite(lifetimeSeconds, nameof(lifetimeSeconds));
            OnWorldHit = SpecGuard.Defined(onWorldHit, nameof(onWorldHit));
            VisualId = SpecGuard.ReferenceId(visualId);
        }

        public float Speed { get; }

        public float GravityMultiplier { get; }

        public float LifetimeSeconds { get; }

        public WorldHitResponse OnWorldHit { get; }

        public string VisualId { get; }
    }
}
