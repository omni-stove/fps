using System;

namespace Shooter.Core
{
    public sealed class MotorSpec
    {
        // プレイヤーの既定 (重力 20 m/s²・ジャンプ 1.2 m) で、平地のジャンプ (約 6.9 m/s) も、乗り越えられる高さ (DefaultVaultMaxHeight) の
        // 段からジャンプして降りたとき (約 9.8 m/s) も減らない値にしてある。同じ重力なら約 3.6 m の段から歩いて降りるまでは減らない。
        public const float DefaultSafeFallSpeed = 12f;

        // 既定の重力 20 m/s² で約 15.6 m の高さから落ちた速さにしてある。
        public const float DefaultLethalFallSpeed = 25f;

        public const float DefaultDodgeDistance = 3f;

        public const float DefaultDodgeSeconds = 0.35f;

        public const float DefaultVaultMaxHeight = 1.2f;

        // 立ち 2 m のプレイヤーが手を伸ばして縁に届く高さにしてある。既定のジャンプ (1.2 m) では上がれない段も、これまではよじ登れる。
        public const float DefaultMantleMaxHeight = 2.2f;

        public const float DefaultDodgeCooldownSeconds = 0.5f;

        // 体の半径 0.5 m で、0.8 m 先の高さ 1 m・厚さ 0.4 m の壁を約 0.65 秒で越える値にしてある。
        public const float DefaultVaultSpeed = 6f;

        public const float DefaultVaultReach = 1.0f;

        public const float DefaultVaultMaxAngleDegrees = 45f;

        public const float DefaultVaultMinHeight = 0.3f;

        public const float DefaultVaultMaxDepth = 1.0f;

        // 乗り越え (DefaultVaultSpeed) の半分で、0.8 m 先の高さ 1.8 m の段を約 1 秒で上がる値にしてある。
        public const float DefaultMantleSpeed = 3f;

        public const float DefaultCoverMaxEnterDistance = 1.0f;

        public const float DefaultCoverMaxEnterAngleDegrees = 45f;

        public const float DefaultCoverLowWallHeight = 1.1f;

        public const float DefaultCoverPeekSideDistance = 0.6f;

        public const float DefaultCoverEnterSeconds = 0.15f;

        public const float DefaultCoverBackHoldSeconds = 0.2f;

        public const float DefaultCoverPeekSlideSeconds = 0.15f;

        public const float DefaultLadderTopEntryDepth = 0.3f;

        public const float DefaultLadderMinApproachDot = 0.5f;

        public const float DefaultLadderMinApproachSpeed = 0.5f;

        public const float DefaultLeanSideOffset = 0.4f;
        public const float DefaultLeanAngleDegrees = 15f;
        public const float DefaultLeanSpeed = 6f;

        /// <summary>0 なのは、空中でもう一度跳べるかはゲームが決めることで、キットの既定では跳べないままにするため。</summary>
        public const int DefaultAirJumps = 0;

        public const float DefaultAirJumpSpeed = 6.5f;

        public const float DefaultWalkSpeed = 5f;
        public const float DefaultSprintSpeed = 8f;
        public const float DefaultCrouchSpeed = 2.5f;
        public const float DefaultJumpHeight = 1.2f;
        public const float DefaultGravity = 20f;
        public const float DefaultAirControl = 0.1f;

        public static readonly MotorSpec Default = new MotorSpec(
            DefaultWalkSpeed, DefaultSprintSpeed, DefaultJumpHeight, DefaultGravity, DefaultAirControl, DefaultCrouchSpeed);

        public float WalkSpeed { get; }

        public float SprintSpeed { get; }

        public float JumpHeight { get; }

        public float Gravity { get; }

        public float AirControl { get; }

        public float CrouchSpeed { get; }

        public float SafeFallSpeed { get; }

        public float LethalFallSpeed { get; }

        public float DodgeDistance { get; }

        public float DodgeSeconds { get; }

        public float VaultMaxHeight { get; }

        public float MantleMaxHeight { get; }

        public float MantleSpeed { get; }

        public float DodgeCooldownSeconds { get; }

        public float VaultSpeed { get; }

        public float VaultReach { get; }

        public float VaultMaxAngleDegrees { get; }

        public float VaultMinHeight { get; }

        public float VaultMaxDepth { get; }

        public float CoverMaxEnterDistance { get; }

        public float CoverMaxEnterAngleDegrees { get; }

        public float CoverLowWallHeight { get; }

        public float CoverPeekSideDistance { get; }

        public float CoverEnterSeconds { get; }

        public float CoverBackHoldSeconds { get; }

        public float CoverPeekSlideSeconds { get; }

        // 上の端からつかまったときに少し下から始めるのは、つかまった瞬間に上の端を越えて降り直さないため。
        public float LadderTopEntryDepth { get; }

        public float LadderMinApproachDot { get; }

        public float LadderMinApproachSpeed { get; }

        public MoveDirectionScale WalkDirection { get; }

        public MoveDirectionScale SprintDirection { get; }

        public CarryLoadSpec CarryLoad { get; }

        public float LeanSideOffset { get; }

        public float LeanAngleDegrees { get; }

        public float LeanSpeed { get; }

        public int AirJumps { get; }

        public float AirJumpSpeed { get; }

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
            float ladderMinApproachSpeed = DefaultLadderMinApproachSpeed,
            float leanSideOffset = DefaultLeanSideOffset,
            float leanAngleDegrees = DefaultLeanAngleDegrees,
            float leanSpeed = DefaultLeanSpeed,
            int airJumps = DefaultAirJumps,
            float airJumpSpeed = DefaultAirJumpSpeed)
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
            RequireNotNegative(leanSideOffset, nameof(leanSideOffset));
            if (!(leanAngleDegrees >= 0f && leanAngleDegrees <= 90f))
            {
                throw new ArgumentOutOfRangeException(nameof(leanAngleDegrees), leanAngleDegrees, "leanAngleDegrees must be between 0 and 90");
            }

            RequirePositive(leanSpeed, nameof(leanSpeed));
            if (airJumps < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(airJumps), airJumps, "airJumps must not be negative");
            }

            RequireNotNegative(airJumpSpeed, nameof(airJumpSpeed));

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
            LeanSideOffset = leanSideOffset;
            LeanAngleDegrees = leanAngleDegrees;
            LeanSpeed = leanSpeed;
            AirJumps = airJumps;
            AirJumpSpeed = airJumpSpeed;
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
