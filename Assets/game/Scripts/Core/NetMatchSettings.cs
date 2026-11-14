using System;

namespace Fps.Core
{
    public sealed class NetMatchSettings
    {
        public const int DefaultScoreLimit = 10;
        public const float DefaultTimeLimitSeconds = 300f;
        public const float DefaultRespawnSeconds = 3f;
        public const float DefaultRestartDelaySeconds = 10f;
        public const int DefaultMaxHealth = 100;
        public const int DefaultDominationScoreLimit = 100;
        public const float DefaultCaptureSeconds = 8f;
        public const float DefaultPointsPerSecond = 1f;

        public NetMatchSettings(
            int scoreLimit = DefaultScoreLimit,
            float timeLimitSeconds = DefaultTimeLimitSeconds,
            float respawnSeconds = DefaultRespawnSeconds,
            float restartDelaySeconds = DefaultRestartDelaySeconds,
            int maxHealth = DefaultMaxHealth,
            float minEnemySpawnDistance = SpawnSelector.DefaultMinEnemyDistance,
            float reconnectGraceSeconds = TeamDeathmatchSession.DefaultReconnectGraceSeconds,
            float maxShotOriginOffset = ShotValidator.DefaultMaxOriginOffset,
            int dominationScoreLimit = DefaultDominationScoreLimit,
            float captureSeconds = DefaultCaptureSeconds,
            float pointsPerSecond = DefaultPointsPerSecond,
            int maxCapturers = DominationSpec.DefaultMaxCapturers)
        {
            RequirePositive(scoreLimit, nameof(scoreLimit));
            RequirePositive(timeLimitSeconds, nameof(timeLimitSeconds));
            RequireNotNegative(respawnSeconds, nameof(respawnSeconds));
            RequireNotNegative(restartDelaySeconds, nameof(restartDelaySeconds));
            RequirePositive(maxHealth, nameof(maxHealth));
            RequireNotNegative(minEnemySpawnDistance, nameof(minEnemySpawnDistance));
            RequireNotNegative(reconnectGraceSeconds, nameof(reconnectGraceSeconds));
            RequireNotNegative(maxShotOriginOffset, nameof(maxShotOriginOffset));
            RequirePositive(dominationScoreLimit, nameof(dominationScoreLimit));
            RequirePositive(captureSeconds, nameof(captureSeconds));
            RequirePositive(pointsPerSecond, nameof(pointsPerSecond));
            RequirePositive(maxCapturers, nameof(maxCapturers));

            ScoreLimit = scoreLimit;
            TimeLimitSeconds = timeLimitSeconds;
            RespawnSeconds = respawnSeconds;
            RestartDelaySeconds = restartDelaySeconds;
            MaxHealth = maxHealth;
            MinEnemySpawnDistance = minEnemySpawnDistance;
            ReconnectGraceSeconds = reconnectGraceSeconds;
            MaxShotOriginOffset = maxShotOriginOffset;
            DominationScoreLimit = dominationScoreLimit;
            CaptureSeconds = captureSeconds;
            PointsPerSecond = pointsPerSecond;
            MaxCapturers = maxCapturers;
        }

        public static NetMatchSettings Default { get; } = new NetMatchSettings();

        public int ScoreLimit { get; }

        public float TimeLimitSeconds { get; }

        public float RespawnSeconds { get; }

        public float RestartDelaySeconds { get; }

        public int MaxHealth { get; }

        public float MinEnemySpawnDistance { get; }

        public float ReconnectGraceSeconds { get; }

        public float MaxShotOriginOffset { get; }

        public int DominationScoreLimit { get; }

        public float CaptureSeconds { get; }

        public float PointsPerSecond { get; }

        public int MaxCapturers { get; }

        private static void RequirePositive(int value, string name)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be positive");
            }
        }

        private static void RequirePositive(float value, string name)
        {
            if (!(value > 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be positive and finite");
            }
        }

        private static void RequireNotNegative(float value, string name)
        {
            if (!(value >= 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must not be negative and must be finite");
            }
        }
    }
}
