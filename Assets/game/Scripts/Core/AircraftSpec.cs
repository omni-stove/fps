using System;

namespace Shooter.Core
{
    public sealed class AircraftSpec
    {
        public const float DefaultMass = 8000f;
        public const float DefaultMaxThrust = 15f;
        public const float DefaultMaxSpeed = 90f;
        public const float DefaultLiftSpeed = 35f;
        public const float DefaultControlSpeed = 25f;
        public const float DefaultMinControl = 0.1f;
        public const float DefaultPitchRate = 60f;
        public const float DefaultRollRate = 120f;
        public const float DefaultYawRate = 25f;
        public const float DefaultResponsiveness = 4f;
        public const float DefaultAlignment = 2f;
        public const float DefaultAirBrakeDrag = 2f;

        public AircraftSpec(
            float mass = DefaultMass,
            float maxThrust = DefaultMaxThrust,
            float maxSpeed = DefaultMaxSpeed,
            float liftSpeed = DefaultLiftSpeed,
            float controlSpeed = DefaultControlSpeed,
            float minControl = DefaultMinControl,
            float pitchRate = DefaultPitchRate,
            float rollRate = DefaultRollRate,
            float yawRate = DefaultYawRate,
            float responsiveness = DefaultResponsiveness,
            float alignment = DefaultAlignment,
            float airBrakeDrag = DefaultAirBrakeDrag)
        {
            RequirePositive(mass, nameof(mass));
            RequirePositive(maxThrust, nameof(maxThrust));
            RequirePositive(maxSpeed, nameof(maxSpeed));
            RequirePositive(liftSpeed, nameof(liftSpeed));
            RequirePositive(controlSpeed, nameof(controlSpeed));
            if (!(minControl >= 0f && minControl <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(minControl), minControl, "minControl must be between 0 and 1");
            }

            RequireNonNegative(pitchRate, nameof(pitchRate));
            RequireNonNegative(rollRate, nameof(rollRate));
            RequireNonNegative(yawRate, nameof(yawRate));
            RequireNonNegative(responsiveness, nameof(responsiveness));
            RequireNonNegative(alignment, nameof(alignment));
            RequireNonNegative(airBrakeDrag, nameof(airBrakeDrag));

            Mass = mass;
            MaxThrust = maxThrust;
            MaxSpeed = maxSpeed;
            LiftSpeed = liftSpeed;
            ControlSpeed = controlSpeed;
            MinControl = minControl;
            PitchRate = pitchRate;
            RollRate = rollRate;
            YawRate = yawRate;
            Responsiveness = responsiveness;
            Alignment = alignment;
            AirBrakeDrag = airBrakeDrag;
        }

        public static AircraftSpec Default => new AircraftSpec();

        public float Mass { get; }

        // 力でなく加速 (m/s²) で持つのは、質量を変えても飛び方が変わらず、質量はぶつかったときの重さだけを決めるようにするため。
        public float MaxThrust { get; }

        public float MaxSpeed { get; }

        public float LiftSpeed { get; }

        public float ControlSpeed { get; }

        public float MinControl { get; }

        public float PitchRate { get; }

        public float RollRate { get; }

        public float YawRate { get; }

        public float Responsiveness { get; }

        public float Alignment { get; }

        public float AirBrakeDrag { get; }

        public float DragCoefficient => MaxThrust / (MaxSpeed * MaxSpeed);

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
