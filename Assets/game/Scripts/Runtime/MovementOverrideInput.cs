using UnityEngine;

namespace Fps.Runtime
{
    /// <summary><see cref="IMovementOverride.Step"/> に渡す、その Tick の入力と接地。</summary>
    public readonly struct MovementOverrideInput
    {
        public MovementOverrideInput(Vector2 move, bool sprint, bool crouch, bool jumpPressed, bool isGrounded)
        {
            Move = move;
            Sprint = sprint;
            Crouch = crouch;
            JumpPressed = jumpPressed;
            IsGrounded = isGrounded;
        }

        /// <summary>移動入力。x が右、y が前。</summary>
        public Vector2 Move { get; }

        /// <summary>ダッシュしたいか。</summary>
        public bool Sprint { get; }

        /// <summary>しゃがみたいか。</summary>
        public bool Crouch { get; }

        /// <summary>この Tick で Jump が押されたか。</summary>
        public bool JumpPressed { get; }

        /// <summary>直前の Move で接地していたか。</summary>
        public bool IsGrounded { get; }
    }
}
