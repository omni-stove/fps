using Fps.Runtime;
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace Fps.Tests.EditMode
{
    public sealed class InputActionPathsTests
    {
        private static InputAction Find(string path)
        {
            var actions = InputSystem.actions;
            Assert.That(actions, Is.Not.Null, "Project-wide Input Actions must be assigned");
            var action = actions.FindAction(path);
            Assert.That(action, Is.Not.Null, path);
            return action;
        }

        [TestCase(InputActionPaths.Move)]
        [TestCase(InputActionPaths.Look)]
        [TestCase(InputActionPaths.Attack)]
        [TestCase(InputActionPaths.Aim)]
        [TestCase(InputActionPaths.Reload)]
        [TestCase(InputActionPaths.Jump)]
        [TestCase(InputActionPaths.Sprint)]
        [TestCase(InputActionPaths.Crouch)]
        [TestCase(InputActionPaths.Interact)]
        [TestCase(InputActionPaths.Previous)]
        [TestCase(InputActionPaths.Next)]
        [TestCase(InputActionPaths.Cover)]
        [TestCase(InputActionPaths.SwapShoulder)]
        [TestCase(InputActionPaths.Pause)]
        [TestCase(InputActionPaths.Scoreboard)]
        [TestCase(InputActionPaths.Dodge)]
        [TestCase(InputActionPaths.Grenade)]
        public void EveryPathExistsInTheProjectWideActions(string path)
        {
            Find(path);
        }

        [TestCase(InputActionPaths.Pause, "<Keyboard>/escape", "<Gamepad>/start")]
        [TestCase(InputActionPaths.Scoreboard, "<Keyboard>/tab", "<Gamepad>/select")]
        [TestCase(InputActionPaths.Dodge, "<Keyboard>/leftAlt", "<Gamepad>/dpad/down")]
        [TestCase(InputActionPaths.Grenade, "<Keyboard>/g", "<Gamepad>/leftShoulder")]
        public void NewButtonsAreBoundToOneKeyAndOneGamepadControlUsedByNoOtherPlayerAction(string path, string key, string gamepad)
        {
            var action = Find(path);

            Assert.That(action.type, Is.EqualTo(InputActionType.Button));
            Assert.That(action.bindings, Has.Some.Matches<InputBinding>(b => b.path == key && b.groups == "Keyboard&Mouse"));
            Assert.That(action.bindings, Has.Some.Matches<InputBinding>(b => b.path == gamepad && b.groups == "Gamepad"));

            foreach (var other in action.actionMap.actions)
            {
                if (other == action)
                {
                    continue;
                }

                Assert.That(other.bindings, Has.None.Matches<InputBinding>(b => b.path == key || b.path == gamepad), other.name);
            }
        }

        [Test]
        public void AimIsAButtonOnTheRightMouseButtonAndLeftTrigger()
        {
            var aim = Find(InputActionPaths.Aim);

            Assert.That(aim.type, Is.EqualTo(InputActionType.Button));
            Assert.That(aim.bindings, Has.Some.Matches<InputBinding>(b => b.path == "<Mouse>/rightButton"));
            Assert.That(aim.bindings, Has.Some.Matches<InputBinding>(b => b.path == "<Gamepad>/leftTrigger"));
        }

        [Test]
        public void AttackUsesTheRightTriggerAndNeitherEnterNorButtonWest()
        {
            var attack = Find(InputActionPaths.Attack);

            Assert.That(attack.bindings, Has.Some.Matches<InputBinding>(b => b.path == "<Mouse>/leftButton"));
            Assert.That(attack.bindings, Has.Some.Matches<InputBinding>(b => b.path == "<Gamepad>/rightTrigger"));
            Assert.That(attack.bindings, Has.None.Matches<InputBinding>(b => b.path == "<Keyboard>/enter"));
            Assert.That(attack.bindings, Has.None.Matches<InputBinding>(b => b.path == "<Gamepad>/buttonWest"));
        }

        [Test]
        public void ReloadAndRestartUseRAndButtonWest()
        {
            var reload = Find(InputActionPaths.Reload);

            Assert.That(reload.bindings, Has.Some.Matches<InputBinding>(b => b.path == "<Keyboard>/r"));
            Assert.That(reload.bindings, Has.Some.Matches<InputBinding>(b => b.path == "<Gamepad>/buttonWest"));
            Assert.That(MatchRunner.RestartActionPath, Is.EqualTo(InputActionPaths.Reload));
        }

        [Test]
        public void CoverIsAButtonOnQAndTheRightShoulder()
        {
            var cover = Find(InputActionPaths.Cover);

            Assert.That(cover.type, Is.EqualTo(InputActionType.Button));
            Assert.That(cover.bindings, Has.Some.Matches<InputBinding>(b => b.path == "<Keyboard>/q" && b.groups == "Keyboard&Mouse"));
            Assert.That(cover.bindings, Has.Some.Matches<InputBinding>(b => b.path == "<Gamepad>/rightShoulder" && b.groups == "Gamepad"));
        }

        [Test]
        public void SwapShoulderIsAButtonOnVAndTheRightStickPress()
        {
            var swap = Find(InputActionPaths.SwapShoulder);

            Assert.That(swap.type, Is.EqualTo(InputActionType.Button));
            Assert.That(swap.bindings, Has.Some.Matches<InputBinding>(b => b.path == "<Keyboard>/v" && b.groups == "Keyboard&Mouse"));
            Assert.That(swap.bindings, Has.Some.Matches<InputBinding>(b => b.path == "<Gamepad>/rightStickPress" && b.groups == "Gamepad"));
        }

        [Test]
        public void NextAndPreviousKeepTheirKeysAndAddTheScrollWheel()
        {
            var next = Find(InputActionPaths.Next);
            var previous = Find(InputActionPaths.Previous);

            Assert.That(next.bindings, Has.Some.Matches<InputBinding>(b => b.path == "<Mouse>/scroll/up"));
            Assert.That(next.bindings, Has.Some.Matches<InputBinding>(b => b.path == "<Keyboard>/2"));
            Assert.That(next.bindings, Has.Some.Matches<InputBinding>(b => b.path == "<Gamepad>/dpad/right"));
            Assert.That(previous.bindings, Has.Some.Matches<InputBinding>(b => b.path == "<Mouse>/scroll/down"));
            Assert.That(previous.bindings, Has.Some.Matches<InputBinding>(b => b.path == "<Keyboard>/1"));
            Assert.That(previous.bindings, Has.Some.Matches<InputBinding>(b => b.path == "<Gamepad>/dpad/left"));
        }
    }
}
