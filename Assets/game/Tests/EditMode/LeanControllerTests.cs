using System;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class LeanControllerTests
    {
        private const PlayerCommands Left = PlayerCommands.LeanLeft;
        private const PlayerCommands Right = PlayerCommands.LeanRight;
        private const PlayerCommands Both = PlayerCommands.LeanLeft | PlayerCommands.LeanRight;
        private const PlayerCommands None = PlayerCommands.None;

        private GameObject _player;
        private LeanController _lean;

        [SetUp]
        public void SetUp()
        {
            _player = new GameObject("Player");
            _lean = _player.AddComponent<LeanController>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_player);
        }

        [Test]
        public void HoldingASideButtonLeansThatWayUntilLetGo()
        {
            Assert.That(_lean.Mode, Is.EqualTo(HoldMode.Hold));

            Assert.That(_lean.Tick(Left, Left), Is.EqualTo(-1));
            Assert.That(_lean.Lean, Is.EqualTo(-1));
            Assert.That(_lean.Tick(Left, None), Is.EqualTo(-1));
            Assert.That(_lean.Tick(None, None), Is.EqualTo(0));
            Assert.That(_lean.Tick(Right, Right), Is.EqualTo(1));
        }

        [Test]
        public void HoldingBothIsUpright()
        {
            _lean.Tick(Right, Right);

            Assert.That(_lean.Tick(Both, Left), Is.EqualTo(0));
            Assert.That(_lean.Tick(Both, Both), Is.EqualTo(0), "pressing both again is still upright");
            Assert.That(_lean.Tick(Right, None), Is.EqualTo(1));
        }

        [Test]
        public void ToggleKeepsLeaningAndSwitchesSides()
        {
            _lean.Mode = HoldMode.Toggle;

            Assert.That(_lean.Tick(Left, Left), Is.EqualTo(-1));
            Assert.That(_lean.Tick(None, None), Is.EqualTo(-1), "letting go keeps leaning");
            Assert.That(_lean.Tick(Right, Right), Is.EqualTo(1), "the other side takes over");
            Assert.That(_lean.Tick(None, None), Is.EqualTo(1));
            Assert.That(_lean.Tick(Right, Right), Is.EqualTo(0), "pressing the same side again stands up");
            Assert.That(_lean.Tick(Both, Both), Is.EqualTo(0), "both at once is upright");
        }

        [Test]
        public void OtherButtonsAreIgnored()
        {
            Assert.That(_lean.Tick(PlayerCommands.Melee | PlayerCommands.Prone, PlayerCommands.Melee), Is.EqualTo(0));
        }

        [Test]
        public void ApplyToPutsTheLeanIntoTheMoveInput()
        {
            var input = new CharacterMotorInput(
                new System.Numerics.Vector2(0f, 1f), 0.5f, false, false, false, false, false, false, false, true);
            _lean.Tick(Left, Left);

            var leaned = _lean.ApplyTo(input);

            Assert.That(leaned.Lean, Is.EqualTo(-1));
            Assert.That(leaned.Move, Is.EqualTo(input.Move));
            Assert.That(leaned.Yaw, Is.EqualTo(input.Yaw));

            _lean.Tick(None, None);
            Assert.That(_lean.ApplyTo(leaned).Lean, Is.EqualTo(0));
        }

        [Test]
        public void ADisabledControllerDoesNotLean()
        {
            _lean.Mode = HoldMode.Toggle;
            _lean.Tick(Right, Right);

            _lean.enabled = false;

            Assert.That(_lean.Lean, Is.EqualTo(0));
        }

        [Test]
        public void ReleaseStandsUpUntilPressedAgain()
        {
            _lean.Tick(Right, Right);

            _lean.Release();

            Assert.That(_lean.Lean, Is.EqualTo(0));
            Assert.That(_lean.Tick(Right, None), Is.EqualTo(0));
            Assert.That(_lean.Tick(Right, Right), Is.EqualTo(1));
        }

        [Test]
        public void ChangingTheModeStandsUp()
        {
            _lean.Mode = HoldMode.Toggle;
            _lean.Tick(Left, Left);

            _lean.Mode = HoldMode.Hold;

            Assert.That(_lean.Lean, Is.EqualTo(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => _lean.Mode = (HoldMode)9);
            Assert.That(_lean.Mode, Is.EqualTo(HoldMode.Hold));
        }

        [Test]
        public void InstallerAddsOneControllerWithItsMode()
        {
            var player = new GameObject("Installed");
            try
            {
                var installer = new LeanActionInstaller(HoldMode.Toggle);

                installer.Install(player, GameRules.Default);
                installer.Install(player, GameRules.Default);

                Assert.That(player.GetComponents<LeanController>().Length, Is.EqualTo(1));
                Assert.That(player.GetComponent<LeanController>().Mode, Is.EqualTo(HoldMode.Toggle));
                Assert.That(installer.Order, Is.EqualTo(LeanActionInstaller.InstallOrder));
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void InstallerDefaultsToHoldAndRejectsAnUnknownMode()
        {
            Assert.That(new LeanActionInstaller().Mode, Is.EqualTo(HoldMode.Hold));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LeanActionInstaller((HoldMode)9));
        }
    }
}
