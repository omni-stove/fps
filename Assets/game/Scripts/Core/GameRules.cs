using System;

namespace Fps.Core
{
    /// <summary>
    /// モードごとのルール。生成時に値を検査し、以後は変わらない。
    /// </summary>
    public sealed class GameRules
    {
        /// <summary>
        /// 今の挙動。味方撃ちなし、個別リスポーン、自動回復なし、体は消える、死亡カメラなし、予備弾は有限で倍率 1、建造物は壊れない、アクションは <see cref="ActionRules.Default"/>。
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
            ActionRules actions = null)
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
        }
    }
}
