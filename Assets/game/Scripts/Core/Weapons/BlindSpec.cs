namespace Fps.Core
{
    public sealed class BlindSpec
    {
        public BlindSpec(float radius, float durationSeconds, float backFraction)
        {
            Radius = SpecGuard.PositiveFinite(radius, nameof(radius));
            DurationSeconds = SpecGuard.PositiveFinite(durationSeconds, nameof(durationSeconds));
            BackFraction = SpecGuard.Fraction(backFraction, nameof(backFraction));
        }

        public float Radius { get; }

        public float DurationSeconds { get; }

        public float BackFraction { get; }
    }
}
