using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace Fps.Tests.EditMode
{
    public sealed class InputDeviceKindsTests
    {
        [Test]
        public void KeyboardAndPointersAreKeyboardMouse()
        {
            AssertKind<Keyboard>(InputDeviceKind.KeyboardMouse);
            AssertKind<Mouse>(InputDeviceKind.KeyboardMouse);
            AssertKind<Touchscreen>(InputDeviceKind.KeyboardMouse);
        }

        [Test]
        public void GamepadIsGamepad()
        {
            AssertKind<Gamepad>(InputDeviceKind.Gamepad);
        }

        [TestCase(InputDeviceKind.KeyboardMouse)]
        [TestCase(InputDeviceKind.Gamepad)]
        public void NoDeviceOrActionGivesTheFallback(InputDeviceKind fallback)
        {
            Assert.That(InputDeviceKinds.Of((InputDevice)null, fallback), Is.EqualTo(fallback));
            Assert.That(InputDeviceKinds.Of((InputAction)null, fallback), Is.EqualTo(fallback));
        }

        [TestCase(InputDeviceKind.KeyboardMouse)]
        [TestCase(InputDeviceKind.Gamepad)]
        public void IdleActionGivesTheFallback(InputDeviceKind fallback)
        {
            var action = new InputAction("Sprint", InputActionType.Button, "<Gamepad>/leftStickPress");
            try
            {
                Assert.That(action.activeControl, Is.Null);
                Assert.That(InputDeviceKinds.Of(action, fallback), Is.EqualTo(fallback));
            }
            finally
            {
                action.Dispose();
            }
        }

        private static void AssertKind<T>(InputDeviceKind expected)
            where T : InputDevice
        {
            // 両方の fallback で同じ結果になることで、fallback ではなく機器から決めたと分かる。
            var device = InputSystem.AddDevice<T>();
            try
            {
                Assert.That(InputDeviceKinds.Of(device, InputDeviceKind.KeyboardMouse), Is.EqualTo(expected));
                Assert.That(InputDeviceKinds.Of(device, InputDeviceKind.Gamepad), Is.EqualTo(expected));
            }
            finally
            {
                InputSystem.RemoveDevice(device);
            }
        }
    }
}
