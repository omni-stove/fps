namespace Fps.Core
{
    public sealed class RicochetSpec
    {
        public RicochetSpec(int maxBounces, float maxGrazingAngleDegrees, float damageMultiplierPerBounce)
        {
            MaxBounces = SpecGuard.AtLeast(maxBounces, 1, nameof(maxBounces));
            MaxGrazingAngleDegrees = SpecGuard.PositiveAngle(maxGrazingAngleDegrees, nameof(maxGrazingAngleDegrees));
            DamageMultiplierPerBounce = SpecGuard.Fraction(damageMultiplierPerBounce, nameof(damageMultiplierPerBounce));
        }

        public int MaxBounces { get; }

        public float MaxGrazingAngleDegrees { get; }

        public float DamageMultiplierPerBounce { get; }
    }
}
