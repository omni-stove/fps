using System.Linq;
using System;
using System.Text.RegularExpressions;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using WeaponAmmo = Shooter.Core.WeaponAmmo;

namespace Shooter.Tests.EditMode
{
    public sealed class WeaponControllerTests
    {
        private GameObject _camera;
        private WeaponController _controller;
        private WeaponDefinition _rifle;
        private InputAction _attack;
        private InputAction _reload;
        private readonly System.Collections.Generic.List<GameObject> _created = new System.Collections.Generic.List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            _camera = new GameObject("Camera", typeof(Camera));
            _controller = _camera.AddComponent<WeaponController>();
            _rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            _rifle.name = "Rifle";
            WeaponAssets.Configure(_rifle, TestWeapons.RifleSpec());
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
            foreach (var created in _created)
            {
                Object.DestroyImmediate(created);
            }

            _created.Clear();
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
        public void InitializeUsesTheAssignedDefinitions()
        {
            var handgun = CreateHandgun();
            try
            {
                _controller.Definitions = new[] { _rifle, handgun };

                _controller.Initialize(_camera.transform, _attack, _reload);

                Assert.That(_controller.IsInitialized, Is.True);
                Assert.That(_controller.Loadout.Count, Is.EqualTo(2));
                Assert.That(_controller.CurrentIndex, Is.Zero);
                Assert.That(_controller.Weapon.Spec.Id, Is.EqualTo(TestWeapons.RifleId));
                Assert.That(_controller.Definition, Is.SameAs(_rifle));
                Assert.That(_controller.NextAction, Is.Null, "no switch input unless passed");
            }
            finally
            {
                Object.DestroyImmediate(handgun);
            }
        }

        [Test]
        public void DefinitionsCopyTheAssignedListAndTreatNullAsEmpty()
        {
            var list = new System.Collections.Generic.List<WeaponDefinition> { _rifle };
            _controller.Definitions = list;
            list.Clear();

            Assert.That(_controller.Definitions.Count(), Is.EqualTo(1));

            _controller.Definitions = null;
            Assert.That(_controller.Definitions, Is.Empty);
        }

        [Test]
        public void NextAndPreviousSwitchWeaponsAndWrapAround()
        {
            var handgun = CreateHandgun();
            try
            {
                InitializeWithRifleAndHandgun(handgun);
                var changes = new System.Collections.Generic.List<Shooter.Core.Weapon>();
                _controller.WeaponChanged += changes.Add;
                var rifle = _controller.Weapon;

                Assert.That(_controller.SelectNext(), Is.True);
                Assert.That(_controller.CurrentIndex, Is.EqualTo(1));
                Assert.That(_controller.Weapon.Spec.Id, Is.EqualTo(TestWeapons.HandgunId));
                Assert.That(_controller.Definition, Is.SameAs(handgun));
                Assert.That(_controller.HitscanRange, Is.EqualTo(handgun.Range), "the range follows the held weapon");

                Assert.That(_controller.SelectNext(), Is.True, "next after the last wraps to the first");
                Assert.That(_controller.Weapon, Is.SameAs(rifle), "switching back holds the same weapon");

                Assert.That(_controller.SelectPrevious(), Is.True, "previous before the first wraps to the last");
                Assert.That(_controller.CurrentIndex, Is.EqualTo(1));

                Assert.That(_controller.Select(TestWeapons.RifleId), Is.True);
                Assert.That(_controller.Select(TestWeapons.RifleId), Is.False, "already held");
                Assert.That(_controller.Select("00000000-0000-4000-8000-00000000ffff"), Is.False, "not in the loadout");
                Assert.That(_controller.Select(5), Is.False);

                Assert.That(changes.Count(), Is.EqualTo(4));
                Assert.That(changes[3], Is.SameAs(rifle));
            }
            finally
            {
                Object.DestroyImmediate(handgun);
            }
        }

        [Test]
        public void TheCarryMultiplierFollowsTheHeldWeaponAndTheWholeLoadout()
        {
            var handgun = CreateHandgun();
            try
            {
                _rifle.Weight = 4f;
                _rifle.MoveSpeedMultiplier = 0.9f;
                handgun.Weight = 1f;
                handgun.MoveSpeedMultiplier = 1.1f;
                InitializeWithRifleAndHandgun(handgun);
                var held = new Shooter.Core.CarryLoadSpec(
                    Shooter.Core.CarrySource.Held, Shooter.Core.CarrySource.Held, 0f, 0.1f, 0f, Shooter.Core.CarryGaits.All);
                var carried = new Shooter.Core.CarryLoadSpec(
                    Shooter.Core.CarrySource.Carried, Shooter.Core.CarrySource.None, 0f, 0.1f, 0f, Shooter.Core.CarryGaits.All);

                // 4 kg → 0.6、それに 0.9
                Assert.That(_controller.CarrySpeedMultiplier(held), Is.EqualTo(0.6f * 0.9f).Within(1e-5f));
                // 4 + 1 kg → 0.5
                Assert.That(_controller.CarrySpeedMultiplier(carried), Is.EqualTo(0.5f).Within(1e-5f));

                Assert.That(_controller.SelectNext(), Is.True);

                // 1 kg → 0.9、それに 1.1
                Assert.That(_controller.CarrySpeedMultiplier(held), Is.EqualTo(0.9f * 1.1f).Within(1e-5f));
                Assert.That(_controller.CarrySpeedMultiplier(carried), Is.EqualTo(0.5f).Within(1e-5f));
            }
            finally
            {
                Object.DestroyImmediate(handgun);
            }
        }

        [Test]
        public void BeforeInitializingTheCarryMultiplierIsOne()
        {
            var carry = new Shooter.Core.CarryLoadSpec(
                Shooter.Core.CarrySource.Carried, Shooter.Core.CarrySource.Carried, 0f, 0.1f, 0f, Shooter.Core.CarryGaits.All);

            Assert.That(_controller.CarrySpeedMultiplier(carry), Is.EqualTo(1f));
            Assert.Throws<ArgumentNullException>(() => _controller.CarrySpeedMultiplier(null));
        }

        [Test]
        public void ASingleWeaponDoesNotSwitch()
        {
            _controller.Initialize(_camera.transform, _attack, _reload, _rifle);

            Assert.That(_controller.SelectNext(), Is.False);
            Assert.That(_controller.SelectPrevious(), Is.False);
            Assert.That(_controller.CurrentIndex, Is.Zero);
        }

        [Test]
        public void SwitchingCancelsTheReloadAndBothWeaponsFire()
        {
            var handgun = CreateHandgun();
            try
            {
                InitializeWithRifleAndHandgun(handgun);
                var rifle = _controller.Weapon;
                LogAssert.Expect(LogType.Log, $"[Combat] fire ammo={_rifle.MagazineSize - 1}");
                Assert.That(_controller.PullTrigger(true, true), Is.True);
                Assert.That(_controller.StartReload(), Is.True);

                _controller.SelectNext();
                Assert.That(rifle.IsReloading, Is.False, "the holstered rifle stops reloading");

                LogAssert.Expect(LogType.Log, $"[Combat] fire ammo={handgun.MagazineSize - 1}");
                Assert.That(_controller.PullTrigger(true, true), Is.True);
                _controller.Tick(handgun.FireInterval);
                Assert.That(_controller.PullTrigger(true, false), Is.False);

                _controller.Tick(_rifle.ReloadSeconds);
                Assert.That(rifle.AmmoInMagazine, Is.EqualTo(_rifle.MagazineSize - 1), "a cancelled reload does not refill");

                // しまっている武器の時間は進まないので、rifle の連射間隔は戻ってから経たせる。
                _controller.SelectPrevious();
                _controller.Tick(_rifle.FireInterval);
                LogAssert.Expect(LogType.Log, $"[Combat] fire ammo={_rifle.MagazineSize - 2}");
                Assert.That(_controller.PullTrigger(true, false), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(handgun);
            }
        }

        [Test]
        public void DuplicateWeaponIdsAreRejectedAndKeepThePreviousLoadout()
        {
            _controller.Initialize(_camera.transform, _attack, _reload, _rifle);
            var previous = _controller.Weapon;

            Assert.Throws<ArgumentException>(
                () => _controller.Initialize(_camera.transform, _attack, _reload, null, null, new[] { _rifle, _rifle }));
            Assert.Throws<ArgumentException>(
                () => _controller.Initialize(_camera.transform, _attack, _reload, null, null, new WeaponDefinition[] { _rifle, null }));
            Assert.Throws<ArgumentException>(
                () => _controller.Initialize(_camera.transform, _attack, _reload, null, null, Array.Empty<WeaponDefinition>()));

            Assert.That(_controller.Weapon, Is.SameAs(previous));
        }

        [Test]
        public void ReloadStartedIsRaisedOnlyWhenAReloadStarts()
        {
            _controller.Initialize(_camera.transform, _attack, _reload, _rifle);
            var started = 0;
            _controller.ReloadStarted += weapon =>
            {
                Assert.That(weapon, Is.SameAs(_controller.Weapon));
                started++;
            };

            Assert.That(_controller.StartReload(), Is.False, "a full magazine does not reload");
            Assert.That(_controller.Fire(), Is.True);
            Assert.That(_controller.StartReload(), Is.True);
            Assert.That(_controller.StartReload(), Is.False, "already reloading");
            Assert.That(started, Is.EqualTo(1));
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
            Assert.Throws<InvalidOperationException>(() => _controller.PressReload());
            Assert.Throws<InvalidOperationException>(() => _controller.TryActiveReload());
            Assert.Throws<InvalidOperationException>(() => _controller.Tick(0.1f));
        }

        [Test]
        public void RestoreLoadoutReplacesTheWeaponsAndCancelsReload()
        {
            var handgun = CreateHandgun();
            try
            {
                InitializeWithRifleAndHandgun(handgun);
                LogAssert.Expect(LogType.Log, $"[Combat] fire ammo={_rifle.MagazineSize - 1}");
                _controller.Fire();
                Assert.That(_controller.StartReload(), Is.True);
                var previous = _controller.Weapon;
                Shooter.Core.Weapon changed = null;
                _controller.WeaponChanged += weapon => changed = weapon;

                _controller.RestoreLoadout(
                    new[] { new WeaponAmmo(TestWeapons.RifleId, 7, 12), new WeaponAmmo(TestWeapons.HandgunId, 3, 9) },
                    TestWeapons.HandgunId);

                Assert.That(_controller.CurrentIndex, Is.EqualTo(1), "the saved weapon is held");
                Assert.That(changed, Is.SameAs(_controller.Weapon));
                Assert.That(_controller.Weapon.AmmoInMagazine, Is.EqualTo(3));
                Assert.That(_controller.Weapon.ReserveAmmo, Is.EqualTo(9));
                var rifle = _controller.Loadout.Items[0];
                Assert.That(rifle, Is.Not.SameAs(previous));
                Assert.That(rifle.Spec, Is.SameAs(previous.Spec));
                Assert.That(rifle.AmmoInMagazine, Is.EqualTo(7));
                Assert.That(rifle.ReserveAmmo, Is.EqualTo(12));
                Assert.That(rifle.IsReloading, Is.False);
                Assert.That(_controller.SnapshotAmmo().Count(), Is.EqualTo(2));

                Assert.That(_controller.SelectNext(), Is.True);
                Assert.That(changed, Is.SameAs(rifle));
            }
            finally
            {
                Object.DestroyImmediate(handgun);
            }
        }

        [Test]
        public void RestoreLoadoutClampsFillsMissingWeaponsAndWarnsAboutUnknownOnes()
        {
            var handgun = CreateHandgun();
            try
            {
                InitializeWithRifleAndHandgun(handgun);
                const string unknownId = "00000000-0000-4000-8000-00000000ffff";
                LogAssert.Expect(LogType.Warning, new Regex(@"^\[WeaponController\] 'Camera' は武器 '" + unknownId + "' を持っていない"));
                LogAssert.Expect(LogType.Warning, new Regex(@"^\[WeaponController\] 'Camera' はセーブで持っていた武器 '" + unknownId + "' を持っていない"));

                _controller.RestoreLoadout(
                    new[] { new WeaponAmmo(TestWeapons.RifleId, _rifle.MagazineSize + 5, 0), new WeaponAmmo(unknownId, 1, 1) },
                    unknownId);

                Assert.That(_controller.CurrentIndex, Is.Zero, "an unknown equipped weapon falls back to the first");
                Assert.That(_controller.Weapon.AmmoInMagazine, Is.EqualTo(_rifle.MagazineSize), "clamped to the magazine");
                Assert.That(_controller.Weapon.ReserveAmmo, Is.Zero);
                Assert.That(_controller.Loadout.Items[1].AmmoInMagazine, Is.EqualTo(handgun.MagazineSize), "a weapon missing from the save starts full");
            }
            finally
            {
                Object.DestroyImmediate(handgun);
            }
        }

        [Test]
        public void RestoreLoadoutNeedsInitializeAndRejectsNull()
        {
            Assert.Throws<InvalidOperationException>(() => _controller.RestoreLoadout(Array.Empty<WeaponAmmo>(), null));
            Assert.Throws<InvalidOperationException>(() => _controller.SnapshotAmmo());
            Assert.Throws<InvalidOperationException>(() => _controller.SelectNext());

            _controller.Initialize(_camera.transform, _attack, _reload, _rifle);
            Assert.Throws<ArgumentNullException>(() => _controller.RestoreLoadout(null, null));
            Assert.Throws<ArgumentException>(() => _controller.RestoreLoadout(new WeaponAmmo[] { null }, null));
        }

        private static Shooter.Core.GameRules ReserveRules(bool infinite, float multiplier)
        {
            return new Shooter.Core.GameRules(
                Shooter.Core.FriendlyFire.Off,
                Shooter.Core.RespawnMode.Individual,
                Shooter.Core.GameRules.DefaultWaveIntervalSeconds,
                0f,
                0f,
                Shooter.Core.DeathBody.Vanish,
                Shooter.Core.DeathCamera.None,
                infinite,
                multiplier,
                Shooter.Core.Destruction.Off);
        }

        [Test]
        public void InitializeBuildsTheWeaponsWithTheConfiguredRules()
        {
            Assert.That(_controller.Rules, Is.SameAs(Shooter.Core.GameRules.Default), "the default before anything is set");
            var rules = ReserveRules(true, 2f);
            _controller.Rules = rules;

            _controller.Initialize(_camera.transform, _attack, _reload, _rifle);

            Assert.That(_controller.LoadedRules, Is.SameAs(rules));
            Assert.That(_controller.Weapon.ReserveAmmo, Is.EqualTo(_rifle.ReserveAmmo * 2));
            Assert.That(_controller.Weapon.HasInfiniteReserve, Is.True);

            _controller.Rules = null;
            Assert.That(_controller.Rules, Is.SameAs(Shooter.Core.GameRules.Default), "null resets to the default");
            Assert.That(_controller.Weapon.HasInfiniteReserve, Is.True, "setting Rules does not rebuild the current weapons");
        }

        [Test]
        public void ApplyRulesRebuildsEveryWeaponFullWithTheNewReserve()
        {
            var handgun = CreateHandgun();
            try
            {
                InitializeWithRifleAndHandgun(handgun);
                LogAssert.Expect(LogType.Log, $"[Combat] fire ammo={_rifle.MagazineSize - 1}");
                _controller.Fire();
                _controller.SelectNext();
                Shooter.Core.Weapon changed = null;
                _controller.WeaponChanged += weapon => changed = weapon;
                var rules = ReserveRules(false, 0.6f);

                _controller.ApplyRules(rules);

                Assert.That(_controller.Rules, Is.SameAs(rules));
                Assert.That(_controller.LoadedRules, Is.SameAs(rules));
                Assert.That(_controller.CurrentIndex, Is.Zero, "starts again with the first weapon");
                Assert.That(changed, Is.SameAs(_controller.Weapon));
                var rifle = _controller.Loadout.Items[0];
                Assert.That(rifle.AmmoInMagazine, Is.EqualTo(_rifle.MagazineSize), "rebuilt full");
                Assert.That(rifle.ReserveAmmo, Is.EqualTo(Shooter.Core.Weapon.ScaleReserveAmmo(_rifle.ReserveAmmo, 0.6f)));
                Assert.That(
                    _controller.Loadout.Items[1].ReserveAmmo,
                    Is.EqualTo(Shooter.Core.Weapon.ScaleReserveAmmo(handgun.ReserveAmmo, 0.6f)));
            }
            finally
            {
                Object.DestroyImmediate(handgun);
            }
        }

        [Test]
        public void RestoreLoadoutKeepsTheSavedReserveAndTheInfiniteFlagOfTheRules()
        {
            var handgun = CreateHandgun();
            try
            {
                InitializeWithRifleAndHandgun(handgun);
                _controller.ApplyRules(ReserveRules(true, 1.5f));

                _controller.RestoreLoadout(new[] { new WeaponAmmo(TestWeapons.RifleId, 7, 12) }, TestWeapons.RifleId);

                Assert.That(_controller.Weapon.ReserveAmmo, Is.EqualTo(12), "the multiplier is not applied again");
                Assert.That(_controller.Weapon.HasInfiniteReserve, Is.True);
                Assert.That(
                    _controller.Loadout.Items[1].ReserveAmmo,
                    Is.EqualTo(Shooter.Core.Weapon.ScaleReserveAmmo(handgun.ReserveAmmo, 1.5f)),
                    "a weapon missing from the save starts with the multiplier");
            }
            finally
            {
                Object.DestroyImmediate(handgun);
            }
        }

        [Test]
        public void ApplyRulesNeedsInitializeAndRules()
        {
            Assert.Throws<InvalidOperationException>(() => _controller.ApplyRules(Shooter.Core.GameRules.Default));
            Assert.That(_controller.LoadedRules, Is.Null);

            _controller.Initialize(_camera.transform, _attack, _reload, _rifle);
            Assert.Throws<ArgumentNullException>(() => _controller.ApplyRules(null));
        }

        [Test]
        public void InitializeBuildsTheWeaponFromTheDefinition()
        {
            Shooter.Core.Weapon changed = null;
            _controller.WeaponChanged += weapon => changed = weapon;

            _controller.Initialize(_camera.transform, _attack, _reload, _rifle);

            var expected = TestWeapons.RifleSpec();
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
                WeaponAssets.Configure(handgun, TestWeapons.HandgunSpec());

                _controller.Initialize(_camera.transform, _attack, _reload, handgun);

                Assert.That(_controller.Definition, Is.SameAs(handgun));
                Assert.That(_controller.Weapon.Spec.Id, Is.EqualTo(TestWeapons.HandgunId));
                Assert.That(_controller.HitscanRange, Is.EqualTo(handgun.Range));
            }
            finally
            {
                Object.DestroyImmediate(handgun);
            }
        }

        [Test]
        public void SpreadGrowsWithEachShotAndRecoversAfterTheDelay()
        {
            _controller.Initialize(_camera.transform, _attack, _reload, _rifle);
            Assert.That(_controller.SpreadFraction, Is.EqualTo(0f));

            LogAssert.Expect(LogType.Log, $"[Combat] fire ammo={_rifle.MagazineSize - 1}");
            Assert.That(_controller.Fire(), Is.True);
            Assert.That(_controller.SpreadFraction, Is.EqualTo(WeaponController.SpreadPerShot));

            _controller.Tick(WeaponController.SpreadRecoveryDelaySeconds);
            Assert.That(_controller.SpreadFraction, Is.EqualTo(WeaponController.SpreadPerShot));

            _controller.Tick(WeaponController.SpreadPerShot / WeaponController.SpreadRecoveryPerSecond / 2f);
            Assert.That(_controller.SpreadFraction, Is.EqualTo(WeaponController.SpreadPerShot / 2f).Within(1e-5f));

            _controller.Tick(10f);
            Assert.That(_controller.SpreadFraction, Is.EqualTo(0f));
        }

        [Test]
        public void FireLogsTheRemainingAmmoAndRespectsTheFireInterval()
        {
            _controller.Initialize(_camera.transform, _attack, _reload, _rifle);
            var full = _rifle.MagazineSize;

            LogAssert.Expect(LogType.Log, $"[Combat] fire ammo={full - 1}");
            Assert.That(_controller.Fire(), Is.True);

            Assert.That(_controller.Fire(), Is.False);

            _controller.Tick(_rifle.FireInterval);
            LogAssert.Expect(LogType.Log, $"[Combat] fire ammo={full - 2}");
            Assert.That(_controller.Fire(), Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ShotFilterIsNullByDefaultAndTheShotGoesStraightAhead()
        {
            _rifle.HipSpreadDegrees = 0f;
            _controller.Initialize(_camera.transform, _attack, _reload, _rifle);
            var ahead = CreateStructure("Ahead", new Vector3(0f, 0f, 10f));
            var aside = CreateStructure("Aside", new Vector3(20f, 0f, 10f));

            Assert.That(_controller.ShotFilter, Is.Null);
            Assert.That(_controller.Fire(), Is.True);

            Assert.That(ahead.Calls.Count(), Is.EqualTo(1));
            Assert.That(aside.Calls, Is.Empty);
        }

        [Test]
        public void ShotFilterOriginIsWhereTheRaycastStarts()
        {
            _rifle.HipSpreadDegrees = 0f;
            _controller.Initialize(_camera.transform, _attack, _reload, _rifle);
            var ahead = CreateStructure("Ahead", new Vector3(0f, 0f, 10f));
            var aside = CreateStructure("Aside", new Vector3(20f, 0f, 10f));
            var filter = new RewritingShotFilter { Origin = new Vector3(20f, 0f, 0f) };
            _controller.ShotFilter = filter;

            LogAssert.Expect(LogType.Log, $"[Combat] fire ammo={_rifle.MagazineSize - 1}");
            Assert.That(_controller.Fire(), Is.True);

            Assert.That(filter.Calls, Is.EqualTo(1));
            Assert.That(filter.SeenOrigin, Is.EqualTo(_camera.transform.position), "the filter gets the resolved shot");
            Assert.That(filter.SeenDirection, Is.EqualTo(_camera.transform.forward));
            Assert.That(ahead.Calls, Is.Empty);
            Assert.That(aside.Calls.Count(), Is.EqualTo(1));
            Assert.That(aside.Calls[0].Point.x, Is.EqualTo(20f).Within(1e-4f));
        }

        [Test]
        public void ShotFilterDirectionIsWhereTheRaycastGoes()
        {
            _controller.Initialize(_camera.transform, _attack, _reload, _rifle);
            var ahead = CreateStructure("Ahead", new Vector3(0f, 0f, 10f));
            var right = CreateStructure("Right", new Vector3(10f, 0f, 0f));
            _controller.ShotFilter = new RewritingShotFilter { Direction = Vector3.right };

            Assert.That(_controller.Fire(), Is.True);

            Assert.That(ahead.Calls, Is.Empty);
            Assert.That(right.Calls.Count(), Is.EqualTo(1));
        }

        [Test]
        public void ShotFilterIsNotCalledWhenTheWeaponCannotFire()
        {
            _controller.Initialize(_camera.transform, _attack, _reload, _rifle);
            var filter = new RewritingShotFilter();
            _controller.ShotFilter = filter;

            Assert.That(_controller.Fire(), Is.True);
            Assert.That(_controller.Fire(), Is.False, "within the fire interval");

            Assert.That(filter.Calls, Is.EqualTo(1));
        }

        [Test]
        public void FireBlockerStopsShotsWithoutSpendingAmmo()
        {
            _controller.Initialize(_camera.transform, _attack, _reload, _rifle);
            var blocked = true;
            var fired = 0;
            _controller.Fired += _ => fired++;
            _controller.FireBlocker = () => blocked;

            Assert.That(_controller.IsFireBlocked, Is.True);
            Assert.That(_controller.Fire(), Is.False);
            Assert.That(_controller.PullTrigger(true, true), Is.False);
            Assert.That(_controller.Weapon.AmmoInMagazine, Is.EqualTo(_rifle.MagazineSize));
            Assert.That(_controller.ShotCount, Is.EqualTo(0));
            Assert.That(fired, Is.EqualTo(0));

            blocked = false;
            LogAssert.Expect(LogType.Log, $"[Combat] fire ammo={_rifle.MagazineSize - 1}");
            Assert.That(_controller.Fire(), Is.True);
            Assert.That(fired, Is.EqualTo(1));
        }

        [Test]
        public void FireBlockerIsNullByDefault()
        {
            Assert.That(_controller.FireBlocker, Is.Null);
            Assert.That(_controller.IsFireBlocked, Is.False);
        }

        [Test]
        public void AWeaponWithoutSpreadOrRecoilShootsStraightAndLeavesTheViewAlone()
        {
            _rifle.HipSpreadDegrees = 0f;
            _rifle.MoveSpreadDegrees = 0f;
            _rifle.RecoilPitchDegrees = 0f;
            _rifle.RecoilYawDegrees = 0f;
            _controller.Initialize(_camera.transform, _attack, _reload, _rifle);
            _controller.MoveFractionSource = () => 1f;
            var filter = new RewritingShotFilter();
            _controller.ShotFilter = filter;

            Assert.That(_controller.Fire(), Is.True);

            Assert.That(filter.SeenDirection, Is.EqualTo(_camera.transform.forward));
            Assert.That(_controller.Recoil.IsAtRest, Is.True);
            Assert.That(_controller.ShotCount, Is.EqualTo(1));
        }

        [Test]
        public void HipFireSpreadsTheShotsInsideTheConeBySeed()
        {
            _rifle.HipSpreadDegrees = 10f;
            _rifle.AdsSpreadDegrees = 0f;
            _controller.Initialize(_camera.transform, _attack, _reload, _rifle);
            _controller.PlayerId = 3;
            var filter = new RewritingShotFilter();
            _controller.ShotFilter = filter;
            var forward = _camera.transform.forward;
            var expected = Shooter.Core.SpreadPattern.Direction(forward.ToNumerics(), 10f, 3, 0).ToUnity();

            Assert.That(_controller.Fire(), Is.True);

            Assert.That(Vector3.Angle(forward, filter.SeenDirection), Is.GreaterThan(0f).And.LessThanOrEqualTo(10f + 1e-3f));
            Assert.That(Vector3.Distance(filter.SeenDirection, expected), Is.LessThan(1e-5f), "the seed decides the direction");

            _controller.Tick(_rifle.FireInterval);
            Assert.That(_controller.Fire(), Is.True);
            Assert.That(Vector3.Distance(filter.SeenDirection, expected), Is.GreaterThan(1e-4f), "the next shot uses the next seed");
        }

        [Test]
        public void AimingFullyUsesTheAdsSpread()
        {
            _rifle.HipSpreadDegrees = 10f;
            _rifle.AdsSpreadDegrees = 0f;
            _controller.Initialize(_camera.transform, _attack, _reload, _rifle);
            _controller.AdsProgressSource = () => 1f;
            var filter = new RewritingShotFilter();
            _controller.ShotFilter = filter;

            Assert.That(_controller.CurrentConeDegrees(_controller.Weapon.Spec), Is.EqualTo(0f));
            Assert.That(_controller.Fire(), Is.True);
            Assert.That(filter.SeenDirection, Is.EqualTo(_camera.transform.forward));
        }

        [Test]
        public void MovingAddsTheMoveSpread()
        {
            _rifle.HipSpreadDegrees = 2f;
            _rifle.MoveSpreadDegrees = 4f;
            _controller.Initialize(_camera.transform, _attack, _reload, _rifle);

            Assert.That(_controller.CurrentConeDegrees(_controller.Weapon.Spec), Is.EqualTo(2f));

            _controller.MoveFractionSource = () => 0.5f;
            Assert.That(_controller.CurrentConeDegrees(_controller.Weapon.Spec), Is.EqualTo(4f).Within(1e-5f));
        }

        [Test]
        public void TheCrosshairConeIsTheWeaponsSpreadEvenWhenStill()
        {
            _rifle.HipSpreadDegrees = 2f;
            _rifle.MoveSpreadDegrees = 4f;
            _controller.Initialize(_camera.transform, _attack, _reload, _rifle);

            Assert.That(_controller.CrosshairConeDegrees, Is.EqualTo(2f));
            Assert.That(_controller.CrosshairSpreadFraction, Is.EqualTo(0f));

            _controller.MoveFractionSource = () => 1f;
            Assert.That(_controller.CrosshairConeDegrees, Is.EqualTo(6f).Within(1e-5f));
            Assert.That(_controller.CrosshairSpreadFraction, Is.EqualTo(0f), "movement is in the cone, not added again as bloom");

            _controller.SpreadScaleSource = () => 0.5f;
            Assert.That(_controller.CrosshairConeDegrees, Is.EqualTo(3f).Within(1e-5f));

            _controller.MoveFractionSource = () => float.NaN;
            Assert.That(_controller.CrosshairConeDegrees, Is.EqualTo(0f));
        }

        [Test]
        public void TheCrosshairConeAddsThePelletSpread()
        {
            _rifle.HipSpreadDegrees = 1.5f;
            _rifle.MoveSpreadDegrees = 0f;
            _rifle.PelletCount = 8;
            _rifle.PelletSpreadDegrees = 4f;
            _controller.Initialize(_camera.transform, _attack, _reload, _rifle);

            Assert.That(_controller.CrosshairConeDegrees, Is.EqualTo(5.5f).Within(1e-5f));
        }

        [Test]
        public void FiringOpensTheCrosshairBeyondTheCone()
        {
            _rifle.HipSpreadDegrees = 2f;
            _rifle.MoveSpreadDegrees = 0f;
            _controller.Initialize(_camera.transform, _attack, _reload, _rifle);

            _controller.Fire();

            Assert.That(_controller.CrosshairSpreadFraction, Is.EqualTo(WeaponController.SpreadPerShot).Within(1e-5f));
            Assert.That(_controller.CrosshairConeDegrees, Is.EqualTo(2f));
        }

        [Test]
        public void ModifiersWidenTheConeAndTheCrosshairInOrderWithinTheLimits()
        {
            _rifle.HipSpreadDegrees = 2f;
            _rifle.MoveSpreadDegrees = 0f;
            _controller.Initialize(_camera.transform, _attack, _reload, _rifle);
            var spec = _controller.Weapon.Spec;
            var widen = new AddingModifier(3f, 0.4f);
            var doubled = new ScalingModifier(2f);

            Assert.That(_controller.AddModifier(widen), Is.True);
            Assert.That(_controller.AddModifier(doubled), Is.True);
            Assert.That(_controller.AddModifier(widen), Is.False, "the same modifier is added once");

            Assert.That(_controller.CurrentConeDegrees(spec), Is.EqualTo((2f + 3f) * 2f).Within(1e-5f));
            Assert.That(_controller.SpreadFraction, Is.EqualTo(0.8f).Within(1e-5f));

            _controller.Fire();
            Assert.That(_controller.SpreadFraction, Is.EqualTo(1f), "the crosshair never goes past 1");

            _controller.AddModifier(new AddingModifier(1000f, 0f));
            Assert.That(_controller.CurrentConeDegrees(spec), Is.EqualTo(Shooter.Core.SpreadPattern.MaxDegrees));

            Assert.That(_controller.RemoveModifier(widen), Is.True);
            Assert.That(_controller.RemoveModifier(widen), Is.False);
            Assert.That(_controller.Modifiers.Count(), Is.EqualTo(2));
            Assert.Throws<ArgumentNullException>(() => _controller.AddModifier(null));
            Assert.Throws<ArgumentNullException>(() => _controller.RemoveModifier(null));
        }

        [Test]
        public void FiringPastATargetReportsANearMiss()
        {
            _rifle.HipSpreadDegrees = 0f;
            _rifle.MoveSpreadDegrees = 0f;
            _controller.Initialize(_camera.transform, _attack, _reload, _rifle);
            var target = new FakeNearMissTarget(new GameObject("Bystander"), _camera.transform.position + (_camera.transform.forward * 5f) + (_camera.transform.right * 0.5f), 1f);
            _created.Add(target.Root.gameObject);
            Hitscan.RegisterNearMissTarget(target);
            try
            {
                Assert.That(_controller.Fire(), Is.True);

                Assert.That(target.NearMisses.Count(), Is.EqualTo(1));
                Assert.That(target.NearMisses[0].Distance, Is.EqualTo(0.5f).Within(1e-4f));
                Assert.That(target.NearMisses[0].Shooter, Is.SameAs(_camera));
            }
            finally
            {
                Hitscan.UnregisterNearMissTarget(target);
            }
        }

        [Test]
        public void HoldingTheLaserDamagesAtTheBeamRateUsesAmmoEveryTickAndStopsOnRelease()
        {
            var laser = Laser(0.125f);
            try
            {
                _controller.Initialize(_camera.transform, _attack, _reload, laser);
                var target = BeamTarget();

                Assert.That(_controller.PullTrigger(true, true), Is.True);
                for (var frame = 0; frame < 60; frame++)
                {
                    _controller.Tick(1f / 60f);
                    _controller.PullTrigger(true, false);
                }

                Assert.That(target.Health.Current, Is.EqualTo(1000 - (9 * _laserDamage)), "a tick on press and every 0.125 s for 1 s");
                Assert.That(_controller.Weapon.AmmoInMagazine, Is.EqualTo(100 - 9));
                var line = _camera.GetComponent<BeamLine>();
                Assert.That(line.IsVisible, Is.True);
                Assert.That(line.Line.GetPosition(1).z, Is.EqualTo(9.5f).Within(1e-3f));

                Assert.That(_controller.PullTrigger(false, false), Is.False);
                for (var frame = 0; frame < 60; frame++)
                {
                    _controller.Tick(1f / 60f);
                    Assert.That(_controller.PullTrigger(false, false), Is.False);
                }

                Assert.That(target.Health.Current, Is.EqualTo(1000 - (9 * _laserDamage)));
            }
            finally
            {
                DestroyBeamVisual();
                Object.DestroyImmediate(laser);
            }
        }

        [Test]
        public void TheLaserDrawsTheVisualOfItsDefinitionWithoutItsColliders()
        {
            var laser = Laser(0.1f);
            var prefab = new GameObject("Laser Visual", typeof(LineRenderer), typeof(BoxCollider));
            _created.Add(prefab);
            laser.Beam.Visual = prefab;
            try
            {
                _controller.Initialize(_camera.transform, _attack, _reload, laser);

                Assert.That(_controller.PullTrigger(true, true), Is.True);

                var line = _camera.GetComponent<BeamLine>();
                Assert.That(line.Visual, Is.Not.SameAs(prefab));
                Assert.That(line.Line, Is.SameAs(line.Visual.GetComponent<LineRenderer>()));
                Assert.That(line.Visual.GetComponentInChildren<Collider>(true), Is.Null);
                Assert.That(BeamLine.FindSettings(_camera, laser.Id), Is.SameAs(laser.Beam));
                Assert.That(BeamLine.FindSettings(_camera, "unknown"), Is.Null);
            }
            finally
            {
                DestroyBeamVisual();
                Object.DestroyImmediate(laser);
            }
        }

        // Play していない間は BeamLine の OnDestroy が呼ばれず、撃った者と別に置いた線が残るので、試験の側で消す。
        private void DestroyBeamVisual()
        {
            var line = _camera.GetComponent<BeamLine>();
            if (line != null && line.Visual != null)
            {
                Object.DestroyImmediate(line.Visual);
            }
        }

        private const string _laserId = "5b8e2f41-7c3d-4a96-b1e0-9d4c2a7f6e18";

        private const int _laserDamage = 4;

        private static WeaponDefinition Laser(float tickSeconds)
        {
            var laser = ScriptableObject.CreateInstance<WeaponDefinition>();
            laser.name = "Laser";
            WeaponAssets.Configure(
                laser,
                new Shooter.Core.WeaponSpec(
                    _laserId,
                    null,
                    Shooter.Core.WeaponCategory.AssaultRifle,
                    new Shooter.Core.TriggerSpec(Shooter.Core.FireMode.Auto, 1f),
                    new Shooter.Core.AmmoSpec(magazineSize: 100, reserveAmmo: 0),
                    new Shooter.Core.RoundSpec(Shooter.Core.RoundDelivery.Beam, 50f, beam: new Shooter.Core.BeamSpec(tickSeconds)),
                    new Shooter.Core.ImpactSpec(_laserDamage)));
            return laser;
        }

        private Damageable BeamTarget()
        {
            var target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _created.Add(target);
            target.transform.position = _camera.transform.position + (_camera.transform.forward * 10f);
            var damageable = target.AddComponent<Damageable>();
            damageable.Initialize("beam-target", 1000);
            target.AddComponent<Hitbox>().Initialize(Shooter.Core.HitZone.Body, damageable);
            Physics.SyncTransforms();
            return damageable;
        }

        [Test]
        public void FiringKicksTheRecoilAndTickRecoversIt()
        {
            _rifle.RecoilPitchDegrees = 2f;
            _rifle.RecoilYawDegrees = 0f;
            _rifle.RecoilRecoveryPerSecond = 100f;
            _controller.Initialize(_camera.transform, _attack, _reload, _rifle);

            Assert.That(_controller.Fire(), Is.True);
            Assert.That(_controller.Recoil.PitchUpDegrees, Is.EqualTo(2f));

            _controller.Tick(10f);
            Assert.That(_controller.Recoil.IsAtRest, Is.True);
        }

        [Test]
        public void FiredIsRaisedOnlyWhenAShotIsFired()
        {
            _controller.Initialize(_camera.transform, _attack, _reload, _rifle);
            var fired = 0;
            _controller.Fired += weapon =>
            {
                Assert.That(weapon, Is.SameAs(_controller.Weapon));
                fired++;
            };

            Assert.That(_controller.Fire(), Is.True);
            Assert.That(_controller.Fire(), Is.False, "within the fire interval");
            Assert.That(fired, Is.EqualTo(1));
        }

        [Test]
        public void PressingTheTriggerOnAnEmptyMagazineClicksAndStartsTheReload()
        {
            _controller.Initialize(_camera.transform, _attack, _reload, _rifle);
            _controller.RestoreLoadout(new[] { new WeaponAmmo(TestWeapons.RifleId, 0, 12) }, TestWeapons.RifleId);
            var dry = new System.Collections.Generic.List<Shooter.Core.Weapon>();
            var reloads = 0;
            _controller.DryFired += dry.Add;
            _controller.ReloadStarted += _ => reloads++;

            Assert.That(WeaponController.IsDryFire(_controller.Weapon), Is.True);
            LogAssert.Expect(LogType.Log, $"{WeaponController.DryFireLogPrefix} id={TestWeapons.RifleId}");
            Assert.That(_controller.PullTrigger(true, true), Is.False);
            Assert.That(dry.Count(), Is.EqualTo(1));
            Assert.That(dry[0], Is.SameAs(_controller.Weapon));
            Assert.That(_controller.Weapon.IsReloading, Is.True);
            Assert.That(reloads, Is.EqualTo(1));
            Assert.That(_controller.Weapon.AmmoInMagazine, Is.Zero, "the magazine fills when the reload ends, not at once");
            Assert.That(_controller.ShotCount, Is.Zero);

            Assert.That(_controller.PullTrigger(true, false), Is.False);
            Assert.That(_controller.PullTrigger(true, true), Is.False);
            Assert.That(dry.Count(), Is.EqualTo(1), "a reloading weapon does not click");
            Assert.That(reloads, Is.EqualTo(1), "nor start the reload again");
        }

        [Test]
        public void PressingTheTriggerWithNoAmmoLeftOnlyClicks()
        {
            _controller.Initialize(_camera.transform, _attack, _reload, _rifle);
            _controller.RestoreLoadout(new[] { new WeaponAmmo(TestWeapons.RifleId, 0, 0) }, TestWeapons.RifleId);
            var dry = 0;
            _controller.DryFired += _ => dry++;

            LogAssert.Expect(LogType.Log, $"{WeaponController.DryFireLogPrefix} id={TestWeapons.RifleId}");
            Assert.That(_controller.PullTrigger(true, true), Is.False);
            Assert.That(dry, Is.EqualTo(1));
            Assert.That(_controller.Weapon.IsReloading, Is.False);

            _controller.Tick(_rifle.FireInterval);
            Assert.That(_controller.PullTrigger(true, false), Is.False);
            Assert.That(dry, Is.EqualTo(1), "holding the trigger does not click again");

            LogAssert.Expect(LogType.Log, $"{WeaponController.DryFireLogPrefix} id={TestWeapons.RifleId}");
            Assert.That(_controller.PullTrigger(true, true), Is.False);
            Assert.That(dry, Is.EqualTo(2), "pressed again");
        }

        [Test]
        public void FiringTheLastRoundStartsTheReloadWhenReloadIsAuto()
        {
            Assert.That(Shooter.Core.GameRules.Default.ReloadMode, Is.EqualTo(Shooter.Core.ReloadMode.Auto));
            _controller.Initialize(_camera.transform, _attack, _reload, _rifle);
            _controller.RestoreLoadout(new[] { new WeaponAmmo(TestWeapons.RifleId, 2, 4) }, TestWeapons.RifleId);
            var reloads = 0;
            _controller.ReloadStarted += _ => reloads++;

            Assert.That(_controller.PullTrigger(true, true), Is.True);
            Assert.That(_controller.Weapon.IsReloading, Is.False, "a round is still in the magazine");

            _controller.Tick(_rifle.FireInterval);
            Assert.That(_controller.PullTrigger(true, false), Is.True, "holding the trigger fires the last round");
            Assert.That(_controller.Weapon.AmmoInMagazine, Is.Zero);
            Assert.That(_controller.Weapon.IsReloading, Is.True);
            Assert.That(reloads, Is.EqualTo(1));

            _controller.Tick(_rifle.ReloadSeconds);
            Assert.That(_controller.Weapon.AmmoInMagazine, Is.EqualTo(4), "the reserve fills the magazine");
        }

        [Test]
        public void ManualReloadNeverStartsFromTheTrigger()
        {
            _controller.Rules = Shooter.Core.GameRules.Default.WithReloadMode(Shooter.Core.ReloadMode.Manual);
            _controller.Initialize(_camera.transform, _attack, _reload, _rifle);
            _controller.RestoreLoadout(new[] { new WeaponAmmo(TestWeapons.RifleId, 1, 4) }, TestWeapons.RifleId);

            Assert.That(_controller.PullTrigger(true, true), Is.True);
            Assert.That(_controller.Weapon.AmmoInMagazine, Is.Zero);
            Assert.That(_controller.Weapon.IsReloading, Is.False, "firing the last round does not reload");

            _controller.Tick(_rifle.FireInterval);
            LogAssert.Expect(LogType.Log, $"{WeaponController.DryFireLogPrefix} id={TestWeapons.RifleId}");
            Assert.That(_controller.PullTrigger(true, true), Is.False);
            Assert.That(_controller.Weapon.IsReloading, Is.False, "pulling on an empty magazine only clicks");

            Assert.That(_controller.StartReload(), Is.True, "the Reload button still reloads");
        }

        [Test]
        public void DryFiredIsNotRaisedWithAmmoOrWhileFiringIsBlocked()
        {
            _controller.Initialize(_camera.transform, _attack, _reload, _rifle);
            var dry = 0;
            _controller.DryFired += _ => dry++;

            Assert.That(_controller.PullTrigger(true, true), Is.True);
            Assert.That(_controller.PullTrigger(true, true), Is.False, "within the fire interval");
            Assert.That(dry, Is.Zero, "a weapon with ammo waiting for its fire interval is not empty");

            _controller.RestoreLoadout(new[] { new WeaponAmmo(TestWeapons.RifleId, 0, 12) }, TestWeapons.RifleId);
            _controller.FireBlocker = () => true;
            Assert.That(_controller.PullTrigger(true, true), Is.False);
            Assert.That(dry, Is.Zero, "a blocked trigger (dodging) does not click");

            Assert.That(WeaponController.IsDryFire(null), Is.False);
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
                WeaponAssets.Configure(handgun, TestWeapons.HandgunSpec());
                Assert.That(handgun.FireMode, Is.EqualTo(Shooter.Core.FireMode.Semi));
                _controller.Initialize(_camera.transform, _attack, _reload, handgun);
                var full = handgun.MagazineSize;

                LogAssert.Expect(LogType.Log, $"[Combat] fire ammo={full - 1}");
                Assert.That(_controller.PullTrigger(true, true), Is.True);

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
        public void SwitchingTheAmmoTypeLogsEmptiesTheMagazineAndLoadsTheNewTypeOnReload()
        {
            var dmr = ScriptableObject.CreateInstance<WeaponDefinition>();
            try
            {
                dmr.name = "Dmr";
                var piercing = new Shooter.Core.AmmoTypeSpec("00000000-0000-4000-8000-0000000000a3", "ammo.dmr.armorPiercing", impact: new Shooter.Core.ImpactSpec(36));
                WeaponAssets.Configure(dmr, new Shooter.Core.WeaponSpec(
                    TestWeapons.RifleId,
                    "weapon.dmr",
                    Shooter.Core.WeaponCategory.MarksmanRifle,
                    ammo: new Shooter.Core.AmmoSpec(15, 60, 2f),
                    impact: new Shooter.Core.ImpactSpec(45),
                    ammoTypes: new[] { new Shooter.Core.AmmoTypeSpec("00000000-0000-4000-8000-0000000000a2", "ammo.dmr.standard"), piercing }));
                _controller.Initialize(_camera.transform, _attack, _reload, dmr);
                var switched = new System.Collections.Generic.List<Shooter.Core.Weapon>();
                _controller.AmmoTypeSwitched += switched.Add;

                LogAssert.Expect(LogType.Log, $"{WeaponController.AmmoTypeSwitchedLogPrefix} weapon=weapon.dmr index=1 id={piercing.Id} damage=36");
                Assert.That(_controller.SwitchAmmoType(), Is.True);

                Assert.That(switched, Is.EqualTo(new[] { _controller.Weapon }));
                Assert.That(_controller.Weapon.AmmoInMagazine, Is.Zero);
                Assert.That(_controller.Weapon.ReserveAmmo, Is.EqualTo(75));

                Assert.That(_controller.StartReload(), Is.True);
                LogAssert.Expect(LogType.Log, WeaponController.ReloadedLog);
                _controller.Tick(2f);

                Assert.That(_controller.Weapon.AmmoInMagazine, Is.EqualTo(15));
                Assert.That(_controller.Weapon.AmmoTypes.Impact.Damage, Is.EqualTo(36));
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                Object.DestroyImmediate(dmr);
            }
        }

        [Test]
        public void PickingUpAWeaponKeepsTheLoadedAmmoTypeOfTheOthers()
        {
            var dmr = ScriptableObject.CreateInstance<WeaponDefinition>();
            var handgun = CreateHandgun();
            try
            {
                dmr.name = "Dmr";
                WeaponAssets.Configure(dmr, new Shooter.Core.WeaponSpec(
                    TestWeapons.RifleId,
                    "weapon.dmr",
                    Shooter.Core.WeaponCategory.MarksmanRifle,
                    ammo: new Shooter.Core.AmmoSpec(15, 60, 2f),
                    impact: new Shooter.Core.ImpactSpec(45),
                    ammoTypes: new[]
                    {
                        new Shooter.Core.AmmoTypeSpec("00000000-0000-4000-8000-0000000000a2", "ammo.dmr.standard"),
                        new Shooter.Core.AmmoTypeSpec("00000000-0000-4000-8000-0000000000a3", "ammo.dmr.armorPiercing", impact: new Shooter.Core.ImpactSpec(36)),
                    }));
                _controller.Initialize(_camera.transform, _attack, _reload, dmr);
                LogAssert.Expect(LogType.Log, new Regex("^" + Regex.Escape(WeaponController.AmmoTypeSwitchedLogPrefix)));
                _controller.SwitchAmmoType();
                _controller.StartReload();
                LogAssert.Expect(LogType.Log, WeaponController.ReloadedLog);
                _controller.Tick(2f);

                LogAssert.Expect(LogType.Log, new Regex(@"^\[Combat\] weapon added"));
                Assert.That(_controller.AddWeapon(handgun), Is.True);

                Assert.That(_controller.Weapon.AmmoInMagazine, Is.EqualTo(15));
                Assert.That(_controller.Weapon.AmmoTypes.Index, Is.EqualTo(1), "the armor-piercing rounds stay loaded");
                Assert.That(_controller.Weapon.AmmoTypes.Impact.Damage, Is.EqualTo(36));
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                Object.DestroyImmediate(dmr);
                Object.DestroyImmediate(handgun);
            }
        }

        [Test]
        public void AWeaponWithoutAmmoTypesDoesNotSwitch()
        {
            _controller.Initialize(_camera.transform, _attack, _reload, _rifle);

            Assert.That(_controller.SwitchAmmoType(), Is.False);
            Assert.That(_controller.Weapon.AmmoInMagazine, Is.EqualTo(_rifle.MagazineSize));
            Assert.That(_controller.SwitchAmmoTypeAction, Is.Null, "no switch input unless found or set");
            Assert.Throws<ArgumentNullException>(() => WeaponController.AmmoTypeSwitchedLog(null));
            LogAssert.NoUnexpectedReceived();
        }

        private static Shooter.Core.GameRules ActiveReloadRules(Shooter.Core.ActiveReload activeReload)
        {
            var d = Shooter.Core.GameRules.Default;
            var a = Shooter.Core.ActionRules.Default;
            return new Shooter.Core.GameRules(
                d.FriendlyFire,
                d.RespawnMode,
                d.WaveIntervalSeconds,
                d.RegenDelaySeconds,
                d.RegenPerSecond,
                d.DeathBody,
                d.DeathCamera,
                d.InfiniteReserve,
                d.ReserveAmmoMultiplier,
                d.Destruction,
                new Shooter.Core.ActionRules(a.Downed, a.DownedBleedoutSeconds, a.DownedHealthFraction, a.ReviveSeconds, a.Cover, a.BlindFire, activeReload));
        }

        private void StartReloadWithRules(Shooter.Core.ActiveReload activeReload)
        {
            _controller.Rules = ActiveReloadRules(activeReload);
            _controller.Initialize(_camera.transform, _attack, _reload, _rifle);
            LogAssert.Expect(LogType.Log, $"[Combat] fire ammo={_rifle.MagazineSize - 1}");
            _controller.Fire();
            Assert.That(_controller.PressReload(), Is.True, "the first press starts the reload");
            Assert.That(_controller.Weapon.IsReloading, Is.True);
        }

        [Test]
        public void PressingReloadInsideTheWindowJudgesASuccessAndRefillsAtOnce()
        {
            StartReloadWithRules(Shooter.Core.ActiveReload.On);
            var judged = new System.Collections.Generic.List<Shooter.Core.ActiveReloadResult>();
            _controller.ActiveReloadJudged += (weapon, result) =>
            {
                Assert.That(weapon, Is.SameAs(_controller.Weapon));
                judged.Add(result);
            };
            _controller.Tick(_rifle.ReloadSeconds * (_rifle.ActiveReloadWindowStart + (_rifle.ActiveReloadWindowLength / 2f)));

            LogAssert.Expect(LogType.Log, WeaponController.ReloadedLog);
            LogAssert.Expect(LogType.Log, "[Combat] active-reload success");
            Assert.That(_controller.PressReload(), Is.True);

            Assert.That(judged, Is.EqualTo(new[] { Shooter.Core.ActiveReloadResult.Success }));
            Assert.That(_controller.Weapon.IsReloading, Is.False);
            Assert.That(_controller.Weapon.AmmoInMagazine, Is.EqualTo(_rifle.MagazineSize));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void PressingReloadOutsideTheWindowJudgesAFailOnlyOnce()
        {
            StartReloadWithRules(Shooter.Core.ActiveReload.On);
            _controller.Tick(_rifle.ReloadSeconds * _rifle.ActiveReloadWindowStart / 2f);

            LogAssert.Expect(LogType.Log, "[Combat] active-reload fail");
            Assert.That(_controller.PressReload(), Is.True);
            Assert.That(_controller.Weapon.IsReloading, Is.True);
            Assert.That(_controller.Weapon.HasJudgedActiveReload, Is.True);

            Assert.That(_controller.PressReload(), Is.False, "the second press of the same reload is ignored");
            Assert.That(_controller.TryActiveReload(), Is.False);
            Assert.That(_controller.Weapon.IsReloading, Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void PressingReloadWhileReloadingDoesNothingWhenTheRuleIsOff()
        {
            StartReloadWithRules(Shooter.Core.ActiveReload.Off);
            var judged = 0;
            _controller.ActiveReloadJudged += (weapon, result) => judged++;
            _controller.Tick(_rifle.ReloadSeconds * (_rifle.ActiveReloadWindowStart + (_rifle.ActiveReloadWindowLength / 2f)));

            Assert.That(_controller.PressReload(), Is.False);

            Assert.That(judged, Is.Zero);
            Assert.That(_controller.Weapon.IsReloading, Is.True);
            Assert.That(_controller.Weapon.AmmoInMagazine, Is.EqualTo(_rifle.MagazineSize - 1));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ActiveReloadLogNamesTheResult()
        {
            Assert.That(WeaponController.ActiveReloadLog(Shooter.Core.ActiveReloadResult.Success), Is.EqualTo("[Combat] active-reload success"));
            Assert.That(WeaponController.ActiveReloadLog(Shooter.Core.ActiveReloadResult.Fail), Is.EqualTo("[Combat] active-reload fail"));
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
            _controller.Definitions = new[] { _rifle };

            Assert.That(_controller.TryInitializeFromHierarchy(), Is.True);
            Assert.That(_controller.Aim, Is.SameAs(_camera.transform));
            Assert.That(_controller.AttackAction, Is.SameAs(actions.FindAction(WeaponController.AttackActionPath)));
            Assert.That(_controller.ReloadAction, Is.SameAs(actions.FindAction(WeaponController.ReloadActionPath)));
            Assert.That(_controller.NextAction, Is.Not.Null);
            Assert.That(_controller.NextAction, Is.SameAs(actions.FindAction(WeaponController.NextActionPath)));
            Assert.That(_controller.PreviousAction, Is.Not.Null);
            Assert.That(_controller.PreviousAction, Is.SameAs(actions.FindAction(WeaponController.PreviousActionPath)));
            Assert.That(_controller.Weapon.Spec.Id, Is.EqualTo(TestWeapons.RifleId));
        }

        [Test]
        public void ResolvingWithDuplicateWeaponsIsReportedAndRejected()
        {
            _controller.Definitions = new[] { _rifle, _rifle };

            LogAssert.Expect(LogType.Error, new Regex(@"^\[WeaponController\] 'Camera' のロードアウトが不正です: .*" + TestWeapons.RifleId));

            Assert.That(_controller.TryInitializeFromHierarchy(), Is.False);
            Assert.That(_controller.IsInitialized, Is.False);
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
            _controller.Definitions = new[] { _rifle };

            LogAssert.Expect(LogType.Error, new Regex(@"^\[WeaponController\] 'Camera' の武器定義が不正です: weapon 'Rifle'"));

            Assert.That(_controller.TryInitializeFromHierarchy(), Is.False);
            Assert.That(_controller.IsInitialized, Is.False);
        }

        [Test]
        public void AddWeaponAppendsTheWeaponWithItsAmmoAndKeepsTheHeldOne()
        {
            var handgun = CreateHandgun();
            try
            {
                _controller.Initialize(_camera.transform, _attack, _reload, _rifle);
                var changes = 0;
                _controller.WeaponChanged += _ => changes++;

                LogAssert.Expect(LogType.Log, new Regex(@"^\[Combat\] weapon added index=1 id=" + TestWeapons.HandgunId + " "));
                Assert.That(_controller.AddWeapon(handgun, new WeaponAmmo(TestWeapons.HandgunId, 5, 20)), Is.True);

                Assert.That(_controller.Loadout.Count, Is.EqualTo(2));
                Assert.That(_controller.CurrentIndex, Is.Zero, "the held rifle stays in hand");
                Assert.That(_controller.HeldDefinitions, Is.EqualTo(new[] { _rifle, handgun }));
                Assert.That(_controller.Loadout.Items[1].AmmoInMagazine, Is.EqualTo(5));
                Assert.That(_controller.Loadout.Items[1].ReserveAmmo, Is.EqualTo(20));
                Assert.That(_controller.Weapon.AmmoInMagazine, Is.EqualTo(30));
                Assert.That(_controller.Weapon.ReserveAmmo, Is.EqualTo(90));
                Assert.That(changes, Is.EqualTo(1));

                Assert.That(_controller.AddWeapon(handgun), Is.False, "the same id is held already");
                Assert.That(_controller.Loadout.Count, Is.EqualTo(2));
                Assert.That(changes, Is.EqualTo(1));
                Assert.Throws<ArgumentException>(() => _controller.AddWeapon(handgun, new WeaponAmmo(TestWeapons.RifleId, 1, 1)));
                Assert.Throws<ArgumentNullException>(() => _controller.AddWeapon(null));
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                Object.DestroyImmediate(handgun);
            }
        }

        [Test]
        public void SwapWeaponPutsTheNewWeaponInTheDroppedSlotAndHandsBackTheDroppedOne()
        {
            var handgun = CreateHandgun();
            var launcher = ScriptableObject.CreateInstance<WeaponDefinition>();
            try
            {
                launcher.name = "Launcher";
                WeaponAssets.Configure(launcher, TestWeapons.LauncherSpec());
                InitializeWithRifleAndHandgun(handgun);
                LogAssert.Expect(LogType.Log, "[Combat] fire ammo=29");
                _controller.Fire();
                LogAssert.Expect(LogType.Log, new Regex("^" + Regex.Escape(WeaponController.SwitchedLogPrefix) + " index=1 "));
                Assert.That(_controller.SelectNext(), Is.True);

                Assert.That(_controller.SwapWeapon(TestWeapons.RifleId, handgun, null, null, out _), Is.False, "the handgun is held already");
                Assert.That(
                    _controller.SwapWeapon("00000000-0000-4000-8000-00000000ffff", launcher, null, null, out _),
                    Is.False,
                    "nothing to drop");

                LogAssert.Expect(
                    LogType.Log,
                    new Regex(@"^\[Combat\] weapon swapped index=0 id=" + TestWeapons.LauncherId + " .* dropped=" + TestWeapons.RifleId + " dropped-ammo=29/90 "));
                Assert.That(
                    _controller.SwapWeapon(TestWeapons.RifleId, launcher, new WeaponAmmo(TestWeapons.LauncherId, 1, 2), null, out var dropped),
                    Is.True);

                Assert.That(dropped.Definition, Is.SameAs(_rifle));
                Assert.That(dropped.Ammo.WeaponId, Is.EqualTo(TestWeapons.RifleId));
                Assert.That(dropped.Ammo.AmmoInMagazine, Is.EqualTo(29));
                Assert.That(dropped.Ammo.ReserveAmmo, Is.EqualTo(90));
                Assert.That(_controller.HeldDefinitions, Is.EqualTo(new[] { launcher, handgun }), "the new weapon takes the dropped slot");
                Assert.That(_controller.CurrentIndex, Is.Zero, "the picked-up weapon is held");
                Assert.That(_controller.Weapon.Spec.Id, Is.EqualTo(TestWeapons.LauncherId));
                Assert.That(_controller.Weapon.AmmoInMagazine, Is.EqualTo(1));
                Assert.That(_controller.Weapon.ReserveAmmo, Is.EqualTo(2));
                Assert.That(_controller.Loadout.Items[1].AmmoInMagazine, Is.EqualTo(12), "the other weapon keeps its ammo");
                Assert.Throws<ArgumentNullException>(() => _controller.SwapWeapon(TestWeapons.HandgunId, null, null, null, out _));
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                Object.DestroyImmediate(handgun);
                Object.DestroyImmediate(launcher);
            }
        }

        [Test]
        public void DropWeaponRemovesItAndTheLastOneLeavesTheHandsEmptyUntilAWeaponIsAdded()
        {
            var handgun = CreateHandgun();
            try
            {
                InitializeWithRifleAndHandgun(handgun);
                LogAssert.Expect(LogType.Log, "[Combat] fire ammo=29");
                _controller.Fire();

                Assert.That(_controller.DropWeapon("00000000-0000-4000-8000-00000000ffff", out _), Is.False);

                LogAssert.Expect(LogType.Log, new Regex(@"^\[Combat\] weapon dropped index=0 id=" + TestWeapons.RifleId + " .* ammo=29/90$"));
                Assert.That(_controller.DropWeapon(TestWeapons.RifleId, out var rifle), Is.True);

                Assert.That(rifle.Definition, Is.SameAs(_rifle));
                Assert.That(rifle.Ammo.AmmoInMagazine, Is.EqualTo(29));
                Assert.That(rifle.Ammo.ReserveAmmo, Is.EqualTo(90));
                Assert.That(_controller.HeldDefinitions, Is.EqualTo(new[] { handgun }));
                Assert.That(_controller.Weapon.Spec.Id, Is.EqualTo(TestWeapons.HandgunId), "dropping the held weapon holds the first left");

                LogAssert.Expect(LogType.Log, new Regex(@"^\[Combat\] weapon dropped index=0 id=" + TestWeapons.HandgunId + " .* ammo=12/48$"));
                LogAssert.Expect(LogType.Log, WeaponController.DisarmedLog);
                Assert.That(_controller.DropWeapon(TestWeapons.HandgunId, out var last), Is.True);

                Assert.That(last.Definition, Is.SameAs(handgun));
                Assert.That(_controller.IsUnarmed, Is.True);
                Assert.That(_controller.Weapon, Is.Null);
                Assert.That(_controller.CurrentIndex, Is.EqualTo(-1));
                Assert.That(_controller.HeldDefinitions, Is.Empty);
                Assert.That(_controller.SnapshotAmmo(), Is.Empty);
                Assert.That(_controller.AddReserveAmmo(TestWeapons.HandgunId, 5), Is.Zero);
                Assert.That(_controller.DropWeapon(TestWeapons.HandgunId, out _), Is.False, "nothing is held");

                LogAssert.Expect(LogType.Log, new Regex(@"^\[Combat\] weapon added index=0 id=" + TestWeapons.RifleId + " "));
                Assert.That(_controller.AddWeapon(_rifle, new WeaponAmmo(TestWeapons.RifleId, 10, 5)), Is.True);

                Assert.That(_controller.IsUnarmed, Is.False);
                Assert.That(_controller.HeldDefinitions, Is.EqualTo(new[] { _rifle }), "the dropped handgun does not come back");
                Assert.That(_controller.Weapon.AmmoInMagazine, Is.EqualTo(10));
                Assert.That(_controller.Weapon.ReserveAmmo, Is.EqualTo(5));
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                Object.DestroyImmediate(handgun);
            }
        }

        [Test]
        public void SnapshotAmmoListsEveryHeldWeaponInOrder()
        {
            var handgun = CreateHandgun();
            try
            {
                InitializeWithRifleAndHandgun(handgun);
                LogAssert.Expect(LogType.Log, "[Combat] fire ammo=29");
                _controller.Fire();

                var ammo = _controller.SnapshotAmmo();

                Assert.That(ammo.Select(a => a.WeaponId), Is.EqualTo(new[] { TestWeapons.RifleId, TestWeapons.HandgunId }));
                Assert.That(ammo.Select(a => a.AmmoInMagazine), Is.EqualTo(new[] { 29, 12 }));
                Assert.That(ammo.Select(a => a.ReserveAmmo), Is.EqualTo(new[] { 90, 48 }));
            }
            finally
            {
                Object.DestroyImmediate(handgun);
            }
        }

        [Test]
        public void AddReserveAmmoFillsOnlyTheRoomOfThatWeapon()
        {
            _controller.Initialize(_camera.transform, _attack, _reload, _rifle);
            _controller.RestoreLoadout(new[] { new WeaponAmmo(TestWeapons.RifleId, 30, 80) }, TestWeapons.RifleId);

            Assert.That(_controller.AddReserveAmmo(TestWeapons.RifleId, 25), Is.EqualTo(10), "up to the starting reserve of 90");
            Assert.That(_controller.Weapon.ReserveAmmo, Is.EqualTo(90));
            Assert.That(_controller.AddReserveAmmo(TestWeapons.RifleId, 5), Is.Zero);
            Assert.That(_controller.AddReserveAmmo(TestWeapons.HandgunId, 5), Is.Zero, "a weapon not held takes nothing");
            Assert.Throws<ArgumentOutOfRangeException>(() => _controller.AddReserveAmmo(TestWeapons.RifleId, -1));
        }

        [Test]
        public void UseAmmoPoolsMovesTheReserveIntoThePoolAndKeepsItForRebuiltWeapons()
        {
            var handgun = CreateHandgun();
            try
            {
                _controller.Initialize(_camera.transform, _attack, _reload, _rifle);
                var pool = new FakeAmmoPool("ammo.shared", 200);
                var replaced = 0;
                _controller.LoadoutReplaced += () => replaced++;

                _controller.UseAmmoPools(_ => pool);

                Assert.That(_controller.AmmoPools, Is.Not.Null);
                Assert.That(_controller.Weapon.AmmoPool, Is.SameAs(pool));
                Assert.That(pool.Count, Is.EqualTo(90), "the rifle's reserve moves into the pool");
                pool.Put(10);
                Assert.That(_controller.Weapon.ReserveAmmo, Is.EqualTo(100), "the reserve is what the pool holds");

                _controller.AddWeapon(handgun);

                Assert.That(replaced, Is.EqualTo(1));
                Assert.That(_controller.Loadout.Items[1].AmmoPool, Is.SameAs(pool), "a weapon added later uses the pool too");
                Assert.That(pool.Count, Is.EqualTo(100), "the pool already covers the handgun's 48");

                _controller.UseAmmoPools(null);

                Assert.That(_controller.AmmoPools, Is.Null);
                Assert.That(_controller.Loadout.Items.Select(w => w.AmmoPool), Is.All.Null);
            }
            finally
            {
                Object.DestroyImmediate(handgun);
            }
        }

        [Test]
        public void ApplySpreadTurnsTheDirectionInsideTheConeBySeed()
        {
            _rifle.HipSpreadDegrees = 0f;
            _rifle.MoveSpreadDegrees = 0f;
            _controller.Initialize(_camera.transform, _attack, _reload, _rifle);
            _controller.PlayerId = 2;
            var forward = Vector3.forward;

            Assert.That(_controller.ApplySpread(_controller.Weapon.Spec, forward, 0), Is.EqualTo(forward), "no cone, no spread");

            _rifle.HipSpreadDegrees = 6f;
            _controller.Initialize(_camera.transform, _attack, _reload, _rifle);
            var spec = _controller.Weapon.Spec;
            var expected = Shooter.Core.SpreadPattern.Direction(forward.ToNumerics(), 6f, 2, 4).ToUnity();

            var spread = _controller.ApplySpread(spec, forward, 4);

            Assert.That(Vector3.Distance(spread, expected), Is.LessThan(1e-5f));
            Assert.That(Vector3.Angle(forward, spread), Is.LessThanOrEqualTo(6f + 1e-3f));

            _controller.SpreadScaleSource = () => 0.5f;
            Assert.That(_controller.CurrentConeDegrees(spec), Is.EqualTo(3f).Within(1e-5f), "the scale narrows the cone");
            Assert.Throws<ArgumentNullException>(() => _controller.ApplySpread(null, forward, 0));
            Assert.Throws<ArgumentNullException>(() => _controller.CurrentConeDegrees(null));
        }

        [Test]
        public void EnablingTurnsOnTheActionsAndDisablingTurnsOffOnlyThoseItTurnedOn()
        {
            _reload.Enable();
            _controller.Initialize(_camera.transform, _attack, _reload, _rifle);
            Assert.That(_attack.enabled, Is.False, "Edit Mode does not enable on Initialize");

            InvokeLifecycle("OnEnable");

            Assert.That(_attack.enabled, Is.True);
            Assert.That(_reload.enabled, Is.True);

            InvokeLifecycle("OnDisable");

            Assert.That(_attack.enabled, Is.False);
            Assert.That(_reload.enabled, Is.True, "an action enabled by someone else stays enabled");
        }

        [Test]
        public void ReplacingAnActionTurnsOffTheOldOneOnlyWhenItWasTurnedOnHere()
        {
            var otherAttack = new InputAction("OtherAttack", InputActionType.Button);
            var otherReload = new InputAction("OtherReload", InputActionType.Button);
            try
            {
                _reload.Enable();
                _controller.Initialize(_camera.transform, _attack, _reload, _rifle);
                InvokeLifecycle("OnEnable");

                _controller.Initialize(_camera.transform, otherAttack, otherReload, _rifle);

                Assert.That(_controller.AttackAction, Is.SameAs(otherAttack));
                Assert.That(_attack.enabled, Is.False, "the replaced action was enabled here");
                Assert.That(_reload.enabled, Is.True, "the replaced action was enabled by someone else");

                InvokeLifecycle("OnEnable");
                _controller.Initialize(_camera.transform, otherAttack, otherReload, _rifle);
                Assert.That(otherAttack.enabled, Is.True, "passing the same action again keeps it");
            }
            finally
            {
                otherAttack.Dispose();
                otherReload.Dispose();
            }
        }

        private void InvokeLifecycle(string method)
        {
            // Edit Mode のテストでは OnEnable と OnDisable を Unity が呼ばないので、直に呼ぶ。
            typeof(WeaponController)
                .GetMethod(method, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(_controller, null);
        }

        private sealed class FakeAmmoPool : Shooter.Core.IAmmoPool
        {
            private readonly int _capacity;

            public FakeAmmoPool(string id, int capacity)
            {
                Id = id;
                _capacity = capacity;
            }

            public event Action Changed;

            public string Id { get; }

            public int Count { get; private set; }

            public int Room => _capacity - Count;

            public int Take(int count)
            {
                var taken = Math.Min(count, Count);
                Count -= taken;
                Changed?.Invoke();
                return taken;
            }

            public int Put(int count)
            {
                var put = Math.Min(count, Room);
                Count += put;
                Changed?.Invoke();
                return put;
            }
        }

        private WeaponDefinition CreateHandgun()
        {
            var handgun = ScriptableObject.CreateInstance<WeaponDefinition>();
            handgun.name = "Handgun";
            WeaponAssets.Configure(handgun, TestWeapons.HandgunSpec());
            return handgun;
        }

        private void InitializeWithRifleAndHandgun(WeaponDefinition handgun)
        {
            _controller.Initialize(_camera.transform, _attack, _reload, null, null, new[] { _rifle, handgun });
        }

        private StructureHitRecorder CreateStructure(string name, Vector3 position)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _created.Add(cube);
            cube.name = name;
            cube.transform.position = position;
            Physics.SyncTransforms();
            return cube.AddComponent<StructureHitRecorder>();
        }

        private sealed class RewritingShotFilter : IShotFilter
        {
            public Vector3? Origin { get; set; }
            public Vector3? Direction { get; set; }

            public int Calls { get; private set; }
            public Vector3 SeenOrigin { get; private set; }
            public Vector3 SeenDirection { get; private set; }

            public void Filter(ref Vector3 origin, ref Vector3 direction)
            {
                Calls++;
                SeenOrigin = origin;
                SeenDirection = direction;
                origin = Origin ?? origin;
                direction = Direction ?? direction;
            }
        }

        private sealed class AddingModifier : IWeaponModifier
        {
            public bool BlocksFire => false;

            public bool BlocksReload => false;

            public float SpreadMultiplier => 1f;

            private readonly float _degrees;
            private readonly float _spread;

            public AddingModifier(float degrees, float spread)
            {
                _degrees = degrees;
                _spread = spread;
            }

            public float ModifyConeDegrees(Shooter.Core.WeaponSpec spec, float coneDegrees) => coneDegrees + _degrees;

            public float ModifySpreadFraction(float spreadFraction) => spreadFraction + _spread;
        }

        private sealed class ScalingModifier : IWeaponModifier
        {
            public bool BlocksFire => false;

            public bool BlocksReload => false;

            public float SpreadMultiplier => 1f;

            private readonly float _scale;

            public ScalingModifier(float scale)
            {
                _scale = scale;
            }

            public float ModifyConeDegrees(Shooter.Core.WeaponSpec spec, float coneDegrees) => coneDegrees * _scale;

            public float ModifySpreadFraction(float spreadFraction) => spreadFraction * _scale;
        }

        private sealed class FakeNearMissTarget : INearMissTarget
        {
            private readonly Vector3 _center;

            public FakeNearMissTarget(GameObject root, Vector3 center, float radius)
            {
                Root = root.transform;
                _center = center;
                NearMissRadius = radius;
            }

            public Transform Root { get; }

            public float NearMissRadius { get; }

            public System.Collections.Generic.List<HitscanNearMiss> NearMisses { get; } = new System.Collections.Generic.List<HitscanNearMiss>();

            public bool TryGetNearMissCenter(out Vector3 center)
            {
                center = _center;
                return true;
            }

            public void OnNearMiss(HitscanNearMiss nearMiss)
            {
                NearMisses.Add(nearMiss);
            }
        }

        [Test]
        public void NextAndPreviousActionsExistInTheProjectWideActions()
        {
            Assert.That(InputSystem.actions.FindAction(WeaponController.NextActionPath), Is.Not.Null);
            Assert.That(InputSystem.actions.FindAction(WeaponController.PreviousActionPath), Is.Not.Null);
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
