using System;
using System.Globalization;

namespace Shooter.Core
{
    public readonly struct CharacterPoseInput : IEquatable<CharacterPoseInput>
    {
        public float HorizontalSpeed { get; }

        public bool IsGrounded { get; }

        public bool IsCrouching { get; }

        public bool IsSprinting { get; }

        public bool IsAiming { get; }

        public CoverHeight? CoverHeight { get; }

        public bool IsDowned { get; }

        public bool IsDead { get; }

        public WeaponCategory? WeaponCategory { get; }

        public bool IsSliding { get; }

        public float Lean { get; }

        public bool IsSwimming { get; }

        public WallRunSide WallRun { get; }

        public bool IsInCover => CoverHeight.HasValue;

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
