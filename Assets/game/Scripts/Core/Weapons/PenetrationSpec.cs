namespace Fps.Core
{
    public sealed class PenetrationSpec
    {
        public PenetrationSpec(int maxPenetrations, float damageMultiplierPerPenetration, float maxThickness)
        {
            MaxPenetrations = SpecGuard.AtLeast(maxPenetrations, 1, nameof(maxPenetrations));
            DamageMultiplierPerPenetration = SpecGuard.Fraction(damageMultiplierPerPenetration, nameof(damageMultiplierPerPenetration));
            MaxThickness = SpecGuard.NonNegativeFinite(maxThickness, nameof(maxThickness));
        }

        public int MaxPenetrations { get; }

        public float DamageMultiplierPerPenetration { get; }

        public float MaxThickness { get; }
    }
}
