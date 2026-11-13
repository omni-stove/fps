using System;
using System.Text.RegularExpressions;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using WeaponAmmo = Fps.Core.WeaponAmmo;

namespace Fps.Tests.EditMode
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
                Assert.That(_controller.Weapon.Spec.Id, Is.EqualTo(WeaponAssetBuilder.RifleId));
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

            Assert.That(_controller.Definitions, Has.Count.EqualTo(1));

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
                var changes = new System.Collections.Generic.List<Fps.Core.Weapon>();
                _controller.WeaponChanged += changes.Add;
                var rifle = _controller.Weapon;

                Assert.That(_controller.SelectNext(), Is.True);
                Assert.That(_controller.CurrentIndex, Is.EqualTo(1));
                Assert.That(_controller.Weapon.Spec.Id, Is.EqualTo(WeaponAssetBuilder.HandgunId));
                Assert.That(_controller.Definition, Is.SameAs(handgun));
                Assert.That(_controller.HitscanRange, Is.EqualTo(handgun.Range), "the range follows the held weapon");

                Assert.That(_controller.SelectNext(), Is.True, "next after the last wraps to the first");
                Assert.That(_controller.Weapon, Is.SameAs(rifle), "switching back holds the same weapon");

                Assert.That(_controller.SelectPrevious(), Is.True, "previous before the first wraps to the last");
                Assert.That(_controller.CurrentIndex, Is.EqualTo(1));

                Assert.That(_controller.Select(WeaponAssetBuilder.RifleId), Is.True);
                Assert.That(_controller.Select(WeaponAssetBuilder.RifleId), Is.False, "already held");
                Assert.That(_controller.Select("00000000-0000-4000-8000-00000000ffff"), Is.False, "not in the loadout");
                Assert.That(_controller.Select(5), Is.False);

                Assert.That(changes, Has.Count.EqualTo(4));
                Assert.That(changes[3], Is.SameAs(rifle));
            }
            finally
            {
                Object.DestroyImmediate(handgun);
            }
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

                // 持ち替えた handgun は Semi: 押したときだけ撃ち、押し続けても 2 発目は出ない。
                LogAssert.Expect(LogType.Log, $"[Combat] fire ammo={handgun.MagazineSize - 1}");
                Assert.That(_controller.PullTrigger(true, true), Is.True);
                _controller.Tick(handgun.FireInterval);
                Assert.That(_controller.PullTrigger(true, false), Is.False);

                _controller.Tick(_rifle.ReloadSeconds);
                Assert.That(rifle.AmmoInMagazine, Is.EqualTo(_rifle.MagazineSize - 1), "a cancelled reload does not refill");

                // rifle に戻ると Auto: 押し続けている間撃つ。
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
                Fps.Core.Weapon changed = null;
                _controller.WeaponChanged += weapon => changed = weapon;

                _controller.RestoreLoadout(
                    new[] { new WeaponAmmo(WeaponAssetBuilder.RifleId, 7, 12), new WeaponAmmo(WeaponAssetBuilder.HandgunId, 3, 9) },
                    WeaponAssetBuilder.HandgunId);

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
                Assert.That(_controller.SnapshotAmmo(), Has.Count.EqualTo(2));

                // 戻した後も持ち替えられ、WeaponChanged が来る。
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
                    new[] { new WeaponAmmo(WeaponAssetBuilder.RifleId, _rifle.MagazineSize + 5, 0), new WeaponAmmo(unknownId, 1, 1) },
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

        private static Fps.Core.GameRules ReserveRules(bool infinite, float multiplier)
        {
            return new Fps.Core.GameRules(
                Fps.Core.FriendlyFire.Off,
                Fps.Core.RespawnMode.Individual,
                Fps.Core.GameRules.DefaultWaveIntervalSeconds,
                0f,
                0f,
                Fps.Core.DeathBody.Vanish,
                Fps.Core.DeathCamera.None,
                infinite,
                multiplier,
                Fps.Core.Destruction.Off);
        }

        [Test]
        public void InitializeBuildsTheWeaponsWithTheConfiguredRules()
        {
            Assert.That(_controller.Rules, Is.SameAs(Fps.Core.GameRules.Default), "the default before anything is set");
            var rules = ReserveRules(true, 2f);
            _controller.Rules = rules;

            _controller.Initialize(_camera.transform, _attack, _reload, _rifle);

            Assert.That(_controller.LoadedRules, Is.SameAs(rules));
            Assert.That(_controller.Weapon.ReserveAmmo, Is.EqualTo(_rifle.ReserveAmmo * 2));
            Assert.That(_controller.Weapon.HasInfiniteReserve, Is.True);

            _controller.Rules = null;
            Assert.That(_controller.Rules, Is.SameAs(Fps.Core.GameRules.Default), "null resets to the default");
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
                Fps.Core.Weapon changed = null;
                _controller.WeaponChanged += weapon => changed = weapon;
                var rules = ReserveRules(false, 0.6f);

                _controller.ApplyRules(rules);

                Assert.That(_controller.Rules, Is.SameAs(rules));
                Assert.That(_controller.LoadedRules, Is.SameAs(rules));
                Assert.That(_controller.CurrentIndex, Is.Zero, "starts again with the first weapon");
                Assert.That(changed, Is.SameAs(_controller.Weapon));
                var rifle = _controller.Loadout.Items[0];
                Assert.That(rifle.AmmoInMagazine, Is.EqualTo(_rifle.MagazineSize), "rebuilt full");
                Assert.That(rifle.ReserveAmmo, Is.EqualTo(Fps.Core.Weapon.ScaleReserveAmmo(_rifle.ReserveAmmo, 0.6f)));
                Assert.That(
                    _controller.Loadout.Items[1].ReserveAmmo,
                    Is.EqualTo(Fps.Core.Weapon.ScaleReserveAmmo(handgun.ReserveAmmo, 0.6f)));
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

                _controller.RestoreLoadout(new[] { new WeaponAmmo(WeaponAssetBuilder.RifleId, 7, 12) }, WeaponAssetBuilder.RifleId);

                Assert.That(_controller.Weapon.ReserveAmmo, Is.EqualTo(12), "the multiplier is not applied again");
                Assert.That(_controller.Weapon.HasInfiniteReserve, Is.True);
                Assert.That(
                    _controller.Loadout.Items[1].ReserveAmmo,
                    Is.EqualTo(Fps.Core.Weapon.ScaleReserveAmmo(handgun.ReserveAmmo, 1.5f)),
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
            Assert.Throws<InvalidOperationException>(() => _controller.ApplyRules(Fps.Core.GameRules.Default));
            Assert.That(_controller.LoadedRules, Is.Null);

            _controller.Initialize(_camera.transform, _attack, _reload, _rifle);
            Assert.Throws<ArgumentNullException>(() => _controller.ApplyRules(null));
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
        public void SpreadGrowsWithEachShotAndRecoversAfterTheDelay()
        {
            _controller.Initialize(_camera.transform, _attack, _reload, _rifle);
            Assert.That(_controller.SpreadFraction, Is.EqualTo(0f));

            LogAssert.Expect(LogType.Log, $"[Combat] fire ammo={_rifle.MagazineSize - 1}");
            Assert.That(_controller.Fire(), Is.True);
            Assert.That(_controller.SpreadFraction, Is.EqualTo(WeaponController.SpreadPerShot));

            // 戻り始めるまでの間は広がったまま。
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

            // 連射間隔が経つまでは撃てず、ログも出ない。
            Assert.That(_controller.Fire(), Is.False);

            _controller.Tick(_rifle.FireInterval);
            LogAssert.Expect(LogType.Log, $"[Combat] fire ammo={full - 2}");
            Assert.That(_controller.Fire(), Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ShotFilterIsNullByDefaultAndTheShotGoesStraightAhead()
        {
            _controller.Initialize(_camera.transform, _attack, _reload, _rifle);
            var ahead = CreateStructure("Ahead", new Vector3(0f, 0f, 10f));
            var aside = CreateStructure("Aside", new Vector3(20f, 0f, 10f));

            Assert.That(_controller.ShotFilter, Is.Null);
            Assert.That(_controller.Fire(), Is.True);

            Assert.That(ahead.Calls, Has.Count.EqualTo(1));
            Assert.That(aside.Calls, Is.Empty);
        }

        [Test]
        public void ShotFilterOriginIsWhereTheRaycastStarts()
        {
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
            Assert.That(aside.Calls, Has.Count.EqualTo(1));
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
            Assert.That(right.Calls, Has.Count.EqualTo(1));
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
            var expected = Fps.Core.SpreadPattern.Direction(forward.ToNumerics(), 10f, 3, 0).ToUnity();

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

        private static Fps.Core.GameRules ActiveReloadRules(Fps.Core.ActiveReload activeReload)
        {
            var d = Fps.Core.GameRules.Default;
            var a = Fps.Core.ActionRules.Default;
            return new Fps.Core.GameRules(
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
                new Fps.Core.ActionRules(a.Downed, a.DownedBleedoutSeconds, a.DownedHealthFraction, a.ReviveSeconds, a.Cover, a.BlindFire, activeReload));
        }

        private void StartReloadWithRules(Fps.Core.ActiveReload activeReload)
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
            StartReloadWithRules(Fps.Core.ActiveReload.On);
            var judged = new System.Collections.Generic.List<Fps.Core.ActiveReloadResult>();
            _controller.ActiveReloadJudged += (weapon, result) =>
            {
                Assert.That(weapon, Is.SameAs(_controller.Weapon));
                judged.Add(result);
            };
            _controller.Tick(_rifle.ReloadSeconds * (_rifle.ActiveReloadWindowStart + (_rifle.ActiveReloadWindowLength / 2f)));

            LogAssert.Expect(LogType.Log, WeaponController.ReloadedLog);
            LogAssert.Expect(LogType.Log, "[Combat] active-reload success");
            Assert.That(_controller.PressReload(), Is.True);

            Assert.That(judged, Is.EqualTo(new[] { Fps.Core.ActiveReloadResult.Success }));
            Assert.That(_controller.Weapon.IsReloading, Is.False);
            Assert.That(_controller.Weapon.AmmoInMagazine, Is.EqualTo(_rifle.MagazineSize));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void PressingReloadOutsideTheWindowJudgesAFailOnlyOnce()
        {
            StartReloadWithRules(Fps.Core.ActiveReload.On);
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
            StartReloadWithRules(Fps.Core.ActiveReload.Off);
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
            Assert.That(WeaponController.ActiveReloadLog(Fps.Core.ActiveReloadResult.Success), Is.EqualTo("[Combat] active-reload success"));
            Assert.That(WeaponController.ActiveReloadLog(Fps.Core.ActiveReloadResult.Fail), Is.EqualTo("[Combat] active-reload fail"));
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
            Assert.That(_controller.Weapon.Spec.Id, Is.EqualTo(WeaponAssetBuilder.RifleId));
        }

        [Test]
        public void ResolvingWithDuplicateWeaponsIsReportedAndRejected()
        {
            _controller.Definitions = new[] { _rifle, _rifle };

            LogAssert.Expect(LogType.Error, new Regex(@"^\[WeaponController\] 'Camera' のロードアウトが不正です: .*" + WeaponAssetBuilder.RifleId));

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

        private WeaponDefinition CreateHandgun()
        {
            var handgun = ScriptableObject.CreateInstance<WeaponDefinition>();
            handgun.name = "Handgun";
            WeaponAssetBuilder.Configure(handgun, WeaponAssetBuilder.HandgunSpec());
            return handgun;
        }

        private void InitializeWithRifleAndHandgun(WeaponDefinition handgun)
        {
            _controller.Initialize(_camera.transform, _attack, _reload, null, null, new[] { _rifle, handgun });
        }

        // Hitbox の無い 1m の立方体。撃たれると StructureDamage から被害を記録する。
        private StructureHitRecorder CreateStructure(string name, Vector3 position)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _created.Add(cube);
            cube.name = name;
            cube.transform.position = position;
            Physics.SyncTransforms();
            return cube.AddComponent<StructureHitRecorder>();
        }

        // 受け取った始点と向きを覚え、値を決めた方だけ書き換える。
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
