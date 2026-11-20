using System;
using System.Linq;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class VisionActionInstallerTests
    {
        private GameObject _player;

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
            _player = new GameObject("VisionActionInstallerTests.Player");
        }

        [TearDown]
        public void TearDown()
        {
            if (_player != null)
            {
                Object.DestroyImmediate(_player);
            }

            GameSignals.ResetAll();
        }

        [Test]
        public void TheInstallerIsRegisteredOnceBeforeTheBuiltIns()
        {
            var installers = PlayerActions.Installers.ToList();

            Assert.That(installers.Count(i => i is VisionActionInstaller), Is.EqualTo(1));
            Assert.That(
                installers.FindIndex(i => i is VisionActionInstaller),
                Is.LessThan(installers.FindIndex(i => i is CoverActionInstaller)),
                "the bootstrap used to attach vision before PlayerActions.Attach, so it still goes first");
            Assert.That(VisionActionInstaller.InstallOrder, Is.EqualTo(50));
            Assert.That(new VisionActionInstaller().Order, Is.EqualTo(VisionActionInstaller.InstallOrder));
        }

        [Test]
        public void RegisteringAgainDoesNotAddASecondInstaller()
        {
            PlayerActions.Register(new VisionActionInstaller());
            PlayerActions.Register(new VisionActionInstaller());

            Assert.That(PlayerActions.Installers.Count(i => i is VisionActionInstaller), Is.EqualTo(1));
        }

        [Test]
        public void InstallAddsOneControllerAndOneHighlighterBoundToIt()
        {
            var damageable = _player.AddComponent<Damageable>();
            damageable.Initialize("Player", 100);

            new VisionActionInstaller().Install(_player, GameRules.Default);

            var vision = _player.GetComponents<VisionModeController>();
            var thermal = _player.GetComponents<ThermalHighlighter>();
            Assert.That(vision, Has.Length.EqualTo(1));
            Assert.That(thermal, Has.Length.EqualTo(1));
            Assert.That(thermal[0].Controller, Is.SameAs(vision[0]));
            Assert.That(vision[0].Damageable, Is.SameAs(damageable));
            Assert.That(vision[0].Goggles, Is.EqualTo(VisionMode.None));
            Assert.That(thermal[0].IsActive, Is.False);
        }

        [Test]
        public void InstallingAgainKeepsOneOfEach()
        {
            var installer = new VisionActionInstaller();
            installer.Install(_player, GameRules.Default);
            var vision = _player.GetComponent<VisionModeController>();
            var thermal = _player.GetComponent<ThermalHighlighter>();

            installer.Install(_player, GameRules.Default);

            Assert.That(_player.GetComponents<VisionModeController>(), Is.EqualTo(new[] { vision }));
            Assert.That(_player.GetComponents<ThermalHighlighter>(), Is.EqualTo(new[] { thermal }));
            Assert.That(thermal.Controller, Is.SameAs(vision));
        }

        [Test]
        public void AttachingTheActionsTwiceLikeTheCampaignKeepsOneOfEach()
        {
            var damageable = _player.AddComponent<Damageable>();
            damageable.Initialize("Player", 100);

            PlayerActions.Attach(_player, GameRules.Default);
            PlayerActions.Attach(_player, GameRules.Default.WithActions(ActionRules.Default.WithAbilities(Abilities.On)));

            Assert.That(_player.GetComponents<VisionModeController>(), Has.Length.EqualTo(1));
            Assert.That(_player.GetComponents<ThermalHighlighter>(), Has.Length.EqualTo(1));
            Assert.That(_player.GetComponent<ThermalHighlighter>().Controller, Is.SameAs(_player.GetComponent<VisionModeController>()));
            Assert.That(_player.GetComponent<VisionModeController>().Damageable, Is.SameAs(damageable));
        }

        [Test]
        public void ADamageableAddedAfterInstallIsStillFound()
        {
            new VisionActionInstaller().Install(_player, GameRules.Default);
            var damageable = _player.AddComponent<Damageable>();
            damageable.Initialize("Player", 100);

            Assert.That(_player.GetComponent<VisionModeController>().Damageable, Is.SameAs(damageable));
        }

        [Test]
        public void InstallRejectsAMissingPlayer()
        {
            Assert.Throws<ArgumentNullException>(() => new VisionActionInstaller().Install(null, GameRules.Default));
        }
    }
}
