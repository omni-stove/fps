using System;

namespace Fps.Core
{
    public sealed class NetBotSpec
    {
        // 既定値は Unity の NetTeamDeathmatch.Bots と同じ。NetTest (60m 四方) で、出会った相手と撃ち合える距離にしてある
        public const float DefaultSightRange = 40f;
        public const float DefaultAttackRange = 25f;
        public const float DefaultLoseSightSeconds = 2f;
        public const float DefaultSearchSeconds = 6f;
        public const float DefaultReactionSeconds = 0.6f;

        // 体の原点 (カプセルの中心) は床から 1m 上にあるので、NavMesh を探す半径はそれより広くとる
        public const float DefaultNavSampleRadius = 2.5f;
        public const float DefaultRepathSeconds = 0.5f;
        public const float DefaultRepathDistance = 1f;
        public const float DefaultStuckSeconds = 2f;
        public const float DefaultStuckProgress = 0.5f;

        public static NetBotSpec Default { get; } = new NetBotSpec(
            new EnemySpec(DefaultSightRange, DefaultAttackRange, DefaultLoseSightSeconds, DefaultSearchSeconds, DefaultReactionSeconds),
            BotInputSpec.Default,
            DefaultNavSampleRadius,
            DefaultRepathSeconds,
            DefaultRepathDistance,
            DefaultStuckSeconds,
            DefaultStuckProgress);

        public NetBotSpec(
            EnemySpec brain,
            BotInputSpec input,
            float navSampleRadius,
            float repathSeconds,
            float repathDistance,
            float stuckSeconds,
            float stuckProgress)
        {
            Brain = brain ?? throw new ArgumentNullException(nameof(brain));
            Input = input ?? throw new ArgumentNullException(nameof(input));
            if (!(navSampleRadius > 0f) || float.IsInfinity(navSampleRadius))
            {
                throw new ArgumentOutOfRangeException(nameof(navSampleRadius), navSampleRadius, "navSampleRadius must be positive and finite");
            }

            RequireNonNegative(repathSeconds, nameof(repathSeconds));
            RequireNonNegative(repathDistance, nameof(repathDistance));
            if (!(stuckSeconds > 0f) || float.IsInfinity(stuckSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(stuckSeconds), stuckSeconds, "stuckSeconds must be positive and finite");
            }

            RequireNonNegative(stuckProgress, nameof(stuckProgress));
            NavSampleRadius = navSampleRadius;
            RepathSeconds = repathSeconds;
            RepathDistance = repathDistance;
            StuckSeconds = stuckSeconds;
            StuckProgress = stuckProgress;
        }

        public EnemySpec Brain { get; }

        public BotInputSpec Input { get; }

        public float NavSampleRadius { get; }

        public float RepathSeconds { get; }

        public float RepathDistance { get; }

        public float StuckSeconds { get; }

        public float StuckProgress { get; }

        private static void RequireNonNegative(float value, string name)
        {
            if (!(value >= 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be finite and not negative");
            }
        }
    }
}
