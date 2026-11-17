using System;

namespace Shooter.Core
{
    public sealed class CharacterPose
    {
        public const string SpeedParameter = "Speed";
        public const string CrouchParameter = "Crouch";
        public const string AimParameter = "Aim";
        public const string CoverParameter = "Cover";
        public const string DownedParameter = "Downed";
        public const string DeadParameter = "Dead";
        public const string WeaponCategoryParameter = "WeaponCategory";
        public const string SlideParameter = "Slide";
        public const string LeanParameter = "Lean";
        public const string SwimmingParameter = "Swimming";
        public const string WallRunParameter = "WallRun";

        public const int WallRunNone = (int)WallRunSide.None;

        public const int WallRunLeft = (int)WallRunSide.Left;

        public const int WallRunRight = (int)WallRunSide.Right;

        public const int CoverNone = 0;

        public const int CoverLow = 1;

        public const int CoverHigh = 2;

        // どの WeaponCategory とも重ならないよう負の値にする。
        public const int NoWeapon = -1;

        public const float IdleSpeedValue = 0f;

        public const float WalkSpeedValue = 1f;

        public const float RunSpeedValue = 2f;

        // 押し戻しや坂の小さな揺れで足が動かないよう、これ未満の速さ (m/s) は止まっていると見なす。
        public const float StillSpeed = 0.1f;

        public float WalkSpeed { get; }

        public float SprintSpeed { get; }

        public float CrouchSpeed { get; }

        public CharacterPose(float walkSpeed, float sprintSpeed, float crouchSpeed)
        {
            // 速さで割って 0〜1 にならすので、止まっていると見なす速さ以下だと歩いても 1 に届かない。
            if (!(walkSpeed > StillSpeed) || float.IsInfinity(walkSpeed))
            {
                throw new ArgumentOutOfRangeException(nameof(walkSpeed), walkSpeed, "walkSpeed must be finite and greater than StillSpeed");
            }

            // 同じ値を許すと、歩きから走りへ寄せる幅が 0 になり割り算できない。
            if (!(sprintSpeed > walkSpeed) || float.IsInfinity(sprintSpeed))
            {
                throw new ArgumentOutOfRangeException(nameof(sprintSpeed), sprintSpeed, "sprintSpeed must be finite and greater than walkSpeed");
            }

            if (!(crouchSpeed > StillSpeed) || float.IsInfinity(crouchSpeed))
            {
                throw new ArgumentOutOfRangeException(nameof(crouchSpeed), crouchSpeed, "crouchSpeed must be finite and greater than StillSpeed");
            }

            WalkSpeed = walkSpeed;
            SprintSpeed = sprintSpeed;
            CrouchSpeed = crouchSpeed;
        }

        public static CharacterPose FromMotor(MotorSpec motor)
        {
            if (motor == null)
            {
                throw new ArgumentNullException(nameof(motor));
            }

            return new CharacterPose(motor.WalkSpeed, motor.SprintSpeed, motor.CrouchSpeed);
        }

        // 死亡・ダウンのときは他のパラメーターを寝かせ、Animator が死亡・ダウンを他の姿勢と取り違えないようにする。
        // カバー中は姿勢が壁の高さで決まるので Crouch は立てない。スライディング中に Crouch も立てるのは、滑る姿勢の無いリグをしゃがみで見せるため。
        // 壁走り・水泳の間はしゃがみ・スライディング・リーンを寝かせるのは、体の向きがそれぞれの姿勢で決まるため。カバー中にリーンを 0 にするのは、覗き込みをカバーの姿勢で見せるため。
        public CharacterPoseParameters Evaluate(CharacterPoseInput input)
        {
            var weapon = input.WeaponCategory.HasValue ? (int)input.WeaponCategory.Value : NoWeapon;

            if (input.IsDead)
            {
                return new CharacterPoseParameters(IdleSpeedValue, false, false, CoverNone, false, true, weapon);
            }

            if (input.IsDowned)
            {
                return new CharacterPoseParameters(IdleSpeedValue, false, false, CoverNone, true, false, weapon);
            }

            var cover = CoverValue(input.CoverHeight);
            var wallRun = cover == CoverNone ? WallRunValue(input.WallRun) : WallRunNone;
            var swimming = input.IsSwimming && cover == CoverNone && wallRun == WallRunNone;
            var free = cover == CoverNone && wallRun == WallRunNone && !swimming;
            var slide = input.IsSliding && free;
            var crouch = (input.IsCrouching || slide) && free;
            var lean = free ? input.Lean : 0f;

            float speed;
            if (wallRun != WallRunNone)
            {
                speed = SpeedValue(input.HorizontalSpeed, false, true);
            }
            else if (swimming)
            {
                speed = SpeedValue(input.HorizontalSpeed, false, false);
            }
            else
            {
                speed = input.IsGrounded ? SpeedValue(input.HorizontalSpeed, crouch, input.IsSprinting) : IdleSpeedValue;
            }

            return new CharacterPoseParameters(speed, crouch, input.IsAiming, cover, false, false, weapon, slide, lean, swimming, wallRun);
        }

        public static int WallRunValue(WallRunSide side)
        {
            switch (side)
            {
                case WallRunSide.None:
                    return WallRunNone;
                case WallRunSide.Left:
                    return WallRunLeft;
                case WallRunSide.Right:
                    return WallRunRight;
                default:
                    throw new ArgumentOutOfRangeException(nameof(side), side, "side is not a defined WallRunSide");
            }
        }

        // ダッシュしていなければ 1 で止めるのは、回避などで速く動いても走る姿勢にしないため。
        public float SpeedValue(float horizontalSpeed, bool crouching, bool sprinting)
        {
            if (!(horizontalSpeed >= 0f) || float.IsInfinity(horizontalSpeed))
            {
                throw new ArgumentOutOfRangeException(nameof(horizontalSpeed), horizontalSpeed, "horizontalSpeed must be finite and not negative");
            }

            if (horizontalSpeed < StillSpeed)
            {
                return IdleSpeedValue;
            }

            if (crouching)
            {
                return Math.Min(horizontalSpeed / CrouchSpeed, WalkSpeedValue);
            }

            if (horizontalSpeed <= WalkSpeed || !sprinting)
            {
                return Math.Min(horizontalSpeed / WalkSpeed, WalkSpeedValue);
            }

            var toSprint = (horizontalSpeed - WalkSpeed) / (SprintSpeed - WalkSpeed);
            return WalkSpeedValue + Math.Min(toSprint, RunSpeedValue - WalkSpeedValue);
        }

        public static int CoverValue(CoverHeight? height)
        {
            if (!height.HasValue)
            {
                return CoverNone;
            }

            switch (height.Value)
            {
                case CoverHeight.Low:
                    return CoverLow;
                case CoverHeight.High:
                    return CoverHigh;
                default:
                    throw new ArgumentOutOfRangeException(nameof(height), height, "height is not a defined CoverHeight");
            }
        }
    }
}
