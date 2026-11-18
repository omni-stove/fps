using System;

namespace Shooter.Core
{
    public sealed class FreeForAllSpec
    {
        public static FreeForAllSpec Default { get; } = new FreeForAllSpec();

        public int ScoreLimit { get; }

        public float TimeLimitSeconds { get; }

        public float RespawnSeconds { get; }

        public int PointsPerKill { get; }

        public FreeForAllSpec(
            int scoreLimit = FreeForAllMode.DefaultScoreLimit,
            float timeLimitSeconds = FreeForAllMode.DefaultTimeLimitSeconds,
            float respawnSeconds = FreeForAllMode.DefaultRespawnSeconds,
            int pointsPerKill = FreeForAllMode.DefaultPointsPerKill)
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

            if (pointsPerKill < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(pointsPerKill), pointsPerKill, "pointsPerKill must be at least 1");
            }

            ScoreLimit = scoreLimit;
            TimeLimitSeconds = timeLimitSeconds;
            RespawnSeconds = respawnSeconds;
            PointsPerKill = pointsPerKill;
        }

        public ModeSession CreateSession()
        {
            return FreeForAllMode.Create(TimeLimitSeconds, ScoreLimit, RespawnSeconds, PointsPerKill);
        }
    }
}
