using Shooter.Core;
using UnityEngine.InputSystem;

namespace Shooter.Runtime
{
    public static class TutorialKeyLabels
    {
        public const string KeyboardMouseGroup = InputKeyLabels.KeyboardMouseGroup;
        public const string GamepadGroup = InputKeyLabels.GamepadGroup;

        public static string For(InputAction action, InputDeviceKind device)
        {
            return InputKeyLabels.For(action, device);
        }
    }
}
