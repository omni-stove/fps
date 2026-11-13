using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class WeaponReserveAmmoTests
    {
        private const string Rifle = "00000000-0000-4000-8000-000000000001";

        private static WeaponSpec Spec(int reserveAmmo = 90)
        {
            return new WeaponSpec(Rifle, null, WeaponCategory.AssaultRifle, FireMode.Auto, 30, reserveAmmo, 0.1f, 2f, 10, 100f, DamageFalloff.None);
        }

        [Test]
        public void TheLimitIsTheStartingReserveWithTheRulesMultiplier()
        {
            Assert.That(new Weapon(Spec()).MaxReserveAmmo, Is.EqualTo(90));
            Assert.That(new Weapon(Spec(), GameRules.Default.WithReserveAmmoMultiplier(2f)).MaxReserveAmmo, Is.EqualTo(180));
        }

        [Test]
        public void RestoringMoreThanTheStartingReserveRaisesTheLimit()
        {
            Assert.That(new Weapon(Spec(), 30, 120).MaxReserveAmmo, Is.EqualTo(120));
            Assert.That(new Weapon(Spec(), 30, 10).MaxReserveAmmo, Is.EqualTo(90));
        }

        [Test]
        public void AddsUpToTheLimitAndReportsWhatWasAdded()
        {
            var weapon = new Weapon(Spec(), 30, 70);
            var raised = 0;
            weapon.ReserveAmmoAdded += () => raised++;

            Assert.That(weapon.ReserveAmmoRoom, Is.EqualTo(20));
            Assert.That(weapon.AddReserveAmmo(15), Is.EqualTo(15));
            Assert.That(weapon.AddReserveAmmo(15), Is.EqualTo(5));
            Assert.That(weapon.ReserveAmmo, Is.EqualTo(90));
            Assert.That(weapon.AddReserveAmmo(15), Is.Zero);
            Assert.That(raised, Is.EqualTo(2), "not raised when nothing was added");
        }

        [Test]
        public void InfiniteReserveTakesNothing()
        {
            var weapon = new Weapon(Spec(), GameRules.Default.WithInfiniteReserve(true));

            Assert.That(weapon.ReserveAmmoRoom, Is.Zero);
            Assert.That(weapon.AddReserveAmmo(30), Is.Zero);
        }

        [Test]
        public void AddedAmmoFeedsTheReloadInProgress()
        {
            var weapon = new Weapon(Spec(), 0, 0);
            weapon.AddReserveAmmo(50);

            Assert.That(weapon.TryStartReload(), Is.True);
            weapon.Tick(2f);

            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(30));
            Assert.That(weapon.ReserveAmmo, Is.EqualTo(20));
        }

        [Test]
        public void RejectsANegativeAmount()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Weapon(Spec()).AddReserveAmmo(-1));
        }
    }
}
