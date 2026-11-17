using System;

namespace Shooter.Core
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

        /// <summary>生成時に指定しなかったときの <see cref="DodgeCooldownSeconds"/>。</summary>
        public const float DefaultDodgeCooldownSeconds = 0.5f;

        /// <summary>生成時に指定しなかったときの <see cref="VaultSpeed"/>。体の半径 0.5 m で、0.8 m 先の高さ 1 m・厚さ 0.4 m の壁を越えるのに約 0.65 秒。</summary>
        public const float DefaultVaultSpeed = 6f;

        /// <summary>生成時に指定しなかったときの <see cref="VaultReach"/>。</summary>
        public const float DefaultVaultReach = 1.0f;

        /// <summary>生成時に指定しなかったときの <see cref="VaultMaxAngleDegrees"/>。</summary>
        public const float DefaultVaultMaxAngleDegrees = 45f;

        /// <summary>生成時に指定しなかったときの <see cref="VaultMinHeight"/>。</summary>
        public const float DefaultVaultMinHeight = 0.3f;

        /// <summary>生成時に指定しなかったときの <see cref="VaultMaxDepth"/>。</summary>
        public const float DefaultVaultMaxDepth = 1.0f;

        /// <summary>
        /// 生成時に指定しなかったときの <see cref="MantleSpeed"/>。乗り越え (<see cref="DefaultVaultSpeed"/>) の半分で、
        /// 0.8 m 先の高さ 1.8 m の段を約 1 秒で上がる。
        /// </summary>
        public const float DefaultMantleSpeed = 3f;

        /// <summary>生成時に指定しなかったときの <see cref="CoverMaxEnterDistance"/>。</summary>
        public const float DefaultCoverMaxEnterDistance = 1.0f;

        /// <summary>生成時に指定しなかったときの <see cref="CoverMaxEnterAngleDegrees"/>。</summary>
        public const float DefaultCoverMaxEnterAngleDegrees = 45f;

        /// <summary>生成時に指定しなかったときの <see cref="CoverLowWallHeight"/>。</summary>
        public const float DefaultCoverLowWallHeight = 1.1f;

        /// <summary>生成時に指定しなかったときの <see cref="CoverPeekSideDistance"/>。</summary>
        public const float DefaultCoverPeekSideDistance = 0.6f;

        /// <summary>生成時に指定しなかったときの <see cref="CoverEnterSeconds"/>。</summary>
        public const float DefaultCoverEnterSeconds = 0.15f;

        /// <summary>生成時に指定しなかったときの <see cref="CoverBackHoldSeconds"/>。</summary>
        public const float DefaultCoverBackHoldSeconds = 0.2f;

        /// <summary>生成時に指定しなかったときの <see cref="CoverPeekSlideSeconds"/>。</summary>
        public const float DefaultCoverPeekSlideSeconds = 0.15f;

        /// <summary>生成時に指定しなかったときの <see cref="LadderTopEntryDepth"/>。</summary>
        public const float DefaultLadderTopEntryDepth = 0.3f;

        /// <summary>生成時に指定しなかったときの <see cref="LadderMinApproachDot"/>。</summary>
        public const float DefaultLadderMinApproachDot = 0.5f;

        /// <summary>生成時に指定しなかったときの <see cref="LadderMinApproachSpeed"/>。</summary>
        public const float DefaultLadderMinApproachSpeed = 0.5f;

        public const float DefaultWalkSpeed = 5f;
        public const float DefaultSprintSpeed = 8f;
        public const float DefaultCrouchSpeed = 2.5f;
        public const float DefaultJumpHeight = 1.2f;
        public const float DefaultGravity = 20f;
        public const float DefaultAirControl = 0.1f;

        public static readonly MotorSpec Default = new MotorSpec(
            DefaultWalkSpeed, DefaultSprintSpeed, DefaultJumpHeight, DefaultGravity, DefaultAirControl, DefaultCrouchSpeed);

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

        /// <summary>回避が終わってから次の回避を始められるまでの秒数。</summary>
        public float DodgeCooldownSeconds { get; }

        /// <summary>乗り越えの軌跡をたどる速さ (m/s)。厚い台の上に乗る乗り越えもこの速さ。</summary>
        public float VaultSpeed { get; }

        /// <summary>乗り越え・よじ登りができる、体の中心から手前の面までの水平距離の上限 (m)。</summary>
        public float VaultReach { get; }

        /// <summary>乗り越え・よじ登りで、向いている方向と障害物へ向かう方向のなす角の上限 (度)。</summary>
        public float VaultMaxAngleDegrees { get; }

        /// <summary>上端がこの高さ (足元から、m) 未満の障害物は段差として扱い、乗り越えない。</summary>
        public float VaultMinHeight { get; }

        /// <summary>乗り越えられる奥行きの上限 (m)。これより厚い物は台で、向こう側へ降りられないとみなす。</summary>
        public float VaultMaxDepth { get; }

        /// <summary>カバーに入れる、体の中心から壁までの水平距離の上限 (m)。</summary>
        public float CoverMaxEnterDistance { get; }

        /// <summary>カバーに入るときの、向いている方向と壁へ向かう方向 (法線の逆) のなす角の上限 (度)。</summary>
        public float CoverMaxEnterAngleDegrees { get; }

        /// <summary>上端がこの高さ (足元から、m) 未満の壁を低い壁とし、しゃがんで隠れ立って覗く。以上なら高い壁で、端から横に出て覗く。</summary>
        public float CoverLowWallHeight { get; }

        /// <summary>高い壁の端から覗くときに横へ出る距離 (m)。</summary>
        public float CoverPeekSideDistance { get; }

        /// <summary>カバーに入るとき、壁へ寄せるのにかける秒数。</summary>
        public float CoverEnterSeconds { get; }

        /// <summary>カバー中に後ろの入力をこれだけ続けると出る (秒)。</summary>
        public float CoverBackHoldSeconds { get; }

        /// <summary>覗くときに <see cref="CoverPeekSideDistance"/> を横へ出るのにかける秒数。戻るときも同じ速さ。</summary>
        public float CoverPeekSlideSeconds { get; }

        /// <summary>
        /// はしごの上の端からつかまったときの足元の、上の足場からの深さ (m)。つかまった瞬間に上の端を越えて降り直さないよう、少し下から始める。
        /// 前からつかまれるのもこの深さより下の高さまで。
        /// </summary>
        public float LadderTopEntryDepth { get; }

        /// <summary>動いている向きとはしごへ向かう向きのなす角の余弦がこれ以上なら、はしごへ進んでいるとみなしてつかまる。</summary>
        public float LadderMinApproachDot { get; }

        /// <summary>水平の速さがこれ未満なら、はしごへ進んでいるとみなさない (m/s)。</summary>
        public float LadderMinApproachSpeed { get; }

        /// <summary>ダッシュしていないとき (歩き・しゃがみ・伏せ) に、移動の向きごとに速さへ掛ける倍率。</summary>
        public MoveDirectionScale WalkDirection { get; }

        /// <summary>ダッシュ中に、移動の向きごとに <see cref="SprintSpeed"/> へ掛ける倍率。</summary>
        public MoveDirectionScale SprintDirection { get; }

        /// <summary>持っている武器の重さと倍率から速さに掛ける倍率の決め方。</summary>
        public CarryLoadSpec CarryLoad { get; }

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
        /// <param name="walkDirection">ダッシュしていないときの向きごとの倍率。null で <see cref="MoveDirectionScale.Uniform"/>。</param>
        /// <param name="sprintDirection">ダッシュ中の向きごとの倍率。null で <see cref="MoveDirectionScale.Uniform"/>。</param>
        /// <param name="carryLoad">持ち物による速さの倍率の決め方。null で <see cref="CarryLoadSpec.Default"/>。</param>
        /// <param name="dodgeCooldownSeconds">回避が終わってから次を始められるまでの秒数。0 以上の有限値。</param>
        /// <param name="vaultSpeed">乗り越えの速さ。0 より大きい有限値。</param>
        /// <param name="vaultReach">乗り越え・よじ登りが届く水平距離。0 以上の有限値。</param>
        /// <param name="vaultMaxAngleDegrees">乗り越え・よじ登りで障害物へ向く角度の上限。0〜180 度。</param>
        /// <param name="vaultMinHeight">乗り越える最低の高さ。0 以上の有限値。</param>
        /// <param name="vaultMaxDepth">乗り越えられる奥行きの上限。0 以上の有限値。</param>
        /// <param name="coverMaxEnterDistance">カバーに入れる壁までの距離の上限。0 以上の有限値。</param>
        /// <param name="coverMaxEnterAngleDegrees">カバーに入るときに壁へ向く角度の上限。0〜180 度。</param>
        /// <param name="coverLowWallHeight">低い壁とする上端の高さ。0 以上の有限値。</param>
        /// <param name="coverPeekSideDistance">高い壁の端から覗くときに横へ出る距離。0 以上の有限値。</param>
        /// <param name="coverEnterSeconds">カバーに入るときに壁へ寄せる秒数。0 より大きい有限値。</param>
        /// <param name="coverBackHoldSeconds">カバーから出る後ろの入力を続ける秒数。0 以上の有限値。</param>
        /// <param name="coverPeekSlideSeconds">覗くときに横へ出る秒数。0 より大きい有限値。</param>
        /// <param name="ladderTopEntryDepth">はしごの上の端からつかまったときの深さ。0 以上の有限値。</param>
        /// <param name="ladderMinApproachDot">はしごへ進んでいるとみなす向きの余弦の下限。-1〜1。</param>
        /// <param name="ladderMinApproachSpeed">はしごへ進んでいるとみなす水平の速さの下限。0 以上の有限値。</param>
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
            float mantleSpeed = DefaultMantleSpeed,
            MoveDirectionScale walkDirection = null,
            MoveDirectionScale sprintDirection = null,
            CarryLoadSpec carryLoad = null,
            float dodgeCooldownSeconds = DefaultDodgeCooldownSeconds,
            float vaultSpeed = DefaultVaultSpeed,
            float vaultReach = DefaultVaultReach,
            float vaultMaxAngleDegrees = DefaultVaultMaxAngleDegrees,
            float vaultMinHeight = DefaultVaultMinHeight,
            float vaultMaxDepth = DefaultVaultMaxDepth,
            float coverMaxEnterDistance = DefaultCoverMaxEnterDistance,
            float coverMaxEnterAngleDegrees = DefaultCoverMaxEnterAngleDegrees,
            float coverLowWallHeight = DefaultCoverLowWallHeight,
            float coverPeekSideDistance = DefaultCoverPeekSideDistance,
            float coverEnterSeconds = DefaultCoverEnterSeconds,
            float coverBackHoldSeconds = DefaultCoverBackHoldSeconds,
            float coverPeekSlideSeconds = DefaultCoverPeekSlideSeconds,
            float ladderTopEntryDepth = DefaultLadderTopEntryDepth,
            float ladderMinApproachDot = DefaultLadderMinApproachDot,
            float ladderMinApproachSpeed = DefaultLadderMinApproachSpeed)
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

            if (!(dodgeCooldownSeconds >= 0f) || float.IsInfinity(dodgeCooldownSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(dodgeCooldownSeconds), dodgeCooldownSeconds, "dodgeCooldownSeconds must be finite and not negative");
            }

            if (!(vaultSpeed > 0f) || float.IsInfinity(vaultSpeed))
            {
                throw new ArgumentOutOfRangeException(nameof(vaultSpeed), vaultSpeed, "vaultSpeed must be finite and positive");
            }

            if (!(vaultReach >= 0f) || float.IsInfinity(vaultReach))
            {
                throw new ArgumentOutOfRangeException(nameof(vaultReach), vaultReach, "vaultReach must be finite and not negative");
            }

            if (!(vaultMaxAngleDegrees >= 0f && vaultMaxAngleDegrees <= 180f))
            {
                throw new ArgumentOutOfRangeException(nameof(vaultMaxAngleDegrees), vaultMaxAngleDegrees, "vaultMaxAngleDegrees must be between 0 and 180");
            }

            if (!(vaultMinHeight >= 0f) || float.IsInfinity(vaultMinHeight))
            {
                throw new ArgumentOutOfRangeException(nameof(vaultMinHeight), vaultMinHeight, "vaultMinHeight must be finite and not negative");
            }

            if (!(vaultMaxDepth >= 0f) || float.IsInfinity(vaultMaxDepth))
            {
                throw new ArgumentOutOfRangeException(nameof(vaultMaxDepth), vaultMaxDepth, "vaultMaxDepth must be finite and not negative");
            }

            RequireNotNegative(coverMaxEnterDistance, nameof(coverMaxEnterDistance));
            if (!(coverMaxEnterAngleDegrees >= 0f && coverMaxEnterAngleDegrees <= 180f))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(coverMaxEnterAngleDegrees), coverMaxEnterAngleDegrees, "coverMaxEnterAngleDegrees must be between 0 and 180");
            }

            RequireNotNegative(coverLowWallHeight, nameof(coverLowWallHeight));
            RequireNotNegative(coverPeekSideDistance, nameof(coverPeekSideDistance));
            RequirePositive(coverEnterSeconds, nameof(coverEnterSeconds));
            RequireNotNegative(coverBackHoldSeconds, nameof(coverBackHoldSeconds));
            RequirePositive(coverPeekSlideSeconds, nameof(coverPeekSlideSeconds));
            RequireNotNegative(ladderTopEntryDepth, nameof(ladderTopEntryDepth));
            if (!(ladderMinApproachDot >= -1f && ladderMinApproachDot <= 1f))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(ladderMinApproachDot), ladderMinApproachDot, "ladderMinApproachDot must be between -1 and 1");
            }

            RequireNotNegative(ladderMinApproachSpeed, nameof(ladderMinApproachSpeed));

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
            WalkDirection = walkDirection ?? MoveDirectionScale.Uniform;
            SprintDirection = sprintDirection ?? MoveDirectionScale.Uniform;
            CarryLoad = carryLoad ?? CarryLoadSpec.Default;
            DodgeCooldownSeconds = dodgeCooldownSeconds;
            VaultSpeed = vaultSpeed;
            VaultReach = vaultReach;
            VaultMaxAngleDegrees = vaultMaxAngleDegrees;
            VaultMinHeight = vaultMinHeight;
            VaultMaxDepth = vaultMaxDepth;
            CoverMaxEnterDistance = coverMaxEnterDistance;
            CoverMaxEnterAngleDegrees = coverMaxEnterAngleDegrees;
            CoverLowWallHeight = coverLowWallHeight;
            CoverPeekSideDistance = coverPeekSideDistance;
            CoverEnterSeconds = coverEnterSeconds;
            CoverBackHoldSeconds = coverBackHoldSeconds;
            CoverPeekSlideSeconds = coverPeekSlideSeconds;
            LadderTopEntryDepth = ladderTopEntryDepth;
            LadderMinApproachDot = ladderMinApproachDot;
            LadderMinApproachSpeed = ladderMinApproachSpeed;
        }

        private static void RequireNotNegative(float value, string name)
        {
            if (!(value >= 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be finite and not negative");
            }
        }

        private static void RequirePositive(float value, string name)
        {
            if (!(value > 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be finite and positive");
            }
        }
    }
}
