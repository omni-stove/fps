using System;
using System.Collections.Generic;

namespace Shooter.Core
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
        public const int DefaultMaxPlayers = 8;
        public const int MinMaxPlayers = 1;
        public const float DefaultMapVoteSeconds = 0f;
        public const int DefaultMaxTeamSizeDifference = 0;
        public const float DefaultChatIntervalSeconds = 1f;
        public const int DefaultMaxChatLength = 120;

        // Core からは Protocol を参照できないので、Shooter.Protocol.SnapshotMessage.MaxPlayers の値を写す
        public const int MaxMaxPlayers = 64;

        // Core からは Protocol を参照できないので、Shooter.Protocol.MapVoteStateMessage.MaxCandidates の値を写す
        public const int MaxMapPoolCount = 16;

        // Core からは Protocol を参照できないので、Shooter.Protocol.MatchProtocol.MaxChatBytes (512) を UTF-8 の 1 文字の最大 3 バイトで割った数にする
        public const int MaxMaxChatLength = 170;

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
            int maxCapturers = DominationSpec.DefaultMaxCapturers,
            int maxPlayers = DefaultMaxPlayers,
            IReadOnlyList<string> mapPool = null,
            float mapVoteSeconds = DefaultMapVoteSeconds,
            int maxTeamSizeDifference = DefaultMaxTeamSizeDifference,
            bool privateMatch = false,
            string joinCode = null,
            float chatIntervalSeconds = DefaultChatIntervalSeconds,
            int maxChatLength = DefaultMaxChatLength)
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
            if (maxPlayers < MinMaxPlayers || maxPlayers > MaxMaxPlayers)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxPlayers), maxPlayers, nameof(maxPlayers) + " must be between " + MinMaxPlayers + " and " + MaxMaxPlayers);
            }

            RequireNotNegative(mapVoteSeconds, nameof(mapVoteSeconds));
            if (maxTeamSizeDifference < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxTeamSizeDifference), maxTeamSizeDifference, nameof(maxTeamSizeDifference) + " must not be negative");
            }

            joinCode = string.IsNullOrEmpty(joinCode) ? null : joinCode;
            if (joinCode != null && !ConnectionExtras.IsValidField(joinCode, ConnectionExtras.MaxJoinCodeLength))
            {
                throw new ArgumentException(
                    nameof(joinCode) + " must be 1-" + ConnectionExtras.MaxJoinCodeLength + " characters of [0-9A-Za-z_-]", nameof(joinCode));
            }

            RequireNotNegative(chatIntervalSeconds, nameof(chatIntervalSeconds));
            if (maxChatLength < 1 || maxChatLength > MaxMaxChatLength)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxChatLength), maxChatLength, nameof(maxChatLength) + " must be between 1 and " + MaxMaxChatLength);
            }

            MapPool = CopyMapPool(mapPool);
            MapVoteSeconds = mapVoteSeconds;
            MaxTeamSizeDifference = maxTeamSizeDifference;
            PrivateMatch = privateMatch;
            JoinCode = joinCode;
            ChatIntervalSeconds = chatIntervalSeconds;
            MaxChatLength = maxChatLength;
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
            MaxPlayers = maxPlayers;
        }

        public static NetMatchSettings Default { get; } = new NetMatchSettings();

        public NetMatchSettings WithPrivacy(bool privateMatch, string joinCode)
        {
            return new NetMatchSettings(
                scoreLimit: ScoreLimit,
                timeLimitSeconds: TimeLimitSeconds,
                respawnSeconds: RespawnSeconds,
                restartDelaySeconds: RestartDelaySeconds,
                maxHealth: MaxHealth,
                minEnemySpawnDistance: MinEnemySpawnDistance,
                reconnectGraceSeconds: ReconnectGraceSeconds,
                maxShotOriginOffset: MaxShotOriginOffset,
                dominationScoreLimit: DominationScoreLimit,
                captureSeconds: CaptureSeconds,
                pointsPerSecond: PointsPerSecond,
                maxCapturers: MaxCapturers,
                maxPlayers: MaxPlayers,
                mapPool: MapPool,
                mapVoteSeconds: MapVoteSeconds,
                maxTeamSizeDifference: MaxTeamSizeDifference,
                privateMatch: privateMatch,
                joinCode: joinCode,
                chatIntervalSeconds: ChatIntervalSeconds,
                maxChatLength: MaxChatLength);
        }

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

        public int MaxPlayers { get; }

        public IReadOnlyList<string> MapPool { get; }

        public float MapVoteSeconds { get; }

        public int MaxTeamSizeDifference { get; }

        public bool PrivateMatch { get; }

        public string JoinCode { get; }

        public float ChatIntervalSeconds { get; }

        public int MaxChatLength { get; }

        private static IReadOnlyList<string> CopyMapPool(IReadOnlyList<string> mapPool)
        {
            if (mapPool == null || mapPool.Count == 0)
            {
                return Array.Empty<string>();
            }

            if (mapPool.Count > MaxMapPoolCount)
            {
                throw new ArgumentException(
                    nameof(mapPool) + " lists " + mapPool.Count + " maps, but at most " + MaxMapPoolCount + " fit a map vote", nameof(mapPool));
            }

            var copy = new string[mapPool.Count];
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < copy.Length; i++)
            {
                var mapId = mapPool[i];
                if (!NetLaunchOptions.IsMapName(mapId))
                {
                    throw new ArgumentException(
                        nameof(mapPool) + "[" + i + "] '" + mapId + "' must be 1-" + NetLaunchOptions.MaxMapNameLength + " characters of [0-9A-Za-z_-]",
                        nameof(mapPool));
                }

                if (!seen.Add(mapId))
                {
                    throw new ArgumentException(nameof(mapPool) + " lists '" + mapId + "' more than once", nameof(mapPool));
                }

                copy[i] = mapId;
            }

            return Array.AsReadOnly(copy);
        }

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
