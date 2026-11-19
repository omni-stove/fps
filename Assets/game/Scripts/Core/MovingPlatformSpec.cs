using System;

namespace Shooter.Core
{
    public sealed class MovingPlatformSpec
    {
        public const PlatformRouteKind DefaultKind = PlatformRouteKind.PingPong;

        public const float DefaultSpeed = 2f;

        public const float DefaultWaitSeconds = 1f;

        public const float DefaultSpinDegreesPerSecond = 0f;

        public const bool DefaultDepartWhenBoarded = true;

        public static readonly MovingPlatformSpec Default = new MovingPlatformSpec(
            DefaultKind, DefaultSpeed, DefaultWaitSeconds, DefaultSpinDegreesPerSecond, DefaultDepartWhenBoarded);

        public MovingPlatformSpec(
            PlatformRouteKind kind, float speed, float waitSeconds, float spinDegreesPerSecond, bool departWhenBoarded)
        {
            if (!Enum.IsDefined(typeof(PlatformRouteKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "kind is not a defined PlatformRouteKind");
            }

            if (!(speed >= 0f) || float.IsInfinity(speed))
            {
                throw new ArgumentOutOfRangeException(nameof(speed), speed, "speed must be finite and not negative");
            }

            if (!(waitSeconds >= 0f) || float.IsInfinity(waitSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(waitSeconds), waitSeconds, "waitSeconds must be finite and not negative");
            }

            if (float.IsNaN(spinDegreesPerSecond) || float.IsInfinity(spinDegreesPerSecond))
            {
                throw new ArgumentOutOfRangeException(nameof(spinDegreesPerSecond), spinDegreesPerSecond, "spinDegreesPerSecond must be finite");
            }

            Kind = kind;
            Speed = speed;
            WaitSeconds = waitSeconds;
            SpinDegreesPerSecond = spinDegreesPerSecond;
            DepartWhenBoarded = departWhenBoarded;
        }

        public PlatformRouteKind Kind { get; }

        public float Speed { get; }

        public float WaitSeconds { get; }

        public float SpinDegreesPerSecond { get; }

        public bool DepartWhenBoarded { get; }
    }
}
