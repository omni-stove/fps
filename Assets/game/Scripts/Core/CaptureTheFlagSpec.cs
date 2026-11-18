using System;

namespace Shooter.Core
{
    public sealed class CaptureTheFlagSpec
    {
        public const int DefaultScoreLimit = 3;
        public const float DefaultTimeLimitSeconds = 600f;
        public const float DefaultRespawnSeconds = 5f;
        public const float DefaultPickupRadius = 1.5f;
        public const float DefaultCaptureRadius = 2f;
        public const float DefaultReturnSeconds = 20f;
        public const int DefaultPointsPerCapture = 1;

        public static CaptureTheFlagSpec Default { get; } = new CaptureTheFlagSpec();

        public int ScoreLimit { get; }

        public float TimeLimitSeconds { get; }

        public float RespawnSeconds { get; }

        public float PickupRadius { get; }

        public float CaptureRadius { get; }

        public float ReturnSeconds { get; }

        public int PointsPerCapture { get; }

        public CaptureTheFlagSpec(
            int scoreLimit = DefaultScoreLimit,
            float timeLimitSeconds = DefaultTimeLimitSeconds,
            float respawnSeconds = DefaultRespawnSeconds,
            float pickupRadius = DefaultPickupRadius,
            float captureRadius = DefaultCaptureRadius,
            float returnSeconds = DefaultReturnSeconds,
            int pointsPerCapture = DefaultPointsPerCapture)
        {
            if (scoreLimit < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(scoreLimit), scoreLimit, "scoreLimit must be at least 1");
            }

            RequirePositiveFinite(timeLimitSeconds, nameof(timeLimitSeconds));

            if (!(respawnSeconds >= 0f) || float.IsInfinity(respawnSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(respawnSeconds), respawnSeconds, "respawnSeconds must not be negative and must be finite");
            }

            RequirePositiveFinite(pickupRadius, nameof(pickupRadius));
            RequirePositiveFinite(captureRadius, nameof(captureRadius));
            RequirePositiveFinite(returnSeconds, nameof(returnSeconds));

            if (pointsPerCapture < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(pointsPerCapture), pointsPerCapture, "pointsPerCapture must be at least 1");
            }

            ScoreLimit = scoreLimit;
            TimeLimitSeconds = timeLimitSeconds;
            RespawnSeconds = respawnSeconds;
            PickupRadius = pickupRadius;
            CaptureRadius = captureRadius;
            ReturnSeconds = returnSeconds;
            PointsPerCapture = pointsPerCapture;
        }

        private static void RequirePositiveFinite(float value, string name)
        {
            if (!(value > 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be positive and finite");
            }
        }
    }
}
