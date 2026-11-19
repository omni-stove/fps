using System;

namespace Shooter.Core
{
    public sealed class AircraftStickSpec
    {
        public const float DefaultDeadzone = 0.1f;
        public const float DefaultExponent = 2f;
        public const float DefaultThrottleRate = 0.5f;
        public const bool DefaultForwardPitchesDown = true;

        public AircraftStickSpec(
            float deadzone = DefaultDeadzone,
            float exponent = DefaultExponent,
            float throttleRate = DefaultThrottleRate,
            bool forwardPitchesDown = DefaultForwardPitchesDown)
        {
            if (!(deadzone >= 0f && deadzone < 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(deadzone), deadzone, "deadzone must be at least 0 and less than 1");
            }

            if (!(exponent >= 1f) || float.IsInfinity(exponent))
            {
                throw new ArgumentOutOfRangeException(nameof(exponent), exponent, "exponent must be a finite number of at least 1");
            }

            if (!(throttleRate > 0f) || float.IsInfinity(throttleRate))
            {
                throw new ArgumentOutOfRangeException(nameof(throttleRate), throttleRate, "throttleRate must be a positive finite number");
            }

            Deadzone = deadzone;
            Exponent = exponent;
            ThrottleRate = throttleRate;
            ForwardPitchesDown = forwardPitchesDown;
        }

        public static AircraftStickSpec Default => new AircraftStickSpec();

        public float Deadzone { get; }

        public float Exponent { get; }

        public float ThrottleRate { get; }

        public bool ForwardPitchesDown { get; }
    }
}
