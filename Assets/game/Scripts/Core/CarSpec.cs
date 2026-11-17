using System;

namespace Shooter.Core
{
    public sealed class CarSpec
    {
        public const float DefaultMass = 1200f;
        public const float DefaultMotorTorque = 2400f;
        public const float DefaultBrakeTorque = 3000f;
        public const float DefaultIdleBrakeTorque = 300f;
        public const float DefaultMaxSteerAngle = 30f;
        public const float DefaultMaxForwardSpeed = 20f;
        public const float DefaultMaxReverseSpeed = 6f;
        public const float DefaultFlipAngle = 60f;
        public const float DefaultRightingMaxSpeed = 2f;
        public const float DefaultRightingLift = 1f;

        public const float MaxSteerAngleLimit = 89f;
        public const float FlipAngleLimit = 180f;

        public CarSpec(
            float mass = DefaultMass,
            float motorTorque = DefaultMotorTorque,
            float brakeTorque = DefaultBrakeTorque,
            float idleBrakeTorque = DefaultIdleBrakeTorque,
            float maxSteerAngle = DefaultMaxSteerAngle,
            float maxForwardSpeed = DefaultMaxForwardSpeed,
            float maxReverseSpeed = DefaultMaxReverseSpeed,
            float flipAngle = DefaultFlipAngle,
            float rightingMaxSpeed = DefaultRightingMaxSpeed,
            float rightingLift = DefaultRightingLift)
        {
            RequirePositive(mass, nameof(mass));
            RequireNonNegative(motorTorque, nameof(motorTorque));
            RequireNonNegative(brakeTorque, nameof(brakeTorque));
            RequireNonNegative(idleBrakeTorque, nameof(idleBrakeTorque));
            if (!(maxSteerAngle >= 0f && maxSteerAngle <= MaxSteerAngleLimit))
            {
                throw new ArgumentOutOfRangeException(nameof(maxSteerAngle), maxSteerAngle, "maxSteerAngle must be between 0 and 89");
            }

            RequireNonNegative(maxForwardSpeed, nameof(maxForwardSpeed));
            RequireNonNegative(maxReverseSpeed, nameof(maxReverseSpeed));
            if (!(flipAngle > 0f && flipAngle < FlipAngleLimit))
            {
                throw new ArgumentOutOfRangeException(nameof(flipAngle), flipAngle, "flipAngle must be greater than 0 and less than 180");
            }

            RequireNonNegative(rightingMaxSpeed, nameof(rightingMaxSpeed));
            RequireNonNegative(rightingLift, nameof(rightingLift));

            Mass = mass;
            MotorTorque = motorTorque;
            BrakeTorque = brakeTorque;
            IdleBrakeTorque = idleBrakeTorque;
            MaxSteerAngle = maxSteerAngle;
            MaxForwardSpeed = maxForwardSpeed;
            MaxReverseSpeed = maxReverseSpeed;
            FlipAngle = flipAngle;
            RightingMaxSpeed = rightingMaxSpeed;
            RightingLift = rightingLift;
        }

        public static CarSpec Default => new CarSpec();

        public float Mass { get; }

        public float MotorTorque { get; }

        public float BrakeTorque { get; }

        public float IdleBrakeTorque { get; }

        public float MaxSteerAngle { get; }

        public float MaxForwardSpeed { get; }

        public float MaxReverseSpeed { get; }

        public float FlipAngle { get; }

        public float RightingMaxSpeed { get; }

        public float RightingLift { get; }

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
