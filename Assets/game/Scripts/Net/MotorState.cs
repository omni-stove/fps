using UnityEngine;

namespace Fps.Net
{
    /// <summary><see cref="NetworkPlayerMotor"/> の 1 tick 分の状態。tick の入力を適用した後の値。</summary>
    public readonly struct MotorState
    {
        /// <summary>CharacterController の中心のワールド座標 (m)。</summary>
        public Vector3 Position { get; }

        /// <summary>速度 (m/s)。</summary>
        public Vector3 Velocity { get; }

        /// <summary>この tick の移動の結果、接地しているか。</summary>
        public bool IsGrounded { get; }

        public MotorState(Vector3 position, Vector3 velocity, bool isGrounded)
        {
            Position = position;
            Velocity = velocity;
            IsGrounded = isGrounded;
        }

        /// <summary>位置と速度がすべて有限値か。ネットワークから受け取った値の検査に使う。</summary>
        public bool IsFinite => NetMath.IsFinite(Position) && NetMath.IsFinite(Velocity);

        /// <summary>予測と確定状態のずれ。位置の距離 (m)。</summary>
        public static float PositionError(MotorState predicted, MotorState authoritative)
        {
            return Vector3.Distance(predicted.Position, authoritative.Position);
        }
    }
}
