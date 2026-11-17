using System;

namespace Shooter.Core
{
    public sealed class SquadOrderSpec
    {
        public const float DefaultHoldSeconds = 0.5f;
        public const float DefaultMaxDistance = PingSpec.DefaultMaxDistance;
        public const float DefaultEnemyAimToleranceDegrees = PingSpec.DefaultEnemyAimToleranceDegrees;
        public const float DefaultNearbyRadius = 30f;
        public const float DefaultFollowDistance = 3f;
        public const float DefaultLeashDistance = 15f;

        public static SquadOrderSpec Default { get; } = new SquadOrderSpec();

        public SquadOrderSpec(
            float holdSeconds = DefaultHoldSeconds,
            float maxDistance = DefaultMaxDistance,
            float enemyAimToleranceDegrees = DefaultEnemyAimToleranceDegrees,
            float nearbyRadius = DefaultNearbyRadius,
            float followDistance = DefaultFollowDistance,
            float leashDistance = DefaultLeashDistance)
        {
            RequirePositive(holdSeconds, nameof(holdSeconds));
            RequirePositive(maxDistance, nameof(maxDistance));
            RequirePositive(nearbyRadius, nameof(nearbyRadius));
            RequirePositive(followDistance, nameof(followDistance));

            if (!(enemyAimToleranceDegrees >= 0f) || enemyAimToleranceDegrees >= 90f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(enemyAimToleranceDegrees), enemyAimToleranceDegrees, "enemyAimToleranceDegrees must be at least 0 and less than 90");
            }

            if (!(leashDistance >= followDistance) || float.IsInfinity(leashDistance))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(leashDistance), leashDistance, "leashDistance must be finite and at least followDistance");
            }

            HoldSeconds = holdSeconds;
            MaxDistance = maxDistance;
            EnemyAimToleranceDegrees = enemyAimToleranceDegrees;
            NearbyRadius = nearbyRadius;
            FollowDistance = followDistance;
            LeashDistance = leashDistance;

            // 何を指したかの決め方は印 (Ping) と同じにし、PingAim をそのまま使う。寿命と数は PingAim が読まない。
            Aim = new PingSpec(maxDistance: maxDistance, enemyAimToleranceDegrees: enemyAimToleranceDegrees);
        }

        public float HoldSeconds { get; }

        public float MaxDistance { get; }

        public float EnemyAimToleranceDegrees { get; }

        public float NearbyRadius { get; }

        public float FollowDistance { get; }

        public float LeashDistance { get; }

        public PingSpec Aim { get; }

        private static void RequirePositive(float value, string name)
        {
            if (!(value > 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be positive and finite");
            }
        }
    }
}
