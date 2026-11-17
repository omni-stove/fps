using System;
using System.Globalization;

namespace Shooter.Core
{
    /// <summary>
    /// 人型のリグの姿勢を決めるための、キャラクターの今の状態。<see cref="CharacterPose.Evaluate"/> に渡す。
    /// 生成時に値を検査する。<c>default</c> は「止まって宙にいる・何もしていない・カバーなし・武器なし」。
    /// </summary>
    public readonly struct CharacterPoseInput : IEquatable<CharacterPoseInput>
    {
        /// <summary>水平の移動の速さ (m/s)。0 以上の有限値。</summary>
        public float HorizontalSpeed { get; }

        /// <summary>足が地面に付いているか。</summary>
        public bool IsGrounded { get; }

        public bool IsCrouching { get; }

        public bool IsSprinting { get; }

        /// <summary>覗き込んで (ADS で) 構えているか。</summary>
        public bool IsAiming { get; }

        /// <summary>カバーしている壁の高さ。カバーしていなければ null。</summary>
        public CoverHeight? CoverHeight { get; }

        public bool IsDowned { get; }

        public bool IsDead { get; }

        /// <summary>今持っている武器の種別。持っていなければ null。</summary>
        public WeaponCategory? WeaponCategory { get; }

        /// <summary>スライディングの最中か。</summary>
        public bool IsSliding { get; }

        /// <summary>体を横へ傾けている量。-1 (左へ傾け切り)〜1 (右へ傾け切り)。0 は傾けていない。</summary>
        public float Lean { get; }

        public bool IsSwimming { get; }

        /// <summary>壁走りしている壁がどちら側にあるか。走っていなければ <see cref="WallRunSide.None"/>。</summary>
        public WallRunSide WallRun { get; }

        public bool IsInCover => CoverHeight.HasValue;

        /// <param name="horizontalSpeed">水平の移動の速さ (m/s)。0 以上の有限値。</param>
        /// <param name="isGrounded">接地しているか。</param>
        /// <param name="isCrouching">しゃがんでいるか。</param>
        /// <param name="isSprinting">ダッシュしているか。</param>
        /// <param name="isAiming">ADS で構えているか。</param>
        /// <param name="coverHeight">カバーしている壁の高さ。カバーしていなければ null。定義済みの値。</param>
        /// <param name="isDowned">ダウンしているか。</param>
        /// <param name="isDead">死んでいるか。</param>
        /// <param name="weaponCategory">持っている武器の種別。持っていなければ null。定義済みの値。</param>
        /// <param name="isSliding">スライディングの最中か。</param>
        /// <param name="lean">体を横へ傾けている量。-1 (左)〜1 (右)。</param>
        /// <param name="isSwimming">泳いでいるか。</param>
        /// <param name="wallRun">壁走りしている壁の側。定義済みの値。</param>
        public CharacterPoseInput(
            float horizontalSpeed,
            bool isGrounded,
            bool isCrouching,
            bool isSprinting,
            bool isAiming,
            CoverHeight? coverHeight,
            bool isDowned,
            bool isDead,
            WeaponCategory? weaponCategory,
            bool isSliding = false,
            float lean = 0f,
            bool isSwimming = false,
            WallRunSide wallRun = WallRunSide.None)
        {
            if (!(horizontalSpeed >= 0f) || float.IsInfinity(horizontalSpeed))
            {
                throw new ArgumentOutOfRangeException(nameof(horizontalSpeed), horizontalSpeed, "horizontalSpeed must be finite and not negative");
            }

            if (coverHeight.HasValue && !Enum.IsDefined(typeof(CoverHeight), coverHeight.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(coverHeight), coverHeight, "coverHeight is not a defined CoverHeight");
            }

            if (weaponCategory.HasValue && !Enum.IsDefined(typeof(WeaponCategory), weaponCategory.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(weaponCategory), weaponCategory, "weaponCategory is not a defined WeaponCategory");
            }

            if (!(lean >= -1f && lean <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(lean), lean, "lean must be between -1 and 1");
            }

            if (!Enum.IsDefined(typeof(WallRunSide), wallRun))
            {
                throw new ArgumentOutOfRangeException(nameof(wallRun), wallRun, "wallRun is not a defined WallRunSide");
            }

            HorizontalSpeed = horizontalSpeed;
            IsGrounded = isGrounded;
            IsCrouching = isCrouching;
            IsSprinting = isSprinting;
            IsAiming = isAiming;
            CoverHeight = coverHeight;
            IsDowned = isDowned;
            IsDead = isDead;
            WeaponCategory = weaponCategory;
            IsSliding = isSliding;
            Lean = lean;
            IsSwimming = isSwimming;
            WallRun = wallRun;
        }

        /// <summary>リーン・水泳・壁走りを motor の値に替えた入力。motor の値が範囲の外なら例外を投げる。</summary>
        public CharacterPoseInput WithMotor(CharacterMotorState motor)
        {
            return new CharacterPoseInput(
                HorizontalSpeed,
                IsGrounded,
                IsCrouching,
                IsSprinting,
                IsAiming,
                CoverHeight,
                IsDowned,
                IsDead,
                WeaponCategory,
                IsSliding,
                motor.Lean.Amount,
                motor.IsSwimming,
                motor.WallRun.Side);
        }

        public bool Equals(CharacterPoseInput other)
        {
            return HorizontalSpeed.Equals(other.HorizontalSpeed)
                && IsGrounded == other.IsGrounded
                && IsCrouching == other.IsCrouching
                && IsSprinting == other.IsSprinting
                && IsAiming == other.IsAiming
                && CoverHeight == other.CoverHeight
                && IsDowned == other.IsDowned
                && IsDead == other.IsDead
                && WeaponCategory == other.WeaponCategory
                && IsSliding == other.IsSliding
                && Lean.Equals(other.Lean)
                && IsSwimming == other.IsSwimming
                && WallRun == other.WallRun;
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterPoseInput other && Equals(other);
        }

        public static bool operator ==(CharacterPoseInput left, CharacterPoseInput right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CharacterPoseInput left, CharacterPoseInput right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            var flags = (IsGrounded ? 1 : 0)
                | (IsCrouching ? 2 : 0)
                | (IsSprinting ? 4 : 0)
                | (IsAiming ? 8 : 0)
                | (IsDowned ? 16 : 0)
                | (IsDead ? 32 : 0)
                | (IsSliding ? 64 : 0)
                | (IsSwimming ? 128 : 0);
            return HashCode.Combine(HorizontalSpeed, flags, CoverHeight, WeaponCategory, Lean, WallRun);
        }

        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "speed={0:0.00} grounded={1} crouch={2} sprint={3} aim={4} cover={5} downed={6} dead={7} weapon={8} lean={9:0.00} swim={10} wallRun={11} slide={12}",
                HorizontalSpeed,
                IsGrounded,
                IsCrouching,
                IsSprinting,
                IsAiming,
                CoverHeight.HasValue ? CoverHeight.Value.ToString() : "-",
                IsDowned,
                IsDead,
                WeaponCategory.HasValue ? WeaponCategory.Value.ToString() : "-",
                Lean,
                IsSwimming,
                WallRun,
                IsSliding);
        }
    }
}
