using System;
using System.IO;
using NUnit.Framework;
using Shooter.Core;

namespace Shooter.Server.Tests
{
    // 箱から拾う・倒れた人から取り出すときに、出直しを待たずに外から武器を足す・手放す口を確かめる
    public sealed class ServerPlayerWeaponsTests
    {
        private const string _shotgunId = "9e8d7c6b-5a49-4382-9170-6f5e4d3c2b1a";
        private const float _switchSeconds = 0.4f;
        private static readonly string[] _magazine = { "1b2c3d4e-5f60-4718-8293-a4b5c6d7e8f9" };
        private const string _rifleItemId = "2c3d4e5f-6071-4829-93a4-b5c6d7e8f90a";

        private StringWriter _log;
        private ServerPlayerWeapons _weapons;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _weapons = new ServerPlayerWeapons(1, _log, () => 0d);
        }

        [TearDown]
        public void TearDown()
        {
            _log.Dispose();
        }

        private static WeaponSpec Shotgun() => new WeaponSpec(
            _shotgunId, "weapon.shotgun", WeaponCategory.Shotgun, FireMode.Semi, 6, 24, 0.8f, 3f, 12, 25f, DamageFalloff.None);

        [Test]
        public void GivingToAnEmptyHandHoldsTheWeapon()
        {
            Assert.That(_weapons.Give(Shotgun(), _switchSeconds, GameRules.Default), Is.True);

            Assert.That(_weapons.IsEquipped, Is.True);
            Assert.That(_weapons.CurrentSpec.Id, Is.EqualTo(_shotgunId));
            Assert.That(_weapons.Items[0].AmmoInMagazine, Is.EqualTo(6));
            Assert.That(_weapons.TryFire(), Is.EqualTo(ShotRejection.None));
            Assert.That(_log.ToString(), Does.Contain("[Net] give weapon player=1 weapon=" + _shotgunId + Environment.NewLine));
        }

        [Test]
        public void GivingAddsToTheEndAndKeepsTheHeldWeaponAndItsAmmo()
        {
            _weapons.Equip(new[] { TestRules.Rifle(), TestRules.Handgun() }, _switchSeconds, GameRules.Default);
            Assert.That(_weapons.TrySwitch(TestRules.HandgunId), Is.True);
            _weapons.Tick(1f);
            Assert.That(_weapons.TryFire(), Is.EqualTo(ShotRejection.None));

            Assert.That(_weapons.Give(Shotgun(), _switchSeconds, GameRules.Default), Is.True);

            Assert.That(_weapons.Specs, Has.Count.EqualTo(3));
            Assert.That(_weapons.Specs[2].Id, Is.EqualTo(_shotgunId));
            Assert.That(_weapons.CurrentSpec.Id, Is.EqualTo(TestRules.HandgunId), "giving does not switch away from the held weapon");
            Assert.That(_weapons.Sync().EquippedIndex, Is.EqualTo(1));
            Assert.That(_weapons.Sync().IsSwitching, Is.False);
            Assert.That(_weapons.Sync().AmmoInMagazine, Is.EqualTo(11), "the shot before giving still counts");
            Assert.That(_weapons.TrySwitch(_shotgunId), Is.True);
        }

        [Test]
        public void AWeaponAlreadyHeldIsNotGivenTwice()
        {
            _weapons.Equip(new[] { TestRules.Rifle() }, _switchSeconds, GameRules.Default);

            Assert.That(_weapons.Give(TestRules.Rifle(), _switchSeconds, GameRules.Default), Is.False);

            Assert.That(_weapons.Specs, Has.Count.EqualTo(1));
            Assert.That(_log.ToString(), Does.Contain("[Net] give weapon player=1 weapon=" + TestRules.RifleId + " rejected=held"));
        }

        [Test]
        public void TakingReturnsTheWeaponsInOrderWithTheirAttachmentsAndEmptiesTheHand()
        {
            var catalog = new ItemCatalog(
                new[]
                {
                    new ItemSpec(_rifleItemId, "weapon.rifle", weaponId: TestRules.RifleId, attachmentSlots: _magazineSlot),
                    new ItemSpec(_magazine[0], "item.extendedMagazine", attachment: new AttachmentSpec("magazine", magazineSizeBonus: 10)),
                },
                InventoryRule.Default);
            _weapons.Equip(new[] { TestRules.Rifle(), TestRules.Handgun() }, _switchSeconds, GameRules.Default);
            Assert.That(_weapons.SetAttachments(TestRules.RifleId, _magazine, catalog), Is.True);

            var taken = _weapons.Take();

            Assert.That(taken, Has.Count.EqualTo(2));
            Assert.That(taken[0].Spec.Id, Is.EqualTo(TestRules.RifleId));
            Assert.That(taken[0].Spec.MagazineSize, Is.EqualTo(30), "the weapon comes out without the attachment's bonus");
            Assert.That(taken[0].Attachments, Is.EqualTo(_magazine));
            Assert.That(taken[1].Spec.Id, Is.EqualTo(TestRules.HandgunId));
            Assert.That(taken[1].Attachments, Is.Empty);
            Assert.That(_weapons.IsEquipped, Is.False);
            Assert.That(_weapons.Specs, Is.Empty);
            Assert.That(_weapons.AttachmentsOf(TestRules.RifleId), Is.Empty);
            Assert.That(_weapons.TryFire(), Is.EqualTo(ShotRejection.NoWeapon));
            Assert.That(_log.ToString(), Does.Contain("[Net] clear weapons player=1 weapons=2"));
        }

        [Test]
        public void ClearingLeavesNothingToTakeAndAWeaponCanBeGivenAgain()
        {
            _weapons.Equip(new[] { TestRules.Rifle() }, _switchSeconds, GameRules.Default);

            _weapons.Clear();

            Assert.That(_weapons.Take(), Is.Empty);
            Assert.That(_weapons.Sync().WeaponCount, Is.Zero);
            Assert.That(_weapons.Give(TestRules.Rifle(), _switchSeconds, GameRules.Default), Is.True);
            Assert.That(_weapons.CurrentSpec.Id, Is.EqualTo(TestRules.RifleId));
        }

        [Test]
        public void GivingNeedsASpecAndRules()
        {
            Assert.Throws<ArgumentNullException>(() => _weapons.Give(null, _switchSeconds, GameRules.Default));
            Assert.Throws<ArgumentNullException>(() => _weapons.Give(Shotgun(), _switchSeconds, null));
        }

        private static readonly string[] _magazineSlot = { "magazine" };
    }
}
