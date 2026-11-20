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
        public const int DefaultRoundWinsToWin = 3;
        public const float DefaultRoundPreparationSeconds = 5f;
        public const float DefaultRoundTimeLimitSeconds = 120f;
        public const int DefaultMaxDisplayNameLength = 16;

        // Core からは Protocol を参照できないので、Shooter.Protocol.SnapshotMessage.MaxPlayers の値を写す
        public const int MaxMaxPlayers = 64;

        // Core からは Protocol を参照できないので、Shooter.Protocol.MapVoteStateMessage.MaxCandidates の値を写す
        public const int MaxMapPoolCount = 16;

        // Core からは Protocol を参照できないので、Shooter.Protocol.MatchProtocol.MaxChatBytes (512) を UTF-8 の 1 文字の最大 3 バイトで割った数にする
        public const int MaxMaxChatLength = 170;

        // 名前を 1 文字 3 バイトで数えても PlayerDisplayName.MaxBytes に収まり、長さの設定の内なら送れない名前が出ないようにする
        public const int MaxMaxDisplayNameLength = PlayerDisplayName.MaxBytes / 3;

        // 天候と時刻の Id はマップ名と同じくファイル名やアセットの Id に使うので、マップ名と同じ長さで切る
        public const int MaxEnvironmentIdLength = NetLaunchOptions.MaxMapNameLength;

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
            int maxChatLength = DefaultMaxChatLength,
            int roundWinsToWin = DefaultRoundWinsToWin,
            float roundPreparationSeconds = DefaultRoundPreparationSeconds,
            float roundTimeLimitSeconds = DefaultRoundTimeLimitSeconds,
            string weatherId = null,
            string timeOfDayId = null,
            CaptureTheFlagSpec captureTheFlag = null,
            HardpointSpec hardpoint = null,
            SearchAndDestroySpec searchAndDestroy = null,
            FreeForAllSpec freeForAll = null,
            KillConfirmedSpec killConfirmed = null,
            GunGameSpec gunGame = null,
            VoteKickSpec voteKick = null,
            PayloadSpec payload = null,
            EconomySpec economy = null,
            int maxDisplayNameLength = DefaultMaxDisplayNameLength)
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

            if (maxDisplayNameLength < 1 || maxDisplayNameLength > MaxMaxDisplayNameLength)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxDisplayNameLength),
                    maxDisplayNameLength,
                    nameof(maxDisplayNameLength) + " must be between 1 and " + MaxMaxDisplayNameLength);
            }

            RequirePositive(roundWinsToWin, nameof(roundWinsToWin));
            RequireNotNegative(roundPreparationSeconds, nameof(roundPreparationSeconds));
            RequirePositive(roundTimeLimitSeconds, nameof(roundTimeLimitSeconds));

            WeatherId = OptionalId(weatherId, nameof(weatherId));
            TimeOfDayId = OptionalId(timeOfDayId, nameof(timeOfDayId));
            CaptureTheFlag = captureTheFlag ?? CaptureTheFlagSpec.Default;
            Hardpoint = hardpoint ?? HardpointSpec.Default;
            SearchAndDestroy = searchAndDestroy ?? SearchAndDestroySpec.Default;
            FreeForAll = freeForAll ?? FreeForAllSpec.Default;
            KillConfirmed = killConfirmed ?? KillConfirmedSpec.Default;
            GunGame = gunGame ?? GunGameSpec.Default;
            VoteKick = voteKick ?? VoteKickSpec.Default;
            Payload = payload ?? PayloadSpec.Default;
            Economy = economy ?? EconomySpec.Default;
            MapPool = CopyMapPool(mapPool);
            MapVoteSeconds = mapVoteSeconds;
            MaxTeamSizeDifference = maxTeamSizeDifference;
            PrivateMatch = privateMatch;
            JoinCode = joinCode;
            ChatIntervalSeconds = chatIntervalSeconds;
            MaxChatLength = maxChatLength;
            MaxDisplayNameLength = maxDisplayNameLength;
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
            RoundWinsToWin = roundWinsToWin;
            RoundPreparationSeconds = roundPreparationSeconds;
            RoundTimeLimitSeconds = roundTimeLimitSeconds;
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
                maxChatLength: MaxChatLength,
                roundWinsToWin: RoundWinsToWin,
                roundPreparationSeconds: RoundPreparationSeconds,
                roundTimeLimitSeconds: RoundTimeLimitSeconds,
                weatherId: WeatherId,
                timeOfDayId: TimeOfDayId,
                captureTheFlag: CaptureTheFlag,
                hardpoint: Hardpoint,
                searchAndDestroy: SearchAndDestroy,
                freeForAll: FreeForAll,
                killConfirmed: KillConfirmed,
                gunGame: GunGame,
                voteKick: VoteKick,
                payload: Payload,
                economy: Economy,
                maxDisplayNameLength: MaxDisplayNameLength);
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

        // UTF-16 の文字数で数えるのは、チャットの長さ (MaxChatLength) と数え方をそろえるため
        public int MaxDisplayNameLength { get; }

        public int RoundWinsToWin { get; }

        public float RoundPreparationSeconds { get; }

        public float RoundTimeLimitSeconds { get; }

        public string WeatherId { get; }

        public string TimeOfDayId { get; }

        public CaptureTheFlagSpec CaptureTheFlag { get; }

        // PointCount はマップの拠点の数で決まるので、ここの値は使わず、試合を始めるときにマップの数で作り直す
        public HardpointSpec Hardpoint { get; }

        // SiteCount はマップの爆破地点の数で決まるので、ここの値は使わず、試合を始めるときにマップの数で作り直す
        public SearchAndDestroySpec SearchAndDestroy { get; }

        public FreeForAllSpec FreeForAll { get; }

        public KillConfirmedSpec KillConfirmed { get; }

        public GunGameSpec GunGame { get; }

        public VoteKickSpec VoteKick { get; }

        public PayloadSpec Payload { get; }

        public EconomySpec Economy { get; }

        private static string OptionalId(string id, string name)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            if (!ConnectionExtras.IsValidField(id, MaxEnvironmentIdLength))
            {
                throw new ArgumentException(name + " must be 1-" + MaxEnvironmentIdLength + " characters of [0-9A-Za-z_-]", name);
            }

            return id;
        }

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
