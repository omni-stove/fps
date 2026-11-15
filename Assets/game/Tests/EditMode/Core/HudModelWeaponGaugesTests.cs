using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class HudModelWeaponGaugesTests
    {
        private const string _rifleId = "00000000-0000-4000-8000-000000000001";

        private static List<HudField> Record(HudModel model)
        {
            var changes = new List<HudField>();
            model.Changed += changes.Add;
            return changes;
        }

        [Test]
        public void StartsWithNothingShown()
        {
            var model = new HudModel();

            Assert.That(model.ChargeFraction, Is.Null);
            Assert.That(model.HeatFraction, Is.Null);
            Assert.That(model.AmmoType, Is.Null);
            Assert.That(model.Magnification, Is.Null);
            Assert.That(model.LockOnProgress, Is.Null);
            Assert.That(model.IsLockedOn, Is.False);
        }

        [Test]
        public void SetsAndClearsEachFieldAndReportsOnlyChanges()
        {
            var model = new HudModel();
            var changes = Record(model);

            model.SetCharge(0.5f);
            model.SetCharge(0.5f);
            model.SetHeat(0.25f);
            var slug = new AmmoTypeSpec("00000000-0000-4000-8000-0000000000a1", "ammo.slug");
            model.SetAmmoType(slug);
            model.SetAmmoType(slug);
            model.SetMagnification(4f);
            model.SetLockOn(1f);

            Assert.That(model.ChargeFraction, Is.EqualTo(0.5f));
            Assert.That(model.HeatFraction, Is.EqualTo(0.25f));
            Assert.That(model.AmmoType, Is.SameAs(slug));
            Assert.That(model.Magnification, Is.EqualTo(4f));
            Assert.That(model.IsLockedOn, Is.True);
            Assert.That(
                changes,
                Is.EqualTo(new[] { HudField.Charge, HudField.Heat, HudField.AmmoType, HudField.Magnification, HudField.LockOn }));

            changes.Clear();
            model.ClearCharge();
            model.ClearHeat();
            model.ClearAmmoType();
            model.ClearMagnification();
            model.ClearLockOn();
            model.ClearLockOn();

            Assert.That(model.ChargeFraction, Is.Null);
            Assert.That(model.HeatFraction, Is.Null);
            Assert.That(model.AmmoType, Is.Null);
            Assert.That(model.Magnification, Is.Null);
            Assert.That(model.LockOnProgress, Is.Null);
            Assert.That(
                changes,
                Is.EqualTo(new[] { HudField.Charge, HudField.Heat, HudField.AmmoType, HudField.Magnification, HudField.LockOn }));
        }

        [Test]
        public void TodaysWeaponsShowNoGauges()
        {
            var model = new HudModel();
            var weapon = new Weapon(new WeaponSpec(_rifleId, "weapon.rifle", WeaponCategory.AssaultRifle));
            var changes = Record(model);

            model.BindWeapon(weapon);
            weapon.TryFire();
            model.RefreshWeaponGauges();

            Assert.That(model.ChargeFraction, Is.Null);
            Assert.That(model.HeatFraction, Is.Null);
            Assert.That(model.AmmoType, Is.Null);
            Assert.That(changes, Has.No.Member(HudField.Charge).And.No.Member(HudField.Heat).And.No.Member(HudField.AmmoType));
        }

        [Test]
        public void ShowsTheChargeOfAWeaponThatCharges()
        {
            var model = new HudModel();
            var trigger = new TriggerSpec(FireMode.Semi, 0.5f, charge: new ChargeSpec(1f, 0.2f, 2f));

            model.BindWeapon(new Weapon(new WeaponSpec(_rifleId, null, WeaponCategory.SniperRifle, trigger)));

            Assert.That(model.ChargeFraction, Is.EqualTo(0f));

            model.BindWeapon(null);

            Assert.That(model.ChargeFraction, Is.Null);
        }

        [Test]
        public void RejectsInvalidInput()
        {
            var model = new HudModel();

            Assert.Throws<ArgumentOutOfRangeException>(() => model.SetCharge(1.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => model.SetHeat(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => model.SetLockOn(-0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => model.SetMagnification(0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => model.SetMagnification(float.PositiveInfinity));
        }
    }
}
