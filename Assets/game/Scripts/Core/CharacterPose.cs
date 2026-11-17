using System;

namespace Shooter.Core
{
    /// <summary>
    /// キャラクターの状態 (<see cref="CharacterPoseInput"/>) から、人型のリグの Animator に渡すパラメーター
    /// (<see cref="CharacterPoseParameters"/>) を決める。生成時に速さを検査し、以後は変わらない。
    /// <para>
    /// 姿勢の優先順は 死亡 &gt; ダウン &gt; それ以外。上位の状態のときは下位のパラメーターを寝かせる (false・0) ので、
    /// Animator は死亡・ダウンを他の姿勢と取り違えない。
    /// <list type="bullet">
    /// <item>死亡: Dead だけを立て、Speed は 0。</item>
    /// <item>ダウン: Downed だけを立て、Speed は 0。</item>
    /// <item>カバー: Cover に壁の高さを入れる。姿勢は壁の高さで決まるので Crouch は立てない。ADS なら覗いて構える (Aim も立てる)。</item>
    /// <item>ADS としゃがみは両方立ちうる (しゃがんで構える)。</item>
    /// <item>スライディング中は Slide と Crouch を立てる (滑る姿勢の無いリグはしゃがみで見せる)。カバー中は立てない。</item>
    /// <item>宙にいる間は足を動かさない (Speed は 0)。</item>
    /// <item>壁走り: WallRun に壁のある側を入れ、宙にいても走る速さで足を動かす。カバー中は立てない。</item>
    /// <item>水泳: Swimming を立て、宙にいても水をかく速さを歩きの速さでならす (0〜1)。壁走り・カバー中は立てない。</item>
    /// <item>壁走りと水泳の間は、しゃがみ・スライディング・リーンを寝かせる (体の向きがそれぞれの姿勢で決まる)。</item>
    /// <item>リーン: Lean に傾きの量 (-1 左〜1 右) を入れる。カバー中は 0 (覗き込みはカバーの姿勢で見せる)。</item>
    /// </list>
    /// 武器の種別はどの状態でもそのまま渡す。
    /// </para>
    /// </summary>
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

        /// <summary><see cref="CharacterPoseParameters.WallRun"/> の、壁走りしていない値。</summary>
        public const int WallRunNone = (int)WallRunSide.None;

        /// <summary><see cref="CharacterPoseParameters.WallRun"/> の、左の壁を走っている値。</summary>
        public const int WallRunLeft = (int)WallRunSide.Left;

        /// <summary><see cref="CharacterPoseParameters.WallRun"/> の、右の壁を走っている値。</summary>
        public const int WallRunRight = (int)WallRunSide.Right;

        /// <summary><see cref="CharacterPoseParameters.Cover"/> の、カバーしていない値。</summary>
        public const int CoverNone = 0;

        /// <summary><see cref="CharacterPoseParameters.Cover"/> の、低い壁 (<see cref="Core.CoverHeight.Low"/>) にカバーしている値。</summary>
        public const int CoverLow = 1;

        /// <summary><see cref="CharacterPoseParameters.Cover"/> の、高い壁 (<see cref="Core.CoverHeight.High"/>) にカバーしている値。</summary>
        public const int CoverHigh = 2;

        /// <summary><see cref="CharacterPoseParameters.WeaponCategory"/> の、武器を持っていない値。どの <see cref="Core.WeaponCategory"/> とも重ならない。</summary>
        public const int NoWeapon = -1;

        /// <summary><see cref="CharacterPoseParameters.Speed"/> の、止まっている値。</summary>
        public const float IdleSpeedValue = 0f;

        /// <summary><see cref="CharacterPoseParameters.Speed"/> の、歩き (しゃがみ中はしゃがみ歩き) の値。</summary>
        public const float WalkSpeedValue = 1f;

        /// <summary><see cref="CharacterPoseParameters.Speed"/> の、走りの値。</summary>
        public const float RunSpeedValue = 2f;

        /// <summary>これ未満の速さ (m/s) は止まっていると見なす。押し戻しや坂の小さな揺れで足が動かないようにする。</summary>
        public const float StillSpeed = 0.1f;

        /// <summary>立って歩くときの水平速度 (m/s)。</summary>
        public float WalkSpeed { get; }

        /// <summary>ダッシュ中の水平速度 (m/s)。<see cref="WalkSpeed"/> より大きい。</summary>
        public float SprintSpeed { get; }

        /// <summary>しゃがみ中の水平速度 (m/s)。</summary>
        public float CrouchSpeed { get; }

        /// <param name="walkSpeed">歩きの水平速度。<see cref="StillSpeed"/> より大きい有限値。</param>
        /// <param name="sprintSpeed">ダッシュ中の水平速度。walkSpeed より大きい有限値。</param>
        /// <param name="crouchSpeed">しゃがみ中の水平速度。<see cref="StillSpeed"/> より大きい有限値。</param>
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

        /// <summary>motor の歩き・ダッシュ・しゃがみの速さを使う。速さが <see cref="CharacterPose(float, float, float)"/> の条件を満たさなければ例外を投げる。</summary>
        public static CharacterPose FromMotor(MotorSpec motor)
        {
            if (motor == null)
            {
                throw new ArgumentNullException(nameof(motor));
            }

            return new CharacterPose(motor.WalkSpeed, motor.SprintSpeed, motor.CrouchSpeed);
        }

        /// <summary>input の状態に合う Animator のパラメーターを返す。</summary>
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

        /// <summary>壁のある側 side を <see cref="CharacterPoseParameters.WallRun"/> の値にする。</summary>
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

        /// <summary>
        /// 水平の速さ horizontalSpeed (m/s) を <see cref="CharacterPoseParameters.Speed"/> の値にならす。
        /// しゃがみ中は <see cref="CrouchSpeed"/> で 1 (ダッシュしても走らない)。立っているときは <see cref="WalkSpeed"/> で 1、
        /// ダッシュ中だけ <see cref="SprintSpeed"/> へ向けて 2 まで上げる。ダッシュしていなければ回避などで速くても 1 で止める。
        /// </summary>
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

        /// <summary>壁の高さ height を <see cref="CharacterPoseParameters.Cover"/> の値にする。カバーしていなければ (null なら) <see cref="CoverNone"/>。</summary>
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
