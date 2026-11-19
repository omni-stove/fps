using System;

namespace Shooter.Core
{
    public sealed class AircraftAutopilotSpec
    {
        public const float DefaultCruiseSpeed = 55f;
        public const float DefaultCruiseThrottle = 0.5f;
        public const float DefaultSpeedGain = 0.05f;
        public const float DefaultMaxBankAngle = 40f;
        public const float DefaultHeadingGain = 1.5f;
        public const float DefaultRollGain = 0.02f;
        public const float DefaultYawGain = 0.005f;
        public const float DefaultMaxClimbAngle = 15f;
        public const float DefaultAltitudeGain = 0.3f;
        public const float DefaultPitchGain = 0.04f;
        public const float DefaultMaxLoadFactor = 5f;
        public const float DefaultLoadFactorMargin = 1.5f;
        public const float DefaultCriticalAngleOfAttack = 15f;
        public const float DefaultAngleOfAttackMargin = 5f;
        public const float DefaultMinGroundClearance = 40f;
        public const float DefaultGroundLookAheadSeconds = 3f;
        public const float DefaultArrivalRadius = 80f;

        // 90 度未満に限るのは、傾きと上昇の角の tan で旋回の半径や上がり方を見積もり、90 度ではそれが無限大になるため。
        public const float MaxAngle = 90f;

        public AircraftAutopilotSpec(
            float cruiseSpeed = DefaultCruiseSpeed,
            float cruiseThrottle = DefaultCruiseThrottle,
            float speedGain = DefaultSpeedGain,
            float maxBankAngle = DefaultMaxBankAngle,
            float headingGain = DefaultHeadingGain,
            float rollGain = DefaultRollGain,
            float yawGain = DefaultYawGain,
            float maxClimbAngle = DefaultMaxClimbAngle,
            float altitudeGain = DefaultAltitudeGain,
            float pitchGain = DefaultPitchGain,
            float maxLoadFactor = DefaultMaxLoadFactor,
            float loadFactorMargin = DefaultLoadFactorMargin,
            float criticalAngleOfAttack = DefaultCriticalAngleOfAttack,
            float angleOfAttackMargin = DefaultAngleOfAttackMargin,
            float minGroundClearance = DefaultMinGroundClearance,
            float groundLookAheadSeconds = DefaultGroundLookAheadSeconds,
            float arrivalRadius = DefaultArrivalRadius)
        {
            RequirePositive(cruiseSpeed, nameof(cruiseSpeed));
            if (!(cruiseThrottle >= 0f && cruiseThrottle <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(cruiseThrottle), cruiseThrottle, "cruiseThrottle must be between 0 and 1");
            }

            RequireNonNegative(speedGain, nameof(speedGain));
            RequireAngle(maxBankAngle, nameof(maxBankAngle));
            RequireNonNegative(headingGain, nameof(headingGain));
            RequireNonNegative(rollGain, nameof(rollGain));
            RequireNonNegative(yawGain, nameof(yawGain));
            RequireAngle(maxClimbAngle, nameof(maxClimbAngle));
            RequireNonNegative(altitudeGain, nameof(altitudeGain));
            RequireNonNegative(pitchGain, nameof(pitchGain));
            RequirePositive(maxLoadFactor, nameof(maxLoadFactor));
            RequireMargin(loadFactorMargin, maxLoadFactor, nameof(loadFactorMargin));
            if (!(criticalAngleOfAttack > 0f && criticalAngleOfAttack <= 180f))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(criticalAngleOfAttack), criticalAngleOfAttack, "criticalAngleOfAttack must be above 0 and at most 180 degrees");
            }

            RequireMargin(angleOfAttackMargin, criticalAngleOfAttack, nameof(angleOfAttackMargin));
            RequireNonNegative(minGroundClearance, nameof(minGroundClearance));
            RequireNonNegative(groundLookAheadSeconds, nameof(groundLookAheadSeconds));
            RequirePositive(arrivalRadius, nameof(arrivalRadius));

            CruiseSpeed = cruiseSpeed;
            CruiseThrottle = cruiseThrottle;
            SpeedGain = speedGain;
            MaxBankAngle = maxBankAngle;
            HeadingGain = headingGain;
            RollGain = rollGain;
            YawGain = yawGain;
            MaxClimbAngle = maxClimbAngle;
            AltitudeGain = altitudeGain;
            PitchGain = pitchGain;
            MaxLoadFactor = maxLoadFactor;
            LoadFactorMargin = loadFactorMargin;
            CriticalAngleOfAttack = criticalAngleOfAttack;
            AngleOfAttackMargin = angleOfAttackMargin;
            MinGroundClearance = minGroundClearance;
            GroundLookAheadSeconds = groundLookAheadSeconds;
            ArrivalRadius = arrivalRadius;
        }

        public static AircraftAutopilotSpec Default => new AircraftAutopilotSpec();

        public float CruiseSpeed { get; }

        // 速さの差だけで開き具合を決めないのは、巡航の速さちょうどで飛んでいるときにスロットルが 0 になり、そこから落ちるのを待つ間に高度を失うため。
        public float CruiseThrottle { get; }

        public float SpeedGain { get; }

        public float MaxBankAngle { get; }

        public float HeadingGain { get; }

        // 角のずれを 1 度あたりの舵の量で持つのは、AircraftAeroSpec と同じく、インスペクターで度のまま調整できるようにするため。
        public float RollGain { get; }

        public float YawGain { get; }

        public float MaxClimbAngle { get; }

        public float AltitudeGain { get; }

        public float PitchGain { get; }

        public float MaxLoadFactor { get; }

        public float LoadFactorMargin { get; }

        public float CriticalAngleOfAttack { get; }

        public float AngleOfAttackMargin { get; }

        public float MinGroundClearance { get; }

        // 今の高さだけで測らないのは、速く降りているときに、地面に近いと分かってからでは引き起こしが間に合わないため。
        public float GroundLookAheadSeconds { get; }

        // 高さを除いた距離で測るのは、決めた高さを保って飛ぶ機体が、地面に置いた点の真上を通っても着いたことにするため。
        public float ArrivalRadius { get; }

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
            if (!(value > 0f && value < MaxAngle))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be greater than 0 and less than 90 degrees");
            }
        }

        // 余裕を上限までに留めるのは、超えると、G や迎え角が 0 のまっすぐ飛んでいるときにもピッチが絞られるため。
        private static void RequireMargin(float margin, float limit, string name)
        {
            if (!(margin >= 0f && margin <= limit))
            {
                throw new ArgumentOutOfRangeException(name, margin, name + " must be between 0 and the limit it belongs to");
            }
        }
    }
}
