using System.Text.RegularExpressions;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Shooter.Tests.EditMode
{
    public sealed class InteractKeyLabelTests
    {
        [Test]
        public void ANullActionIsEmptyAndLogged()
        {
            var label = new InteractKeyLabel();

            LogAssert.Expect(LogType.Log, "[InteractKey] label='' device=KeyboardMouse action=null");
            Assert.That(label.For(null), Is.Empty);
        }

        [Test]
        public void TheKeyboardKeyIsShownAndLoggedOnceWhileItStaysTheSame()
        {
            using (var interact = new InputAction("Interact", InputActionType.Button))
            {
                interact.AddBinding("<Keyboard>/e", groups: "Keyboard&Mouse");
                var label = new InteractKeyLabel();

                LogAssert.Expect(LogType.Log, new Regex(@"^\[InteractKey\] label='E' device=KeyboardMouse action=Interact enabled=False bindings=\[<Keyboard>/e groups='Keyboard&Mouse' display='E'\]"));
                Assert.That(label.For(interact), Is.EqualTo("E"));
                Assert.That(label.For(interact), Is.EqualTo("E"));
                LogAssert.NoUnexpectedReceived();
            }
        }

        [Test]
        public void DescribeListsEveryBinding()
        {
            using (var interact = new InputAction("Interact", InputActionType.Button))
            {
                interact.AddBinding("<Keyboard>/e", groups: "Keyboard&Mouse");
                interact.AddBinding("<Gamepad>/buttonNorth", groups: "Gamepad");

                var text = InteractKeyLabel.Describe(interact, null);

                Assert.That(text, Does.StartWith("action=Interact enabled=False bindings=[<Keyboard>/e groups='Keyboard&Mouse' display='E', <Gamepad>/buttonNorth groups='Gamepad' display="));
                Assert.That(text, Does.EndWith("]"));
            }
        }
    }
}
