using System;

namespace Shooter.Core
{
    public sealed class VehicleRouteSpec
    {
        public const float DefaultLookAheadDistance = 5f;
        public const float DefaultLookAheadSeconds = 0.5f;
        public const float DefaultArriveRadius = 3f;
        public const float DefaultFullSteerAngle = 35f;
        public const float DefaultCruiseSpeed = 12f;
        public const float DefaultCornerSpeed = 5f;
        public const float DefaultSharpCornerAngle = 90f;
        public const float DefaultDeceleration = 5f;
        public const float DefaultSpeedBand = 2f;
        public const float DefaultStuckSpeed = 0.5f;
        public const float DefaultStuckSeconds = 1.5f;
        public const float DefaultReverseSeconds = 1.5f;
        public const float DefaultReverseThrottle = 0.8f;

        public const float MaxAngle = 180f;

        public VehicleRouteSpec(
            float lookAheadDistance = DefaultLookAheadDistance,
            float lookAheadSeconds = DefaultLookAheadSeconds,
            float arriveRadius = DefaultArriveRadius,
            float fullSteerAngle = DefaultFullSteerAngle,
            float cruiseSpeed = DefaultCruiseSpeed,
            float cornerSpeed = DefaultCornerSpeed,
            float sharpCornerAngle = DefaultSharpCornerAngle,
            float deceleration = DefaultDeceleration,
            float speedBand = DefaultSpeedBand,
            float stuckSpeed = DefaultStuckSpeed,
            float stuckSeconds = DefaultStuckSeconds,
            float reverseSeconds = DefaultReverseSeconds,
            float reverseThrottle = DefaultReverseThrottle)
        {
            RequirePositive(lookAheadDistance, nameof(lookAheadDistance));
            RequireNonNegative(lookAheadSeconds, nameof(lookAheadSeconds));
            RequirePositive(arriveRadius, nameof(arriveRadius));
            RequireAngle(fullSteerAngle, nameof(fullSteerAngle));
            RequirePositive(cruiseSpeed, nameof(cruiseSpeed));
            RequireNonNegative(cornerSpeed, nameof(cornerSpeed));
            if (cornerSpeed > cruiseSpeed)
            {
                throw new ArgumentOutOfRangeException(nameof(cornerSpeed), cornerSpeed, "cornerSpeed must not exceed cruiseSpeed");
            }

            RequireAngle(sharpCornerAngle, nameof(sharpCornerAngle));
            RequirePositive(deceleration, nameof(deceleration));
            RequirePositive(speedBand, nameof(speedBand));
            RequireNonNegative(stuckSpeed, nameof(stuckSpeed));
            RequirePositive(stuckSeconds, nameof(stuckSeconds));
            RequirePositive(reverseSeconds, nameof(reverseSeconds));
            if (!(reverseThrottle > 0f && reverseThrottle <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(reverseThrottle), reverseThrottle, "reverseThrottle must be greater than 0 and at most 1");
            }

            LookAheadDistance = lookAheadDistance;
            LookAheadSeconds = lookAheadSeconds;
            ArriveRadius = arriveRadius;
            FullSteerAngle = fullSteerAngle;
            CruiseSpeed = cruiseSpeed;
            CornerSpeed = cornerSpeed;
            SharpCornerAngle = sharpCornerAngle;
            Deceleration = deceleration;
            SpeedBand = speedBand;
            StuckSpeed = stuckSpeed;
            StuckSeconds = stuckSeconds;
            ReverseSeconds = reverseSeconds;
            ReverseThrottle = reverseThrottle;
        }

        public static VehicleRouteSpec Default => new VehicleRouteSpec();

        public float LookAheadDistance { get; }

        public float LookAheadSeconds { get; }

        public float ArriveRadius { get; }

        public float FullSteerAngle { get; }

        public float CruiseSpeed { get; }

        public float CornerSpeed { get; }

        public float SharpCornerAngle { get; }

        public float Deceleration { get; }

        public float SpeedBand { get; }

        public float StuckSpeed { get; }

        public float StuckSeconds { get; }

        public float ReverseSeconds { get; }

        public float ReverseThrottle { get; }

        public float LookAheadAt(float speed)
        {
            return LookAheadDistance + (Math.Abs(speed) * LookAheadSeconds);
        }

        // 角の速さだけで抑えると角に着いてから落とし始めて間に合わないので、残りの距離で落とし切れる速さに抑える。
        public float SpeedToReach(float speedAtPoint, float distance)
        {
            return (float)Math.Sqrt((speedAtPoint * speedAtPoint) + (2f * Deceleration * Math.Max(0f, distance)));
        }

        public float CornerSpeedFor(float turnDegrees)
        {
            var t = Math.Min(1f, Math.Max(0f, Math.Abs(turnDegrees) / SharpCornerAngle));
            return CruiseSpeed + ((CornerSpeed - CruiseSpeed) * t);
        }

        private static void RequirePositive(float value, string name)
        {
            if (!(value > 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be positive and finite");
            }
        }

        private static void RequireNonNegative(float value, string name)
        {
            if (!(value >= 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be a non-negative finite number");
            }
        }

        private static void RequireAngle(float value, string name)
        {
            if (!(value > 0f && value <= MaxAngle))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be greater than 0 and at most 180");
            }
        }
    }
}
