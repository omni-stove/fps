using System;

namespace Shooter.Core
{
    // オンラインの Bot が判断 (EnemyBrain) に沿って手榴弾を投げる・近接で殴る・物陰に隠れるときの調整値。bots.json の平らな項目から読む
    public sealed class BotTacticsSpec
    {
        public const bool DefaultThrowGrenades = true;

        // 爆発の半径 (既定の手榴弾で数 m) に自分が入らない距離から、投げて届く距離まで
        public const float DefaultGrenadeMinDistance = 8f;
        public const float DefaultGrenadeMaxDistance = 20f;
        public const float DefaultGrenadeIntervalSeconds = 10f;

        public const bool DefaultMeleeAttacks = true;

        public const bool DefaultTakeCover = true;
        public const float DefaultCoverSearchRadius = 8f;
        public const float DefaultCoverSearchStep = 2f;
        public const int DefaultCoverSearchDirections = 8;
        public const float DefaultCoverSearchSeconds = 1f;
        public const float DefaultCoverMaxAngleDegrees = CoverPointSelector.DefaultMaxCoverAngleDegrees;
        public const float DefaultCoverMinThreatDistance = CoverPointSelector.DefaultMinThreatDistance;

        // 物陰を探すたびに 1 点あたり世界へ線を 2 本引くので、輪と向きの数に上限を置き、1 回の探しの手間を抑える
        public const int MaxCoverSearchDirections = 64;
        public const int MaxCoverSearchRings = 32;

        public static BotTacticsSpec Default { get; } = new BotTacticsSpec();

        public BotTacticsSpec(
            bool throwGrenades = DefaultThrowGrenades,
            float grenadeMinDistance = DefaultGrenadeMinDistance,
            float grenadeMaxDistance = DefaultGrenadeMaxDistance,
            float grenadeIntervalSeconds = DefaultGrenadeIntervalSeconds,
            bool meleeAttacks = DefaultMeleeAttacks,
            bool takeCover = DefaultTakeCover,
            float coverSearchRadius = DefaultCoverSearchRadius,
            float coverSearchStep = DefaultCoverSearchStep,
            int coverSearchDirections = DefaultCoverSearchDirections,
            float coverSearchSeconds = DefaultCoverSearchSeconds,
            float coverMaxAngleDegrees = DefaultCoverMaxAngleDegrees,
            float coverMinThreatDistance = DefaultCoverMinThreatDistance)
        {
            RequireNonNegative(grenadeMinDistance, nameof(grenadeMinDistance));
            RequireNonNegative(grenadeMaxDistance, nameof(grenadeMaxDistance));
            if (grenadeMaxDistance < grenadeMinDistance)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(grenadeMaxDistance), grenadeMaxDistance, "grenadeMaxDistance must not be less than grenadeMinDistance");
            }

            RequireNonNegative(grenadeIntervalSeconds, nameof(grenadeIntervalSeconds));
            RequirePositive(coverSearchRadius, nameof(coverSearchRadius));
            RequirePositive(coverSearchStep, nameof(coverSearchStep));
            if (coverSearchRadius / coverSearchStep > MaxCoverSearchRings)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(coverSearchStep), coverSearchStep, "coverSearchStep must be at least coverSearchRadius / " + MaxCoverSearchRings);
            }

            if (coverSearchDirections < 1 || coverSearchDirections > MaxCoverSearchDirections)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(coverSearchDirections), coverSearchDirections, "coverSearchDirections must be between 1 and " + MaxCoverSearchDirections);
            }

            RequireNonNegative(coverSearchSeconds, nameof(coverSearchSeconds));
            if (!(coverMaxAngleDegrees >= 0f) || coverMaxAngleDegrees > 180f)
            {
                throw new ArgumentOutOfRangeException(nameof(coverMaxAngleDegrees), coverMaxAngleDegrees, "coverMaxAngleDegrees must be between 0 and 180");
            }

            RequireNonNegative(coverMinThreatDistance, nameof(coverMinThreatDistance));
            ThrowGrenades = throwGrenades;
            GrenadeMinDistance = grenadeMinDistance;
            GrenadeMaxDistance = grenadeMaxDistance;
            GrenadeIntervalSeconds = grenadeIntervalSeconds;
            MeleeAttacks = meleeAttacks;
            TakeCover = takeCover;
            CoverSearchRadius = coverSearchRadius;
            CoverSearchStep = coverSearchStep;
            CoverSearchDirections = coverSearchDirections;
            CoverSearchSeconds = coverSearchSeconds;
            CoverMaxAngleDegrees = coverMaxAngleDegrees;
            CoverMinThreatDistance = coverMinThreatDistance;
        }

        public bool ThrowGrenades { get; }

        public float GrenadeMinDistance { get; }

        public float GrenadeMaxDistance { get; }

        public float GrenadeIntervalSeconds { get; }

        public bool MeleeAttacks { get; }

        public bool TakeCover { get; }

        public float CoverSearchRadius { get; }

        public float CoverSearchStep { get; }

        public int CoverSearchDirections { get; }

        public float CoverSearchSeconds { get; }

        public float CoverMaxAngleDegrees { get; }

        public float CoverMinThreatDistance { get; }

        private static void RequireNonNegative(float value, string name)
        {
            if (!(value >= 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be finite and not negative");
            }
        }

        private static void RequirePositive(float value, string name)
        {
            if (!(value > 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be positive and finite");
            }
        }
    }
}
