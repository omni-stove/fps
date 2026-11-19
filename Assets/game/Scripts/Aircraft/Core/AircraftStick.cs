using System;

namespace Shooter.Core
{
    public static class AircraftStick
    {
        // デッドゾーンの端から測り直すのは、端を越えた途端に飛んだ値から始まらず、0 から滑らかに効き始めるようにするため。
        public static float Shape(AircraftStickSpec spec, float value)
        {
            RequireSpec(spec);
            if (!(Math.Abs(value) <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "value must be between -1 and 1");
            }

            var magnitude = Math.Abs(value);
            if (magnitude <= spec.Deadzone)
            {
                return 0f;
            }

            var past = Math.Min(1f, (magnitude - spec.Deadzone) / (1f - spec.Deadzone));
            var shaped = (float)Math.Pow(past, spec.Exponent);
            return value < 0f ? -shaped : shaped;
        }

        public static float StepThrottle(AircraftStickSpec spec, float level, float target, float deltaSeconds)
        {
            RequireSpec(spec);
            if (!(level >= 0f && level <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(level), level, "level must be between 0 and 1");
            }

            if (!(target >= 0f && target <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(target), target, "target must be between 0 and 1");
            }

            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            var step = spec.ThrottleRate * deltaSeconds;
            return target > level ? Math.Min(target, level + step) : Math.Max(target, level - step);
        }

        public static VehicleMotorInput Compute(AircraftStickSpec spec, float pitchStick, float rollStick, float rudder, float throttle)
        {
            RequireSpec(spec);
            if (!(throttle >= 0f && throttle <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(throttle), throttle, "throttle must be between 0 and 1");
            }

            var pitch = Shape(spec, pitchStick);
            return VehicleMotorInput.Idle
                .With(AircraftFlight.ThrottleAxis, throttle)
                .With(AircraftFlight.PitchAxis, spec.ForwardPitchesDown ? -pitch : pitch)
                .With(AircraftFlight.RollAxis, Shape(spec, rollStick))
                .With(AircraftFlight.YawAxis, Shape(spec, rudder));
        }

        private static void RequireSpec(AircraftStickSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }
        }
    }
}
