using System;

namespace Shooter.Core
{
    public sealed class KillConfirmedSpec
    {
        public const int DefaultScoreLimit = 20;

        public const float DefaultTimeLimitSeconds = 300f;

        public const float DefaultRespawnSeconds = 3f;

        public const float DefaultTagLifetimeSeconds = 30f;

        public const int DefaultPointsPerConfirm = 1;

        public const float DefaultTagPickupRadius = 1f;

        public static KillConfirmedSpec Default { get; } = new KillConfirmedSpec(
            DefaultScoreLimit, DefaultTimeLimitSeconds, DefaultRespawnSeconds, DefaultTagLifetimeSeconds);

        public int ScoreLimit { get; }

        public float TimeLimitSeconds { get; }

        public float RespawnSeconds { get; }

        public float TagLifetimeSeconds { get; }

        public int PointsPerConfirm { get; }

        public float TagPickupRadius { get; }

        public KillConfirmedSpec(
            int scoreLimit,
            float timeLimitSeconds,
            float respawnSeconds,
            float tagLifetimeSeconds,
            int pointsPerConfirm = DefaultPointsPerConfirm,
            float tagPickupRadius = DefaultTagPickupRadius)
        {
            if (scoreLimit < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(scoreLimit), scoreLimit, "scoreLimit must be at least 1");
            }

            if (!(timeLimitSeconds > 0f) || float.IsInfinity(timeLimitSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(timeLimitSeconds), timeLimitSeconds, "timeLimitSeconds must be positive and finite");
            }

            if (!(respawnSeconds >= 0f) || float.IsInfinity(respawnSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(respawnSeconds), respawnSeconds, "respawnSeconds must not be negative and must be finite");
            }

            if (!(tagLifetimeSeconds > 0f) || float.IsInfinity(tagLifetimeSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(tagLifetimeSeconds), tagLifetimeSeconds, "tagLifetimeSeconds must be positive and finite");
            }

            if (pointsPerConfirm < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(pointsPerConfirm), pointsPerConfirm, "pointsPerConfirm must be at least 1");
            }

            if (!(tagPickupRadius > 0f) || float.IsInfinity(tagPickupRadius))
            {
                throw new ArgumentOutOfRangeException(nameof(tagPickupRadius), tagPickupRadius, "tagPickupRadius must be positive and finite");
            }

            ScoreLimit = scoreLimit;
            TimeLimitSeconds = timeLimitSeconds;
            RespawnSeconds = respawnSeconds;
            TagLifetimeSeconds = tagLifetimeSeconds;
            PointsPerConfirm = pointsPerConfirm;
            TagPickupRadius = tagPickupRadius;
        }
    }
}
