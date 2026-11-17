using System;

namespace Shooter.Core
{
    public sealed class LadderSpec
    {
        public const float DefaultClimbSpeed = 2.5f;

        public const float DefaultGrabDistance = 0.4f;

        public const float DefaultJumpOffSpeed = 4f;

        public const float DefaultRegrabSeconds = 0.4f;

        public static readonly LadderSpec Default = new LadderSpec(
            DefaultClimbSpeed, DefaultGrabDistance, DefaultJumpOffSpeed, DefaultRegrabSeconds);

        public LadderSpec(float climbSpeed, float grabDistance, float jumpOffSpeed, float regrabSeconds)
        {
            if (!(climbSpeed > 0f) || float.IsInfinity(climbSpeed))
            {
                throw new ArgumentOutOfRangeException(nameof(climbSpeed), climbSpeed, "climbSpeed must be positive and finite");
            }

            RequireNonNegative(grabDistance, nameof(grabDistance));
            RequireNonNegative(jumpOffSpeed, nameof(jumpOffSpeed));
            RequireNonNegative(regrabSeconds, nameof(regrabSeconds));

            ClimbSpeed = climbSpeed;
            GrabDistance = grabDistance;
            JumpOffSpeed = jumpOffSpeed;
            RegrabSeconds = regrabSeconds;
        }

        public float ClimbSpeed { get; }

        public float GrabDistance { get; }

        public float JumpOffSpeed { get; }

        public float RegrabSeconds { get; }

        private static void RequireNonNegative(float value, string paramName)
        {
            if (!(value >= 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(paramName, value, paramName + " must be finite and not negative");
            }
        }
    }
}
