namespace Shooter.Core
{
    public sealed class BreathHoldSpec
    {
        public BreathHoldSpec(float maxHoldSeconds, float recoverSeconds, float swayMultiplier)
        {
            MaxHoldSeconds = SpecGuard.PositiveFinite(maxHoldSeconds, nameof(maxHoldSeconds));
            RecoverSeconds = SpecGuard.NonNegativeFinite(recoverSeconds, nameof(recoverSeconds));
            SwayMultiplier = SpecGuard.Fraction(swayMultiplier, nameof(swayMultiplier));
        }

        public float MaxHoldSeconds { get; }

        public float RecoverSeconds { get; }

        public float SwayMultiplier { get; }
    }
}
