using System;

namespace Fps.Core
{
    /// <summary>
    /// モードごとのルール。生成時に値を検査し、以後は変わらない。
    /// </summary>
    public sealed class GameRules
    {
        /// <summary>
        /// 今の挙動。味方撃ちなし、個別リスポーン、自動回復なし、体は消える、死亡カメラなし、予備弾は有限で倍率 1、建造物は壊れない、アクションは <see cref="ActionRules.Default"/>、
        /// リスポーン直後の無敵なし、落下ダメージなし、HUD はすべて出す、拠点なし (<see cref="MatchObjective.None"/>)、人数を Bot で埋めない、放置しても切らない。
        /// </summary>
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

        /// <summary><see cref="Default"/> の <see cref="WaveIntervalSeconds"/>。Wave 以外では使われない。</summary>
        public const float DefaultWaveIntervalSeconds = 10f;

        /// <summary><see cref="MinPlayers"/> の上限。サーバーが Bot を際限なく足さないようにする。</summary>
        public const int MaxMinPlayers = 64;

        /// <summary>味方に弾が当たったときの扱い。</summary>
        public FriendlyFire FriendlyFire { get; }

        /// <summary>倒されたプレイヤーがいつ戻ってくるか。</summary>
        public RespawnMode RespawnMode { get; }

        /// <summary>波と波の間隔 (秒)。<see cref="Core.RespawnMode.Wave"/> のときだけ使う。0 より大きい有限値。</summary>
        public float WaveIntervalSeconds { get; }

        /// <summary>最後に被弾してから回復が始まるまでの秒数。0 以上の有限値。</summary>
        public float RegenDelaySeconds { get; }

        /// <summary>1 秒あたりの回復量。0 なら自動回復しない。0 以上の有限値。</summary>
        public float RegenPerSecond { get; }

        /// <summary>倒されたプレイヤーの体をどう見せるか。</summary>
        public DeathBody DeathBody { get; }

        /// <summary>倒されてからリスポーンするまで、自分のカメラに何を映すか。</summary>
        public DeathCamera DeathCamera { get; }

        /// <summary>true なら予備弾が減らない。</summary>
        public bool InfiniteReserve { get; }

        /// <summary>初期予備弾の倍率。0 以上の有限値。</summary>
        public float ReserveAmmoMultiplier { get; }

        /// <summary>建造物を弾で壊せるか。</summary>
        public Destruction Destruction { get; }

        /// <summary>TPS のアクションのルール。null にならない。</summary>
        public ActionRules Actions { get; }

        /// <summary>リスポーンしてからダメージを受けなくなる秒数。0 なら守らない。0 以上の有限値。</summary>
        public float SpawnProtectionSeconds { get; }

        /// <summary>高い所から落ちたときにダメージを受けるか。</summary>
        public FallDamage FallDamage { get; }

        /// <summary>HUD にどれだけの情報を出すか。</summary>
        public HudDensity HudDensity { get; }

        /// <summary>オンラインの試合の勝敗を何で決めるか。</summary>
        public MatchObjective Objective { get; }

        /// <summary>
        /// オンラインの試合で、人がこれより少なければ足りない分を Bot で埋める人数。0 なら埋めない。0 以上 <see cref="MaxMinPlayers"/> 以下。
        /// </summary>
        public int MinPlayers { get; }

        /// <summary>オンラインの試合で、入力がこの秒数無ければサーバーから切る。0 なら切らない。0 以上の有限値。</summary>
        public float IdleKickSeconds { get; }

        /// <param name="friendlyFire">味方撃ちの扱い。定義済みの値。</param>
        /// <param name="respawnMode">リスポーン方式。定義済みの値。</param>
        /// <param name="waveIntervalSeconds">波の間隔 (秒)。0 より大きい有限値。</param>
        /// <param name="regenDelaySeconds">回復が始まるまでの秒数。0 以上の有限値。</param>
        /// <param name="regenPerSecond">1 秒あたりの回復量。0 以上の有限値。</param>
        /// <param name="deathBody">倒れた体の見せ方。定義済みの値。</param>
        /// <param name="deathCamera">死亡カメラ。定義済みの値。</param>
        /// <param name="infiniteReserve">予備弾を減らさないか。</param>
        /// <param name="reserveAmmoMultiplier">初期予備弾の倍率。0 以上の有限値。</param>
        /// <param name="destruction">建造物を壊せるか。定義済みの値。</param>
        /// <param name="actions">アクションのルール。null なら <see cref="ActionRules.Default"/>。</param>
        /// <param name="spawnProtectionSeconds">リスポーン直後にダメージを受けない秒数。0 以上の有限値。</param>
        /// <param name="fallDamage">落下ダメージ。定義済みの値。</param>
        /// <param name="hudDensity">HUD の情報量。定義済みの値。</param>
        /// <param name="objective">勝敗の決め方。定義済みの値。</param>
        /// <param name="minPlayers">Bot で埋める人数。0 以上 <see cref="MaxMinPlayers"/> 以下。</param>
        /// <param name="idleKickSeconds">放置で切るまでの秒数。0 以上の有限値。</param>
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
            float idleKickSeconds = 0f)
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
        }

        /// <summary><see cref="FriendlyFire"/> だけを value にした複製。値の検査はコンストラクターと同じ。</summary>
        public GameRules WithFriendlyFire(FriendlyFire value) => Copy(friendlyFire: value);

        /// <summary><see cref="RespawnMode"/> だけを value にした複製。値の検査はコンストラクターと同じ。</summary>
        public GameRules WithRespawnMode(RespawnMode value) => Copy(respawnMode: value);

        /// <summary><see cref="WaveIntervalSeconds"/> だけを value にした複製。値の検査はコンストラクターと同じ。</summary>
        public GameRules WithWaveIntervalSeconds(float value) => Copy(waveIntervalSeconds: value);

        /// <summary><see cref="RegenDelaySeconds"/> だけを value にした複製。値の検査はコンストラクターと同じ。</summary>
        public GameRules WithRegenDelaySeconds(float value) => Copy(regenDelaySeconds: value);

        /// <summary><see cref="RegenPerSecond"/> だけを value にした複製。値の検査はコンストラクターと同じ。</summary>
        public GameRules WithRegenPerSecond(float value) => Copy(regenPerSecond: value);

        /// <summary><see cref="DeathBody"/> だけを value にした複製。値の検査はコンストラクターと同じ。</summary>
        public GameRules WithDeathBody(DeathBody value) => Copy(deathBody: value);

        /// <summary><see cref="DeathCamera"/> だけを value にした複製。値の検査はコンストラクターと同じ。</summary>
        public GameRules WithDeathCamera(DeathCamera value) => Copy(deathCamera: value);

        /// <summary><see cref="InfiniteReserve"/> だけを value にした複製。</summary>
        public GameRules WithInfiniteReserve(bool value) => Copy(infiniteReserve: value);

        /// <summary><see cref="ReserveAmmoMultiplier"/> だけを value にした複製。値の検査はコンストラクターと同じ。</summary>
        public GameRules WithReserveAmmoMultiplier(float value) => Copy(reserveAmmoMultiplier: value);

        /// <summary><see cref="Destruction"/> だけを value にした複製。値の検査はコンストラクターと同じ。</summary>
        public GameRules WithDestruction(Destruction value) => Copy(destruction: value);

        /// <summary>
        /// <see cref="Actions"/> だけを value にした複製。value が null なら <see cref="ArgumentNullException"/>
        /// (コンストラクターと違って <see cref="ActionRules.Default"/> に置き換えないのは、差し替えたつもりの値が黙って既定に戻らないようにするため)。
        /// </summary>
        public GameRules WithActions(ActionRules value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            return Copy(actions: value);
        }

        /// <summary><see cref="SpawnProtectionSeconds"/> だけを value にした複製。値の検査はコンストラクターと同じ。</summary>
        public GameRules WithSpawnProtectionSeconds(float value) => Copy(spawnProtectionSeconds: value);

        /// <summary><see cref="FallDamage"/> だけを value にした複製。値の検査はコンストラクターと同じ。</summary>
        public GameRules WithFallDamage(FallDamage value) => Copy(fallDamage: value);

        /// <summary><see cref="HudDensity"/> だけを value にした複製。値の検査はコンストラクターと同じ。</summary>
        public GameRules WithHudDensity(HudDensity value) => Copy(hudDensity: value);

        /// <summary><see cref="Objective"/> だけを value にした複製。値の検査はコンストラクターと同じ。</summary>
        public GameRules WithObjective(MatchObjective value) => Copy(objective: value);

        /// <summary><see cref="MinPlayers"/> だけを value にした複製。値の検査はコンストラクターと同じ。</summary>
        public GameRules WithMinPlayers(int value) => Copy(minPlayers: value);

        /// <summary><see cref="IdleKickSeconds"/> だけを value にした複製。値の検査はコンストラクターと同じ。</summary>
        public GameRules WithIdleKickSeconds(float value) => Copy(idleKickSeconds: value);

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
            float? idleKickSeconds = null)
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
                idleKickSeconds ?? IdleKickSeconds);
        }
    }
}
