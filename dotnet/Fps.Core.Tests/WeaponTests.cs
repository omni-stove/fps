using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class WeaponTests
    {
        private const string _rifleId = "00000000-0000-4000-8000-000000000001";

        private static Weapon Create(int magazineSize = 3, int reserveAmmo = 10, float fireInterval = 0.5f, float reloadSeconds = 2f, int damage = 10)
        {
            return new Weapon(new WeaponSpec(_rifleId, null, WeaponCategory.AssaultRifle, FireMode.Auto, magazineSize, reserveAmmo, fireInterval, reloadSeconds, damage, 100f, DamageFalloff.None));
        }

        private static void Empty(Weapon weapon, float fireInterval)
        {
            while (weapon.AmmoInMagazine > 0)
            {
                Assert.That(weapon.TryFire(), Is.True);
                weapon.Tick(fireInterval);
            }
        }

        [Test]
        public void StartsWithFullMagazineAndInitialReserve()
        {
            var weapon = Create(magazineSize: 30, reserveAmmo: 90);

            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(30));
            Assert.That(weapon.ReserveAmmo, Is.EqualTo(90));
            Assert.That(weapon.IsReloading, Is.False);
        }

        [Test]
        public void FireConsumesOneRoundAndRaisesFired()
        {
            var weapon = Create();
            var fired = 0;
            weapon.Fired += () => fired++;

            Assert.That(weapon.TryFire(), Is.True);

            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(2));
            Assert.That(fired, Is.EqualTo(1));
        }

        [Test]
        public void CannotFireAgainWithinInterval()
        {
            var weapon = Create(fireInterval: 0.5f);
            var fired = 0;
            weapon.Fired += () => fired++;
            weapon.TryFire();

            weapon.Tick(0.25f);

            Assert.That(weapon.TryFire(), Is.False);
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(2));
            Assert.That(fired, Is.EqualTo(1));
        }

        [Test]
        public void CanFireExactlyWhenIntervalElapses()
        {
            var weapon = Create(fireInterval: 0.5f);
            weapon.TryFire();

            weapon.Tick(0.25f);
            weapon.Tick(0.25f);

            Assert.That(weapon.TryFire(), Is.True);
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(1));
        }

        [Test]
        public void ZeroIntervalAllowsConsecutiveShots()
        {
            var weapon = Create(fireInterval: 0f);

            Assert.That(weapon.TryFire(), Is.True);
            Assert.That(weapon.TryFire(), Is.True);
        }

        [Test]
        public void CannotFireWhenMagazineIsEmpty()
        {
            var weapon = Create(magazineSize: 2, fireInterval: 0.5f);
            Empty(weapon, 0.5f);
            var fired = 0;
            weapon.Fired += () => fired++;

            Assert.That(weapon.TryFire(), Is.False);
            Assert.That(weapon.AmmoInMagazine, Is.Zero);
            Assert.That(fired, Is.Zero);
        }

        [Test]
        public void EmptyMagazineDoesNotReloadAutomatically()
        {
            var weapon = Create(magazineSize: 2, reserveAmmo: 10, fireInterval: 0.5f);
            Empty(weapon, 0.5f);

            weapon.Tick(10f);

            Assert.That(weapon.IsReloading, Is.False);
            Assert.That(weapon.AmmoInMagazine, Is.Zero);
            Assert.That(weapon.ReserveAmmo, Is.EqualTo(10));
        }

        [Test]
        public void CannotFireWhileReloading()
        {
            var weapon = Create(magazineSize: 3, fireInterval: 0.5f, reloadSeconds: 2f);
            weapon.TryFire();
            weapon.Tick(0.5f);

            Assert.That(weapon.TryStartReload(), Is.True);

            Assert.That(weapon.IsReloading, Is.True);
            Assert.That(weapon.TryFire(), Is.False);
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(2));
        }

        [Test]
        public void ReloadRefillsAfterReloadSecondsAndRaisesReloaded()
        {
            var weapon = Create(magazineSize: 3, reserveAmmo: 10, fireInterval: 0.5f, reloadSeconds: 2f);
            var reloaded = 0;
            weapon.Reloaded += () => reloaded++;
            Empty(weapon, 0.5f);
            weapon.TryStartReload();

            weapon.Tick(1.5f);
            Assert.That(weapon.IsReloading, Is.True);
            Assert.That(weapon.AmmoInMagazine, Is.Zero);
            Assert.That(reloaded, Is.Zero);

            weapon.Tick(0.5f);
            Assert.That(weapon.IsReloading, Is.False);
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(3));
            Assert.That(weapon.ReserveAmmo, Is.EqualTo(7));
            Assert.That(reloaded, Is.EqualTo(1));
            Assert.That(weapon.TryFire(), Is.True);
        }

        [Test]
        public void ReloadOnlyFillsWhatReserveHas()
        {
            var weapon = Create(magazineSize: 5, reserveAmmo: 2, fireInterval: 0.5f, reloadSeconds: 1f);
            Empty(weapon, 0.5f);

            weapon.TryStartReload();
            weapon.Tick(1f);

            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(2));
            Assert.That(weapon.ReserveAmmo, Is.Zero);
        }

        [Test]
        public void PartialMagazineTakesOnlyMissingRounds()
        {
            var weapon = Create(magazineSize: 5, reserveAmmo: 10, fireInterval: 0.5f, reloadSeconds: 1f);
            weapon.TryFire();
            weapon.Tick(0.5f);
            weapon.TryFire();

            weapon.TryStartReload();
            weapon.Tick(1f);

            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(5));
            Assert.That(weapon.ReserveAmmo, Is.EqualTo(8));
        }

        [Test]
        public void DoesNotStartReloadWhenMagazineIsFull()
        {
            var weapon = Create();

            Assert.That(weapon.TryStartReload(), Is.False);
            Assert.That(weapon.IsReloading, Is.False);
        }

        [Test]
        public void DoesNotStartReloadWithoutReserve()
        {
            var weapon = Create(reserveAmmo: 0);
            weapon.TryFire();

            Assert.That(weapon.TryStartReload(), Is.False);
            Assert.That(weapon.IsReloading, Is.False);
        }

        [Test]
        public void DoesNotRestartReloadWhileReloading()
        {
            var weapon = Create(reloadSeconds: 2f);
            weapon.TryFire();
            weapon.TryStartReload();
            weapon.Tick(1f);

            Assert.That(weapon.TryStartReload(), Is.False);

            weapon.Tick(1f);
            Assert.That(weapon.IsReloading, Is.False);
        }

        [Test]
        public void ZeroReloadSecondsCompletesOnNextTick()
        {
            var weapon = Create(reloadSeconds: 0f);
            weapon.TryFire();
            weapon.TryStartReload();

            weapon.Tick(0f);

            Assert.That(weapon.IsReloading, Is.False);
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(3));
        }

        [TestCase(-0.1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void RejectsInvalidDelta(float deltaSeconds)
        {
            var weapon = Create();

            Assert.Throws<ArgumentOutOfRangeException>(() => weapon.Tick(deltaSeconds));
        }

        [Test]
        public void RejectsNullSpec()
        {
            Assert.Throws<ArgumentNullException>(() => new Weapon(null));
        }

        [Test]
        public void StartsWithTheGivenAmmo()
        {
            var spec = new WeaponSpec(_rifleId, null, WeaponCategory.AssaultRifle, FireMode.Auto, 5, 10, 0.5f, 1f, 10, 100f, DamageFalloff.None);

            var weapon = new Weapon(spec, 2, 7);

            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(2));
            Assert.That(weapon.ReserveAmmo, Is.EqualTo(7));
            Assert.That(weapon.IsReloading, Is.False);
            Assert.That(weapon.TryFire(), Is.True);

            weapon.Tick(0.5f);
            Assert.That(weapon.TryStartReload(), Is.True);
            weapon.Tick(1f);
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(5));
            Assert.That(weapon.ReserveAmmo, Is.EqualTo(3));
        }

        [Test]
        public void AcceptsEmptyAndFullAmmo()
        {
            var spec = new WeaponSpec(_rifleId, null, WeaponCategory.AssaultRifle, FireMode.Auto, 5, 10, 0.5f, 1f, 10, 100f, DamageFalloff.None);

            Assert.That(new Weapon(spec, 0, 0).AmmoInMagazine, Is.Zero);
            Assert.That(new Weapon(spec, 5, 0).AmmoInMagazine, Is.EqualTo(5));
        }

        [TestCase(-1, 0)]
        [TestCase(6, 0)]
        [TestCase(0, -1)]
        public void RejectsAmmoOutOfRange(int ammoInMagazine, int reserveAmmo)
        {
            var spec = new WeaponSpec(_rifleId, null, WeaponCategory.AssaultRifle, FireMode.Auto, 5, 10, 0.5f, 1f, 10, 100f, DamageFalloff.None);

            Assert.Throws<ArgumentOutOfRangeException>(() => new Weapon(spec, ammoInMagazine, reserveAmmo));
        }

        [Test]
        public void RejectsNullSpecWithAmmo()
        {
            Assert.Throws<ArgumentNullException>(() => new Weapon(null, 0, 0));
        }

        [TestCase(0, 10, 0.5f, 2f, 10)]
        [TestCase(-1, 10, 0.5f, 2f, 10)]
        [TestCase(3, -1, 0.5f, 2f, 10)]
        [TestCase(3, 10, -0.1f, 2f, 10)]
        [TestCase(3, 10, float.NaN, 2f, 10)]
        [TestCase(3, 10, float.PositiveInfinity, 2f, 10)]
        [TestCase(3, 10, 0.5f, -0.1f, 10)]
        [TestCase(3, 10, 0.5f, float.NaN, 10)]
        [TestCase(3, 10, 0.5f, float.PositiveInfinity, 10)]
        [TestCase(3, 10, 0.5f, 2f, -1)]
        public void RejectsInvalidSpec(int magazineSize, int reserveAmmo, float fireInterval, float reloadSeconds, int damage)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new WeaponSpec(_rifleId, null, WeaponCategory.AssaultRifle, FireMode.Auto, magazineSize, reserveAmmo, fireInterval, reloadSeconds, damage, 100f, DamageFalloff.None));
        }
    }
}
