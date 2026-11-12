using System;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    public sealed class WeaponControllerTests
    {
        private GameObject _camera;
        private WeaponController _controller;
        private InputAction _attack;
        private InputAction _reload;

        [SetUp]
        public void SetUp()
        {
            _camera = new GameObject("Camera", typeof(Camera));
            _controller = _camera.AddComponent<WeaponController>();
            _attack = new InputAction("Attack", InputActionType.Button);
            _reload = new InputAction("Reload", InputActionType.Button);
        }

        [TearDown]
        public void TearDown()
        {
            _attack.Dispose();
            _reload.Dispose();
            Object.DestroyImmediate(_camera);
        }

        [Test]
        public void InitializeRejectsMissingReferences()
        {
            Assert.Throws<ArgumentNullException>(() => _controller.Initialize(null, _attack, _reload));
            Assert.Throws<ArgumentNullException>(() => _controller.Initialize(_camera.transform, null, _reload));
            Assert.Throws<ArgumentNullException>(() => _controller.Initialize(_camera.transform, _attack, null));
            Assert.That(_controller.IsInitialized, Is.False);
        }

        [Test]
        public void ActionsBeforeInitializeThrow()
        {
            Assert.Throws<InvalidOperationException>(() => _controller.Fire());
            Assert.Throws<InvalidOperationException>(() => _controller.StartReload());
            Assert.Throws<InvalidOperationException>(() => _controller.Tick(0.1f));
        }

        [Test]
        public void RestoreAmmoReplacesTheWeaponAndCancelsReload()
        {
            _controller.Initialize(_camera.transform, _attack, _reload);
            LogAssert.Expect(LogType.Log, "[Combat] fire ammo=29");
            _controller.Fire();
            Assert.That(_controller.StartReload(), Is.True);
            var previous = _controller.Weapon;
            Fps.Core.Weapon changed = null;
            _controller.WeaponChanged += weapon => changed = weapon;

            _controller.RestoreAmmo(7, 12);

            Assert.That(_controller.Weapon, Is.Not.SameAs(previous));
            Assert.That(changed, Is.SameAs(_controller.Weapon));
            Assert.That(_controller.Weapon.Spec, Is.SameAs(previous.Spec));
            Assert.That(_controller.Weapon.AmmoInMagazine, Is.EqualTo(7));
            Assert.That(_controller.Weapon.ReserveAmmo, Is.EqualTo(12));
            Assert.That(_controller.Weapon.IsReloading, Is.False);

            _controller.RestoreAmmo(WeaponController.DefaultMagazineSize + 5, 0);
            Assert.That(_controller.Weapon.AmmoInMagazine, Is.EqualTo(WeaponController.DefaultMagazineSize), "clamped to the magazine");
        }

        [Test]
        public void RestoreAmmoRejectsNegativeValuesAndNeedsInitialize()
        {
            Assert.Throws<InvalidOperationException>(() => _controller.RestoreAmmo(1, 1));

            _controller.Initialize(_camera.transform, _attack, _reload);
            Assert.Throws<ArgumentOutOfRangeException>(() => _controller.RestoreAmmo(-1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => _controller.RestoreAmmo(0, -1));
        }

        [Test]
        public void InitializeBuildsTheWeaponFromCodeDefaults()
        {
            _controller.Initialize(_camera.transform, _attack, _reload);

            var spec = _controller.Weapon.Spec;
            Assert.That(spec.MagazineSize, Is.EqualTo(WeaponController.DefaultMagazineSize));
            Assert.That(spec.ReserveAmmo, Is.EqualTo(WeaponController.DefaultReserveAmmo));
            Assert.That(spec.FireInterval, Is.EqualTo(WeaponController.DefaultFireInterval));
            Assert.That(spec.ReloadSeconds, Is.EqualTo(WeaponController.DefaultReloadSeconds));
            Assert.That(spec.Damage, Is.EqualTo(WeaponController.DefaultDamage));
            Assert.That(_controller.HitscanRange, Is.EqualTo(WeaponController.DefaultRange));
            Assert.That(_controller.Falloff.StartDistance, Is.EqualTo(WeaponController.DefaultFalloffStart));
            Assert.That(_controller.Falloff.EndDistance, Is.EqualTo(WeaponController.DefaultFalloffEnd));
            Assert.That(_controller.Aim, Is.SameAs(_camera.transform));
        }

        [Test]
        public void FireLogsTheRemainingAmmoAndRespectsTheFireInterval()
        {
            _controller.Initialize(_camera.transform, _attack, _reload);
            var full = WeaponController.DefaultMagazineSize;

            LogAssert.Expect(LogType.Log, $"[Combat] fire ammo={full - 1}");
            Assert.That(_controller.Fire(), Is.True);

            // 連射間隔が経つまでは撃てず、ログも出ない。
            Assert.That(_controller.Fire(), Is.False);

            _controller.Tick(WeaponController.DefaultFireInterval);
            LogAssert.Expect(LogType.Log, $"[Combat] fire ammo={full - 2}");
            Assert.That(_controller.Fire(), Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ReloadLogsWhenTheMagazineIsRefilled()
        {
            _controller.Initialize(_camera.transform, _attack, _reload);
            LogAssert.Expect(LogType.Log, $"[Combat] fire ammo={WeaponController.DefaultMagazineSize - 1}");
            _controller.Fire();

            Assert.That(_controller.StartReload(), Is.True);
            _controller.Tick(WeaponController.DefaultReloadSeconds / 2f);
            Assert.That(_controller.Weapon.IsReloading, Is.True);

            LogAssert.Expect(LogType.Log, WeaponController.ReloadedLog);
            _controller.Tick(WeaponController.DefaultReloadSeconds);

            Assert.That(_controller.Weapon.IsReloading, Is.False);
            Assert.That(_controller.Weapon.AmmoInMagazine, Is.EqualTo(WeaponController.DefaultMagazineSize));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ReloadWithFullMagazineDoesNothing()
        {
            _controller.Initialize(_camera.transform, _attack, _reload);

            Assert.That(_controller.StartReload(), Is.False);
        }

        [Test]
        public void ResolvesTheCameraAndProjectWideActions()
        {
            var actions = InputSystem.actions;
            Assert.That(actions, Is.Not.Null, "Project-wide Input Actions must be assigned");

            Assert.That(_controller.TryInitializeFromHierarchy(), Is.True);
            Assert.That(_controller.Aim, Is.SameAs(_camera.transform));
            Assert.That(_controller.AttackAction, Is.SameAs(actions.FindAction(WeaponController.AttackActionPath)));
            Assert.That(_controller.ReloadAction, Is.SameAs(actions.FindAction(WeaponController.ReloadActionPath)));
        }

        [Test]
        public void ReloadActionIsBoundToTheRKey()
        {
            var reload = InputSystem.actions.FindAction(WeaponController.ReloadActionPath);

            Assert.That(reload, Is.Not.Null);
            Assert.That(reload.bindings, Has.Some.Matches<InputBinding>(b => b.path == "<Keyboard>/r"));
        }
    }
}
