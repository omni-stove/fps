using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class LoadoutRuleTests
    {
        private const string _rifleId = "00000000-0000-4000-8000-000000000001";
        private const string _handgunId = "00000000-0000-4000-8000-000000000002";
        private const string _smgId = "00000000-0000-4000-8000-000000000003";

        private static readonly WeaponCategory[] _primary =
        {
            WeaponCategory.AssaultRifle,
            WeaponCategory.SubmachineGun,
            WeaponCategory.Shotgun,
            WeaponCategory.MarksmanRifle,
            WeaponCategory.SniperRifle,
            WeaponCategory.LightMachineGun,
        };

        private static readonly WeaponCategory[] _secondary = { WeaponCategory.Handgun };

        private static WeaponSpec Spec(string id, string displayName, WeaponCategory category)
        {
            return new WeaponSpec(id, displayName, category, FireMode.Auto, 30, 90, 0.1f, 2f, 25, 100f, DamageFalloff.None);
        }

        private static LoadoutRule PrimaryAndSecondary()
        {
            return new LoadoutRule(new IEnumerable<WeaponCategory>[] { _primary, _secondary });
        }

        [Test]
        public void KeepsTheSlotOrderAndTheAcceptedCategories()
        {
            var rule = PrimaryAndSecondary();

            Assert.That(rule.SlotCount, Is.EqualTo(2));
            Assert.That(rule.Slots[0], Is.EqualTo(_primary));
            Assert.That(rule.Slots[1], Is.EqualTo(_secondary));
            Assert.That(rule.Accepts(0, WeaponCategory.AssaultRifle), Is.True);
            Assert.That(rule.Accepts(0, WeaponCategory.Handgun), Is.False);
            Assert.That(rule.Accepts(1, WeaponCategory.Handgun), Is.True);
            Assert.That(rule.Accepts(1, WeaponCategory.SniperRifle), Is.False);
        }

        [Test]
        public void SortsAndDeduplicatesTheCategoriesOfASlot()
        {
            var rule = new LoadoutRule(new[]
            {
                new[] { WeaponCategory.Shotgun, WeaponCategory.AssaultRifle, WeaponCategory.Shotgun },
            });

            Assert.That(rule.Slots[0], Is.EqualTo(new[] { WeaponCategory.AssaultRifle, WeaponCategory.Shotgun }));
        }

        [Test]
        public void IsNotAffectedByChangesToTheSourceLists()
        {
            var slot = new List<WeaponCategory> { WeaponCategory.Handgun };
            var slots = new List<IEnumerable<WeaponCategory>> { slot };
            var rule = new LoadoutRule(slots);

            slot.Add(WeaponCategory.Shotgun);
            slots.Add(new[] { WeaponCategory.Shotgun });

            Assert.That(rule.SlotCount, Is.EqualTo(1));
            Assert.That(rule.Slots[0], Is.EqualTo(new[] { WeaponCategory.Handgun }));
            Assert.That(rule.Accepts(0, WeaponCategory.Shotgun), Is.False);
        }

        [Test]
        public void TheSlotsCannotBeModifiedThroughTheProperty()
        {
            var rule = PrimaryAndSecondary();

            Assert.That(rule.Slots, Is.Not.InstanceOf<List<IReadOnlyList<WeaponCategory>>>());
            Assert.That(rule.Slots[0], Is.Not.InstanceOf<List<WeaponCategory>>());
        }

        [Test]
        public void RejectsInvalidSlots()
        {
            Assert.Throws<ArgumentNullException>(() => new LoadoutRule(null));
            Assert.Throws<ArgumentException>(() => new LoadoutRule(Array.Empty<IEnumerable<WeaponCategory>>()));
            Assert.Throws<ArgumentException>(() => new LoadoutRule(new IEnumerable<WeaponCategory>[] { _primary, null }));
            Assert.Throws<ArgumentException>(
                () => new LoadoutRule(new IEnumerable<WeaponCategory>[] { _primary, Array.Empty<WeaponCategory>() }));
            Assert.Throws<ArgumentException>(
                () => new LoadoutRule(new IEnumerable<WeaponCategory>[] { new[] { (WeaponCategory)99 } }));
        }

        [Test]
        public void AcceptsRejectsAnOutOfRangeSlot()
        {
            var rule = PrimaryAndSecondary();

            Assert.Throws<ArgumentOutOfRangeException>(() => rule.Accepts(-1, WeaponCategory.Handgun));
            Assert.Throws<ArgumentOutOfRangeException>(() => rule.Accepts(2, WeaponCategory.Handgun));
        }

        [Test]
        public void ValidateReturnsNullForWeaponsThatFitTheSlots()
        {
            var rule = PrimaryAndSecondary();
            var weapons = new[]
            {
                Spec(_rifleId, "Rifle", WeaponCategory.AssaultRifle),
                Spec(_handgunId, "Handgun", WeaponCategory.Handgun),
            };

            Assert.That(rule.Validate(weapons), Is.Null);
        }

        [Test]
        public void ValidateRejectsAWrongNumberOfWeapons()
        {
            var rule = PrimaryAndSecondary();
            var rifle = Spec(_rifleId, "Rifle", WeaponCategory.AssaultRifle);

            Assert.That(rule.Validate(Array.Empty<WeaponSpec>()), Does.Contain("expected 2"));
            Assert.That(rule.Validate(new[] { rifle }), Does.Contain("expected 2").And.Contain("got 1"));
            Assert.That(
                rule.Validate(new[] { rifle, Spec(_handgunId, "Handgun", WeaponCategory.Handgun), Spec(_smgId, "Smg", WeaponCategory.SubmachineGun) }),
                Does.Contain("got 3"));
        }

        [Test]
        public void ValidateRejectsAWeaponTheSlotDoesNotAccept()
        {
            var rule = PrimaryAndSecondary();
            var weapons = new[]
            {
                Spec(_smgId, "Smg", WeaponCategory.SubmachineGun),
                Spec(_rifleId, "Rifle", WeaponCategory.AssaultRifle),
            };

            Assert.That(rule.Validate(weapons), Is.EqualTo("slot 1 does not accept AssaultRifle (weapon 'Rifle')"));
        }

        [Test]
        public void ValidateRejectsTheSameWeaponTwice()
        {
            var rule = new LoadoutRule(new IEnumerable<WeaponCategory>[] { _primary, _primary });
            var rifle = Spec(_rifleId, "Rifle", WeaponCategory.AssaultRifle);
            var sameIdOtherInstance = Spec(_rifleId, "Rifle Copy", WeaponCategory.AssaultRifle);

            Assert.That(rule.Validate(new[] { rifle, rifle }), Does.Contain("slot 1").And.Contain(_rifleId).And.Contain("slot 0"));
            Assert.That(rule.Validate(new[] { rifle, sameIdOtherInstance }), Does.Contain("slot 1").And.Contain(_rifleId));
            Assert.That(rule.Validate(new[] { rifle, Spec(_smgId, "Smg", WeaponCategory.SubmachineGun) }), Is.Null);
        }

        [Test]
        public void ValidateRejectsANullWeapon()
        {
            var rule = PrimaryAndSecondary();

            Assert.That(
                rule.Validate(new[] { Spec(_rifleId, "Rifle", WeaponCategory.AssaultRifle), null }),
                Is.EqualTo("slot 1 has no weapon"));
        }

        [Test]
        public void ValidateRejectsANullList()
        {
            Assert.Throws<ArgumentNullException>(() => PrimaryAndSecondary().Validate(null));
        }
    }
}
