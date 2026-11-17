using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public readonly struct PlayerMoveInput
    {
        public PlayerMoveInput(
            Vector2 move,
            bool sprintHeld,
            bool sprintPressed,
            InputDeviceKind sprintDevice,
            bool crouchHeld,
            bool crouchPressed,
            InputDeviceKind crouchDevice,
            bool jumpPressed,
            bool dodgePressed,
            bool pronePressed = false)
        {
            Move = move;
            SprintHeld = sprintHeld;
            SprintPressed = sprintPressed;
            SprintDevice = sprintDevice;
            CrouchHeld = crouchHeld;
            CrouchPressed = crouchPressed;
            CrouchDevice = crouchDevice;
            JumpPressed = jumpPressed;
            DodgePressed = dodgePressed;
            PronePressed = pronePressed;
        }

        public Vector2 Move { get; }

        public bool SprintHeld { get; }

        public bool SprintPressed { get; }

        public InputDeviceKind SprintDevice { get; }

        public bool CrouchHeld { get; }

        public bool CrouchPressed { get; }

        public InputDeviceKind CrouchDevice { get; }

        public bool JumpPressed { get; }

        public bool DodgePressed { get; }

        public bool PronePressed { get; }

        // 機器は渡したものを使うのは、何も押していない間にダッシュ・しゃがみのモードを切り替えないため。
        public static PlayerMoveInput Idle(InputDeviceKind sprintDevice, InputDeviceKind crouchDevice)
        {
            return new PlayerMoveInput(Vector2.zero, false, false, sprintDevice, false, false, crouchDevice, false, false);
        }
    }
}
