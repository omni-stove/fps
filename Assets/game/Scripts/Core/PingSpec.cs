using System;

namespace Shooter.Core
{
    public sealed class PingSpec
    {
        public const float DefaultLifetimeSeconds = 6f;

        public const int DefaultMaxMarkers = 3;

        public const float DefaultMaxDistance = 150f;

        public const float DefaultEnemyAimToleranceDegrees = 3f;

        public const float DefaultMinIntervalSeconds = 0.5f;

        public static PingSpec Default { get; } = new PingSpec();

        public PingSpec(
            float lifetimeSeconds = DefaultLifetimeSeconds,
            int maxMarkers = DefaultMaxMarkers,
            float maxDistance = DefaultMaxDistance,
            float enemyAimToleranceDegrees = DefaultEnemyAimToleranceDegrees,
            float minIntervalSeconds = DefaultMinIntervalSeconds)
        {
            if (!(lifetimeSeconds > 0f) || float.IsInfinity(lifetimeSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(lifetimeSeconds), lifetimeSeconds, "lifetimeSeconds must be positive and finite");
            }

            if (maxMarkers < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxMarkers), maxMarkers, "maxMarkers must be at least 1");
            }

            if (!(maxDistance > 0f) || float.IsInfinity(maxDistance))
            {
                throw new ArgumentOutOfRangeException(nameof(maxDistance), maxDistance, "maxDistance must be positive and finite");
            }

            if (!(enemyAimToleranceDegrees >= 0f) || enemyAimToleranceDegrees >= 90f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(enemyAimToleranceDegrees), enemyAimToleranceDegrees, "enemyAimToleranceDegrees must be at least 0 and less than 90");
            }

            if (!(minIntervalSeconds >= 0f) || float.IsInfinity(minIntervalSeconds))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(minIntervalSeconds), minIntervalSeconds, "minIntervalSeconds must be at least 0 and finite");
            }

            LifetimeSeconds = lifetimeSeconds;
            MaxMarkers = maxMarkers;
            MaxDistance = maxDistance;
            EnemyAimToleranceDegrees = enemyAimToleranceDegrees;
            MinIntervalSeconds = minIntervalSeconds;
        }

        public float LifetimeSeconds { get; }

        public int MaxMarkers { get; }

        public float MaxDistance { get; }

        public float EnemyAimToleranceDegrees { get; }

        public float MinIntervalSeconds { get; }
    }
}
