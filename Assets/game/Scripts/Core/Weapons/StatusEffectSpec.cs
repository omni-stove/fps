namespace Shooter.Core
{
    public sealed class StatusEffectSpec
    {
        public StatusEffectSpec(
            float durationSeconds, float tickSeconds, int damagePerTick, string visualId = null, string kindId = StatusEffectKinds.Burn)
        {
            DurationSeconds = SpecGuard.PositiveFinite(durationSeconds, nameof(durationSeconds));
            TickSeconds = SpecGuard.PositiveFinite(tickSeconds, nameof(tickSeconds));
            // 極端に短い間隔だと StatusEffects.Tick が float の丸めで進まなくなる・1 回に何百万回もまわるため、下限を設ける。
            if (TickSeconds < MinTickSeconds)
            {
                throw new System.ArgumentOutOfRangeException(nameof(tickSeconds), tickSeconds, "tickSeconds must be at least " + MinTickSeconds);
            }

            DamagePerTick = SpecGuard.NonNegative(damagePerTick, nameof(damagePerTick));
            VisualId = SpecGuard.ReferenceId(visualId);
            var kind = SpecGuard.ReferenceId(kindId);
            KindId = kind.Length > 0 ? kind : StatusEffectKinds.Burn;
        }

        public const float MinTickSeconds = 0.01f;

        public float DurationSeconds { get; }

        public float TickSeconds { get; }

        public int DamagePerTick { get; }

        public string VisualId { get; }

        /// <summary>種類の Id (<see cref="StatusEffectKinds"/>)。同じ種類をかけ直すと、別に重ねずに延ばす。</summary>
        public string KindId { get; }
    }
}
