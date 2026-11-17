namespace Shooter.Core
{
    public sealed class AreaEffectSpec
    {
        public AreaEffectSpec(
            float radius,
            float durationSeconds,
            float tickSeconds,
            int damagePerTick,
            StatusEffectSpec statusEffect = null,
            string visualId = null)
        {
            Radius = SpecGuard.PositiveFinite(radius, nameof(radius));
            DurationSeconds = SpecGuard.PositiveFinite(durationSeconds, nameof(durationSeconds));
            TickSeconds = SpecGuard.PositiveFinite(tickSeconds, nameof(tickSeconds));
            DamagePerTick = SpecGuard.NonNegative(damagePerTick, nameof(damagePerTick));
            StatusEffect = statusEffect;
            VisualId = SpecGuard.ReferenceId(visualId);
        }

        public float Radius { get; }

        public float DurationSeconds { get; }

        public float TickSeconds { get; }

        public int DamagePerTick { get; }

        public StatusEffectSpec StatusEffect { get; }

        public string VisualId { get; }
    }
}
