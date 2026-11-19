using System;

namespace Shooter.Core
{
    public sealed class AircraftGroundSpec
    {
        public const float DefaultMaxSteerAngle = 40f;
        public const float DefaultHighSpeedSteerAngle = 5f;
        public const float DefaultSteerFadeSpeed = 25f;
        public const float DefaultBrakeTorque = 20000f;
        public const float DefaultIdleBrakeTorque = 5000f;
        public const float DefaultHeightProbeDistance = 200f;

        public const float MaxSteerAngleLimit = 89f;

        public AircraftGroundSpec(
            float maxSteerAngle = DefaultMaxSteerAngle,
            float highSpeedSteerAngle = DefaultHighSpeedSteerAngle,
            float steerFadeSpeed = DefaultSteerFadeSpeed,
            float brakeTorque = DefaultBrakeTorque,
            float idleBrakeTorque = DefaultIdleBrakeTorque,
            float heightProbeDistance = DefaultHeightProbeDistance)
        {
            if (!(maxSteerAngle >= 0f && maxSteerAngle <= MaxSteerAngleLimit))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxSteerAngle), maxSteerAngle, "maxSteerAngle must be between 0 and " + MaxSteerAngleLimit);
            }

            if (!(highSpeedSteerAngle >= 0f && highSpeedSteerAngle <= maxSteerAngle))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(highSpeedSteerAngle), highSpeedSteerAngle, "highSpeedSteerAngle must be between 0 and maxSteerAngle");
            }

            RequirePositive(steerFadeSpeed, nameof(steerFadeSpeed));
            RequireNonNegative(brakeTorque, nameof(brakeTorque));
            RequireNonNegative(idleBrakeTorque, nameof(idleBrakeTorque));
            RequirePositive(heightProbeDistance, nameof(heightProbeDistance));

            MaxSteerAngle = maxSteerAngle;
            HighSpeedSteerAngle = highSpeedSteerAngle;
            SteerFadeSpeed = steerFadeSpeed;
            BrakeTorque = brakeTorque;
            IdleBrakeTorque = idleBrakeTorque;
            HeightProbeDistance = heightProbeDistance;
        }

        public static AircraftGroundSpec Default => new AircraftGroundSpec();

        public float MaxSteerAngle { get; }

        public float HighSpeedSteerAngle { get; }

        public float SteerFadeSpeed { get; }

        public float BrakeTorque { get; }

        public float IdleBrakeTorque { get; }

        public float HeightProbeDistance { get; }

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
