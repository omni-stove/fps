using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // Edit Mode では LateUpdate が走らないので、照準の変化は Refresh を直接呼んで確かめる。
    public sealed class VisionModeControllerTests
    {
        private static readonly GameRules _abilitiesOn = GameRules.Default.WithActions(ActionRules.Default.WithAbilities(Abilities.On));

        private readonly List<Object> _created = new List<Object>();
        private GameObject _player;
        private VisionModeController _controller;
        private SightView _sight;

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
            _player = new GameObject("VisionModeControllerTests.Player");
            _created.Add(_player);
            _controller = _player.AddComponent<VisionModeController>();
            _controller.Catalog = null;
            _sight = SightView.None;
            _controller.SightSource = () => _sight;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
            GameSignals.ResetAll();
        }

        [Test]
        public void TheScopeWinsOverTheGoggles()
        {
            Assert.That(
                VisionModeController.Resolve(VisionMode.Thermal, VisionMode.Night, false),
                Is.EqualTo(new VisionStatus(VisionMode.Thermal, VisionSource.Scope)));
            Assert.That(
                VisionModeController.Resolve(VisionMode.None, VisionMode.Night, false),
                Is.EqualTo(new VisionStatus(VisionMode.Night, VisionSource.Goggles)));
            Assert.That(
                VisionModeController.Resolve(VisionMode.Night, VisionMode.None, false),
                Is.EqualTo(new VisionStatus(VisionMode.Night, VisionSource.Scope)));
            Assert.That(VisionModeController.Resolve(VisionMode.None, VisionMode.None, false), Is.EqualTo(VisionStatus.None));
            Assert.That(VisionModeController.Resolve(VisionMode.Thermal, VisionMode.Night, true), Is.EqualTo(VisionStatus.None), "nothing while dead");
        }

        [Test]
        public void AimingAThermalScopeOverNightGogglesShowsTheScopeUntilTheGunComesDown()
        {
            _controller.SetGoggles(VisionMode.Night);
            Assert.That(_controller.Current, Is.EqualTo(new VisionStatus(VisionMode.Night, VisionSource.Goggles)));
            Assert.That(GameSignals.Vision.Value, Is.EqualTo((VisionStatus?)new VisionStatus(VisionMode.Night, VisionSource.Goggles)));

            _sight = SightView.Of(new SightSpec(visionMode: VisionMode.Thermal), ViewMode.FirstPerson, 1f);
            _controller.Refresh();

            Assert.That(_controller.Current, Is.EqualTo(new VisionStatus(VisionMode.Thermal, VisionSource.Scope)));
            Assert.That(GameSignals.Vision.Value, Is.EqualTo((VisionStatus?)new VisionStatus(VisionMode.Thermal, VisionSource.Scope)));
            Assert.That(_controller.Goggles, Is.EqualTo(VisionMode.Night), "the goggles stay on under the scope");

            _sight = SightView.Of(new SightSpec(visionMode: VisionMode.Thermal), ViewMode.FirstPerson, 0.5f);
            _controller.Refresh();

            Assert.That(_controller.Current, Is.EqualTo(new VisionStatus(VisionMode.Night, VisionSource.Goggles)), "back to the goggles once not aimed in");
            Assert.That(GameSignals.Vision.Value, Is.EqualTo((VisionStatus?)new VisionStatus(VisionMode.Night, VisionSource.Goggles)));
        }

        [Test]
        public void AScopeWithoutVisionLeavesTheGogglesShowing()
        {
            _controller.SetGoggles(VisionMode.Thermal);
            _sight = SightView.Of(new SightSpec("redDot"), ViewMode.FirstPerson, 1f);

            _controller.Refresh();

            Assert.That(_controller.Current, Is.EqualTo(new VisionStatus(VisionMode.Thermal, VisionSource.Goggles)));
        }

        [Test]
        public void AScopeAloneShowsItsVisionAndClearsTheSignalWhenLowered()
        {
            _sight = SightView.Of(new SightSpec(visionMode: VisionMode.Night), ViewMode.FirstPerson, 1f);
            _controller.Refresh();
            Assert.That(GameSignals.Vision.Value, Is.EqualTo((VisionStatus?)new VisionStatus(VisionMode.Night, VisionSource.Scope)));

            _sight = SightView.None;
            _controller.Refresh();

            Assert.That(_controller.Current, Is.EqualTo(VisionStatus.None));
            Assert.That(GameSignals.Vision.Value, Is.Null);
        }

        [Test]
        public void TogglingTheGogglesTurnsThemOnAndOffAndSwapsTheMode()
        {
            var changes = new List<VisionStatus>();
            _controller.Changed += changes.Add;

            Assert.That(_controller.ToggleGoggles(VisionMode.Night), Is.True);
            Assert.That(_controller.Goggles, Is.EqualTo(VisionMode.Night));

            Assert.That(_controller.ToggleGoggles(VisionMode.Thermal), Is.True, "other goggles swap rather than switch off");
            Assert.That(_controller.Goggles, Is.EqualTo(VisionMode.Thermal));

            Assert.That(_controller.ToggleGoggles(VisionMode.Thermal), Is.False);
            Assert.That(_controller.GogglesOn, Is.False);
            Assert.That(_controller.Current, Is.EqualTo(VisionStatus.None));
            Assert.That(GameSignals.Vision.Value, Is.Null);

            Assert.That(changes, Is.EqualTo(new[]
            {
                new VisionStatus(VisionMode.Night, VisionSource.Goggles),
                new VisionStatus(VisionMode.Thermal, VisionSource.Goggles),
                VisionStatus.None,
            }));
            Assert.Throws<System.ArgumentException>(() => _controller.ToggleGoggles(VisionMode.None));
        }

        [Test]
        public void ClearingDoesNotRemoveAValueSomeoneElsePublished()
        {
            _controller.SetGoggles(VisionMode.Night);
            var other = new VisionStatus(VisionMode.Thermal, VisionSource.Scope);
            GameSignals.Vision.Publish(other);

            _controller.SetGoggles(VisionMode.None);

            Assert.That(GameSignals.Vision.Value, Is.EqualTo((VisionStatus?)other));
        }

        [Test]
        public void DyingTurnsTheGogglesOffAndKeepsThemOffUntilAlive()
        {
            var body = AddBody();
            _controller.SetGoggles(VisionMode.Thermal);
            _sight = SightView.Of(new SightSpec(visionMode: VisionMode.Night), ViewMode.FirstPerson, 1f);
            _controller.Refresh();

            body.Kill();

            Assert.That(_controller.GogglesOn, Is.False);
            Assert.That(_controller.Current, Is.EqualTo(VisionStatus.None), "a scope shows nothing while dead either");
            Assert.That(GameSignals.Vision.Value, Is.Null);
            Assert.That(_controller.SetGoggles(VisionMode.Night), Is.False, "goggles cannot go on while dead");

            body.ResetHealth();
            _sight = SightView.None;
            _controller.Refresh();

            Assert.That(_controller.GogglesOn, Is.False, "the goggles stay off after respawning");
            Assert.That(_controller.SetGoggles(VisionMode.Night), Is.True);
        }

        [Test]
        public void TheSwitchSoundsComeFromTheCatalog()
        {
            var catalog = ScriptableObject.CreateInstance<VisionVisualCatalog>();
            _created.Add(catalog);
            catalog.SwitchOn = Clip("on");
            catalog.SwitchOff = Clip("off");
            _controller.Catalog = catalog;

            _controller.SetGoggles(VisionMode.Night);
            Assert.That(_controller.LastSwitchSound, Is.SameAs(catalog.SwitchOn));

            _controller.SetGoggles(VisionMode.None);
            Assert.That(_controller.LastSwitchSound, Is.SameAs(catalog.SwitchOff));

            Assert.That(VisionModeController.SoundsOn(VisionStatus.None, new VisionStatus(VisionMode.Night, VisionSource.Goggles)), Is.True);
            Assert.That(
                VisionModeController.SoundsOn(new VisionStatus(VisionMode.Night, VisionSource.Goggles), new VisionStatus(VisionMode.Night, VisionSource.Scope)),
                Is.False,
                "the same picture from another source does not click again");
            Assert.That(
                VisionModeController.SoundsOn(new VisionStatus(VisionMode.Night, VisionSource.Goggles), new VisionStatus(VisionMode.Thermal, VisionSource.Scope)),
                Is.True);
            Assert.That(VisionModeController.SoundsOff(new VisionStatus(VisionMode.Night, VisionSource.Goggles), VisionStatus.None), Is.True);
        }

        [Test]
        public void WithoutACatalogTheSwitchIsSilent()
        {
            _controller.SetGoggles(VisionMode.Night);

            Assert.That(_controller.LastSwitchSound, Is.Null);
            Assert.That(_player.GetComponent<AudioSource>(), Is.Null);
        }

        [Test]
        public void TheGogglesAbilityPutsThemOnAndTakesThemOffWithoutACharge()
        {
            var host = _player.AddComponent<AbilityHost>();
            host.Configure(_abilitiesOn);
            var goggles = Goggles(VisionMode.Thermal, maxCharges: 2);
            host.Equip(0, goggles);

            Assert.That(host.TryUse(0), Is.True);
            host.Step(PlayerCommands.None, 0.01f);

            Assert.That(_controller.Goggles, Is.EqualTo(VisionMode.Thermal));
            Assert.That(host.IsRunning(0), Is.False);
            Assert.That(host.Slot(0).Charges, Is.EqualTo(1));

            Assert.That(host.TryUse(0), Is.True);

            Assert.That(_controller.GogglesOn, Is.False, "taken off at once");
            Assert.That(host.Slot(0).Charges, Is.EqualTo(1), "taking them off gives the charge back");
            host.Step(PlayerCommands.None, 0.01f);
            Assert.That(host.IsRunning(0), Is.False);
        }

        [Test]
        public void TheGogglesComeOffEvenWithoutChargesLeft()
        {
            var host = _player.AddComponent<AbilityHost>();
            host.Configure(_abilitiesOn);
            host.Equip(0, Goggles(VisionMode.Night, maxCharges: 1));

            host.TryUse(0);
            host.Step(PlayerCommands.None, 0.01f);
            Assert.That(_controller.Goggles, Is.EqualTo(VisionMode.Night));
            Assert.That(host.Slot(0).Charges, Is.Zero);

            Assert.That(host.TryUse(0), Is.True, "the follow-up takes them off");
            Assert.That(_controller.GogglesOn, Is.False);

            Assert.That(host.TryUse(0), Is.False, "putting them on again waits for the charge");
            Assert.That(_controller.GogglesOn, Is.False);
        }

        [Test]
        public void TheGogglesWaitForTheWindup()
        {
            var host = _player.AddComponent<AbilityHost>();
            host.Configure(_abilitiesOn);
            var goggles = Goggles(VisionMode.Night, maxCharges: 1);
            goggles.WindupSeconds = 0.5f;
            host.Equip(0, goggles);

            host.TryUse(0);
            host.Step(PlayerCommands.None, 0.2f);
            Assert.That(_controller.GogglesOn, Is.False, "still putting them on");

            host.Step(PlayerCommands.None, 0.4f);
            Assert.That(_controller.Goggles, Is.EqualTo(VisionMode.Night));
        }

        [Test]
        public void TheGogglesNeedAController()
        {
            Object.DestroyImmediate(_controller);
            var host = _player.AddComponent<AbilityHost>();
            host.Configure(_abilitiesOn);
            host.Equip(0, Goggles(VisionMode.Night, maxCharges: 1));

            Assert.That(host.UseBlock(0), Is.EqualTo(AbilityUseBlock.Refused));
        }

        [Test]
        public void AGogglesDefinitionWithoutAModeIsRejected()
        {
            var goggles = Goggles(VisionMode.None, maxCharges: 1);

            Assert.That(VisionGogglesDefinition.DefaultMode, Is.EqualTo(VisionMode.Night));
            var error = Assert.Throws<AbilityDefinitionException>(() => goggles.CreateAbility());
            Assert.That(error.Message, Does.Contain("'goggles'"));
        }

        private Damageable AddBody()
        {
            var body = _player.AddComponent<Damageable>();
            body.Initialize("player", 100);
            return body;
        }

        private VisionGogglesDefinition Goggles(VisionMode mode, int maxCharges)
        {
            var goggles = ScriptableObject.CreateInstance<VisionGogglesDefinition>();
            _created.Add(goggles);
            goggles.name = "Goggles";
            goggles.Id = "goggles";
            goggles.Mode = mode;
            goggles.MaxCharges = maxCharges;
            goggles.RechargeSeconds = 10f;
            goggles.CooldownSeconds = 0f;
            goggles.WindupSeconds = 0f;
            return goggles;
        }

        private AudioClip Clip(string name)
        {
            var clip = AudioClip.Create(name, 64, 1, 44100, false);
            _created.Add(clip);
            return clip;
        }
    }
}
