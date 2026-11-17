using System;

namespace Shooter.Core
{
    public sealed class DominationSpec
    {
        public const int DefaultMaxCapturers = 3;

        public int PointCount { get; }

        public float CaptureSeconds { get; }

        public float PointsPerSecond { get; }

        public int ScoreLimit { get; }

        public float TimeLimitSeconds { get; }

        public int MaxCapturers { get; }

        public DominationSpec(
            int pointCount,
            float captureSeconds,
            float pointsPerSecond,
            int scoreLimit,
            float timeLimitSeconds,
            int maxCapturers = DefaultMaxCapturers)
        {
            if (pointCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(pointCount), pointCount, "pointCount must be at least 1");
            }

            if (!(captureSeconds > 0f) || float.IsInfinity(captureSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(captureSeconds), captureSeconds, "captureSeconds must be positive and finite");
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

            if (maxCapturers < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCapturers), maxCapturers, "maxCapturers must be at least 1");
            }

            PointCount = pointCount;
            CaptureSeconds = captureSeconds;
            PointsPerSecond = pointsPerSecond;
            ScoreLimit = scoreLimit;
            TimeLimitSeconds = timeLimitSeconds;
            MaxCapturers = maxCapturers;
        }
    }
}
