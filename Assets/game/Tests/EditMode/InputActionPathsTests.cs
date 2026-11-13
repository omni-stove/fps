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
        public void EveryPathExistsInTheProjectWideActions(string path)
        {
            Find(path);
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
    }
}
