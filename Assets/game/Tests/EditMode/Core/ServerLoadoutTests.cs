using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ServerLoadoutTests
    {
        private const string _rifle = "00000000-0000-4000-8000-000000000001";
        private const string _handgun = "00000000-0000-4000-8000-000000000002";

        // Catalog に無い Id
        private const string _shotgun = "00000000-0000-4000-8000-000000000003";

        private static WeaponSpec Spec(string id, float fireInterval = 1f, int damage = 25)
        {
            return new WeaponSpec(id, null, WeaponCategory.AssaultRifle, FireMode.Auto, 30, 90, fireInterval, 2f, damage, 100f, DamageFalloff.None);
        }

        private static ServerLoadout Loadout(int burst = 1)
        {
            return new ServerLoadout(new WeaponCatalog(new[] { Spec(_rifle, 1f, 25), Spec(_handgun, 2f, 34) }), burst);
        }

        [Test]
        public void RejectsInvalidArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new ServerLoadout(null));
            var catalog = new WeaponCatalog(new[] { Spec(_rifle) });
            Assert.Throws<ArgumentOutOfRangeException>(() => new ServerLoadout(catalog, 0));
        }

        [Test]
        public void EquipReturnsTheCatalogWeapon()
        {
            var loadout = Loadout();

            var spec = loadout.Equip(1, _handgun);

            Assert.That(spec, Is.SameAs(loadout.Catalog.Get(_handgun)));
            Assert.That(loadout.TryGetEquipped(1, out var equipped), Is.True);
            Assert.That(equipped, Is.SameAs(spec));
            Assert.That(loadout.Count, Is.EqualTo(1));
        }

        [Test]
        public void EquipRejectsAnUnknownWeapon()
        {
            var loadout = Loadout();

            Assert.Throws<KeyNotFoundException>(() => loadout.Equip(1, _shotgun));
            Assert.Throws<ArgumentNullException>(() => loadout.Equip(1, null));
            Assert.That(loadout.TryGetEquipped(1, out _), Is.False);
        }

        [Test]
        public void ChecksTheWeaponIdOfAShot()
        {
            var loadout = Loadout();
            loadout.Equip(1, _rifle);

            Assert.That(loadout.CheckWeapon(1, _rifle, out var spec), Is.EqualTo(ShotRejection.None));
            Assert.That(spec.Id, Is.EqualTo(_rifle));
            Assert.That(loadout.CheckWeapon(1, _handgun, out spec), Is.EqualTo(ShotRejection.WeaponMismatch));
            Assert.That(spec, Is.Null);
            Assert.That(loadout.CheckWeapon(1, _shotgun, out spec), Is.EqualTo(ShotRejection.UnknownWeapon));
            Assert.That(spec, Is.Null);
            Assert.That(loadout.CheckWeapon(1, null, out spec), Is.EqualTo(ShotRejection.UnknownWeapon));
            Assert.That(loadout.CheckWeapon(2, _rifle, out spec), Is.EqualTo(ShotRejection.NoWeapon));
            Assert.That(spec, Is.Null);
        }

        [Test]
        public void LimitsTheFireRateWithTheEquippedWeapon()
        {
            var loadout = Loadout();
            loadout.Equip(1, _handgun);

            Assert.That(loadout.TryFire(1, _handgun, 0d, out var spec), Is.EqualTo(ShotRejection.None));
            Assert.That(spec.Damage, Is.EqualTo(34));
            Assert.That(loadout.TryFire(1, _handgun, 1d, out spec), Is.EqualTo(ShotRejection.FireRateExceeded));
            Assert.That(spec, Is.Null);
            Assert.That(loadout.TryFire(1, _handgun, 2d, out _), Is.EqualTo(ShotRejection.None));
        }

        [Test]
        public void WrongWeaponShotsDoNotUseTheBudget()
        {
            var loadout = Loadout();
            loadout.Equip(1, _rifle);

            Assert.That(loadout.TryFire(1, _handgun, 0d, out _), Is.EqualTo(ShotRejection.WeaponMismatch));
            Assert.That(loadout.TryFire(1, _shotgun, 0d, out _), Is.EqualTo(ShotRejection.UnknownWeapon));
            Assert.That(loadout.TryFire(1, _rifle, 0d, out _), Is.EqualTo(ShotRejection.None));
        }

        [Test]
        public void EachPlayerHasItsOwnBudget()
        {
            var loadout = Loadout();
            loadout.Equip(1, _rifle);
            loadout.Equip(2, _rifle);

            Assert.That(loadout.TryFire(1, _rifle, 0d, out _), Is.EqualTo(ShotRejection.None));
            Assert.That(loadout.TryFire(2, _rifle, 0d, out _), Is.EqualTo(ShotRejection.None));
            Assert.That(loadout.TryFire(1, _rifle, 0d, out _), Is.EqualTo(ShotRejection.FireRateExceeded));
        }

        [Test]
        public void EquipAgainSwitchesTheWeaponAndRefillsTheBudget()
        {
            var loadout = Loadout();
            loadout.Equip(1, _rifle);
            Assert.That(loadout.TryFire(1, _rifle, 0d, out _), Is.EqualTo(ShotRejection.None));

            loadout.Equip(1, _handgun);

            Assert.That(loadout.TryFire(1, _rifle, 0d, out _), Is.EqualTo(ShotRejection.WeaponMismatch));
            Assert.That(loadout.TryFire(1, _handgun, 0d, out _), Is.EqualTo(ShotRejection.None));
        }

        [Test]
        public void ResetBudgetsKeepsTheWeapons()
        {
            var loadout = Loadout();
            loadout.Equip(1, _handgun);
            Assert.That(loadout.TryFire(1, _handgun, 0d, out _), Is.EqualTo(ShotRejection.None));

            loadout.ResetBudgets();

            Assert.That(loadout.TryGetEquipped(1, out var spec), Is.True);
            Assert.That(spec.Id, Is.EqualTo(_handgun));
            Assert.That(loadout.TryFire(1, _handgun, 0d, out _), Is.EqualTo(ShotRejection.None));
        }

        [Test]
        public void RemoveAndClearTakeTheWeaponsAway()
        {
            var loadout = Loadout();
            loadout.Equip(1, _rifle);
            loadout.Equip(2, _rifle);

            Assert.That(loadout.Remove(1), Is.True);
            Assert.That(loadout.Remove(1), Is.False);
            Assert.That(loadout.CheckWeapon(1, _rifle, out _), Is.EqualTo(ShotRejection.NoWeapon));

            loadout.Clear();

            Assert.That(loadout.Count, Is.Zero);
            Assert.That(loadout.CheckWeapon(2, _rifle, out _), Is.EqualTo(ShotRejection.NoWeapon));
        }
    }
}
