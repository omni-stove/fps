using System;

namespace Fps.Core
{
    public sealed class DeploySpec
    {
        public const int DefaultMaxActive = 2;

        public const DeployOverflow DefaultOverflow = DeployOverflow.ReplaceOldest;

        public const int DefaultMaxHealth = 300;

        public const float DefaultMaxRange = 4f;

        public const float DefaultMaxSlopeDegrees = 30f;

        public const float DefaultMaxStepHeight = 0.25f;

        public const float DefaultWidth = 2f;

        public const float DefaultHeight = 1.4f;

        public const float DefaultDepth = 0.2f;

        public static DeploySpec Default { get; } = new DeploySpec(
            DefaultMaxActive,
            DefaultOverflow,
            DefaultMaxHealth,
            DefaultMaxRange,
            DefaultMaxSlopeDegrees,
            DefaultMaxStepHeight,
            DefaultWidth,
            DefaultHeight,
            DefaultDepth);

        public DeploySpec(
            int maxActive,
            DeployOverflow overflow,
            int maxHealth,
            float maxRange,
            float maxSlopeDegrees,
            float maxStepHeight,
            float width,
            float height,
            float depth)
        {
            if (maxActive < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxActive), maxActive, "maxActive must be at least 1");
            }

            if (!Enum.IsDefined(typeof(DeployOverflow), overflow))
            {
                throw new ArgumentOutOfRangeException(nameof(overflow), overflow, "overflow is not a defined DeployOverflow");
            }

            if (maxHealth < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHealth), maxHealth, "maxHealth must be at least 1");
            }

            RequirePositive(maxRange, nameof(maxRange));
            if (!(maxSlopeDegrees >= 0f) || !(maxSlopeDegrees < 90f))
            {
                throw new ArgumentOutOfRangeException(nameof(maxSlopeDegrees), maxSlopeDegrees, "maxSlopeDegrees must be at least 0 and less than 90");
            }

            if (!(maxStepHeight >= 0f) || float.IsInfinity(maxStepHeight))
            {
                throw new ArgumentOutOfRangeException(nameof(maxStepHeight), maxStepHeight, "maxStepHeight must not be negative and must be finite");
            }

            RequirePositive(width, nameof(width));
            RequirePositive(height, nameof(height));
            RequirePositive(depth, nameof(depth));

            MaxActive = maxActive;
            Overflow = overflow;
            MaxHealth = maxHealth;
            MaxRange = maxRange;
            MaxSlopeDegrees = maxSlopeDegrees;
            MaxStepHeight = maxStepHeight;
            Width = width;
            Height = height;
            Depth = depth;
        }

        public int MaxActive { get; }

        public DeployOverflow Overflow { get; }

        public int MaxHealth { get; }

        // 足元からの水平距離。高低差は足場を探す側の都合で決まるので数えない。
        public float MaxRange { get; }

        public float MaxSlopeDegrees { get; }

        public float MaxStepHeight { get; }

        public float Width { get; }

        public float Height { get; }

        public float Depth { get; }

        private static void RequirePositive(float value, string paramName)
        {
            if (!(value > 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(paramName, value, paramName + " must be positive and finite");
            }
        }
    }
}
