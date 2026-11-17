using System;

namespace Shooter.Core
{
    public readonly struct ZonePhase
    {
        public float WaitSeconds { get; }

        public float ShrinkSeconds { get; }

        public float Radius { get; }

        public float DamagePerSecond { get; }

        public float DurationSeconds => WaitSeconds + ShrinkSeconds;

        public ZonePhase(float waitSeconds, float shrinkSeconds, float radius, float damagePerSecond)
        {
            if (!(waitSeconds >= 0f) || float.IsInfinity(waitSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(waitSeconds), waitSeconds, "waitSeconds must be a non-negative finite number");
            }

            if (!(shrinkSeconds >= 0f) || float.IsInfinity(shrinkSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(shrinkSeconds), shrinkSeconds, "shrinkSeconds must be a non-negative finite number");
            }

            if (!(radius > 0f) || float.IsInfinity(radius))
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "radius must be positive and finite");
            }

            if (!(damagePerSecond >= 0f) || float.IsInfinity(damagePerSecond))
            {
                throw new ArgumentOutOfRangeException(nameof(damagePerSecond), damagePerSecond, "damagePerSecond must be a non-negative finite number");
            }

            WaitSeconds = waitSeconds;
            ShrinkSeconds = shrinkSeconds;
            Radius = radius;
            DamagePerSecond = damagePerSecond;
        }
    }
}
