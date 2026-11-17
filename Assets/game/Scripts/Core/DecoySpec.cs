using System;

namespace Shooter.Core
{
    public sealed class DecoySpec
    {
        public const float DefaultLifetimeSeconds = 20f;

        public const float DefaultCallIntervalSeconds = 2f;

        // 射撃場で隣の試し場所の敵まで届かない距離に収める。
        public const float DefaultCallRadius = 10f;

        // 置ける物の既定 (DeploySpec.Default*) は盾の形なので、囮の形と数はここに分けて持つ。
        public const int DefaultMaxActive = 1;

        public const int DefaultMaxHealth = 100;

        public const float DefaultWidth = 0.6f;

        public const float DefaultHeight = 1.8f;

        public const float DefaultDepth = 0.6f;

        public static DecoySpec Default { get; } = new DecoySpec(DefaultLifetimeSeconds, DefaultCallIntervalSeconds, DefaultCallRadius);

        public DecoySpec(float lifetimeSeconds, float callIntervalSeconds, float callRadius)
        {
            RequirePositive(lifetimeSeconds, nameof(lifetimeSeconds));
            RequirePositive(callIntervalSeconds, nameof(callIntervalSeconds));
            if (!(callRadius >= 0f) || float.IsInfinity(callRadius))
            {
                throw new ArgumentOutOfRangeException(nameof(callRadius), callRadius, "callRadius must not be negative and must be finite");
            }

            LifetimeSeconds = lifetimeSeconds;
            CallIntervalSeconds = callIntervalSeconds;
            CallRadius = callRadius;
        }

        public float LifetimeSeconds { get; }

        public float CallIntervalSeconds { get; }

        public float CallRadius { get; }

        private static void RequirePositive(float value, string paramName)
        {
            if (!(value > 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(paramName, value, paramName + " must be positive and finite");
            }
        }
    }
}
