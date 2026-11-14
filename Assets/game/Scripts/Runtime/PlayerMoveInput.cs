using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// <see cref="PlayerController.ApplyInput"/> に渡す 1 フレーム分の移動とボタンの入力。Play 中は PlayerController が Input Actions から作る。
    /// </summary>
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

        /// <summary>移動入力。x が右、y が前。</summary>
        public Vector2 Move { get; }

        /// <summary>Sprint が押されているか。</summary>
        public bool SprintHeld { get; }

        /// <summary>Sprint がこのフレームに押されたか。</summary>
        public bool SprintPressed { get; }

        /// <summary>ダッシュのモードを決める機器。</summary>
        public InputDeviceKind SprintDevice { get; }

        /// <summary>Crouch が押されているか。</summary>
        public bool CrouchHeld { get; }

        /// <summary>Crouch がこのフレームに押されたか。</summary>
        public bool CrouchPressed { get; }

        /// <summary>しゃがみのモードを決める機器。</summary>
        public InputDeviceKind CrouchDevice { get; }

        /// <summary>Jump がこのフレームに押されたか。</summary>
        public bool JumpPressed { get; }

        /// <summary>Dodge がこのフレームに押されたか。</summary>
        public bool DodgePressed { get; }

        /// <summary>Prone がこのフレームに押されたか。</summary>
        public bool PronePressed { get; }

        /// <summary>何も押していない入力。機器は渡したものを使い、モードを切り替えない。</summary>
        public static PlayerMoveInput Idle(InputDeviceKind sprintDevice, InputDeviceKind crouchDevice)
        {
            return new PlayerMoveInput(Vector2.zero, false, false, sprintDevice, false, false, crouchDevice, false, false);
        }
    }
}
