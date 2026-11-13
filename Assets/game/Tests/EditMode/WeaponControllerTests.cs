using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Fps.Core;
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
        private WeaponDefinition _handgun;
        private LoadoutDefinition _loadout;
        private InputAction _attack;
        private InputAction _reload;
        private InputAction _next;
        private InputAction _previous;
        private readonly List<Object> _created = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _camera = new GameObject("Camera", typeof(Camera));
            _controller = _camera.AddComponent<WeaponController>();
            _rifle = Track(ScriptableObject.CreateInstance<WeaponDefinition>());
            _rifle.name = "Rifle";
            WeaponAssetBuilder.Configure(_rifle, WeaponAssetBuilder.RifleSpec());
            _handgun = Track(ScriptableObject.CreateInstance<WeaponDefinition>());
            _handgun.name = "Handgun";
            WeaponAssetBuilder.Configure(_handgun, WeaponAssetBuilder.HandgunSpec());
            _loadout = Track(ScriptableObject.CreateInstance<LoadoutDefinition>());
            _loadout.name = "Loadout";
            WeaponAssetBuilder.Configure(
                _loadout, WeaponAssetBuilder.DefaultLoadoutSlots(_rifle, _handgun), LoadoutDefinition.DefaultSwitchSeconds);
            _attack = new InputAction("Attack", InputActionType.Button);
            _reload = new InputAction("Reload", InputActionType.Button);
            _next = new InputAction("Next", InputActionType.Button);
            _previous = new InputAction("Previous", InputActionType.Button);
        }

        [TearDown]
        public void TearDown()
        {
            _attack.Dispose();
            _reload.Dispose();
            _next.Dispose();
            _previous.Dispose();
            Object.DestroyImmediate(_camera);
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }

        private void InitializeWith(LoadoutDefinition loadout)
        {
            _controller.Initialize(_camera.transform, _attack, _reload, _next, _previous, loadout);
        }

        // 持ち替えてから持ち替えの時間を済ませる。
        private void SwitchTo(int index)
        {
            var spec = _controller.Loadout.Items[index].Spec;
            LogAssert.Expect(LogType.Log, WeaponController.SwitchedLog(index, spec.DisplayName));
            Assert.That(_controller.Select(index), Is.True);
            _controller.Tick(_loadout.SwitchSeconds);
            Assert.That(_controller.IsSwitching, Is.False);
        }

        private Damageable CreateTarget(float distance)
        {
            var target = Track(new GameObject("Target", typeof(BoxCollider)));
            target.transform.position = new Vector3(0f, 0f, distance);
            var damageable = target.AddComponent<Damageable>();
            damageable.Initialize("Target", 1000);
            target.AddComponent<Hitbox>().Initialize(HitZone.Body, damageable);
            Physics.SyncTransforms();
            return damageable;
        }

        [Test]
        public void InitializeRejectsMissingReferences()
        {
            var aim = _camera.transform;
            Assert.Throws<ArgumentNullException>(() => _controller.Initialize(null, _attack, _reload, _next, _previous, _loadout));
            Assert.Throws<ArgumentNullException>(() => _controller.Initialize(aim, null, _reload, _next, _previous, _loadout));
            Assert.Throws<ArgumentNullException>(() => _controller.Initialize(aim, _attack, null, _next, _previous, _loadout));
            Assert.Throws<ArgumentNullException>(() => _controller.Initialize(aim, _attack, _reload, null, _previous, _loadout));
            Assert.Throws<ArgumentNullException>(() => _controller.Initialize(aim, _attack, _reload, _next, null, _loadout));
            Assert.Throws<ArgumentNullException>(() => _controller.Initialize(aim, _attack, _reload, _next, _previous, null));
            Assert.That(_controller.IsInitialized, Is.False);
            Assert.That(_controller.Weapon, Is.Null);
            Assert.That(_controller.Loadout, Is.Null);
            Assert.That(_controller.CurrentIndex, Is.EqualTo(-1));
        }

        [Test]
        public void InitializeWithoutALoadoutThrows()
        {
            Assert.That(_controller.LoadoutDefinition, Is.Null);

            Assert.Throws<InvalidOperationException>(() => _controller.Initialize(_camera.transform, _attack, _reload, _next, _previous));
            Assert.That(_controller.IsInitialized, Is.False);
        }

        [Test]
        public void InitializeUsesTheAssignedLoadout()
        {
            _controller.LoadoutDefinition = _loadout;

            _controller.Initialize(_camera.transform, _attack, _reload, _next, _previous);

            Assert.That(_controller.IsInitialized, Is.True);
            Assert.That(_controller.Weapon.Spec.Id, Is.EqualTo(WeaponAssetBuilder.RifleId));
            Assert.That(_controller.NextAction, Is.SameAs(_next));
            Assert.That(_controller.PreviousAction, Is.SameAs(_previous));
        }

        [Test]
        public void InitializeBuildsEveryWeaponOfTheLoadoutAndHoldsTheFirst()
        {
            Weapon changed = null;
            _controller.WeaponChanged += weapon => changed = weapon;

            InitializeWith(_loadout);

            var loadout = _controller.Loadout;
            Assert.That(loadout.Count, Is.EqualTo(2));
            Assert.That(loadout.Items[0].Spec.Id, Is.EqualTo(WeaponAssetBuilder.RifleId));
            Assert.That(loadout.Items[1].Spec.Id, Is.EqualTo(WeaponAssetBuilder.HandgunId));
            Assert.That(loadout.SwitchSeconds, Is.EqualTo(_loadout.SwitchSeconds));
            Assert.That(_controller.CurrentIndex, Is.Zero);
            Assert.That(_controller.IsSwitching, Is.False);
            Assert.That(_controller.Weapon, Is.SameAs(loadout.Items[0]));
            Assert.That(changed, Is.SameAs(_controller.Weapon));
            Assert.That(_controller.LoadoutDefinition, Is.SameAs(_loadout));
            Assert.That(_controller.Aim, Is.SameAs(_camera.transform));

            var expected = WeaponAssetBuilder.RifleSpec();
            var spec = _controller.Weapon.Spec;
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
        }

        [Test]
        public void InvalidLoadoutIsRejectedAndKeepsThePreviousWeapons()
        {
            InitializeWith(_loadout);
            var previous = _controller.Loadout;

            var broken = Track(ScriptableObject.CreateInstance<WeaponDefinition>());
            broken.name = "Broken";
            broken.AssignIdIfEmpty();
            broken.FalloffStart = 50f;
            broken.FalloffEnd = 10f;
            var brokenLoadout = Track(TestLoadouts.Of("BrokenLoadout", broken));

            Assert.Throws<WeaponDefinitionException>(() => InitializeWith(brokenLoadout));
            Assert.That(_controller.Loadout, Is.SameAs(previous));
            Assert.That(_controller.LoadoutDefinition, Is.SameAs(_loadout));
        }

        [Test]
        public void ALoadoutThatBreaksItsRuleIsRejected()
        {
            // 0 番のスロットは Handgun を許さない
            var swapped = Track(ScriptableObject.CreateInstance<LoadoutDefinition>());
            swapped.name = "Swapped";
            swapped.SetSlots(WeaponAssetBuilder.DefaultLoadoutSlots(_handgun, _rifle));

            Assert.Throws<WeaponDefinitionException>(() => InitializeWith(swapped));
            Assert.That(_controller.IsInitialized, Is.False);
        }

        [Test]
        public void ReinitializingWithAnotherLoadoutSwapsTheWeapons()
        {
            InitializeWith(_loadout);
            var handgunOnly = Track(TestLoadouts.Of("HandgunOnly", _handgun));

            InitializeWith(handgunOnly);

            Assert.That(_controller.LoadoutDefinition, Is.SameAs(handgunOnly));
            Assert.That(_controller.Loadout.Count, Is.EqualTo(1));
            Assert.That(_controller.Weapon.Spec.Id, Is.EqualTo(WeaponAssetBuilder.HandgunId));
            Assert.That(_controller.HitscanRange, Is.EqualTo(_handgun.Range));
        }

        [Test]
        public void ActionsBeforeInitializeThrow()
        {
            Assert.Throws<InvalidOperationException>(() => _controller.Fire());
            Assert.Throws<InvalidOperationException>(() => _controller.StartReload());
            Assert.Throws<InvalidOperationException>(() => _controller.Tick(0.1f));
            Assert.Throws<InvalidOperationException>(() => _controller.Select(1));
            Assert.Throws<InvalidOperationException>(() => _controller.SelectNext());
            Assert.Throws<InvalidOperationException>(() => _controller.SelectPrevious());
            Assert.Throws<InvalidOperationException>(() => _controller.RestoreAmmo(0, 1, 1));
        }

        [Test]
        public void SwitchingHoldsTheOtherWeaponLogsAndNotifies()
        {
            InitializeWith(_loadout);
            var events = new List<string>();
            _controller.WeaponChanged += weapon => events.Add("changed " + weapon.Spec.Id);
            _controller.WeaponSwitched += index => events.Add("switched " + index);

            LogAssert.Expect(LogType.Log, "[Combat] switched index=1 weapon=Handgun");
            Assert.That(_controller.SelectNext(), Is.True);

            Assert.That(_controller.CurrentIndex, Is.EqualTo(1));
            Assert.That(_controller.Weapon, Is.SameAs(_controller.Loadout.Items[1]));
            Assert.That(_controller.IsSwitching, Is.True);
            Assert.That(_controller.HitscanRange, Is.EqualTo(_handgun.Range), "the range comes from the current weapon");
            Assert.That(_controller.Falloff, Is.SameAs(_controller.Weapon.Spec.Falloff));
            Assert.That(events, Is.EqualTo(new[] { "changed " + WeaponAssetBuilder.HandgunId, "switched 1" }));

            LogAssert.Expect(LogType.Log, "[Combat] switched index=0 weapon=Rifle");
            Assert.That(_controller.SelectPrevious(), Is.True);
            Assert.That(_controller.CurrentIndex, Is.Zero);
            Assert.That(_controller.HitscanRange, Is.EqualTo(_rifle.Range));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void SelectingTheCurrentOrAMissingIndexDoesNothing()
        {
            InitializeWith(_loadout);
            var switched = 0;
            _controller.WeaponSwitched += _ => switched++;

            Assert.That(_controller.Select(0), Is.False);
            Assert.That(_controller.Select(2), Is.False);
            Assert.That(_controller.Select(-1), Is.False);

            Assert.That(switched, Is.Zero);
            Assert.That(_controller.IsSwitching, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ASingleWeaponLoadoutDoesNotSwitch()
        {
            InitializeWith(Track(TestLoadouts.Of("RifleOnly", _rifle)));

            Assert.That(_controller.SelectNext(), Is.False);
            Assert.That(_controller.SelectPrevious(), Is.False);
            Assert.That(_controller.IsSwitching, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void FiringIsRefusedWhileSwitching()
        {
            InitializeWith(_loadout);
            LogAssert.Expect(LogType.Log, "[Combat] switched index=1 weapon=Handgun");
            _controller.SelectNext();

            Assert.That(_controller.Fire(), Is.False);
            Assert.That(_controller.Weapon.AmmoInMagazine, Is.EqualTo(_handgun.MagazineSize), "no round is spent");

            _controller.Tick(_loadout.SwitchSeconds / 2f);
            Assert.That(_controller.IsSwitching, Is.True);
            Assert.That(_controller.Fire(), Is.False);

            _controller.Tick(_loadout.SwitchSeconds / 2f);
            Assert.That(_controller.IsSwitching, Is.False);
            LogAssert.Expect(LogType.Log, $"[Combat] fire ammo={_handgun.MagazineSize - 1}");
            Assert.That(_controller.Fire(), Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void SwitchingCancelsTheReloadAndReloadIsNotStartedWhileSwitching()
        {
            InitializeWith(_loadout);
            var rifle = _controller.Weapon;
            LogAssert.Expect(LogType.Log, $"[Combat] fire ammo={_rifle.MagazineSize - 1}");
            _controller.Fire();
            Assert.That(_controller.StartReload(), Is.True);

            SwitchTo(1);
            Assert.That(rifle.IsReloading, Is.False, "holstering cancels the reload");
            Assert.That(rifle.AmmoInMagazine, Is.EqualTo(_rifle.MagazineSize - 1), "a cancelled reload refills nothing");

            LogAssert.Expect(LogType.Log, "[Combat] switched index=0 weapon=Rifle");
            _controller.SelectPrevious();
            Assert.That(_controller.StartReload(), Is.False, "no reload while switching");
            Assert.That(rifle.IsReloading, Is.False);

            _controller.Tick(_loadout.SwitchSeconds);
            Assert.That(_controller.StartReload(), Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void EachWeaponKeepsItsAmmoAcrossSwitches()
        {
            InitializeWith(_loadout);
            LogAssert.Expect(LogType.Log, $"[Combat] fire ammo={_rifle.MagazineSize - 1}");
            _controller.Fire();
            _controller.Tick(_rifle.FireInterval);
            LogAssert.Expect(LogType.Log, $"[Combat] fire ammo={_rifle.MagazineSize - 2}");
            _controller.Fire();

            SwitchTo(1);
            Assert.That(_controller.Weapon.AmmoInMagazine, Is.EqualTo(_handgun.MagazineSize));
            LogAssert.Expect(LogType.Log, $"[Combat] fire ammo={_handgun.MagazineSize - 1}");
            _controller.Fire();

            SwitchTo(0);
            Assert.That(_controller.Weapon.AmmoInMagazine, Is.EqualTo(_rifle.MagazineSize - 2));
            Assert.That(_controller.Weapon.ReserveAmmo, Is.EqualTo(_rifle.ReserveAmmo));

            SwitchTo(1);
            Assert.That(_controller.Weapon.AmmoInMagazine, Is.EqualTo(_handgun.MagazineSize - 1));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void TheDamageComesFromTheCurrentWeapon()
        {
            InitializeWith(_loadout);
            var target = CreateTarget(5f);

            LogAssert.Expect(LogType.Log, $"[Combat] fire ammo={_rifle.MagazineSize - 1}");
            LogAssert.Expect(LogType.Log, $"[Combat] hit zone=Body dmg={_rifle.Damage}");
            Assert.That(_controller.Fire(), Is.True);
            Assert.That(target.Health.Current, Is.EqualTo(1000 - _rifle.Damage));

            SwitchTo(1);
            LogAssert.Expect(LogType.Log, $"[Combat] fire ammo={_handgun.MagazineSize - 1}");
            LogAssert.Expect(LogType.Log, $"[Combat] hit zone=Body dmg={_handgun.Damage}");
            Assert.That(_controller.Fire(), Is.True);
            Assert.That(target.Health.Current, Is.EqualTo(1000 - _rifle.Damage - _handgun.Damage));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void TheRangeComesFromTheCurrentWeapon()
        {
            InitializeWith(_loadout);

            // rifle (100m) には届き、handgun (60m) には届かない距離
            var distance = (_rifle.Range + _handgun.Range) / 2f;
            var target = CreateTarget(distance);

            LogAssert.Expect(LogType.Log, $"[Combat] fire ammo={_rifle.MagazineSize - 1}");
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Combat\] hit zone=Body dmg=\d+$"));
            _controller.Fire();
            var afterRifle = target.Health.Current;
            Assert.That(afterRifle, Is.LessThan(1000));

            SwitchTo(1);
            LogAssert.Expect(LogType.Log, $"[Combat] fire ammo={_handgun.MagazineSize - 1}");
            _controller.Fire();
            Assert.That(target.Health.Current, Is.EqualTo(afterRifle), "out of the handgun's range");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void RestoreAmmoReplacesTheCurrentWeaponAndCancelsReload()
        {
            InitializeWith(_loadout);
            LogAssert.Expect(LogType.Log, $"[Combat] fire ammo={_rifle.MagazineSize - 1}");
            _controller.Fire();
            Assert.That(_controller.StartReload(), Is.True);
            var previous = _controller.Weapon;
            Weapon changed = null;
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
        public void RestoreAmmoOfAnotherSlotKeepsTheCurrentWeapon()
        {
            InitializeWith(_loadout);
            SwitchTo(1);
            var handgun = _controller.Weapon;
            var changed = 0;
            var switched = 0;
            _controller.WeaponChanged += _ => changed++;
            _controller.WeaponSwitched += _ => switched++;

            _controller.RestoreAmmo(0, 3, 4);

            Assert.That(_controller.CurrentIndex, Is.EqualTo(1));
            Assert.That(_controller.Weapon, Is.SameAs(handgun));
            Assert.That(_controller.Loadout.Items[0].AmmoInMagazine, Is.EqualTo(3));
            Assert.That(_controller.Loadout.Items[0].ReserveAmmo, Is.EqualTo(4));
            Assert.That(changed, Is.Zero, "the current weapon did not change");
            Assert.That(switched, Is.Zero);

            // 作り直した後も持ち替えは今まで通り届く
            LogAssert.Expect(LogType.Log, "[Combat] switched index=0 weapon=Rifle");
            Assert.That(_controller.SelectPrevious(), Is.True);
            Assert.That(switched, Is.EqualTo(1));
            Assert.That(_controller.Weapon.AmmoInMagazine, Is.EqualTo(3));
        }

        [Test]
        public void RestoreAmmoRejectsBadValuesAndNeedsInitialize()
        {
            Assert.Throws<InvalidOperationException>(() => _controller.RestoreAmmo(1, 1));

            InitializeWith(_loadout);
            Assert.Throws<ArgumentOutOfRangeException>(() => _controller.RestoreAmmo(-1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => _controller.RestoreAmmo(0, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => _controller.RestoreAmmo(2, 1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => _controller.RestoreAmmo(-1, 1, 1));
        }

        [Test]
        public void FireLogsTheRemainingAmmoAndRespectsTheFireInterval()
        {
            InitializeWith(_loadout);
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
        public void ReloadLogsWhenTheMagazineIsRefilled()
        {
            InitializeWith(_loadout);
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
            InitializeWith(_loadout);

            Assert.That(_controller.StartReload(), Is.False);
        }

        [Test]
        public void ResolvesTheCameraAndProjectWideActions()
        {
            var actions = InputSystem.actions;
            Assert.That(actions, Is.Not.Null, "Project-wide Input Actions must be assigned");
            _controller.LoadoutDefinition = _loadout;

            Assert.That(_controller.TryInitializeFromHierarchy(), Is.True);
            Assert.That(_controller.Aim, Is.SameAs(_camera.transform));
            Assert.That(_controller.AttackAction, Is.SameAs(actions.FindAction(WeaponController.AttackActionPath)));
            Assert.That(_controller.ReloadAction, Is.SameAs(actions.FindAction(WeaponController.ReloadActionPath)));
            Assert.That(_controller.NextAction, Is.SameAs(actions.FindAction(WeaponController.NextActionPath)));
            Assert.That(_controller.PreviousAction, Is.SameAs(actions.FindAction(WeaponController.PreviousActionPath)));
            Assert.That(_controller.Weapon.Spec.Id, Is.EqualTo(WeaponAssetBuilder.RifleId));
        }

        [Test]
        public void ResolvingWithoutALoadoutIsReportedAndRejected()
        {
            LogAssert.Expect(LogType.Error, new Regex(@"^\[WeaponController\] 'Camera' に LoadoutDefinition が設定されていません"));

            Assert.That(_controller.TryInitializeFromHierarchy(), Is.False);
            Assert.That(_controller.IsInitialized, Is.False);
        }

        [Test]
        public void ResolvingWithAnInvalidLoadoutIsReportedAndRejected()
        {
            _rifle.Range = 0f;
            _controller.LoadoutDefinition = _loadout;

            LogAssert.Expect(LogType.Error, new Regex(@"^\[WeaponController\] 'Camera' のロードアウトが不正です: loadout 'Loadout' slot #0: weapon 'Rifle'"));

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

        [Test]
        public void NextAndPreviousAreBoundToTheMouseWheelAndKeepTheirKeys()
        {
            var next = InputSystem.actions.FindAction(WeaponController.NextActionPath);
            var previous = InputSystem.actions.FindAction(WeaponController.PreviousActionPath);

            Assert.That(next, Is.Not.Null);
            Assert.That(previous, Is.Not.Null);
            Assert.That(next.bindings, Has.Some.Matches<InputBinding>(b => b.path == "<Mouse>/scroll/up"));
            Assert.That(next.bindings, Has.Some.Matches<InputBinding>(b => b.path == "<Keyboard>/2"));
            Assert.That(previous.bindings, Has.Some.Matches<InputBinding>(b => b.path == "<Mouse>/scroll/down"));
            Assert.That(previous.bindings, Has.Some.Matches<InputBinding>(b => b.path == "<Keyboard>/1"));
        }
    }

    /// <summary>テスト用の <see cref="LoadoutDefinition"/> を作る。WeaponController を使う他のテストからも呼ぶ。</summary>
    internal static class TestLoadouts
    {
        /// <summary>
        /// weapons を並べたロードアウトを作る。各スロットはその武器の種別だけを許す。持ち替え秒数は既定値。
        /// 呼んだ側が Object.DestroyImmediate で消す (武器のアセットは消さない)。
        /// </summary>
        public static LoadoutDefinition Of(string name, params WeaponDefinition[] weapons)
        {
            var loadout = ScriptableObject.CreateInstance<LoadoutDefinition>();
            loadout.name = name;
            var slots = new List<LoadoutSlotDefinition>(weapons.Length);
            foreach (var weapon in weapons)
            {
                slots.Add(new LoadoutSlotDefinition(new[] { weapon.Category }, weapon));
            }

            loadout.SetSlots(slots);
            return loadout;
        }
    }
}
