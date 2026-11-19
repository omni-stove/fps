using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
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
        public void ShowsTheHeatOfAHeatWeaponInsteadOfItsRounds()
        {
            var model = new HudModel();
            var ammo = new AmmoSpec(1, 0, 1f, kind: AmmoKind.Heat, heat: new HeatSpec(0.25f, 0.5f, 1f, 1f));
            var weapon = new Weapon(new WeaponSpec(_rifleId, "weapon.energyRifle", WeaponCategory.AssaultRifle, ammo: ammo));

            model.BindWeapon(weapon);
            model.RefreshWeaponGauges();
            Assert.That(model.HeatFraction, Is.EqualTo(0f));
            Assert.That(model.MagazineAmmo, Is.Null);
            Assert.That(model.ReserveAmmo, Is.Null);

            weapon.TryFire();
            model.RefreshWeaponGauges();
            Assert.That(model.HeatFraction, Is.EqualTo(0.25f));
            Assert.That(model.MagazineAmmo, Is.Null);
            Assert.That(model.ReserveAmmo, Is.Null);
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
        public void ShowsTheChosenAmmoTypeAndTheEmptiedMagazineWhenItIsSwitched()
        {
            var standard = new AmmoTypeSpec("00000000-0000-4000-8000-0000000000a2", "ammo.dmr.standard");
            var piercing = new AmmoTypeSpec("00000000-0000-4000-8000-0000000000a3", "ammo.dmr.armorPiercing", impact: new ImpactSpec(30));
            var weapon = new Weapon(new WeaponSpec(
                _rifleId, null, WeaponCategory.SniperRifle, ammo: new AmmoSpec(5, 10, 1f), ammoTypes: new[] { standard, piercing }));
            var model = new HudModel();

            model.BindWeapon(weapon);

            Assert.That(model.AmmoType, Is.SameAs(standard));

            var changes = Record(model);
            weapon.TrySwitchAmmoType();

            Assert.That(model.AmmoType, Is.SameAs(piercing));
            Assert.That(changes, Has.Member(HudField.AmmoType).And.Member(HudField.Ammo));

            model.BindWeapon(null);
            changes.Clear();
            weapon.TrySwitchAmmoType();

            Assert.That(changes, Is.Empty, "unbound weapons no longer report");
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

        [Test]
        public void AMeleeWeaponShowsItsChargeAndNoAmmo()
        {
            var melee = new MeleeWeaponSpec(
                new MeleeSpec(30, 2f, 70f, 0.4f, 0.1f, 0.1f, 0.2f),
                new MeleeSpec(90, 3f, 110f, 0.8f, 0.3f, 0.5f, 1f),
                0.2f,
                0.8f);
            var weapon = new Weapon(new WeaponSpec(_rifleId, "weapon.crowbar", WeaponCategory.Melee, melee: melee));
            var model = new HudModel();
            model.BindWeapon(weapon);
            model.RefreshWeaponGauges();

            Assert.That(model.MagazineAmmo, Is.Null, "a melee weapon uses no ammo");
            Assert.That(model.ReserveAmmo, Is.Null);
            Assert.That(model.ChargeFraction, Is.EqualTo(0f));

            weapon.MeleeCharge.Press();
            weapon.MeleeCharge.Advance(0.6f);
            model.RefreshWeaponGauges();
            Assert.That(model.ChargeFraction, Is.EqualTo(0.5f).Within(1e-5f));

            var noCharge = new Weapon(new WeaponSpec(_rifleId, "weapon.crowbar", WeaponCategory.Melee, melee: new MeleeWeaponSpec()));
            model.BindWeapon(noCharge);
            model.RefreshWeaponGauges();
            Assert.That(model.ChargeFraction, Is.Null, "a melee weapon that cannot charge shows no gauge");
        }
    }
}
