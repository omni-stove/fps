using System;
using System.Text.RegularExpressions;
using Fps.Editor;
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
        private WeaponDefinition _rifle;
        private InputAction _attack;
        private InputAction _reload;

        [SetUp]
        public void SetUp()
        {
            _camera = new GameObject("Camera", typeof(Camera));
            _controller = _camera.AddComponent<WeaponController>();
            _rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            _rifle.name = "Rifle";
            WeaponAssetBuilder.Configure(_rifle, WeaponAssetBuilder.RifleSpec());
            _attack = new InputAction("Attack", InputActionType.Button);
            _reload = new InputAction("Reload", InputActionType.Button);
        }

        [TearDown]
        public void TearDown()
        {
            _attack.Dispose();
            _reload.Dispose();
            Object.DestroyImmediate(_camera);
            Object.DestroyImmediate(_rifle);
        }

        [Test]
        public void InitializeRejectsMissingReferences()
        {
            Assert.Throws<ArgumentNullException>(() => _controller.Initialize(null, _attack, _reload, _rifle));
            Assert.Throws<ArgumentNullException>(() => _controller.Initialize(_camera.transform, null, _reload, _rifle));
            Assert.Throws<ArgumentNullException>(() => _controller.Initialize(_camera.transform, _attack, null, _rifle));
            Assert.Throws<ArgumentNullException>(() => _controller.Initialize(_camera.transform, _attack, _reload, null));
            Assert.That(_controller.IsInitialized, Is.False);
        }

        [Test]
        public void InitializeWithoutADefinitionThrows()
        {
            Assert.That(_controller.Definition, Is.Null);

            Assert.Throws<InvalidOperationException>(() => _controller.Initialize(_camera.transform, _attack, _reload));
            Assert.That(_controller.IsInitialized, Is.False);
        }

        [Test]
        public void InitializeUsesTheAssignedDefinition()
        {
            _controller.Definition = _rifle;

            _controller.Initialize(_camera.transform, _attack, _reload);

            Assert.That(_controller.IsInitialized, Is.True);
            Assert.That(_controller.Weapon.Spec.Id, Is.EqualTo(WeaponAssetBuilder.RifleId));
        }

        [Test]
        public void InvalidDefinitionIsRejectedAndKeepsThePreviousWeapon()
        {
            _controller.Initialize(_camera.transform, _attack, _reload, _rifle);
            var previous = _controller.Weapon;

            var broken = ScriptableObject.CreateInstance<WeaponDefinition>();
            try
            {
                broken.name = "Broken";
                broken.AssignIdIfEmpty();
                broken.FalloffStart = 50f;
                broken.FalloffEnd = 10f;

                Assert.Throws<WeaponDefinitionException>(() => _controller.Initialize(_camera.transform, _attack, _reload, broken));
                Assert.That(_controller.Weapon, Is.SameAs(previous));
                Assert.That(_controller.Definition, Is.SameAs(_rifle));
            }
            finally
            {
                Object.DestroyImmediate(broken);
            }
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
            _controller.Initialize(_camera.transform, _attack, _reload, _rifle);
            LogAssert.Expect(LogType.Log, $"[Combat] fire ammo={_rifle.MagazineSize - 1}");
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

            _controller.RestoreAmmo(_rifle.MagazineSize + 5, 0);
            Assert.That(_controller.Weapon.AmmoInMagazine, Is.EqualTo(_rifle.MagazineSize), "clamped to the magazine");
        }

        [Test]
        public void RestoreAmmoRejectsNegativeValuesAndNeedsInitialize()
        {
            Assert.Throws<InvalidOperationException>(() => _controller.RestoreAmmo(1, 1));

            _controller.Initialize(_camera.transform, _attack, _reload, _rifle);
            Assert.Throws<ArgumentOutOfRangeException>(() => _controller.RestoreAmmo(-1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => _controller.RestoreAmmo(0, -1));
        }

        [Test]
        public void InitializeBuildsTheWeaponFromTheDefinition()
        {
            Fps.Core.Weapon changed = null;
            _controller.WeaponChanged += weapon => changed = weapon;

            _controller.Initialize(_camera.transform, _attack, _reload, _rifle);

            var expected = WeaponAssetBuilder.RifleSpec();
            var spec = _controller.Weapon.Spec;
            Assert.That(changed, Is.SameAs(_controller.Weapon));
            Assert.That(_controller.Definition, Is.SameAs(_rifle));
            Assert.That(spec.Id, Is.EqualTo(expected.Id));
            Assert.That(spec.Category, Is.EqualTo(expected.Category));
            Assert.That(spec.FireMode, Is.EqualTo(expected.FireMode));
            Assert.That(spec.MagazineSize, Is.EqualTo(expected.MagazineSize));
            Assert.That(spec.ReserveAmmo, Is.EqualTo(expected.ReserveAmmo));
            Assert.That(spec.FireInterval, Is.EqualTo(expected.FireInterval));
            Assert.That(spec.ReloadSeconds, Is.EqualTo(expected.ReloadSeconds));
            Assert.That(spec.Damage, Is.EqualTo(expected.Damage));
            Assert.That(_controller.HitscanRange, Is.EqualTo(expected.Range));
            Assert.That(_controller.Falloff, Is.SameAs(spec.Falloff));
            Assert.That(_controller.Falloff.StartDistance, Is.EqualTo(expected.Falloff.StartDistance));
            Assert.That(_controller.Falloff.EndDistance, Is.EqualTo(expected.Falloff.EndDistance));
            Assert.That(_controller.Falloff.MinMultiplier, Is.EqualTo(expected.Falloff.MinMultiplier));
            Assert.That(_controller.Aim, Is.SameAs(_camera.transform));
        }

        [Test]
        public void ReinitializingWithAnotherDefinitionSwapsTheWeapon()
        {
            _controller.Initialize(_camera.transform, _attack, _reload, _rifle);
            var handgun = ScriptableObject.CreateInstance<WeaponDefinition>();
            try
            {
                handgun.name = "Handgun";
                WeaponAssetBuilder.Configure(handgun, WeaponAssetBuilder.HandgunSpec());

                _controller.Initialize(_camera.transform, _attack, _reload, handgun);

                Assert.That(_controller.Definition, Is.SameAs(handgun));
                Assert.That(_controller.Weapon.Spec.Id, Is.EqualTo(WeaponAssetBuilder.HandgunId));
                Assert.That(_controller.HitscanRange, Is.EqualTo(handgun.Range));
            }
            finally
            {
                Object.DestroyImmediate(handgun);
            }
        }

        [Test]
        public void FireLogsTheRemainingAmmoAndRespectsTheFireInterval()
        {
            _controller.Initialize(_camera.transform, _attack, _reload, _rifle);
            var full = _rifle.MagazineSize;

            LogAssert.Expect(LogType.Log, $"[Combat] fire ammo={full - 1}");
            Assert.That(_controller.Fire(), Is.True);

            // 連射間隔が経つまでは撃てず、ログも出ない。
            Assert.That(_controller.Fire(), Is.False);

            _controller.Tick(_rifle.FireInterval);
            LogAssert.Expect(LogType.Log, $"[Combat] fire ammo={full - 2}");
            Assert.That(_controller.Fire(), Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void AutoWeaponKeepsFiringWhileTheTriggerIsHeld()
        {
            _controller.Initialize(_camera.transform, _attack, _reload, _rifle);
            var full = _rifle.MagazineSize;

            LogAssert.Expect(LogType.Log, $"[Combat] fire ammo={full - 1}");
            Assert.That(_controller.PullTrigger(true, true), Is.True);
            Assert.That(_controller.PullTrigger(true, false), Is.False, "still within the fire interval");

            _controller.Tick(_rifle.FireInterval);
            LogAssert.Expect(LogType.Log, $"[Combat] fire ammo={full - 2}");
            Assert.That(_controller.PullTrigger(true, false), Is.True);

            _controller.Tick(_rifle.FireInterval);
            Assert.That(_controller.PullTrigger(false, false), Is.False, "released");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void SemiWeaponFiresOncePerPress()
        {
            var handgun = ScriptableObject.CreateInstance<WeaponDefinition>();
            try
            {
                handgun.name = "Handgun";
                WeaponAssetBuilder.Configure(handgun, WeaponAssetBuilder.HandgunSpec());
                Assert.That(handgun.FireMode, Is.EqualTo(Fps.Core.FireMode.Semi));
                _controller.Initialize(_camera.transform, _attack, _reload, handgun);
                var full = handgun.MagazineSize;

                LogAssert.Expect(LogType.Log, $"[Combat] fire ammo={full - 1}");
                Assert.That(_controller.PullTrigger(true, true), Is.True);

                // 連射間隔を過ぎても押し続けている間は 2 発目が出ない。
                for (var i = 0; i < 5; i++)
                {
                    _controller.Tick(handgun.FireInterval);
                    Assert.That(_controller.PullTrigger(true, false), Is.False);
                }

                Assert.That(_controller.Weapon.AmmoInMagazine, Is.EqualTo(full - 1));

                Assert.That(_controller.PullTrigger(false, false), Is.False);
                LogAssert.Expect(LogType.Log, $"[Combat] fire ammo={full - 2}");
                Assert.That(_controller.PullTrigger(true, true), Is.True, "pressed again");
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                Object.DestroyImmediate(handgun);
            }
        }

        [Test]
        public void ReloadLogsWhenTheMagazineIsRefilled()
        {
            _controller.Initialize(_camera.transform, _attack, _reload, _rifle);
            LogAssert.Expect(LogType.Log, $"[Combat] fire ammo={_rifle.MagazineSize - 1}");
            _controller.Fire();

            Assert.That(_controller.StartReload(), Is.True);
            _controller.Tick(_rifle.ReloadSeconds / 2f);
            Assert.That(_controller.Weapon.IsReloading, Is.True);

            LogAssert.Expect(LogType.Log, WeaponController.ReloadedLog);
            _controller.Tick(_rifle.ReloadSeconds);

            Assert.That(_controller.Weapon.IsReloading, Is.False);
            Assert.That(_controller.Weapon.AmmoInMagazine, Is.EqualTo(_rifle.MagazineSize));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ReloadWithFullMagazineDoesNothing()
        {
            _controller.Initialize(_camera.transform, _attack, _reload, _rifle);

            Assert.That(_controller.StartReload(), Is.False);
        }

        [Test]
        public void ResolvesTheCameraAndProjectWideActions()
        {
            var actions = InputSystem.actions;
            Assert.That(actions, Is.Not.Null, "Project-wide Input Actions must be assigned");
            _controller.Definition = _rifle;

            Assert.That(_controller.TryInitializeFromHierarchy(), Is.True);
            Assert.That(_controller.Aim, Is.SameAs(_camera.transform));
            Assert.That(_controller.AttackAction, Is.SameAs(actions.FindAction(WeaponController.AttackActionPath)));
            Assert.That(_controller.ReloadAction, Is.SameAs(actions.FindAction(WeaponController.ReloadActionPath)));
            Assert.That(_controller.Weapon.Spec.Id, Is.EqualTo(WeaponAssetBuilder.RifleId));
        }

        [Test]
        public void ResolvingWithoutADefinitionIsReportedAndRejected()
        {
            LogAssert.Expect(LogType.Error, new Regex(@"^\[WeaponController\] 'Camera' に WeaponDefinition が設定されていません"));

            Assert.That(_controller.TryInitializeFromHierarchy(), Is.False);
            Assert.That(_controller.IsInitialized, Is.False);
        }

        [Test]
        public void ResolvingWithAnInvalidDefinitionIsReportedAndRejected()
        {
            _rifle.Range = 0f;
            _controller.Definition = _rifle;

            LogAssert.Expect(LogType.Error, new Regex(@"^\[WeaponController\] 'Camera' の武器定義が不正です: weapon 'Rifle'"));

            Assert.That(_controller.TryInitializeFromHierarchy(), Is.False);
            Assert.That(_controller.IsInitialized, Is.False);
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
