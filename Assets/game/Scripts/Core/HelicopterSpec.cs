using System;

namespace Shooter.Core
{
    public sealed class HelicopterSpec
    {
        public const float DefaultMass = 3000f;
        public const float DefaultMaxSpeed = 30f;
        public const float DefaultHorizontalResponse = 1.5f;
        public const float DefaultMaxHorizontalAcceleration = 8f;
        public const float DefaultMaxClimbRate = 8f;
        public const float DefaultMaxDescentRate = 6f;
        public const float DefaultVerticalResponse = 3f;
        public const float DefaultMaxVerticalAcceleration = 6f;
        public const float DefaultGroundEffectHeight = 4f;
        public const float DefaultGroundEffectBoost = 0.5f;
        public const float DefaultMaxTiltAngle = 20f;
        public const float DefaultTiltStiffness = 40f;
        public const float DefaultTiltDamping = 12f;
        public const float DefaultMaxYawRate = 90f;
        public const float DefaultYawResponse = 4f;
        public const float DefaultYawAimAngle = 30f;
        public const float DefaultSpoolSeconds = 2f;

        public const float MaxTiltAngleLimit = 60f;
        public const float YawAimAngleLimit = 180f;

        public HelicopterSpec(
            float mass = DefaultMass,
            float maxSpeed = DefaultMaxSpeed,
            float horizontalResponse = DefaultHorizontalResponse,
            float maxHorizontalAcceleration = DefaultMaxHorizontalAcceleration,
            float maxClimbRate = DefaultMaxClimbRate,
            float maxDescentRate = DefaultMaxDescentRate,
            float verticalResponse = DefaultVerticalResponse,
            float maxVerticalAcceleration = DefaultMaxVerticalAcceleration,
            float groundEffectHeight = DefaultGroundEffectHeight,
            float groundEffectBoost = DefaultGroundEffectBoost,
            float maxTiltAngle = DefaultMaxTiltAngle,
            float tiltStiffness = DefaultTiltStiffness,
            float tiltDamping = DefaultTiltDamping,
            float maxYawRate = DefaultMaxYawRate,
            float yawResponse = DefaultYawResponse,
            float yawAimAngle = DefaultYawAimAngle,
            float spoolSeconds = DefaultSpoolSeconds)
        {
            RequirePositive(mass, nameof(mass));
            RequirePositive(maxSpeed, nameof(maxSpeed));
            RequireNonNegative(horizontalResponse, nameof(horizontalResponse));
            RequireNonNegative(maxHorizontalAcceleration, nameof(maxHorizontalAcceleration));
            RequireNonNegative(maxClimbRate, nameof(maxClimbRate));
            RequireNonNegative(maxDescentRate, nameof(maxDescentRate));
            RequireNonNegative(verticalResponse, nameof(verticalResponse));
            RequireNonNegative(maxVerticalAcceleration, nameof(maxVerticalAcceleration));
            RequireNonNegative(groundEffectHeight, nameof(groundEffectHeight));
            RequireNonNegative(groundEffectBoost, nameof(groundEffectBoost));
            if (!(maxTiltAngle >= 0f && maxTiltAngle <= MaxTiltAngleLimit))
            {
                throw new ArgumentOutOfRangeException(nameof(maxTiltAngle), maxTiltAngle, "maxTiltAngle must be between 0 and 60");
            }

            RequireNonNegative(tiltStiffness, nameof(tiltStiffness));
            RequireNonNegative(tiltDamping, nameof(tiltDamping));
            RequireNonNegative(maxYawRate, nameof(maxYawRate));
            RequireNonNegative(yawResponse, nameof(yawResponse));
            if (!(yawAimAngle > 0f && yawAimAngle <= YawAimAngleLimit))
            {
                throw new ArgumentOutOfRangeException(nameof(yawAimAngle), yawAimAngle, "yawAimAngle must be greater than 0 and at most 180");
            }

            RequireNonNegative(spoolSeconds, nameof(spoolSeconds));

            Mass = mass;
            MaxSpeed = maxSpeed;
            HorizontalResponse = horizontalResponse;
            MaxHorizontalAcceleration = maxHorizontalAcceleration;
            MaxClimbRate = maxClimbRate;
            MaxDescentRate = maxDescentRate;
            VerticalResponse = verticalResponse;
            MaxVerticalAcceleration = maxVerticalAcceleration;
            GroundEffectHeight = groundEffectHeight;
            GroundEffectBoost = groundEffectBoost;
            MaxTiltAngle = maxTiltAngle;
            TiltStiffness = tiltStiffness;
            TiltDamping = tiltDamping;
            MaxYawRate = maxYawRate;
            YawResponse = yawResponse;
            YawAimAngle = yawAimAngle;
            SpoolSeconds = spoolSeconds;
        }

        public static HelicopterSpec Default => new HelicopterSpec();

        public float Mass { get; }

        // 傾けた向きへの速さを力でなく狙う速さで持つのは、傾け切って最大速度に達したら押す力が 0 になり、それより速くならないようにするため。
        public float MaxSpeed { get; }

        public float HorizontalResponse { get; }

        public float MaxHorizontalAcceleration { get; }

        public float MaxClimbRate { get; }

        public float MaxDescentRate { get; }

        public float VerticalResponse { get; }

        public float MaxVerticalAcceleration { get; }

        public float GroundEffectHeight { get; }

        public float GroundEffectBoost { get; }

        public float MaxTiltAngle { get; }

        public float TiltStiffness { get; }

        public float TiltDamping { get; }

        public float MaxYawRate { get; }

        public float YawResponse { get; }

        public float YawAimAngle { get; }

        public float SpoolSeconds { get; }

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
    }
}
