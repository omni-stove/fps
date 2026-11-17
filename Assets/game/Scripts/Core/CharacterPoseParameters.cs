using System;
using System.Globalization;

namespace Shooter.Core
{
    public readonly struct CharacterPoseParameters : IEquatable<CharacterPoseParameters>
    {
        public float Speed { get; }

        public bool Crouch { get; }

        public bool Aim { get; }

        public int Cover { get; }

        public bool Downed { get; }

        public bool Dead { get; }

        public int WeaponCategory { get; }

        public bool Slide { get; }

        public float Lean { get; }

        public bool Swimming { get; }

        public int WallRun { get; }

        public CharacterPoseParameters(
            float speed,
            bool crouch,
            bool aim,
            int cover,
            bool downed,
            bool dead,
            int weaponCategory,
            bool slide = false,
            float lean = 0f,
            bool swimming = false,
            int wallRun = CharacterPose.WallRunNone)
        {
            if (!(speed >= 0f && speed <= CharacterPose.RunSpeedValue))
            {
                throw new ArgumentOutOfRangeException(nameof(speed), speed, "speed must be between 0 and 2");
            }

            if (cover != CharacterPose.CoverNone && cover != CharacterPose.CoverLow && cover != CharacterPose.CoverHigh)
            {
                throw new ArgumentOutOfRangeException(nameof(cover), cover, "cover must be CoverNone, CoverLow or CoverHigh");
            }

            if (weaponCategory != CharacterPose.NoWeapon && !Enum.IsDefined(typeof(Core.WeaponCategory), weaponCategory))
            {
                throw new ArgumentOutOfRangeException(nameof(weaponCategory), weaponCategory, "weaponCategory must be NoWeapon or a defined WeaponCategory");
            }

            if (!(lean >= -1f && lean <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(lean), lean, "lean must be between -1 and 1");
            }

            if (wallRun != CharacterPose.WallRunNone && wallRun != CharacterPose.WallRunLeft && wallRun != CharacterPose.WallRunRight)
            {
                throw new ArgumentOutOfRangeException(nameof(wallRun), wallRun, "wallRun must be WallRunNone, WallRunLeft or WallRunRight");
            }

            Speed = speed;
            Crouch = crouch;
            Aim = aim;
            Cover = cover;
            Downed = downed;
            Dead = dead;
            WeaponCategory = weaponCategory;
            Slide = slide;
            Lean = lean;
            Swimming = swimming;
            WallRun = wallRun;
        }

        public bool Equals(CharacterPoseParameters other)
        {
            return Speed.Equals(other.Speed)
                && Crouch == other.Crouch
                && Aim == other.Aim
                && Cover == other.Cover
                && Downed == other.Downed
                && Dead == other.Dead
                && WeaponCategory == other.WeaponCategory
                && Slide == other.Slide
                && Lean.Equals(other.Lean)
                && Swimming == other.Swimming
                && WallRun == other.WallRun;
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterPoseParameters other && Equals(other);
        }

        public static bool operator ==(CharacterPoseParameters left, CharacterPoseParameters right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CharacterPoseParameters left, CharacterPoseParameters right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            var flags = (Crouch ? 1 : 0) | (Aim ? 2 : 0) | (Downed ? 4 : 0) | (Dead ? 8 : 0) | (Slide ? 16 : 0) | (Swimming ? 32 : 0);
            return HashCode.Combine(Speed, flags, Cover, WeaponCategory, Lean, WallRun);
        }

        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "Speed={0:0.00} Crouch={1} Aim={2} Cover={3} Downed={4} Dead={5} WeaponCategory={6} Lean={7:0.00} Swimming={8} WallRun={9} Slide={10}",
                Speed,
                Crouch,
                Aim,
                Cover,
                Downed,
                Dead,
                WeaponCategory,
                Lean,
                Swimming,
                WallRun,
                Slide);
        }
    }
}
