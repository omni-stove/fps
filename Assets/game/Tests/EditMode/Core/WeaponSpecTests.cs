using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class WeaponSpecTests
    {
        private const string _rifleId = "3f2b8c1e-0a4d-4c6e-9b1a-7d2e5f8c0b13";

        private static readonly DamageFalloff _falloff = new DamageFalloff(20f, 60f, 0.5f);

        private static WeaponSpec Create(
            string id = _rifleId,
            string displayName = "Rifle",
            WeaponCategory category = WeaponCategory.AssaultRifle,
            FireMode fireMode = FireMode.Auto,
            int magazineSize = 30,
            int reserveAmmo = 90,
            float fireInterval = 0.1f,
            float reloadSeconds = 2f,
            int damage = 25,
            float range = 100f,
            DamageFalloff falloff = null,
            bool nullFalloff = false)
        {
            return new WeaponSpec(
                id,
                displayName,
                category,
                fireMode,
                magazineSize,
                reserveAmmo,
                fireInterval,
                reloadSeconds,
                damage,
                range,
                nullFalloff ? null : falloff ?? _falloff);
        }

        [Test]
        public void KeepsEveryValue()
        {
            var spec = Create(category: WeaponCategory.MarksmanRifle, fireMode: FireMode.Semi);

            Assert.That(spec.Id, Is.EqualTo(_rifleId));
            Assert.That(spec.DisplayName, Is.EqualTo("Rifle"));
            Assert.That(spec.Category, Is.EqualTo(WeaponCategory.MarksmanRifle));
            Assert.That(spec.FireMode, Is.EqualTo(FireMode.Semi));
            Assert.That(spec.MagazineSize, Is.EqualTo(30));
            Assert.That(spec.ReserveAmmo, Is.EqualTo(90));
            Assert.That(spec.FireInterval, Is.EqualTo(0.1f));
            Assert.That(spec.ReloadSeconds, Is.EqualTo(2f));
            Assert.That(spec.Damage, Is.EqualTo(25));
            Assert.That(spec.Range, Is.EqualTo(100f));
            Assert.That(spec.Falloff, Is.SameAs(_falloff));
        }

        [Test]
        public void TrimsTheId()
        {
            Assert.That(Create(id: "  " + _rifleId + " \t").Id, Is.EqualTo(_rifleId));
        }

        [TestCase("3f2b8c1e-0a4d-4c6e-9b1a-7d2e5f8c0b13")]
        [TestCase("00000000-0000-0000-0000-000000000000")]
        [TestCase("ffffffff-ffff-ffff-ffff-ffffffffffff")]
        public void AcceptsLowercaseHyphenatedUuids(string id)
        {
            Assert.That(Create(id: id).Id, Is.EqualTo(id));
        }

        [Test]
        public void AcceptsTheFormatOfGuidToStringD()
        {
            var id = Guid.NewGuid().ToString("D");
            Assert.That(Create(id: id).Id, Is.EqualTo(id));
        }

        [Test]
        public void RejectsNullId()
        {
            var e = Assert.Throws<ArgumentNullException>(() => Create(id: null));
            Assert.That(e.ParamName, Is.EqualTo("id"));
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("rifle")]
        [TestCase("handgun")]
        [TestCase("3F2B8C1E-0A4D-4C6E-9B1A-7D2E5F8C0B13")]
        [TestCase("3f2b8c1e-0a4d-4c6e-9b1a-7D2e5f8c0b13")]
        [TestCase("{3f2b8c1e-0a4d-4c6e-9b1a-7d2e5f8c0b13}")]
        [TestCase("(3f2b8c1e-0a4d-4c6e-9b1a-7d2e5f8c0b13)")]
        [TestCase("3f2b8c1e0a4d4c6e9b1a7d2e5f8c0b13")]
        [TestCase("3f2b8c1e-0a4d-4c6e-9b1a-7d2e5f8c0b1")]
        [TestCase("3f2b8c1e-0a4d-4c6e-9b1a-7d2e5f8c0b134")]
        [TestCase("3f2b8c1e-0a4d-4c6e-9b1a_7d2e5f8c0b13")]
        [TestCase("3f2b8c1e0-a4d-4c6e-9b1a-7d2e5f8c0b13")]
        [TestCase("3f2b8c1g-0a4d-4c6e-9b1a-7d2e5f8c0b13")]
        [TestCase("3f2b8c1e 0a4d 4c6e 9b1a 7d2e5f8c0b13")]
        [TestCase("0x3f2b8c1e-0a4d-4c6e-9b1a-7d2e5f8c0b")]
        [TestCase("3f2b8c1e-0a4d-4c6e-9b1a-７d2e5f8c0b13")]
        public void RejectsIdsThatAreNotLowercaseHyphenatedUuids(string id)
        {
            var e = Assert.Throws<ArgumentException>(() => Create(id: id));
            Assert.That(e.ParamName, Is.EqualTo("id"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        public void UsesTheIdWhenTheDisplayNameIsEmpty(string displayName)
        {
            Assert.That(Create(id: " " + _rifleId + " ", displayName: displayName).DisplayName, Is.EqualTo(_rifleId));
        }

        [TestCase(WeaponCategory.AssaultRifle)]
        [TestCase(WeaponCategory.SubmachineGun)]
        [TestCase(WeaponCategory.Shotgun)]
        [TestCase(WeaponCategory.MarksmanRifle)]
        [TestCase(WeaponCategory.SniperRifle)]
        [TestCase(WeaponCategory.LightMachineGun)]
        [TestCase(WeaponCategory.Handgun)]
        public void AcceptsEveryDefinedCategory(WeaponCategory category)
        {
            Assert.That(Create(category: category).Category, Is.EqualTo(category));
        }

        [TestCase(-1)]
        [TestCase(7)]
        [TestCase(99)]
        public void RejectsUndefinedCategory(int category)
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(() => Create(category: (WeaponCategory)category));
            Assert.That(e.ParamName, Is.EqualTo("category"));
        }
        [Test]
        public void TrimsTheDisplayName()
        {
            Assert.That(Create(displayName: "  Assault Rifle ").DisplayName, Is.EqualTo("Assault Rifle"));
        }

        [Test]
        public void RejectsUndefinedFireMode()
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(() => Create(fireMode: (FireMode)99));
            Assert.That(e.ParamName, Is.EqualTo("fireMode"));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void RejectsNonPositiveMagazineSize(int magazineSize)
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(() => Create(magazineSize: magazineSize));
            Assert.That(e.ParamName, Is.EqualTo("magazineSize"));
        }

        [Test]
        public void RejectsNegativeReserveAmmo()
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(() => Create(reserveAmmo: -1));
            Assert.That(e.ParamName, Is.EqualTo("reserveAmmo"));
        }

        [TestCase(-0.1f)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NaN)]
        public void RejectsInvalidFireInterval(float fireInterval)
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(() => Create(fireInterval: fireInterval));
            Assert.That(e.ParamName, Is.EqualTo("fireInterval"));
        }

        [TestCase(-0.1f)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NaN)]
        public void RejectsInvalidReloadSeconds(float reloadSeconds)
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(() => Create(reloadSeconds: reloadSeconds));
            Assert.That(e.ParamName, Is.EqualTo("reloadSeconds"));
        }

        [Test]
        public void RejectsNegativeDamage()
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(() => Create(damage: -1));
            Assert.That(e.ParamName, Is.EqualTo("damage"));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NaN)]
        public void RejectsInvalidRange(float range)
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(() => Create(range: range));
            Assert.That(e.ParamName, Is.EqualTo("range"));
        }

        [Test]
        public void RejectsNullFalloff()
        {
            var e = Assert.Throws<ArgumentNullException>(() => Create(nullFalloff: true));
            Assert.That(e.ParamName, Is.EqualTo("falloff"));
        }

        [Test]
        public void AcceptsNoFalloff()
        {
            Assert.That(Create(falloff: DamageFalloff.None).Falloff, Is.SameAs(DamageFalloff.None));
        }
    }
}
