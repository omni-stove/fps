using System;

namespace Shooter.Core
{
    public sealed class HardpointSpec
    {
        public int PointCount { get; }

        public float RotateSeconds { get; }

        public float PointsPerSecond { get; }

        public int ScoreLimit { get; }

        public float TimeLimitSeconds { get; }

        public float RespawnSeconds { get; }

        public HardpointSpec(
            int pointCount,
            float rotateSeconds,
            float pointsPerSecond,
            int scoreLimit,
            float timeLimitSeconds,
            float respawnSeconds)
        {
            // 1 つだけでは移る先が無いので 2 以上にする。
            if (pointCount < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(pointCount), pointCount, "pointCount must be at least 2");
            }

            if (!(rotateSeconds > 0f) || float.IsInfinity(rotateSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(rotateSeconds), rotateSeconds, "rotateSeconds must be positive and finite");
            }

            if (!(pointsPerSecond > 0f) || float.IsInfinity(pointsPerSecond))
            {
                throw new ArgumentOutOfRangeException(nameof(pointsPerSecond), pointsPerSecond, "pointsPerSecond must be positive and finite");
            }

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

            PointCount = pointCount;
            RotateSeconds = rotateSeconds;
            PointsPerSecond = pointsPerSecond;
            ScoreLimit = scoreLimit;
            TimeLimitSeconds = timeLimitSeconds;
            RespawnSeconds = respawnSeconds;
        }
    }
}
