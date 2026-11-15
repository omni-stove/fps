namespace Fps.Core
{
    public sealed class StatusEffectSpec
    {
        public StatusEffectSpec(
            float durationSeconds, float tickSeconds, int damagePerTick, string visualId = null, string kindId = StatusEffectKinds.Burn)
        {
            DurationSeconds = SpecGuard.PositiveFinite(durationSeconds, nameof(durationSeconds));
            TickSeconds = SpecGuard.PositiveFinite(tickSeconds, nameof(tickSeconds));
            DamagePerTick = SpecGuard.NonNegative(damagePerTick, nameof(damagePerTick));
            VisualId = SpecGuard.ReferenceId(visualId);
            var kind = SpecGuard.ReferenceId(kindId);
            KindId = kind.Length > 0 ? kind : StatusEffectKinds.Burn;
        }

        public float DurationSeconds { get; }

        public float TickSeconds { get; }

        public int DamagePerTick { get; }

        public string VisualId { get; }

        /// <summary>種類の Id (<see cref="StatusEffectKinds"/>)。同じ種類をかけ直すと、別に重ねずに延ばす。</summary>
        public string KindId { get; }
    }
}
