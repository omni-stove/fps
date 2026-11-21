using System;

namespace Shooter.Core
{
    public sealed class NetBotSpec
    {
        // NetTest (60m 四方) で、出会った相手と撃ち合える距離にしてある
        public const float DefaultSightRange = 40f;
        public const float DefaultAttackRange = 25f;
        public const float DefaultLoseSightSeconds = 2f;
        public const float DefaultSearchSeconds = 6f;
        public const float DefaultReactionSeconds = 0.6f;

        // 以下は bots.json で敵の判断を変えられるようにする前の bot と同じ値で、省いたデータの bot の挙動を変えない。
        // 撃ち始める距離を攻撃距離にそろえ、Attack になるまで撃たない
        public const float DefaultFireRange = DefaultAttackRange;

        // 狙いの合い方は BotDriver が照準の許し (BotInputSpec.FireToleranceDegrees) で見るので、体の向きでは止めない
        public const float DefaultFacingToleranceDegrees = EnemySpec.MaxFireToleranceDegrees;

        // 外し方は BotDriver の照準の誤差が持つので、散らばりを足さない
        public const float DefaultRangeSpreadDegrees = 0f;
        public const float DefaultOpeningSpreadDegrees = 0f;
        public const float DefaultSettledSpreadDegrees = 0f;
        public const int DefaultOpeningShots = 0;

        // 体の原点 (カプセルの中心) は床から 1m 上にあるので、NavMesh を探す半径はそれより広くとる
        public const float DefaultNavSampleRadius = 2.5f;
        public const float DefaultRepathSeconds = 0.5f;
        public const float DefaultRepathDistance = 1f;
        public const float DefaultStuckSeconds = 2f;
        public const float DefaultStuckProgress = 0.5f;

        // オフラインの HardpointRunner.RouteAround・PayloadRunner と同じ巡り方にして、オンラインとオフラインで Bot の集まり方を揃える
        public const float DefaultRallyRouteRadiusFraction = 0.5f;
        public const float DefaultRallyRouteAngleStepDegrees = 137.5f;
        public const float DefaultRallyMoveRadiusFraction = 0.5f;

        public static NetBotSpec Default { get; } = new NetBotSpec(
            new EnemySpec(
                DefaultSightRange,
                DefaultAttackRange,
                DefaultLoseSightSeconds,
                DefaultSearchSeconds,
                DefaultReactionSeconds,
                DefaultOpeningSpreadDegrees,
                DefaultSettledSpreadDegrees,
                DefaultOpeningShots,
                EnemySpec.DefaultCoverHideSeconds,
                EnemySpec.DefaultCoverPeekSeconds,
                EnemySpec.DefaultCoverHealthFraction,
                EnemyArchetype.Rifleman,
                fireRange: 0f,
                fireToleranceDegrees: DefaultFacingToleranceDegrees,
                rangeSpreadDegrees: DefaultRangeSpreadDegrees),
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
            float stuckProgress,
            float rallyRouteRadiusFraction = DefaultRallyRouteRadiusFraction,
            float rallyRouteAngleStepDegrees = DefaultRallyRouteAngleStepDegrees,
            float rallyMoveRadiusFraction = DefaultRallyMoveRadiusFraction,
            BotTacticsSpec tactics = null)
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
            RequireNonNegative(rallyRouteRadiusFraction, nameof(rallyRouteRadiusFraction));
            RequireNonNegative(rallyRouteAngleStepDegrees, nameof(rallyRouteAngleStepDegrees));
            RequireNonNegative(rallyMoveRadiusFraction, nameof(rallyMoveRadiusFraction));
            RallyRouteRadiusFraction = rallyRouteRadiusFraction;
            RallyRouteAngleStepDegrees = rallyRouteAngleStepDegrees;
            RallyMoveRadiusFraction = rallyMoveRadiusFraction;
            NavSampleRadius = navSampleRadius;
            RepathSeconds = repathSeconds;
            RepathDistance = repathDistance;
            StuckSeconds = stuckSeconds;
            StuckProgress = stuckProgress;
            Tactics = tactics ?? BotTacticsSpec.Default;
        }

        public EnemySpec Brain { get; }

        public BotInputSpec Input { get; }

        public float NavSampleRadius { get; }

        public float RepathSeconds { get; }

        public float RepathDistance { get; }

        public float StuckSeconds { get; }

        public float StuckProgress { get; }

        public float RallyRouteRadiusFraction { get; }

        public float RallyRouteAngleStepDegrees { get; }

        public float RallyMoveRadiusFraction { get; }

        public BotTacticsSpec Tactics { get; }

        private static void RequireNonNegative(float value, string name)
        {
            if (!(value >= 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be finite and not negative");
            }
        }
    }
}
