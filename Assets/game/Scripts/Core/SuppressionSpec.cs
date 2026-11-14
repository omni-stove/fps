using System;

namespace Fps.Core
{
    public sealed class SuppressionSpec
    {
        public const float DefaultNearMissRadius = 1.5f;
        public const float DefaultBuildPerNearMiss = 0.2f;
        public const float DefaultDecayDelaySeconds = 0.75f;
        public const float DefaultDecayPerSecond = 0.4f;
        public const float DefaultSuppressedLevel = 0.5f;
        public const float DefaultReleasedLevel = 0.2f;
        public const float DefaultMaxSpreadDegrees = 4f;
        public const float DefaultMaxCrosshairSpread = 0.6f;
        public const float DefaultMaxVignetteAlpha = 0.65f;

        public static SuppressionSpec Default { get; } = new SuppressionSpec();

        public float NearMissRadius { get; }

        // 体の中心をかすめた (距離 0 の) 1 発の量。半径の端へ向けて 0 まで減らすので、遠い弾ほど効かない。
        public float BuildPerNearMiss { get; }

        public float DecayDelaySeconds { get; }

        public float DecayPerSecond { get; }

        public float SuppressedLevel { get; }

        // SuppressedLevel と別に持つのは、境目で制圧と解除を毎 tick 行き来させないため。
        public float ReleasedLevel { get; }

        public float MaxSpreadDegrees { get; }

        public float MaxCrosshairSpread { get; }

        public float MaxVignetteAlpha { get; }

        /// <exception cref="ArgumentOutOfRangeException">値が範囲外のとき。</exception>
        public SuppressionSpec(
            float nearMissRadius = DefaultNearMissRadius,
            float buildPerNearMiss = DefaultBuildPerNearMiss,
            float decayDelaySeconds = DefaultDecayDelaySeconds,
            float decayPerSecond = DefaultDecayPerSecond,
            float suppressedLevel = DefaultSuppressedLevel,
            float releasedLevel = DefaultReleasedLevel,
            float maxSpreadDegrees = DefaultMaxSpreadDegrees,
            float maxCrosshairSpread = DefaultMaxCrosshairSpread,
            float maxVignetteAlpha = DefaultMaxVignetteAlpha)
        {
            RequirePositive(nearMissRadius, nameof(nearMissRadius));
            RequirePositive(buildPerNearMiss, nameof(buildPerNearMiss));
            RequireNonNegative(decayDelaySeconds, nameof(decayDelaySeconds));
            RequirePositive(decayPerSecond, nameof(decayPerSecond));

            if (!(suppressedLevel > 0f) || suppressedLevel > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(suppressedLevel), suppressedLevel, "suppressedLevel must be greater than 0 and at most 1");
            }

            if (!(releasedLevel >= 0f) || releasedLevel >= suppressedLevel)
            {
                throw new ArgumentOutOfRangeException(nameof(releasedLevel), releasedLevel, "releasedLevel must be at least 0 and below suppressedLevel");
            }

            RequireNonNegative(maxSpreadDegrees, nameof(maxSpreadDegrees));
            RequireFraction(maxCrosshairSpread, nameof(maxCrosshairSpread));
            RequireFraction(maxVignetteAlpha, nameof(maxVignetteAlpha));

            NearMissRadius = nearMissRadius;
            BuildPerNearMiss = buildPerNearMiss;
            DecayDelaySeconds = decayDelaySeconds;
            DecayPerSecond = decayPerSecond;
            SuppressedLevel = suppressedLevel;
            ReleasedLevel = releasedLevel;
            MaxSpreadDegrees = maxSpreadDegrees;
            MaxCrosshairSpread = maxCrosshairSpread;
            MaxVignetteAlpha = maxVignetteAlpha;
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
                throw new ArgumentOutOfRangeException(name, value, name + " must be non-negative and finite");
            }
        }

        private static void RequireFraction(float value, string name)
        {
            if (!(value >= 0f) || value > 1f)
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be between 0 and 1");
            }
        }
    }
}
