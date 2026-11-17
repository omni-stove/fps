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
            bool pronePressed = false,
            int lean = 0,
            bool jumpHeld = false)
        {
            if (lean < -1 || lean > 1)
            {
                throw new System.ArgumentOutOfRangeException(nameof(lean), lean, "lean must be -1, 0 or 1");
            }

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
            Lean = lean;
            JumpHeld = jumpHeld;
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

        public int Lean { get; }

        // 地上のジャンプは押した瞬間だけで跳ぶが、泳いでいる間は押している間ずっと上がるので、押したままかも運ぶ。
        public bool JumpHeld { get; }

        // 機器は渡したものを使うのは、何も押していない間にダッシュ・しゃがみのモードを切り替えないため。
        public static PlayerMoveInput Idle(InputDeviceKind sprintDevice, InputDeviceKind crouchDevice)
        {
            return new PlayerMoveInput(Vector2.zero, false, false, sprintDevice, false, false, crouchDevice, false, false);
        }
    }
}
