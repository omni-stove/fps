using System;

namespace Shooter.Core
{
    public sealed class CoopHoldSpec
    {
        public const int DefaultRequiredCount = 2;

        public const float DefaultHoldSeconds = 3f;

        public const bool DefaultResetOnInterrupt = false;

        public static CoopHoldSpec Default { get; } = new CoopHoldSpec(DefaultRequiredCount, DefaultHoldSeconds, DefaultResetOnInterrupt);

        public CoopHoldSpec(int requiredCount, float holdSeconds, bool resetOnInterrupt = DefaultResetOnInterrupt)
        {
            if (requiredCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(requiredCount), requiredCount, "requiredCount must be at least 1");
            }

            if (!(holdSeconds >= 0f) || float.IsInfinity(holdSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(holdSeconds), holdSeconds, "holdSeconds must not be negative and must be finite");
            }

            RequiredCount = requiredCount;
            HoldSeconds = holdSeconds;
            ResetOnInterrupt = resetOnInterrupt;
        }

        public int RequiredCount { get; }

        public float HoldSeconds { get; }

        public bool ResetOnInterrupt { get; }
    }
}
