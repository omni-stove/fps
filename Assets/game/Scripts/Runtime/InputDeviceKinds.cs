using Shooter.Core;
using UnityEngine.InputSystem;

namespace Shooter.Runtime
{
    public static class InputDeviceKinds
    {
        public static InputDeviceKind Of(InputAction action, InputDeviceKind fallback)
        {
            return action == null ? fallback : Of(action.activeControl?.device, fallback);
        }

        public static InputDeviceKind Of(InputDevice device, InputDeviceKind fallback)
        {
            if (device == null)
            {
                return fallback;
            }

            return device is Keyboard || device is Pointer ? InputDeviceKind.KeyboardMouse : InputDeviceKind.Gamepad;
        }
    }
}
