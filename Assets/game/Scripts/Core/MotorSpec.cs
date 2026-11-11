using System;

namespace Fps.Core
{
    /// <summary>
    /// <see cref="PlayerMotor"/> の設定。速度は m/s、高さは m、重力は m/s² (正の値で下向きに適用する)。
    /// </summary>
    public sealed class MotorSpec
    {
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

        /// <param name="walkSpeed">歩きの水平速度。0 以上。</param>
        /// <param name="sprintSpeed">ダッシュ中の水平速度。0 以上。</param>
        /// <param name="jumpHeight">ジャンプで到達する高さ。0 以上。</param>
        /// <param name="gravity">重力加速度の大きさ。正の値。</param>
        /// <param name="airControl">空中制御の割合。0〜1。</param>
        public MotorSpec(float walkSpeed, float sprintSpeed, float jumpHeight, float gravity, float airControl)
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

            WalkSpeed = walkSpeed;
            SprintSpeed = sprintSpeed;
            JumpHeight = jumpHeight;
            Gravity = gravity;
            AirControl = airControl;
        }
    }
}
