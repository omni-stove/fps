namespace Shooter.Core
{
    public sealed class AimAssistSpec
    {
        public const float DefaultSlowdownDegrees = 5f;
        public const float DefaultSlowdownScale = 0.5f;
        public const float DefaultMagnetismDegrees = 3f;
        public const float DefaultMagnetismDegreesPerSecond = 30f;
        public const float DefaultMagnetismSeconds = 0.3f;
        public const float DefaultMaxDistance = 50f;

        public static AimAssistSpec Default { get; } = new AimAssistSpec();

        public float SlowdownDegrees { get; }

        public float SlowdownScale { get; }

        public float MagnetismDegrees { get; }

        // 割合でなく角速度で持つのは、刻みの長さで吸い付く速さが変わらず、狙いを越えて振り回さないため。
        public float MagnetismDegreesPerSecond { get; }

        public float MagnetismSeconds { get; }

        public float MaxDistance { get; }

        public AimAssistSpec(
            float slowdownDegrees = DefaultSlowdownDegrees,
            float slowdownScale = DefaultSlowdownScale,
            float magnetismDegrees = DefaultMagnetismDegrees,
            float magnetismDegreesPerSecond = DefaultMagnetismDegreesPerSecond,
            float magnetismSeconds = DefaultMagnetismSeconds,
            float maxDistance = DefaultMaxDistance)
        {
            SlowdownDegrees = SpecGuard.Angle(slowdownDegrees, nameof(slowdownDegrees));
            SlowdownScale = SpecGuard.Fraction(slowdownScale, nameof(slowdownScale));
            MagnetismDegrees = SpecGuard.Angle(magnetismDegrees, nameof(magnetismDegrees));
            MagnetismDegreesPerSecond = SpecGuard.NonNegativeFinite(magnetismDegreesPerSecond, nameof(magnetismDegreesPerSecond));
            MagnetismSeconds = SpecGuard.NonNegativeFinite(magnetismSeconds, nameof(magnetismSeconds));
            MaxDistance = SpecGuard.PositiveFinite(maxDistance, nameof(maxDistance));
        }
    }
}
