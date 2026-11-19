using System;

namespace Shooter.Core
{
    public sealed class AircraftEffectsSpec
    {
        public const float DefaultElevatorAngle = 25f;
        public const float DefaultAileronAngle = 20f;
        public const float DefaultRudderAngle = 25f;
        public const float DefaultMinEnginePitch = 0.6f;
        public const float DefaultMaxEnginePitch = 1.6f;
        public const float DefaultMinEngineVolume = 0.3f;
        public const float DefaultMaxEngineVolume = 1f;
        public const float DefaultEngineSpeedShare = 0.3f;
        public const float DefaultVaporLoadFactor = 4f;

        public AircraftEffectsSpec(
            float elevatorAngle = DefaultElevatorAngle,
            float aileronAngle = DefaultAileronAngle,
            float rudderAngle = DefaultRudderAngle,
            float minEnginePitch = DefaultMinEnginePitch,
            float maxEnginePitch = DefaultMaxEnginePitch,
            float minEngineVolume = DefaultMinEngineVolume,
            float maxEngineVolume = DefaultMaxEngineVolume,
            float engineSpeedShare = DefaultEngineSpeedShare,
            float vaporLoadFactor = DefaultVaporLoadFactor)
        {
            RequireNonNegative(elevatorAngle, nameof(elevatorAngle));
            RequireNonNegative(aileronAngle, nameof(aileronAngle));
            RequireNonNegative(rudderAngle, nameof(rudderAngle));
            RequirePositive(minEnginePitch, nameof(minEnginePitch));
            RequirePositive(maxEnginePitch, nameof(maxEnginePitch));
            if (maxEnginePitch < minEnginePitch)
            {
                throw new ArgumentOutOfRangeException(nameof(maxEnginePitch), maxEnginePitch, "maxEnginePitch must not be below minEnginePitch");
            }

            RequireFraction(minEngineVolume, nameof(minEngineVolume));
            RequireFraction(maxEngineVolume, nameof(maxEngineVolume));
            if (maxEngineVolume < minEngineVolume)
            {
                throw new ArgumentOutOfRangeException(nameof(maxEngineVolume), maxEngineVolume, "maxEngineVolume must not be below minEngineVolume");
            }

            RequireFraction(engineSpeedShare, nameof(engineSpeedShare));
            RequireNonNegative(vaporLoadFactor, nameof(vaporLoadFactor));

            ElevatorAngle = elevatorAngle;
            AileronAngle = aileronAngle;
            RudderAngle = rudderAngle;
            MinEnginePitch = minEnginePitch;
            MaxEnginePitch = maxEnginePitch;
            MinEngineVolume = minEngineVolume;
            MaxEngineVolume = maxEngineVolume;
            EngineSpeedShare = engineSpeedShare;
            VaporLoadFactor = vaporLoadFactor;
        }

        public static AircraftEffectsSpec Default => new AircraftEffectsSpec();

        public float ElevatorAngle { get; }

        public float AileronAngle { get; }

        public float RudderAngle { get; }

        public float MinEnginePitch { get; }

        public float MaxEnginePitch { get; }

        public float MinEngineVolume { get; }

        public float MaxEngineVolume { get; }

        public float EngineSpeedShare { get; }

        public float VaporLoadFactor { get; }

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

        private static void RequireFraction(float value, string name)
        {
            if (!(value >= 0f && value <= 1f))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be between 0 and 1");
            }
        }
    }
}
