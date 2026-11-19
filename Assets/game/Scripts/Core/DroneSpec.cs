using System;

namespace Shooter.Core
{
    public sealed class DroneSpec
    {
        public const string DefaultNameKey = "deployable.reconDrone";

        public const float DefaultSpeed = 6f;

        // 置いた所からの距離で数え、射撃場の端から端までは届くが、マップの向こう側までは行けない長さにする。
        public const float DefaultControlRange = 40f;

        public const float DefaultBatterySeconds = 30f;

        public const float DefaultSpotRadius = 12f;

        public const float DefaultSpotIntervalSeconds = 1f;

        // 見つける間隔より長くし、見つけ続けている敵の印がその間に消えないようにする。
        public const float DefaultRevealSeconds = 3f;

        // 置く操作 (押している間に狙って離す) と見分けられる長さにし、狙う前に離した押し方だけを戻す操作とみなす (C4 の起爆と同じ)。
        public const float DefaultRecallTapSeconds = 0.25f;

        // 置ける物の既定 (DeploySpec.Default*) は盾の形なので、ドローンの形と数はここに分けて持つ。
        public const int DefaultMaxActive = 1;

        public const int DefaultMaxHealth = 40;

        public const float DefaultWidth = 0.5f;

        public const float DefaultHeight = 0.25f;

        public const float DefaultDepth = 0.5f;

        public static DroneSpec Default { get; } = new DroneSpec(
            DefaultSpeed, DefaultControlRange, DefaultBatterySeconds, DefaultSpotRadius, DefaultSpotIntervalSeconds, DefaultRevealSeconds);

        public DroneSpec(float speed, float controlRange, float batterySeconds, float spotRadius, float spotIntervalSeconds, float revealSeconds)
        {
            RequirePositive(speed, nameof(speed));
            RequirePositive(controlRange, nameof(controlRange));
            RequirePositive(batterySeconds, nameof(batterySeconds));
            RequirePositive(spotRadius, nameof(spotRadius));
            RequirePositive(spotIntervalSeconds, nameof(spotIntervalSeconds));
            RequirePositive(revealSeconds, nameof(revealSeconds));

            Speed = speed;
            ControlRange = controlRange;
            BatterySeconds = batterySeconds;
            SpotRadius = spotRadius;
            SpotIntervalSeconds = spotIntervalSeconds;
            RevealSeconds = revealSeconds;
        }

        public float Speed { get; }

        public float ControlRange { get; }

        public float BatterySeconds { get; }

        public float SpotRadius { get; }

        public float SpotIntervalSeconds { get; }

        public float RevealSeconds { get; }

        private static void RequirePositive(float value, string paramName)
        {
            if (!(value > 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(paramName, value, paramName + " must be positive and finite");
            }
        }
    }
}
