namespace Shooter.Core
{
    public sealed class HomingSpec
    {
        public HomingSpec(float lockOnSeconds, float lockOnConeDegrees, float turnDegreesPerSecond)
        {
            LockOnSeconds = SpecGuard.NonNegativeFinite(lockOnSeconds, nameof(lockOnSeconds));
            LockOnConeDegrees = SpecGuard.PositiveAngle(lockOnConeDegrees, nameof(lockOnConeDegrees));
            TurnDegreesPerSecond = SpecGuard.PositiveFinite(turnDegreesPerSecond, nameof(turnDegreesPerSecond));
        }

        public float LockOnSeconds { get; }

        public float LockOnConeDegrees { get; }

        public float TurnDegreesPerSecond { get; }
    }
}
