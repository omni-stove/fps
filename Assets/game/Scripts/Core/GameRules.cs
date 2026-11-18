using System;

namespace Shooter.Core
{
    public sealed class GameRules
    {
        public static readonly GameRules Default = new GameRules(
            FriendlyFire.Off,
            RespawnMode.Individual,
            DefaultWaveIntervalSeconds,
            0f,
            0f,
            DeathBody.Vanish,
            DeathCamera.None,
            false,
            1f,
            Destruction.Off);

        public const float DefaultWaveIntervalSeconds = 10f;

        // サーバーが Bot を際限なく足さないよう、MinPlayers に上限を置く。
        public const int MaxMinPlayers = 64;

        // 持ち武器を送る電文 (OwnerSnapshot.MaxWeapons) に収まる数にする。
        public const int MaxWeaponsLimit = byte.MaxValue;

        // Core は Runtime の Damageable.DefaultMaxHealth を参照できないので同じ値を持ち、揃っていることは EditMode のテストで確かめる。
        public const int DefaultPlayerMaxHealth = 100;

        public FriendlyFire FriendlyFire { get; }

        public RespawnMode RespawnMode { get; }

        public float WaveIntervalSeconds { get; }

        public float RegenDelaySeconds { get; }

        public float RegenPerSecond { get; }

        public DeathBody DeathBody { get; }

        public DeathCamera DeathCamera { get; }

        public bool InfiniteReserve { get; }

        public float ReserveAmmoMultiplier { get; }

        public Destruction Destruction { get; }

        public ActionRules Actions { get; }

        public float SpawnProtectionSeconds { get; }

        public FallDamage FallDamage { get; }

        public HudDensity HudDensity { get; }

        public MatchObjective Objective { get; }

        public int MinPlayers { get; }

        public float IdleKickSeconds { get; }

        public int MaxWeapons { get; }

        public ReloadMode ReloadMode { get; }

        public ViewMode ViewMode { get; }

        public int PlayerMaxHealth { get; }

        public ZoneDamageMultipliers ZoneDamage { get; }

        public Streaks Streaks { get; }

        public TextChat TextChat { get; }

        public Spectating Spectating { get; }

        public GameRules(
            FriendlyFire friendlyFire,
            RespawnMode respawnMode,
            float waveIntervalSeconds,
            float regenDelaySeconds,
            float regenPerSecond,
            DeathBody deathBody,
            DeathCamera deathCamera,
            bool infiniteReserve,
            float reserveAmmoMultiplier,
            Destruction destruction,
            ActionRules actions = null,
            float spawnProtectionSeconds = 0f,
            FallDamage fallDamage = FallDamage.Off,
            HudDensity hudDensity = HudDensity.Full,
            MatchObjective objective = MatchObjective.None,
            int minPlayers = 0,
            float idleKickSeconds = 0f,
            int maxWeapons = 0,
            ReloadMode reloadMode = ReloadMode.Auto,
            ViewMode viewMode = ViewMode.FirstPerson,
            int playerMaxHealth = DefaultPlayerMaxHealth,
            ZoneDamageMultipliers zoneDamage = null,
            Streaks streaks = Streaks.On,
            TextChat textChat = TextChat.On,
            Spectating spectating = Spectating.On)
        {
            if (!Enum.IsDefined(typeof(FriendlyFire), friendlyFire))
            {
                throw new ArgumentOutOfRangeException(nameof(friendlyFire), friendlyFire, "friendlyFire is not a defined FriendlyFire");
            }

            if (!Enum.IsDefined(typeof(RespawnMode), respawnMode))
            {
                throw new ArgumentOutOfRangeException(nameof(respawnMode), respawnMode, "respawnMode is not a defined RespawnMode");
            }

            // Wave 以外でも検査するのは、後からモードだけ差し替えた値でも間隔が必ず使える状態にしておくため。
            if (!(waveIntervalSeconds > 0f) || float.IsInfinity(waveIntervalSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(waveIntervalSeconds), waveIntervalSeconds, "waveIntervalSeconds must be positive and finite");
            }

            if (!(regenDelaySeconds >= 0f) || float.IsInfinity(regenDelaySeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(regenDelaySeconds), regenDelaySeconds, "regenDelaySeconds must be a non-negative finite number");
            }

            if (!(regenPerSecond >= 0f) || float.IsInfinity(regenPerSecond))
            {
                throw new ArgumentOutOfRangeException(nameof(regenPerSecond), regenPerSecond, "regenPerSecond must be a non-negative finite number");
            }

            if (!Enum.IsDefined(typeof(DeathBody), deathBody))
            {
                throw new ArgumentOutOfRangeException(nameof(deathBody), deathBody, "deathBody is not a defined DeathBody");
            }

            if (!Enum.IsDefined(typeof(DeathCamera), deathCamera))
            {
                throw new ArgumentOutOfRangeException(nameof(deathCamera), deathCamera, "deathCamera is not a defined DeathCamera");
            }

            if (!(reserveAmmoMultiplier >= 0f) || float.IsInfinity(reserveAmmoMultiplier))
            {
                throw new ArgumentOutOfRangeException(nameof(reserveAmmoMultiplier), reserveAmmoMultiplier, "reserveAmmoMultiplier must be a non-negative finite number");
            }

            if (!Enum.IsDefined(typeof(Destruction), destruction))
            {
                throw new ArgumentOutOfRangeException(nameof(destruction), destruction, "destruction is not a defined Destruction");
            }

            if (!(spawnProtectionSeconds >= 0f) || float.IsInfinity(spawnProtectionSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(spawnProtectionSeconds), spawnProtectionSeconds, "spawnProtectionSeconds must be a non-negative finite number");
            }

            if (!Enum.IsDefined(typeof(FallDamage), fallDamage))
            {
                throw new ArgumentOutOfRangeException(nameof(fallDamage), fallDamage, "fallDamage is not a defined FallDamage");
            }

            if (!Enum.IsDefined(typeof(HudDensity), hudDensity))
            {
                throw new ArgumentOutOfRangeException(nameof(hudDensity), hudDensity, "hudDensity is not a defined HudDensity");
            }

            if (!Enum.IsDefined(typeof(MatchObjective), objective))
            {
                throw new ArgumentOutOfRangeException(nameof(objective), objective, "objective is not a defined MatchObjective");
            }

            if (minPlayers < 0 || minPlayers > MaxMinPlayers)
            {
                throw new ArgumentOutOfRangeException(nameof(minPlayers), minPlayers, "minPlayers must be between 0 and " + MaxMinPlayers);
            }

            if (!(idleKickSeconds >= 0f) || float.IsInfinity(idleKickSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(idleKickSeconds), idleKickSeconds, "idleKickSeconds must be a non-negative finite number");
            }

            if (maxWeapons < 0 || maxWeapons > MaxWeaponsLimit)
            {
                throw new ArgumentOutOfRangeException(nameof(maxWeapons), maxWeapons, "maxWeapons must be between 0 and " + MaxWeaponsLimit);
            }

            if (!Enum.IsDefined(typeof(ReloadMode), reloadMode))
            {
                throw new ArgumentOutOfRangeException(nameof(reloadMode), reloadMode, "reloadMode is not a defined ReloadMode");
            }

            if (!Enum.IsDefined(typeof(ViewMode), viewMode))
            {
                throw new ArgumentOutOfRangeException(nameof(viewMode), viewMode, "viewMode is not a defined ViewMode");
            }

            if (playerMaxHealth < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(playerMaxHealth), playerMaxHealth, "playerMaxHealth must be at least 1");
            }

            if (!Enum.IsDefined(typeof(Streaks), streaks))
            {
                throw new ArgumentOutOfRangeException(nameof(streaks), streaks, "streaks is not a defined Streaks");
            }

            if (!Enum.IsDefined(typeof(TextChat), textChat))
            {
                throw new ArgumentOutOfRangeException(nameof(textChat), textChat, "textChat is not a defined TextChat");
            }

            if (!Enum.IsDefined(typeof(Spectating), spectating))
            {
                throw new ArgumentOutOfRangeException(nameof(spectating), spectating, "spectating is not a defined Spectating");
            }

            FriendlyFire = friendlyFire;
            RespawnMode = respawnMode;
            WaveIntervalSeconds = waveIntervalSeconds;
            RegenDelaySeconds = regenDelaySeconds;
            RegenPerSecond = regenPerSecond;
            DeathBody = deathBody;
            DeathCamera = deathCamera;
            InfiniteReserve = infiniteReserve;
            ReserveAmmoMultiplier = reserveAmmoMultiplier;
            Destruction = destruction;
            Actions = actions ?? ActionRules.Default;
            SpawnProtectionSeconds = spawnProtectionSeconds;
            FallDamage = fallDamage;
            HudDensity = hudDensity;
            Objective = objective;
            MinPlayers = minPlayers;
            IdleKickSeconds = idleKickSeconds;
            MaxWeapons = maxWeapons;
            ReloadMode = reloadMode;
            ViewMode = viewMode;
            PlayerMaxHealth = playerMaxHealth;
            ZoneDamage = zoneDamage ?? ZoneDamageMultipliers.Default;
            Streaks = streaks;
            TextChat = textChat;
            Spectating = spectating;
        }

        public GameRules WithFriendlyFire(FriendlyFire value) => Copy(friendlyFire: value);

        public GameRules WithRespawnMode(RespawnMode value) => Copy(respawnMode: value);

        public GameRules WithWaveIntervalSeconds(float value) => Copy(waveIntervalSeconds: value);

        public GameRules WithRegenDelaySeconds(float value) => Copy(regenDelaySeconds: value);

        public GameRules WithRegenPerSecond(float value) => Copy(regenPerSecond: value);

        public GameRules WithDeathBody(DeathBody value) => Copy(deathBody: value);

        public GameRules WithDeathCamera(DeathCamera value) => Copy(deathCamera: value);

        public GameRules WithInfiniteReserve(bool value) => Copy(infiniteReserve: value);

        public GameRules WithReserveAmmoMultiplier(float value) => Copy(reserveAmmoMultiplier: value);

        public GameRules WithDestruction(Destruction value) => Copy(destruction: value);

        // コンストラクターと違って null を ActionRules.Default に置き換えないのは、差し替えたつもりの値が黙って既定に戻らないようにするため。
        public GameRules WithActions(ActionRules value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            return Copy(actions: value);
        }

        public GameRules WithSpawnProtectionSeconds(float value) => Copy(spawnProtectionSeconds: value);

        public GameRules WithFallDamage(FallDamage value) => Copy(fallDamage: value);

        public GameRules WithHudDensity(HudDensity value) => Copy(hudDensity: value);

        public GameRules WithObjective(MatchObjective value) => Copy(objective: value);

        public GameRules WithMinPlayers(int value) => Copy(minPlayers: value);

        public GameRules WithIdleKickSeconds(float value) => Copy(idleKickSeconds: value);

        public GameRules WithMaxWeapons(int value) => Copy(maxWeapons: value);

        public GameRules WithReloadMode(ReloadMode value) => Copy(reloadMode: value);

        public GameRules WithViewMode(ViewMode value) => Copy(viewMode: value);

        public GameRules WithPlayerMaxHealth(int value) => Copy(playerMaxHealth: value);

        // WithActions と同じく、差し替えたつもりの値が黙って既定に戻らないよう null を受け付けない。
        public GameRules WithZoneDamage(ZoneDamageMultipliers value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            return Copy(zoneDamage: value);
        }

        public GameRules WithStreaks(Streaks value) => Copy(streaks: value);

        public GameRules WithTextChat(TextChat value) => Copy(textChat: value);

        public GameRules WithSpectating(Spectating value) => Copy(spectating: value);

        private GameRules Copy(
            FriendlyFire? friendlyFire = null,
            RespawnMode? respawnMode = null,
            float? waveIntervalSeconds = null,
            float? regenDelaySeconds = null,
            float? regenPerSecond = null,
            DeathBody? deathBody = null,
            DeathCamera? deathCamera = null,
            bool? infiniteReserve = null,
            float? reserveAmmoMultiplier = null,
            Destruction? destruction = null,
            ActionRules actions = null,
            float? spawnProtectionSeconds = null,
            FallDamage? fallDamage = null,
            HudDensity? hudDensity = null,
            MatchObjective? objective = null,
            int? minPlayers = null,
            float? idleKickSeconds = null,
            int? maxWeapons = null,
            ReloadMode? reloadMode = null,
            ViewMode? viewMode = null,
            int? playerMaxHealth = null,
            ZoneDamageMultipliers zoneDamage = null,
            Streaks? streaks = null,
            TextChat? textChat = null,
            Spectating? spectating = null)
        {
            return new GameRules(
                friendlyFire ?? FriendlyFire,
                respawnMode ?? RespawnMode,
                waveIntervalSeconds ?? WaveIntervalSeconds,
                regenDelaySeconds ?? RegenDelaySeconds,
                regenPerSecond ?? RegenPerSecond,
                deathBody ?? DeathBody,
                deathCamera ?? DeathCamera,
                infiniteReserve ?? InfiniteReserve,
                reserveAmmoMultiplier ?? ReserveAmmoMultiplier,
                destruction ?? Destruction,
                actions ?? Actions,
                spawnProtectionSeconds ?? SpawnProtectionSeconds,
                fallDamage ?? FallDamage,
                hudDensity ?? HudDensity,
                objective ?? Objective,
                minPlayers ?? MinPlayers,
                idleKickSeconds ?? IdleKickSeconds,
                maxWeapons ?? MaxWeapons,
                reloadMode ?? ReloadMode,
                viewMode ?? ViewMode,
                playerMaxHealth ?? PlayerMaxHealth,
                zoneDamage ?? ZoneDamage,
                streaks ?? Streaks,
                textChat ?? TextChat,
                spectating ?? Spectating);
        }
    }
}
