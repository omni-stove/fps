using NUnit.Framework;
using Shooter.Runtime;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Shooter.Tests.EditMode
{
    public sealed class HeldInputTests
    {
        private Mouse _mouse;
        private InputAction _aim;

        [SetUp]
        public void SetUp()
        {
            _mouse = InputSystem.AddDevice<Mouse>("HeldInputTestMouse");
            _aim = new InputAction("Aim", InputActionType.Button, "<Mouse>/rightButton");
            _aim.Enable();
        }

        [TearDown]
        public void TearDown()
        {
            _aim.Dispose();
            InputSystem.RemoveDevice(_mouse);
        }

        [Test]
        public void AButtonStillDownCountsAsHeldAfterTheActionIsCancelled()
        {
            Press(true);
            Assert.That(HeldInput.IsHeld(_aim), Is.True);

            // macOS で Shift を離すと Input System が割り当てを解決し直し、押していたアクションを取り消すのと同じ状態にする
            _aim.Reset();

            Assert.That(_aim.IsPressed(), Is.False, "the action alone forgets the held button");
            Assert.That(HeldInput.IsHeld(_aim), Is.True, "the button itself is still down");
        }

        [Test]
        public void ReleasedOrDisabledIsNotHeld()
        {
            Press(true);
            Press(false);
            Assert.That(HeldInput.IsHeld(_aim), Is.False);

            Press(true);
            _aim.Disable();
            Assert.That(HeldInput.IsHeld(_aim), Is.False, "a disabled action is never held");
            Assert.That(HeldInput.IsHeld(null), Is.False);
        }

        private void Press(bool down)
        {
            var state = new MouseState().WithButton(MouseButton.Right, down);
            InputSystem.QueueStateEvent(_mouse, state);
            InputSystem.Update();
        }
    }
}
