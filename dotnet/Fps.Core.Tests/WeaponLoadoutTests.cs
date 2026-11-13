using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class WeaponLoadoutTests
    {
        private const string _rifleId = "6c1f3b2a-8d4e-4f7a-9b0c-2e5d7a1f4c83";
        private const string _handgunId = "b4e2a9d7-3c16-4a58-8f2e-7d0b9c6a1e45";
        private const string _otherId = "00000000-0000-4000-8000-000000000009";

        private static readonly WeaponSpec _rifle = new WeaponSpec(
            _rifleId, "Rifle", WeaponCategory.AssaultRifle, FireMode.Auto, 30, 90, 0.1f, 2f, 25, 100f, DamageFalloff.None);

        private static readonly WeaponSpec _handgun = new WeaponSpec(
            _handgunId, "Handgun", WeaponCategory.Handgun, FireMode.Semi, 12, 48, 0.2f, 1.5f, 34, 60f, DamageFalloff.None);

        private static readonly int[] _expectedSwitches = { 1, 0, 1 };
        private static readonly string[] _unknownOther = { _otherId };
        private static readonly string[] _expectedSnapshot = { _rifleId + ":29/90", _handgunId + ":10/48" };

        private static Loadout<Weapon> Create()
        {
            return WeaponLoadout.Create(new[] { _rifle, _handgun });
        }

        [Test]
        public void CreatesFullWeaponsInOrderHoldingTheFirst()
        {
            var loadout = Create();

            Assert.That(loadout.Items.Select(w => w.Spec), Is.EqualTo(new[] { _rifle, _handgun }));
            Assert.That(loadout.CurrentIndex, Is.Zero);
            Assert.That(loadout.Current.Spec, Is.SameAs(_rifle));
            Assert.That(loadout.Current.AmmoInMagazine, Is.EqualTo(30));
            Assert.That(loadout.Items[1].AmmoInMagazine, Is.EqualTo(12));
            Assert.That(loadout.SwitchSeconds, Is.Zero, "switching takes no time offline");
            Assert.That(loadout.IsSwitching, Is.False);
        }

        [Test]
        public void NextAndPreviousWrapAroundAndNotify()
        {
            var loadout = Create();
            var switched = new List<int>();
            loadout.Switched += switched.Add;

            Assert.That(loadout.SelectNext(), Is.True);
            Assert.That(loadout.Current.Spec, Is.SameAs(_handgun));
            Assert.That(loadout.IsSwitching, Is.False, "the switched weapon can fire at once");

            Assert.That(loadout.SelectNext(), Is.True, "next after the last wraps to the first");
            Assert.That(loadout.CurrentIndex, Is.Zero);

            Assert.That(loadout.SelectPrevious(), Is.True, "previous before the first wraps to the last");
            Assert.That(loadout.CurrentIndex, Is.EqualTo(1));

            Assert.That(switched, Is.EqualTo(_expectedSwitches));
        }

        [Test]
        public void SelectsByWeaponId()
        {
            var loadout = Create();

            Assert.That(loadout.IndexOf(_handgunId), Is.EqualTo(1));
            Assert.That(loadout.IndexOf(_otherId), Is.EqualTo(-1));
            Assert.That(loadout.IndexOf(null), Is.EqualTo(-1));
            Assert.That(loadout.IndexOf(""), Is.EqualTo(-1));

            Assert.That(loadout.TrySelect(_handgunId), Is.True);
            Assert.That(loadout.Current.Spec, Is.SameAs(_handgun));
            Assert.That(loadout.TrySelect(_handgunId), Is.False, "already held");
            Assert.That(loadout.TrySelect(_otherId), Is.False, "not in the loadout");
            Assert.That(loadout.TrySelect((string)null), Is.False);
            Assert.That(loadout.CurrentIndex, Is.EqualTo(1));
        }

        [Test]
        public void SwitchingCancelsTheReloadOfTheHolsteredWeaponAndKeepsItsAmmo()
        {
            var loadout = Create();
            var rifle = loadout.Current;
            rifle.TryFire();
            Assert.That(rifle.TryStartReload(), Is.True);

            loadout.SelectNext();
            rifle.Tick(10f);

            Assert.That(rifle.IsReloading, Is.False);
            Assert.That(rifle.AmmoInMagazine, Is.EqualTo(29), "a cancelled reload does not refill");
            Assert.That(rifle.ReserveAmmo, Is.EqualTo(90));

            loadout.SelectPrevious();
            Assert.That(loadout.Current, Is.SameAs(rifle), "switching back holds the same weapon");
        }

        [Test]
        public void RestoresSavedAmmoPerWeaponAndTheEquippedWeapon()
        {
            var saved = new[] { new WeaponAmmo(_rifleId, 7, 20), new WeaponAmmo(_handgunId, 3, 9) };
            var loadout = WeaponLoadout.Restore(new[] { _rifle, _handgun }, saved, _handgunId, out var unknown);
            var switched = 0;
            loadout.Switched += _ => switched++;

            Assert.That(loadout.CurrentIndex, Is.EqualTo(1));
            Assert.That(loadout.Items[0].AmmoInMagazine, Is.EqualTo(7));
            Assert.That(loadout.Items[0].ReserveAmmo, Is.EqualTo(20));
            Assert.That(loadout.Items[1].AmmoInMagazine, Is.EqualTo(3));
            Assert.That(loadout.Items[1].ReserveAmmo, Is.EqualTo(9));
            Assert.That(loadout.Items.Any(w => w.IsReloading), Is.False);
            Assert.That(unknown, Is.Empty);
            Assert.That(switched, Is.Zero);
        }

        [Test]
        public void RestoreFillsMissingWeaponsAndClampsTheMagazine()
        {
            var saved = new[] { new WeaponAmmo(_rifleId, 99, 5) };

            var loadout = WeaponLoadout.Restore(new[] { _rifle, _handgun }, saved, _rifleId, out _);

            Assert.That(loadout.Items[0].AmmoInMagazine, Is.EqualTo(30), "clamped to the magazine");
            Assert.That(loadout.Items[0].ReserveAmmo, Is.EqualTo(5));
            Assert.That(loadout.Items[1].AmmoInMagazine, Is.EqualTo(12), "a weapon without saved ammo starts full");
            Assert.That(loadout.Items[1].ReserveAmmo, Is.EqualTo(48));
        }

        [Test]
        public void RestoreReportsUnknownWeaponsAndFallsBackToTheFirstWeapon()
        {
            var saved = new[] { new WeaponAmmo(_otherId, 1, 1), new WeaponAmmo(_handgunId, 2, 2), new WeaponAmmo(_otherId, 3, 3) };

            var loadout = WeaponLoadout.Restore(new[] { _rifle, _handgun }, saved, _otherId, out var unknown);

            Assert.That(unknown, Is.EqualTo(_unknownOther));
            Assert.That(loadout.CurrentIndex, Is.Zero);
            Assert.That(loadout.Items[1].AmmoInMagazine, Is.EqualTo(2));

            var none = WeaponLoadout.Restore(new[] { _rifle, _handgun }, Array.Empty<WeaponAmmo>(), null, out _);
            Assert.That(none.CurrentIndex, Is.Zero);
        }

        [Test]
        public void SnapshotCopiesEveryWeaponInOrder()
        {
            var loadout = Create();
            loadout.Current.TryFire();
            loadout.SelectNext();
            loadout.Current.TryFire();
            loadout.Current.Tick(1f);
            loadout.Current.TryFire();

            var snapshot = loadout.SnapshotAmmo();

            Assert.That(snapshot.Select(a => a.WeaponId + ":" + a.AmmoInMagazine + "/" + a.ReserveAmmo),
                Is.EqualTo(_expectedSnapshot));

            var restored = WeaponLoadout.Restore(new[] { _rifle, _handgun }, snapshot, loadout.Current.Spec.Id, out _);
            Assert.That(restored.CurrentIndex, Is.EqualTo(1));
            Assert.That(restored.Items[1].AmmoInMagazine, Is.EqualTo(10));
        }

        [Test]
        public void RejectsInvalidArguments()
        {
            Assert.Throws<ArgumentNullException>(() => WeaponLoadout.Create(null));
            Assert.Throws<ArgumentException>(() => WeaponLoadout.Create(Array.Empty<WeaponSpec>()));
            Assert.Throws<ArgumentException>(() => WeaponLoadout.Create(new[] { _rifle, null }));
            var duplicate = Assert.Throws<ArgumentException>(() => WeaponLoadout.Create(new[] { _rifle, _handgun, _rifle }));
            Assert.That(duplicate.Message, Does.Contain(_rifleId));

            Assert.Throws<ArgumentNullException>(() => WeaponLoadout.Restore(new[] { _rifle }, null, null, out _));
            Assert.Throws<ArgumentException>(() => WeaponLoadout.Restore(new[] { _rifle }, new WeaponAmmo[] { null }, null, out _));
            Assert.Throws<ArgumentNullException>(() => WeaponLoadout.IndexOf(null, _rifleId));
            Assert.Throws<ArgumentNullException>(() => WeaponLoadout.SnapshotAmmo(null));
        }
    }
}
