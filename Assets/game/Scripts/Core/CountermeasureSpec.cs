using System;

namespace Shooter.Core
{
    public sealed class CountermeasureSpec
    {
        public const int DefaultCount = 6;
        public const float DefaultIntervalSeconds = 1f;
        public const float DefaultLifetimeSeconds = 4f;
        public const float DefaultDecoyConeDegrees = 30f;
        public const float DefaultEjectSpeed = 12f;
        public const float DefaultGravityMultiplier = 0.3f;

        public CountermeasureSpec(
            int count = DefaultCount,
            float intervalSeconds = DefaultIntervalSeconds,
            float lifetimeSeconds = DefaultLifetimeSeconds,
            float decoyConeDegrees = DefaultDecoyConeDegrees,
            float ejectSpeed = DefaultEjectSpeed,
            float gravityMultiplier = DefaultGravityMultiplier)
        {
            Count = SpecGuard.NonNegative(count, nameof(count));
            IntervalSeconds = SpecGuard.NonNegativeFinite(intervalSeconds, nameof(intervalSeconds));
            LifetimeSeconds = SpecGuard.PositiveFinite(lifetimeSeconds, nameof(lifetimeSeconds));
            DecoyConeDegrees = SpecGuard.PositiveAngle(decoyConeDegrees, nameof(decoyConeDegrees));
            EjectSpeed = SpecGuard.NonNegativeFinite(ejectSpeed, nameof(ejectSpeed));
            if (!(gravityMultiplier >= 0f) || float.IsInfinity(gravityMultiplier))
            {
                throw new ArgumentOutOfRangeException(nameof(gravityMultiplier), gravityMultiplier, "gravityMultiplier must be a non-negative finite number");
            }

            GravityMultiplier = gravityMultiplier;
        }

        public static CountermeasureSpec Default => new CountermeasureSpec();

        public int Count { get; }

        public float IntervalSeconds { get; }

        public float LifetimeSeconds { get; }

        // 相手とフレアの距離でなく狙う側から見た角度で決めるのは、横へ外れた所から来る弾には、同じ距離のフレアでも見分けやすい向きがあるため。
        public float DecoyConeDegrees { get; }

        public float EjectSpeed { get; }

        public float GravityMultiplier { get; }
    }
}
