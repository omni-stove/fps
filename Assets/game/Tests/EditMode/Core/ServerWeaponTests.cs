using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ServerWeaponTests
    {
        private static WeaponSpec Spec(int magazine = 3, int reserve = 5, float fireInterval = 0.1f, float reloadSeconds = 2f)
        {
            return new WeaponSpec("rifle", null, FireMode.Auto, magazine, reserve, fireInterval, reloadSeconds, 25, 100f, DamageFalloff.None);
        }

        [Test]
        public void StartsWithFullMagazineAndReserve()
        {
            var weapon = new ServerWeapon(Spec());

            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(3));
            Assert.That(weapon.ReserveAmmo, Is.EqualTo(5));
            Assert.That(weapon.IsReloading, Is.False);
        }

        [Test]
        public void RejectsNullSpec()
        {
            Assert.Throws<ArgumentNullException>(() => new ServerWeapon(null));
        }

        [Test]
        public void DoesNotLimitTheFireIntervalItself()
        {
            // 連射間隔は ShotBudget が見るので、同じフレームに届いた射撃も弾がある限り撃てる
            var weapon = new ServerWeapon(Spec(fireInterval: 1f));

            Assert.That(weapon.TryFire(), Is.EqualTo(ShotRejection.None));
            Assert.That(weapon.TryFire(), Is.EqualTo(ShotRejection.None));
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(1));
            Assert.That(weapon.Spec.FireInterval, Is.EqualTo(1f));
        }

        [Test]
        public void EmptyMagazineRejectsAndStartsReloadAutomatically()
        {
            var weapon = new ServerWeapon(Spec());
            bool? automatic = null;
            weapon.ReloadStarted += a => automatic = a;
            for (var i = 0; i < 3; i++)
            {
                Assert.That(weapon.TryFire(), Is.EqualTo(ShotRejection.None));
            }

            Assert.That(weapon.TryFire(), Is.EqualTo(ShotRejection.OutOfAmmo));
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(0));
            Assert.That(weapon.IsReloading, Is.True);
            Assert.That(automatic, Is.True);

            Assert.That(weapon.TryFire(), Is.EqualTo(ShotRejection.Reloading));
        }

        [Test]
        public void EmptyMagazineWithoutReserveStaysEmpty()
        {
            var weapon = new ServerWeapon(Spec(magazine: 1, reserve: 0));
            weapon.TryFire();

            Assert.That(weapon.TryFire(), Is.EqualTo(ShotRejection.OutOfAmmo));
            Assert.That(weapon.IsReloading, Is.False);
            Assert.That(weapon.TryStartReload(), Is.False);
        }

        [Test]
        public void CanFireAgainAfterTheReloadFinishes()
        {
            var weapon = new ServerWeapon(Spec());
            var reloaded = 0;
            weapon.Reloaded += () => reloaded++;
            weapon.TryFire();
            weapon.TryFire();

            Assert.That(weapon.TryStartReload(), Is.True);
            weapon.Tick(1.5f);
            Assert.That(weapon.TryFire(), Is.EqualTo(ShotRejection.Reloading));
            Assert.That(reloaded, Is.EqualTo(0));

            weapon.Tick(0.5f);
            Assert.That(reloaded, Is.EqualTo(1));
            Assert.That(weapon.IsReloading, Is.False);
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(3));
            Assert.That(weapon.ReserveAmmo, Is.EqualTo(3));
            Assert.That(weapon.TryFire(), Is.EqualTo(ShotRejection.None));
        }

        [Test]
        public void ManualReloadIsNotAutomaticAndRefusedWhenFull()
        {
            var weapon = new ServerWeapon(Spec());
            bool? automatic = null;
            weapon.ReloadStarted += a => automatic = a;

            Assert.That(weapon.TryStartReload(), Is.False);
            Assert.That(automatic, Is.Null);

            weapon.TryFire();
            Assert.That(weapon.TryStartReload(), Is.True);
            Assert.That(automatic, Is.False);
            Assert.That(weapon.TryStartReload(), Is.False);
        }

        [Test]
        public void RefillRestoresFullAmmoAndCancelsTheReload()
        {
            var weapon = new ServerWeapon(Spec());
            var reloaded = 0;
            weapon.Reloaded += () => reloaded++;
            weapon.TryFire();
            weapon.TryFire();
            weapon.TryFire();
            weapon.TryFire();
            Assert.That(weapon.IsReloading, Is.True);

            weapon.Refill();

            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(3));
            Assert.That(weapon.ReserveAmmo, Is.EqualTo(5));
            Assert.That(weapon.IsReloading, Is.False);

            // 捨てたリロードが後から完了しない
            weapon.Tick(5f);
            Assert.That(reloaded, Is.EqualTo(0));
            Assert.That(weapon.ReserveAmmo, Is.EqualTo(5));
        }

        [Test]
        public void ReloadingAfterRefillStillNotifies()
        {
            var weapon = new ServerWeapon(Spec());
            var reloaded = 0;
            weapon.Reloaded += () => reloaded++;
            weapon.Refill();
            weapon.TryFire();
            weapon.TryStartReload();

            weapon.Tick(2f);

            Assert.That(reloaded, Is.EqualTo(1));
        }

        [Test]
        public void ChangedFiresOnEveryStateChange()
        {
            var weapon = new ServerWeapon(Spec());
            var changes = 0;
            weapon.Changed += () => changes++;

            weapon.TryFire();
            Assert.That(changes, Is.EqualTo(1), "fire");

            weapon.TryStartReload();
            Assert.That(changes, Is.EqualTo(2), "reload start");

            Assert.That(weapon.TryFire(), Is.EqualTo(ShotRejection.Reloading));
            Assert.That(changes, Is.EqualTo(2), "a rejected shot changes nothing");

            weapon.Tick(2f);
            Assert.That(changes, Is.EqualTo(3), "reload end");

            weapon.Refill();
            Assert.That(changes, Is.EqualTo(4), "refill");
        }

        [Test]
        public void CancelReloadStopsWithoutChangingAmmoAndNotifies()
        {
            var weapon = new ServerWeapon(Spec());
            var reloaded = 0;
            weapon.Reloaded += () => reloaded++;
            weapon.TryFire();
            weapon.TryStartReload();
            weapon.Tick(1f);
            var changes = 0;
            weapon.Changed += () => changes++;

            weapon.CancelReload();

            Assert.That(changes, Is.EqualTo(1));
            Assert.That(weapon.IsReloading, Is.False);
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(2));
            Assert.That(weapon.ReserveAmmo, Is.EqualTo(5));
            Assert.That(weapon.TryFire(), Is.EqualTo(ShotRejection.None));

            weapon.Tick(5f);
            Assert.That(reloaded, Is.EqualTo(0));
        }

        [Test]
        public void CancelReloadWhenNotReloadingDoesNotNotify()
        {
            var weapon = new ServerWeapon(Spec());
            var changes = 0;
            weapon.Changed += () => changes++;

            weapon.CancelReload();

            Assert.That(changes, Is.EqualTo(0));
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(3));
        }

        [Test]
        public void TickRejectsInvalidDelta()
        {
            var weapon = new ServerWeapon(Spec());

            Assert.Throws<ArgumentOutOfRangeException>(() => weapon.Tick(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => weapon.Tick(float.NaN));
        }
    }
}
