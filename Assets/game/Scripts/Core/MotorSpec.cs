using System;

namespace Fps.Core
{
    /// <summary>
    /// <see cref="PlayerMotor"/> の設定。速度は m/s、高さは m、重力は m/s² (正の値で下向きに適用する)。
    /// </summary>
    public sealed class MotorSpec
    {
        /// <summary>
        /// 生成時に指定しなかったときの <see cref="SafeFallSpeed"/>。
        /// プレイヤーの既定 (重力 20 m/s²・ジャンプ 1.2 m) で、平地のジャンプ (約 6.9 m/s) も、乗り越えられる高さ (<see cref="DefaultVaultMaxHeight"/>) の
        /// 段からジャンプして降りたとき (約 9.8 m/s) も減らない。同じ重力なら約 3.6 m の段から歩いて降りるまでは減らない。
        /// </summary>
        public const float DefaultSafeFallSpeed = 12f;

        /// <summary>生成時に指定しなかったときの <see cref="LethalFallSpeed"/>。既定の重力 20 m/s² で約 15.6 m の高さから落ちた速さ。</summary>
        public const float DefaultLethalFallSpeed = 25f;

        /// <summary>生成時に指定しなかったときの <see cref="DodgeDistance"/>。</summary>
        public const float DefaultDodgeDistance = 3f;

        /// <summary>生成時に指定しなかったときの <see cref="DodgeSeconds"/>。</summary>
        public const float DefaultDodgeSeconds = 0.35f;

        /// <summary>生成時に指定しなかったときの <see cref="VaultMaxHeight"/>。</summary>
        public const float DefaultVaultMaxHeight = 1.2f;

        /// <summary>
        /// 生成時に指定しなかったときの <see cref="MantleMaxHeight"/>。立ち 2 m のプレイヤーが手を伸ばして縁に届く高さ。
        /// プレイヤーの既定のジャンプ (1.2 m) では上がれない段も、これまではよじ登れる。
        /// </summary>
        public const float DefaultMantleMaxHeight = 2.2f;

        /// <summary>
        /// 生成時に指定しなかったときの <see cref="MantleSpeed"/>。乗り越え (<see cref="VaultProbe.Speed"/>) の半分で、
        /// 0.8 m 先の高さ 1.8 m の段を約 1 秒で上がる。
        /// </summary>
        public const float DefaultMantleSpeed = 3f;

        /// <summary>歩きの水平速度。</summary>
        public float WalkSpeed { get; }

        /// <summary>ダッシュ中の水平速度。</summary>
        public float SprintSpeed { get; }

        /// <summary>ジャンプで到達する高さ。</summary>
        public float JumpHeight { get; }

        /// <summary>重力加速度の大きさ。下向きに適用する。</summary>
        public float Gravity { get; }

        /// <summary>空中で水平速度を入力方向へ寄せる 1 ステップあたりの割合。0 で操作不能、1 で即座に入力どおり。</summary>
        public float AirControl { get; }

        /// <summary>しゃがみ中の水平速度。しゃがみ中はダッシュしてもこの速度になる。</summary>
        public float CrouchSpeed { get; }

        /// <summary>着地したときにこの落下速さ以下ならダメージを受けない。<see cref="GameRules.FallDamage"/> が On のときだけ使う。</summary>
        public float SafeFallSpeed { get; }

        /// <summary>着地したときにこの落下速さ以上なら倒れる。<see cref="SafeFallSpeed"/> より大きい。</summary>
        public float LethalFallSpeed { get; }

        /// <summary>回避 1 回で動く水平距離。</summary>
        public float DodgeDistance { get; }

        /// <summary>回避 1 回にかかる秒数。</summary>
        public float DodgeSeconds { get; }

        /// <summary>乗り越えられる障害物の、足元からの最大の高さ。</summary>
        public float VaultMaxHeight { get; }

        /// <summary>よじ登れる段の、足元からの最大の高さ。<see cref="VaultMaxHeight"/> 以下の段はよじ登らずに乗り越える。</summary>
        public float MantleMaxHeight { get; }

        /// <summary>よじ登る軌跡をたどる速さ (m/s)。</summary>
        public float MantleSpeed { get; }

        /// <param name="walkSpeed">歩きの水平速度。0 以上。</param>
        /// <param name="sprintSpeed">ダッシュ中の水平速度。0 以上。</param>
        /// <param name="jumpHeight">ジャンプで到達する高さ。0 以上。</param>
        /// <param name="gravity">重力加速度の大きさ。正の値。</param>
        /// <param name="airControl">空中制御の割合。0〜1。</param>
        /// <param name="crouchSpeed">しゃがみ中の水平速度。0 以上。</param>
        /// <param name="safeFallSpeed">ダメージを受けない落下速さの上限。0 以上の有限値。</param>
        /// <param name="lethalFallSpeed">倒れる落下速さの下限。safeFallSpeed より大きい有限値。</param>
        /// <param name="dodgeDistance">回避 1 回の水平距離。0 以上の有限値。</param>
        /// <param name="dodgeSeconds">回避 1 回の秒数。0 より大きい有限値。</param>
        /// <param name="vaultMaxHeight">乗り越えられる最大の高さ。0 以上の有限値。</param>
        /// <param name="mantleMaxHeight">よじ登れる最大の高さ。0 以上の有限値。</param>
        /// <param name="mantleSpeed">よじ登る速さ。0 より大きい有限値。</param>
        public MotorSpec(
            float walkSpeed,
            float sprintSpeed,
            float jumpHeight,
            float gravity,
            float airControl,
            float crouchSpeed,
            float safeFallSpeed = DefaultSafeFallSpeed,
            float lethalFallSpeed = DefaultLethalFallSpeed,
            float dodgeDistance = DefaultDodgeDistance,
            float dodgeSeconds = DefaultDodgeSeconds,
            float vaultMaxHeight = DefaultVaultMaxHeight,
            float mantleMaxHeight = DefaultMantleMaxHeight,
            float mantleSpeed = DefaultMantleSpeed)
        {
            if (!(walkSpeed >= 0f) || float.IsInfinity(walkSpeed))
            {
                throw new ArgumentOutOfRangeException(nameof(walkSpeed), walkSpeed, "walkSpeed must be finite and not negative");
            }

            if (!(sprintSpeed >= 0f) || float.IsInfinity(sprintSpeed))
            {
                throw new ArgumentOutOfRangeException(nameof(sprintSpeed), sprintSpeed, "sprintSpeed must be finite and not negative");
            }

            if (!(jumpHeight >= 0f) || float.IsInfinity(jumpHeight))
            {
                throw new ArgumentOutOfRangeException(nameof(jumpHeight), jumpHeight, "jumpHeight must be finite and not negative");
            }

            if (!(gravity > 0f) || float.IsInfinity(gravity))
            {
                throw new ArgumentOutOfRangeException(nameof(gravity), gravity, "gravity must be finite and positive");
            }

            if (!(airControl >= 0f && airControl <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(airControl), airControl, "airControl must be between 0 and 1");
            }

            if (!(crouchSpeed >= 0f) || float.IsInfinity(crouchSpeed))
            {
                throw new ArgumentOutOfRangeException(nameof(crouchSpeed), crouchSpeed, "crouchSpeed must be finite and not negative");
            }

            if (!(safeFallSpeed >= 0f) || float.IsInfinity(safeFallSpeed))
            {
                throw new ArgumentOutOfRangeException(nameof(safeFallSpeed), safeFallSpeed, "safeFallSpeed must be finite and not negative");
            }

            // 同じ値を許すと、安全な上限と致死の下限の間でダメージを割り振る幅が 0 になり割り算できない。
            if (!(lethalFallSpeed > safeFallSpeed) || float.IsInfinity(lethalFallSpeed))
            {
                throw new ArgumentOutOfRangeException(nameof(lethalFallSpeed), lethalFallSpeed, "lethalFallSpeed must be finite and greater than safeFallSpeed");
            }

            if (!(dodgeDistance >= 0f) || float.IsInfinity(dodgeDistance))
            {
                throw new ArgumentOutOfRangeException(nameof(dodgeDistance), dodgeDistance, "dodgeDistance must be finite and not negative");
            }

            if (!(dodgeSeconds > 0f) || float.IsInfinity(dodgeSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(dodgeSeconds), dodgeSeconds, "dodgeSeconds must be finite and positive");
            }

            if (!(vaultMaxHeight >= 0f) || float.IsInfinity(vaultMaxHeight))
            {
                throw new ArgumentOutOfRangeException(nameof(vaultMaxHeight), vaultMaxHeight, "vaultMaxHeight must be finite and not negative");
            }

            if (!(mantleMaxHeight >= 0f) || float.IsInfinity(mantleMaxHeight))
            {
                throw new ArgumentOutOfRangeException(nameof(mantleMaxHeight), mantleMaxHeight, "mantleMaxHeight must be finite and not negative");
            }

            if (!(mantleSpeed > 0f) || float.IsInfinity(mantleSpeed))
            {
                throw new ArgumentOutOfRangeException(nameof(mantleSpeed), mantleSpeed, "mantleSpeed must be finite and positive");
            }

            WalkSpeed = walkSpeed;
            SprintSpeed = sprintSpeed;
            JumpHeight = jumpHeight;
            Gravity = gravity;
            AirControl = airControl;
            CrouchSpeed = crouchSpeed;
            SafeFallSpeed = safeFallSpeed;
            LethalFallSpeed = lethalFallSpeed;
            DodgeDistance = dodgeDistance;
            DodgeSeconds = dodgeSeconds;
            VaultMaxHeight = vaultMaxHeight;
            MantleMaxHeight = mantleMaxHeight;
            MantleSpeed = mantleSpeed;
        }
    }
}
