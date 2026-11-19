using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace Shooter.Tests.EditMode
{
    // 表記の作り方そのものは InputKeyLabelsTests で見る。ここではチュートリアルが同じ表記を出すことだけを見る。
    public sealed class TutorialKeyLabelsTests
    {
        [Test]
        public void TheTutorialShowsTheSameLabelAsTheRestOfTheGame()
        {
            using (var reload = new InputAction("Reload", InputActionType.Button))
            {
                reload.AddBinding("<Keyboard>/r", groups: "Keyboard&Mouse");
                reload.AddBinding("<Gamepad>/buttonWest", groups: "Gamepad");

                Assert.That(TutorialKeyLabels.For(reload, InputDeviceKind.KeyboardMouse), Is.EqualTo("R"));
                Assert.That(
                    TutorialKeyLabels.For(reload, InputDeviceKind.Gamepad),
                    Is.EqualTo(InputKeyLabels.For(reload, InputDeviceKind.Gamepad)));
            }
        }

        [Test]
        public void TheGroupsAreTheSharedOnes()
        {
            Assert.That(TutorialKeyLabels.KeyboardMouseGroup, Is.EqualTo(InputKeyLabels.KeyboardMouseGroup));
            Assert.That(TutorialKeyLabels.GamepadGroup, Is.EqualTo(InputKeyLabels.GamepadGroup));
        }
    }
}
