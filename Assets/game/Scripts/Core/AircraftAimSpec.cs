using System;

namespace Shooter.Core
{
    public sealed class AircraftAimSpec
    {
        public const float DefaultPitchGain = 3f;
        public const float DefaultYawGain = 1f;
        public const float DefaultRollGain = 3f;
        public const float DefaultLevelGain = 1.5f;
        public const float DefaultThrottleRate = 0.5f;

        public AircraftAimSpec(
            float pitchGain = DefaultPitchGain,
            float yawGain = DefaultYawGain,
            float rollGain = DefaultRollGain,
            float levelGain = DefaultLevelGain,
            float throttleRate = DefaultThrottleRate)
        {
            RequireNonNegative(pitchGain, nameof(pitchGain));
            RequireNonNegative(yawGain, nameof(yawGain));
            RequireNonNegative(rollGain, nameof(rollGain));
            RequireNonNegative(levelGain, nameof(levelGain));
            RequireNonNegative(throttleRate, nameof(throttleRate));
            PitchGain = pitchGain;
            YawGain = yawGain;
            RollGain = rollGain;
            LevelGain = levelGain;
            ThrottleRate = throttleRate;
        }

        public static AircraftAimSpec Default => new AircraftAimSpec();

        public float PitchGain { get; }

        public float YawGain { get; }

        public float RollGain { get; }

        public float LevelGain { get; }

        public float ThrottleRate { get; }

        private static void RequireNonNegative(float value, string name)
        {
            if (!(value >= 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be a non-negative finite number");
            }
        }
    }
}
