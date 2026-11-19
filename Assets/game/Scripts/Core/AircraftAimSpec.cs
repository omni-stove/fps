using System;

namespace Shooter.Core
{
    public sealed class AircraftAimSpec
    {
        public const float DefaultPitchGain = 3f;
        public const float DefaultYawGain = 1f;
        public const float DefaultRollGain = 3f;
        public const float DefaultLevelGain = 1.5f;
        public const float DefaultThrottleRate = 0.5f;
        public const float DefaultMaxLoadFactor = 7f;
        public const float DefaultLoadFactorMargin = 2f;
        public const float DefaultCriticalAngleOfAttack = 15f;
        public const float DefaultAngleOfAttackMargin = 5f;
        public const float DefaultGroundWarningSeconds = 3f;
        public const float DefaultMinSinkRate = 4f;

        public AircraftAimSpec(
            float pitchGain = DefaultPitchGain,
            float yawGain = DefaultYawGain,
            float rollGain = DefaultRollGain,
            float levelGain = DefaultLevelGain,
            float throttleRate = DefaultThrottleRate,
            float maxLoadFactor = DefaultMaxLoadFactor,
            float loadFactorMargin = DefaultLoadFactorMargin,
            float criticalAngleOfAttack = DefaultCriticalAngleOfAttack,
            float angleOfAttackMargin = DefaultAngleOfAttackMargin,
            float groundWarningSeconds = DefaultGroundWarningSeconds,
            float minSinkRate = DefaultMinSinkRate)
        {
            RequireNonNegative(pitchGain, nameof(pitchGain));
            RequireNonNegative(yawGain, nameof(yawGain));
            RequireNonNegative(rollGain, nameof(rollGain));
            RequireNonNegative(levelGain, nameof(levelGain));
            RequireNonNegative(throttleRate, nameof(throttleRate));
            RequirePositive(maxLoadFactor, nameof(maxLoadFactor));
            RequireMargin(loadFactorMargin, maxLoadFactor, nameof(loadFactorMargin));
            if (!(criticalAngleOfAttack > 0f && criticalAngleOfAttack <= 180f))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(criticalAngleOfAttack), criticalAngleOfAttack, "criticalAngleOfAttack must be above 0 and at most 180 degrees");
            }

            RequireMargin(angleOfAttackMargin, criticalAngleOfAttack, nameof(angleOfAttackMargin));
            RequireNonNegative(groundWarningSeconds, nameof(groundWarningSeconds));
            RequireNonNegative(minSinkRate, nameof(minSinkRate));

            PitchGain = pitchGain;
            YawGain = yawGain;
            RollGain = rollGain;
            LevelGain = levelGain;
            ThrottleRate = throttleRate;
            MaxLoadFactor = maxLoadFactor;
            LoadFactorMargin = loadFactorMargin;
            CriticalAngleOfAttack = criticalAngleOfAttack;
            AngleOfAttackMargin = angleOfAttackMargin;
            GroundWarningSeconds = groundWarningSeconds;
            MinSinkRate = minSinkRate;
        }

        public static AircraftAimSpec Default => new AircraftAimSpec();

        public float PitchGain { get; }

        public float YawGain { get; }

        public float RollGain { get; }

        public float LevelGain { get; }

        public float ThrottleRate { get; }

        public float MaxLoadFactor { get; }

        public float LoadFactorMargin { get; }

        // 度で持つのは、空力の値 (AircraftAeroSpec の回る速さ) と同じく、インスペクターで読める単位にそろえるため。
        public float CriticalAngleOfAttack { get; }

        public float AngleOfAttackMargin { get; }

        public float GroundWarningSeconds { get; }

        // 降りる速さに下限を設けるのは、着陸のためにゆっくり降りているときまで引き起こすと、地面に降りられなくなるため。
        public float MinSinkRate { get; }

        private static void RequireNonNegative(float value, string name)
        {
            if (!(value >= 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be a non-negative finite number");
            }
        }

        private static void RequirePositive(float value, string name)
        {
            if (!(value > 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be positive and finite");
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
