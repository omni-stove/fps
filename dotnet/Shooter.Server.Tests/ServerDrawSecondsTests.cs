using System.IO;
using NUnit.Framework;
using Shooter.Core;

namespace Shooter.Server.Tests
{
    public sealed class ServerDrawSecondsTests
    {
        private const string _rifleId = "5a1c2e3f-4b5d-4e6f-8a7b-9c0d1e2f3a4b";
        private const string _handgunId = "6b2d3f4a-5c6e-4f7a-9b8c-0d1e2f3a4b5c";

        private StringWriter _log;
        private ServerPlayerWeapons _weapons;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _weapons = new ServerPlayerWeapons(1, _log, () => 0d);
            var rifle = new WeaponSpec(_rifleId, "weapon.rifle", WeaponCategory.AssaultRifle);
            var handgun = new WeaponSpec(_handgunId, "weapon.handgun", WeaponCategory.Handgun, drawSeconds: 0.25f, holsterSeconds: 0.125f);
            _weapons.Equip(new[] { rifle, handgun }, 0.5f, GameRules.Default);
        }

        [TearDown]
        public void TearDown()
        {
            _log.Dispose();
        }

        [Test]
        public void TheHandgunIsDrawnInItsOwnTime()
        {
            Assert.That(_weapons.TrySwitch(_handgunId), Is.True);
            Assert.That(_weapons.TryFire(), Is.EqualTo(ShotRejection.Switching));
            _weapons.Tick(0.25f);
            Assert.That(_weapons.Sync().IsSwitching, Is.False, "the handgun's 0.25 s, not the loadout's 0.5 s");
        }

        [Test]
        public void TheRifleWithoutItsOwnTimeUsesTheLoadoutTime()
        {
            Assert.That(_weapons.TrySwitch(_handgunId), Is.True);
            _weapons.Tick(0.25f);
            Assert.That(_weapons.TrySwitch(_rifleId), Is.True);
            _weapons.Tick(0.375f);
            Assert.That(_weapons.Sync().IsSwitching, Is.True);
            _weapons.Tick(0.25f);
            Assert.That(_weapons.Sync().IsSwitching, Is.False, "the handgun's 0.125 s holster and the loadout's 0.5 s");
        }

        [Test]
        public void TheHandgunIsHolsteredBeforeTheRifleIsDrawn()
        {
            Assert.That(_weapons.TrySwitch(_handgunId), Is.True);
            _weapons.Tick(0.25f);
            Assert.That(_weapons.TrySwitch(_rifleId), Is.True);
            _weapons.Tick(0.5f);
            Assert.That(_weapons.TryFire(), Is.EqualTo(ShotRejection.Switching), "0.5 s is the draw alone, without the holster");
            _weapons.Tick(0.125f);
            Assert.That(_weapons.Sync().IsSwitching, Is.False);
        }
    }
}
