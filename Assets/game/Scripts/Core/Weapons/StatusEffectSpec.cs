namespace Fps.Core
{
    public sealed class StatusEffectSpec
    {
        public StatusEffectSpec(float durationSeconds, float tickSeconds, int damagePerTick, string visualId = null)
        {
            DurationSeconds = SpecGuard.PositiveFinite(durationSeconds, nameof(durationSeconds));
            TickSeconds = SpecGuard.PositiveFinite(tickSeconds, nameof(tickSeconds));
            DamagePerTick = SpecGuard.NonNegative(damagePerTick, nameof(damagePerTick));
            VisualId = SpecGuard.ReferenceId(visualId);
        }

        public float DurationSeconds { get; }

        public float TickSeconds { get; }

        public int DamagePerTick { get; }

        public string VisualId { get; }
    }
}
