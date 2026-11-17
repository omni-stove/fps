using UnityEngine;

namespace Shooter.Runtime
{
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

        public Vector2 Move { get; }

        public bool Sprint { get; }

        public bool Crouch { get; }

        public bool JumpPressed { get; }

        public bool IsGrounded { get; }
    }
}
