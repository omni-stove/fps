using System;

namespace Shooter.Core
{
    public sealed class AircraftAeroSpec
    {
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
        public const float DefaultLiftSlope = 0.08f;

        // 18 度にするのは、試走場の機体が機首を 15 度上げて離陸するとき、臨界角に余裕を残したまま 35 m/s の少し上で浮くようにするため。
        public const float DefaultCriticalAngle = 18f;
        public const float DefaultPostStallLift = 0.6f;
        public const float DefaultInducedDrag = 0.05f;
        public const float DefaultWeathervane = 3f;
        public const float DefaultDihedral = 1.5f;
        public const float DefaultWingRoll = 0.5f;

        // 90 度未満に限るのは、失速後の係数を平板の形 (迎え角の 2 倍の sin) で出しており、90 度では係数が 0 に戻って臨界角の意味が無くなるため。
        public const float MaxCriticalAngle = 90f;

        public AircraftAeroSpec(
            float maxSpeed = DefaultMaxSpeed,
            float liftSpeed = DefaultLiftSpeed,
            float controlSpeed = DefaultControlSpeed,
            float minControl = DefaultMinControl,
            float pitchRate = DefaultPitchRate,
            float rollRate = DefaultRollRate,
            float yawRate = DefaultYawRate,
            float responsiveness = DefaultResponsiveness,
            float alignment = DefaultAlignment,
            float airBrakeDrag = DefaultAirBrakeDrag,
            float liftSlope = DefaultLiftSlope,
            float criticalAngle = DefaultCriticalAngle,
            float postStallLift = DefaultPostStallLift,
            float inducedDrag = DefaultInducedDrag,
            float weathervane = DefaultWeathervane,
            float dihedral = DefaultDihedral,
            float wingRoll = DefaultWingRoll)
        {
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
            RequirePositive(liftSlope, nameof(liftSlope));
            if (!(criticalAngle > 0f && criticalAngle < MaxCriticalAngle))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(criticalAngle), criticalAngle, "criticalAngle must be greater than 0 and less than 90 degrees");
            }

            RequireNonNegative(postStallLift, nameof(postStallLift));
            RequireNonNegative(inducedDrag, nameof(inducedDrag));
            RequireNonNegative(weathervane, nameof(weathervane));
            RequireNonNegative(dihedral, nameof(dihedral));
            RequireNonNegative(wingRoll, nameof(wingRoll));

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
            LiftSlope = liftSlope;
            CriticalAngle = criticalAngle;
            PostStallLift = postStallLift;
            InducedDrag = inducedDrag;
            Weathervane = weathervane;
            Dihedral = dihedral;
            WingRoll = wingRoll;
        }

        public static AircraftAeroSpec Default => new AircraftAeroSpec();

        public float MaxSpeed { get; }

        // 翼の大きさ (揚力の式の翼の値) を別の値で持たず、この速さ・臨界角で重さと釣り合うように決めるのは、「どの速さで浮くか」だけを調整すれば、翼の面積を知らなくても飛べる機体になるため。
        public float LiftSpeed { get; }

        public float ControlSpeed { get; }

        public float MinControl { get; }

        public float PitchRate { get; }

        public float RollRate { get; }

        public float YawRate { get; }

        public float Responsiveness { get; }

        public float Alignment { get; }

        public float AirBrakeDrag { get; }

        // ラジアンでなく 1 度あたりで持つのは、臨界角や舵の速さと同じく度で調整できるようにするため。
        public float LiftSlope { get; }

        public float CriticalAngle { get; }

        public float PostStallLift { get; }

        public float InducedDrag { get; }

        public float Weathervane { get; }

        public float Dihedral { get; }

        public float WingRoll { get; }

        public float MaxLiftCoefficient => LiftSlope * CriticalAngle;

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
