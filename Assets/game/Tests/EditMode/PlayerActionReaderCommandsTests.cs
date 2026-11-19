using System;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class PlayerActionReaderCommandsTests
    {
        private GameObject _player;
        private PlayerActionReader _reader;

        [SetUp]
        public void SetUp()
        {
            _player = new GameObject("Player");
            _reader = _player.AddComponent<PlayerActionReader>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_player);
        }

        [TestCase(PlayerCommands.Melee, InputActionPaths.Melee)]
        [TestCase(PlayerCommands.Prone, InputActionPaths.Prone)]
        [TestCase(PlayerCommands.Ping, InputActionPaths.Ping)]
        [TestCase(PlayerCommands.Ability1, InputActionPaths.Ability1)]
        [TestCase(PlayerCommands.Ability2, InputActionPaths.Ability2)]
        [TestCase(PlayerCommands.SquadCommand, InputActionPaths.SquadCommand)]
        [TestCase(PlayerCommands.UseStreak, InputActionPaths.UseStreak)]
        [TestCase(PlayerCommands.LeanLeft, InputActionPaths.LeanLeft)]
        [TestCase(PlayerCommands.LeanRight, InputActionPaths.LeanRight)]
        [TestCase(PlayerCommands.Chat, InputActionPaths.Chat)]
        [TestCase(PlayerCommands.TeamChat, InputActionPaths.TeamChat)]
        [TestCase(PlayerCommands.PushToTalk, InputActionPaths.PushToTalk)]
        [TestCase(PlayerCommands.SwitchStreak, InputActionPaths.SwitchStreak)]
        public void ResolvesEachCommandFromTheProjectWideActions(PlayerCommands command, string path)
        {
            Assert.That(PlayerActionReader.CommandActionPath(command), Is.EqualTo(path));
            Assert.That(_reader.TryResolveCommandActions(), Is.True);

            Assert.That(_reader.CommandAction(command), Is.Not.Null);
            Assert.That(_reader.CommandAction(command), Is.SameAs(InputSystem.actions.FindAction(path)));
        }

        [Test]
        public void NothingIsPressedWithoutInput()
        {
            var melee = new InputAction("Melee", InputActionType.Button, "<Keyboard>/v");
            try
            {
                _reader.SetCommandAction(PlayerCommands.Melee, melee);
                melee.Enable();

                Assert.That(_reader.ReadCommands(), Is.EqualTo(PlayerCommands.None));
                Assert.That(_reader.PressedCommands, Is.EqualTo(PlayerCommands.None));
                Assert.That(_reader.HeldCommands, Is.EqualTo(PlayerCommands.None));
            }
            finally
            {
                melee.Dispose();
            }
        }

        [Test]
        public void ACommandWithoutAnActionIsNeverPressed()
        {
            _reader.SetCommandAction(PlayerCommands.Ping, null);

            Assert.That(_reader.CommandAction(PlayerCommands.Ping), Is.Null);
            Assert.That(_reader.ReadCommands(), Is.EqualTo(PlayerCommands.None));
        }

        [Test]
        public void RejectsSomethingThatIsNotASingleCommand()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => PlayerActionReader.CommandActionPath(PlayerCommands.None));
            Assert.Throws<ArgumentOutOfRangeException>(() => PlayerActionReader.CommandActionPath(PlayerCommands.Melee | PlayerCommands.Ping));
            Assert.Throws<ArgumentOutOfRangeException>(() => _reader.CommandAction(PlayerCommands.None));
            Assert.Throws<ArgumentOutOfRangeException>(() => _reader.SetCommandAction(PlayerCommands.Melee | PlayerCommands.Prone, null));
        }
    }
}
