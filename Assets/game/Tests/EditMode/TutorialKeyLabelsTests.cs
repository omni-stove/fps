using System;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace Fps.Tests.EditMode
{
    public sealed class TutorialKeyLabelsTests
    {
        private InputAction _move;
        private InputAction _reload;

        [SetUp]
        public void SetUp()
        {
            // Assets/InputSystem_Actions.inputactions の Move と同じく、ゲームパッドのスティックの後に WASD と矢印キーを同じ合成に書く
            _move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            _move.AddBinding("<Gamepad>/leftStick", groups: ";Gamepad");
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w", groups: ";Keyboard&Mouse")
                .With("Up", "<Keyboard>/upArrow", groups: ";Keyboard&Mouse")
                .With("Down", "<Keyboard>/s", groups: ";Keyboard&Mouse")
                .With("Down", "<Keyboard>/downArrow", groups: ";Keyboard&Mouse")
                .With("Left", "<Keyboard>/a", groups: ";Keyboard&Mouse")
                .With("Left", "<Keyboard>/leftArrow", groups: ";Keyboard&Mouse")
                .With("Right", "<Keyboard>/d", groups: ";Keyboard&Mouse")
                .With("Right", "<Keyboard>/rightArrow", groups: ";Keyboard&Mouse");

            _reload = new InputAction("Reload", InputActionType.Button);
            _reload.AddBinding("<Keyboard>/r", groups: "Keyboard&Mouse");
            _reload.AddBinding("<Gamepad>/buttonWest", groups: "Gamepad");
        }

        [TearDown]
        public void TearDown()
        {
            _move.Dispose();
            _reload.Dispose();
        }

        [Test]
        public void TheKeyboardMoveIsWasdInKeyboardOrder()
        {
            Assert.That(TutorialKeyLabels.For(_move, InputDeviceKind.KeyboardMouse), Is.EqualTo("WASD"));
        }

        [Test]
        public void TheGamepadMoveIsTheStick()
        {
            var label = TutorialKeyLabels.For(_move, InputDeviceKind.Gamepad);

            Assert.That(label, Is.EqualTo(_move.GetBindingDisplayString(0).Trim()));
            Assert.That(label, Is.Not.Empty);
        }

        [Test]
        public void EachDeviceGetsItsOwnButton()
        {
            Assert.That(TutorialKeyLabels.For(_reload, InputDeviceKind.KeyboardMouse), Is.EqualTo("R"));
            Assert.That(TutorialKeyLabels.For(_reload, InputDeviceKind.Gamepad), Is.EqualTo(_reload.GetBindingDisplayString(1).Trim()));
            Assert.That(
                TutorialKeyLabels.For(_reload, InputDeviceKind.Gamepad),
                Is.Not.EqualTo(TutorialKeyLabels.For(_reload, InputDeviceKind.KeyboardMouse)));
        }

        [Test]
        public void ARebindIsShown()
        {
            _reload.ApplyBindingOverride(0, "<Keyboard>/t");

            Assert.That(TutorialKeyLabels.For(_reload, InputDeviceKind.KeyboardMouse), Is.EqualTo("T"));
        }

        [Test]
        public void ADeviceWithoutABindingIsEmpty()
        {
            using (var keyboardOnly = new InputAction("Cover", InputActionType.Button))
            {
                keyboardOnly.AddBinding("<Keyboard>/q", groups: "Keyboard&Mouse");

                Assert.That(TutorialKeyLabels.For(keyboardOnly, InputDeviceKind.Gamepad), Is.Empty);
            }
        }

        [Test]
        public void GroupsAreMatchedWholeAndWithoutCase()
        {
            var binding = new InputBinding("<Keyboard>/r") { groups = ";keyboard&mouse;Touch" };

            Assert.That(TutorialKeyLabels.InGroup(binding, TutorialKeyLabels.KeyboardMouseGroup), Is.True);
            Assert.That(TutorialKeyLabels.InGroup(binding, "Keyboard"), Is.False);
            Assert.That(TutorialKeyLabels.InGroup(binding, TutorialKeyLabels.GamepadGroup), Is.False);
            Assert.That(TutorialKeyLabels.InGroup(new InputBinding("<Keyboard>/r"), TutorialKeyLabels.KeyboardMouseGroup), Is.False);
        }

        [Test]
        public void RejectsNullAndUnknownDevices()
        {
            Assert.Throws<ArgumentNullException>(() => TutorialKeyLabels.For(null, InputDeviceKind.KeyboardMouse));
            Assert.Throws<ArgumentOutOfRangeException>(() => TutorialKeyLabels.GroupOf((InputDeviceKind)99));
        }
    }
}
