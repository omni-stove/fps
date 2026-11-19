using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class MeleeWeaponSpecTests
    {
        private const string _weaponId = "00000000-0000-4000-8000-0000000000c1";

        private static readonly MeleeSpec _light = new MeleeSpec(30, 2f, 70f, 0.4f, 0.1f, 0.1f, 0.2f);
        private static readonly MeleeSpec _heavy = new MeleeSpec(90, 3f, 110f, 0.8f, 0.3f, 0.5f, 1f);

        [Test]
        public void TheDefaultsAreTheDefaultConstants()
        {
            var spec = MeleeWeaponSpec.Default;

            Assert.That(spec.Swing, Is.SameAs(MeleeWeaponSpec.DefaultSwing));
            Assert.That(spec.Swing.Damage, Is.EqualTo(MeleeWeaponSpec.DefaultSwingDamage));
            Assert.That(spec.HeavySwing, Is.Null);
            Assert.That(spec.CanCharge, Is.False);
            Assert.That(spec.Throw, Is.Null);
            Assert.That(spec.CanThrow, Is.False);
            Assert.That(spec.ChargeDelaySeconds, Is.EqualTo(MeleeWeaponSpec.DefaultChargeDelaySeconds));
            Assert.That(spec.ChargeSeconds, Is.EqualTo(MeleeWeaponSpec.DefaultChargeSeconds));
        }

        [TestCase(-0.1f, 0.8f, "chargeDelaySeconds")]
        [TestCase(float.PositiveInfinity, 0.8f, "chargeDelaySeconds")]
        [TestCase(0.2f, 0f, "chargeSeconds")]
        [TestCase(0.2f, float.NaN, "chargeSeconds")]
        public void BadChargeTimesAreRefusedByName(float delay, float seconds, string param)
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(() => new MeleeWeaponSpec(_light, _heavy, delay, seconds));

            Assert.That(e.ParamName, Is.EqualTo(param));
        }

        [TestCase(0f, 0f)]
        [TestCase(0.2f, 0f)]
        [TestCase(0.6f, 0.5f)]
        [TestCase(1f, 1f)]
        [TestCase(5f, 1f)]
        public void TheChargeStartsAfterTheDelayAndStopsWhenFull(float held, float expected)
        {
            var spec = new MeleeWeaponSpec(_light, _heavy, 0.2f, 0.8f);

            Assert.That(spec.ChargeFraction(held), Is.EqualTo(expected).Within(1e-5f));
        }

        [Test]
        public void AWeaponWithoutAHeavySwingNeverCharges()
        {
            var spec = new MeleeWeaponSpec(_light);

            Assert.That(spec.ChargeFraction(10f), Is.Zero);
            Assert.That(spec.SwingFor(1f), Is.SameAs(_light));
        }

        [Test]
        public void TheSwingMovesFromTheLightToTheHeavyValuesWithTheCharge()
        {
            var spec = new MeleeWeaponSpec(_light, _heavy);

            Assert.That(spec.SwingFor(0f), Is.SameAs(_light));
            Assert.That(spec.SwingFor(1f), Is.SameAs(_heavy));

            var half = spec.SwingFor(0.5f);
            Assert.That(half.Damage, Is.EqualTo(60));
            Assert.That(half.Reach, Is.EqualTo(2.5f).Within(1e-5f));
            Assert.That(half.ArcDegrees, Is.EqualTo(90f).Within(1e-5f));
            Assert.That(half.SwingSeconds, Is.EqualTo(0.6f).Within(1e-5f));
            Assert.That(half.CooldownSeconds, Is.EqualTo(0.2f).Within(1e-5f));
            Assert.That(half.ImpactSeconds, Is.EqualTo(0.3f).Within(1e-5f));
            Assert.That(half.LungeDistance, Is.EqualTo(0.6f).Within(1e-5f));
        }

        [Test]
        public void ThrowValuesAreCheckedByName()
        {
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => new MeleeThrowSpec(damage: 0)).ParamName, Is.EqualTo("damage"));
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => new MeleeThrowSpec(speed: 0f)).ParamName, Is.EqualTo("speed"));
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => new MeleeThrowSpec(upwardDegrees: 91f)).ParamName, Is.EqualTo("upwardDegrees"));
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => new MeleeThrowSpec(spinDegreesPerSecond: -1f)).ParamName, Is.EqualTo("spinDegreesPerSecond"));
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => new MeleeThrowSpec(radius: 0f)).ParamName, Is.EqualTo("radius"));
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => new MeleeThrowSpec(maxFlightSeconds: 0f)).ParamName, Is.EqualTo("maxFlightSeconds"));
        }

        [Test]
        public void AWeaponCannotBeBothAShieldAndAMeleeWeapon()
        {
            var e = Assert.Throws<ArgumentException>(() => new WeaponSpec(
                _weaponId, "weapon.x", WeaponCategory.Melee, shield: RiotShieldSpec.Default, melee: MeleeWeaponSpec.Default));

            Assert.That(e.ParamName, Is.EqualTo("melee"));
        }

        [Test]
        public void TheMeleePartIsKeptWhenTheSpecIsRebuilt()
        {
            var melee = new MeleeWeaponSpec(_light, _heavy, @throw: MeleeThrowSpec.Default);
            var spec = new WeaponSpec(_weaponId, "weapon.crowbar", WeaponCategory.Melee, melee: melee);

            Assert.That(spec.IsMelee, Is.True);
            Assert.That(spec.WithFireInterval(spec.FireInterval * 2f).Melee, Is.SameAs(melee));
            Assert.That(new WeaponSpec(_weaponId, "weapon.rifle", WeaponCategory.AssaultRifle).IsMelee, Is.False);
        }

        [Test]
        public void AMeleeWeaponHasItsOwnChargeAndAGunHasNone()
        {
            var melee = new MeleeWeaponSpec(_light, _heavy);
            var weapon = new Weapon(new WeaponSpec(_weaponId, "weapon.crowbar", WeaponCategory.Melee, melee: melee));

            Assert.That(weapon.MeleeCharge, Is.Not.Null);
            Assert.That(weapon.MeleeCharge.Spec, Is.SameAs(melee));
            Assert.That(new Weapon(new WeaponSpec(_weaponId, "weapon.rifle", WeaponCategory.AssaultRifle)).MeleeCharge, Is.Null);
        }
    }
}
