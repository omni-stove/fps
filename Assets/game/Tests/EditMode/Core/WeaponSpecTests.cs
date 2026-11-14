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
            string nameKey = "weapon.rifle",
            WeaponCategory category = WeaponCategory.AssaultRifle,
            FireMode fireMode = FireMode.Auto,
            int magazineSize = 30,
            int reserveAmmo = 90,
            float fireInterval = 0.1f,
            float reloadSeconds = 2f,
            int damage = 25,
            float range = 100f,
            DamageFalloff falloff = null,
            bool nullFalloff = false,
            float explosionRadius = 0f)
        {
            return new WeaponSpec(
                id,
                nameKey,
                category,
                fireMode,
                magazineSize,
                reserveAmmo,
                fireInterval,
                reloadSeconds,
                damage,
                range,
                nullFalloff ? null : falloff ?? _falloff,
                explosionRadius);
        }

        [Test]
        public void KeepsEveryValue()
        {
            var spec = Create(category: WeaponCategory.MarksmanRifle, fireMode: FireMode.Semi);

            Assert.That(spec.Id, Is.EqualTo(_rifleId));
            Assert.That(spec.NameKey, Is.EqualTo("weapon.rifle"));
            Assert.That(spec.Name, Is.EqualTo(new LocalizedText("weapon.rifle")));
            Assert.That(spec.LogName, Is.EqualTo("weapon.rifle"));
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
        public void AdsFieldOfViewFallsBackToTheCommonDefault()
        {
            Assert.That(Create().AdsFieldOfView, Is.EqualTo(AdsZoom.DefaultFieldOfView));
        }

        [Test]
        public void KeepsAnAdsFieldOfView()
        {
            var spec = new WeaponSpec(
                _rifleId, "Rifle", WeaponCategory.AssaultRifle, FireMode.Auto, 30, 90, 0.1f, 2f, 25, 100f, _falloff, adsFieldOfView: 42f);

            Assert.That(spec.AdsFieldOfView, Is.EqualTo(42f));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(180f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void RejectsAnAdsFieldOfViewThatCannotBeProjected(float fieldOfView)
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(() => new WeaponSpec(
                _rifleId, "Rifle", WeaponCategory.AssaultRifle, FireMode.Auto, 30, 90, 0.1f, 2f, 25, 100f, _falloff, adsFieldOfView: fieldOfView));
            Assert.That(e.ParamName, Is.EqualTo("adsFieldOfView"));
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
        public void ShowsTheIdAsIsWhenTheNameKeyIsEmpty(string nameKey)
        {
            var spec = Create(id: " " + _rifleId + " ", nameKey: nameKey);

            Assert.That(spec.NameKey, Is.Empty);
            Assert.That(spec.Name, Is.EqualTo(LocalizedText.Literal(_rifleId)), "the id is shown without looking up the table");
            Assert.That(spec.LogName, Is.EqualTo(_rifleId));
        }

        [TestCase(WeaponCategory.AssaultRifle)]
        [TestCase(WeaponCategory.SubmachineGun)]
        [TestCase(WeaponCategory.Shotgun)]
        [TestCase(WeaponCategory.MarksmanRifle)]
        [TestCase(WeaponCategory.SniperRifle)]
        [TestCase(WeaponCategory.LightMachineGun)]
        [TestCase(WeaponCategory.Handgun)]
        [TestCase(WeaponCategory.Launcher)]
        public void AcceptsEveryDefinedCategory(WeaponCategory category)
        {
            Assert.That(Create(category: category).Category, Is.EqualTo(category));
        }

        [TestCase(-1)]
        [TestCase(8)]
        [TestCase(99)]
        public void RejectsUndefinedCategory(int category)
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(() => Create(category: (WeaponCategory)category));
            Assert.That(e.ParamName, Is.EqualTo("category"));
        }
        [Test]
        public void TrimsTheNameKey()
        {
            Assert.That(Create(nameKey: "  weapon.assaultRifle ").NameKey, Is.EqualTo("weapon.assaultRifle"));
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

        [Test]
        public void ExplosionRadiusIsZeroWhenOmitted()
        {
            var spec = new WeaponSpec(_rifleId, "Rifle", WeaponCategory.AssaultRifle, FireMode.Auto, 30, 90, 0.1f, 2f, 25, 100f, _falloff);

            Assert.That(spec.ExplosionRadius, Is.EqualTo(0f));
        }

        [TestCase(0f)]
        [TestCase(3.5f)]
        public void KeepsTheExplosionRadius(float explosionRadius)
        {
            Assert.That(Create(explosionRadius: explosionRadius).ExplosionRadius, Is.EqualTo(explosionRadius));
        }

        [TestCase(-0.1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void RejectsInvalidExplosionRadius(float explosionRadius)
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(() => Create(explosionRadius: explosionRadius));
            Assert.That(e.ParamName, Is.EqualTo("explosionRadius"));
        }

        private static WeaponSpec CreateWithActions(
            float blindFireSpreadDegrees = 0f,
            float activeReloadWindowStart = WeaponSpec.DefaultActiveReloadWindowStart,
            float activeReloadWindowLength = WeaponSpec.DefaultActiveReloadWindowLength,
            float activeReloadFailSeconds = WeaponSpec.DefaultActiveReloadFailSeconds)
        {
            return new WeaponSpec(
                _rifleId, "Rifle", WeaponCategory.AssaultRifle, FireMode.Auto, 30, 90, 0.1f, 2f, 25, 100f, _falloff,
                blindFireSpreadDegrees: blindFireSpreadDegrees,
                activeReloadWindowStart: activeReloadWindowStart,
                activeReloadWindowLength: activeReloadWindowLength,
                activeReloadFailSeconds: activeReloadFailSeconds);
        }

        [Test]
        public void ActionValuesUseTheDefaultsWhenOmitted()
        {
            var spec = Create();

            Assert.That(spec.BlindFireSpreadDegrees, Is.EqualTo(0f));
            Assert.That(spec.ActiveReloadWindowStart, Is.EqualTo(0.5f));
            Assert.That(spec.ActiveReloadWindowLength, Is.EqualTo(0.15f));
            Assert.That(spec.ActiveReloadFailSeconds, Is.EqualTo(0.5f));
        }

        [Test]
        public void KeepsTheActionValues()
        {
            var spec = CreateWithActions(12f, 0.45f, 0.2f, 0.4f);

            Assert.That(spec.BlindFireSpreadDegrees, Is.EqualTo(12f));
            Assert.That(spec.ActiveReloadWindowStart, Is.EqualTo(0.45f));
            Assert.That(spec.ActiveReloadWindowLength, Is.EqualTo(0.2f));
            Assert.That(spec.ActiveReloadFailSeconds, Is.EqualTo(0.4f));
        }

        [TestCase(0f)]
        [TestCase(90f)]
        public void AcceptsABlindFireSpreadAtTheBounds(float degrees)
        {
            Assert.That(CreateWithActions(blindFireSpreadDegrees: degrees).BlindFireSpreadDegrees, Is.EqualTo(degrees));
        }

        [TestCase(-0.1f)]
        [TestCase(90.1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void RejectsABlindFireSpreadOutsideZeroToNinety(float degrees)
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(() => CreateWithActions(blindFireSpreadDegrees: degrees));
            Assert.That(e.ParamName, Is.EqualTo("blindFireSpreadDegrees"));
        }

        [Test]
        public void AcceptsAnActiveReloadWindowThatStartsAtZero()
        {
            Assert.That(CreateWithActions(activeReloadWindowStart: 0f).ActiveReloadWindowStart, Is.EqualTo(0f));
        }

        [Test]
        public void AcceptsAnActiveReloadWindowThatEndsAtTheEndOfTheReload()
        {
            var spec = CreateWithActions(activeReloadWindowStart: 0.75f, activeReloadWindowLength: 0.25f);

            Assert.That(spec.ActiveReloadWindowStart, Is.EqualTo(0.75f));
            Assert.That(spec.ActiveReloadWindowLength, Is.EqualTo(0.25f));
        }

        [TestCase(-0.01f)]
        [TestCase(1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void RejectsAnActiveReloadWindowStartOutsideZeroToOne(float start)
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(() => CreateWithActions(activeReloadWindowStart: start));
            Assert.That(e.ParamName, Is.EqualTo("activeReloadWindowStart"));
        }

        [TestCase(0f)]
        [TestCase(-0.1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void RejectsANonPositiveActiveReloadWindowLength(float length)
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(() => CreateWithActions(activeReloadWindowLength: length));
            Assert.That(e.ParamName, Is.EqualTo("activeReloadWindowLength"));
        }

        [Test]
        public void RejectsAnActiveReloadWindowThatEndsAfterTheReload()
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(
                () => CreateWithActions(activeReloadWindowStart: 0.9f, activeReloadWindowLength: 0.2f));
            Assert.That(e.ParamName, Is.EqualTo("activeReloadWindowLength"));
        }

        [Test]
        public void AcceptsZeroActiveReloadFailSeconds()
        {
            Assert.That(CreateWithActions(activeReloadFailSeconds: 0f).ActiveReloadFailSeconds, Is.EqualTo(0f));
        }

        [TestCase(-0.1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void RejectsInvalidActiveReloadFailSeconds(float seconds)
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(() => CreateWithActions(activeReloadFailSeconds: seconds));
            Assert.That(e.ParamName, Is.EqualTo("activeReloadFailSeconds"));
        }

        private static WeaponSpec CreateWithHandling(
            float recoilPitchDegrees = 0f,
            float recoilYawDegrees = 0f,
            float recoilRecoveryPerSecond = 0f,
            float hipSpreadDegrees = 0f,
            float adsSpreadDegrees = 0f,
            float moveSpreadDegrees = 0f)
        {
            return new WeaponSpec(
                _rifleId, "Rifle", WeaponCategory.AssaultRifle, FireMode.Auto, 30, 90, 0.1f, 2f, 25, 100f, _falloff,
                recoilPitchDegrees: recoilPitchDegrees,
                recoilYawDegrees: recoilYawDegrees,
                recoilRecoveryPerSecond: recoilRecoveryPerSecond,
                hipSpreadDegrees: hipSpreadDegrees,
                adsSpreadDegrees: adsSpreadDegrees,
                moveSpreadDegrees: moveSpreadDegrees);
        }

        private static WeaponSpec CreateWithAngle(string name, float degrees)
        {
            switch (name)
            {
                case "recoilPitchDegrees":
                    return CreateWithHandling(recoilPitchDegrees: degrees);
                case "recoilYawDegrees":
                    return CreateWithHandling(recoilYawDegrees: degrees);
                case "hipSpreadDegrees":
                    return CreateWithHandling(hipSpreadDegrees: degrees);
                case "adsSpreadDegrees":
                    return CreateWithHandling(adsSpreadDegrees: degrees);
                case "moveSpreadDegrees":
                    return CreateWithHandling(moveSpreadDegrees: degrees);
                default:
                    throw new ArgumentException("unknown angle " + name, nameof(name));
            }
        }

        [Test]
        public void RecoilAndSpreadAreZeroWhenOmitted()
        {
            var spec = Create();

            Assert.That(spec.RecoilPitchDegrees, Is.EqualTo(0f));
            Assert.That(spec.RecoilYawDegrees, Is.EqualTo(0f));
            Assert.That(spec.RecoilRecoveryPerSecond, Is.EqualTo(0f));
            Assert.That(spec.HipSpreadDegrees, Is.EqualTo(0f));
            Assert.That(spec.AdsSpreadDegrees, Is.EqualTo(0f));
            Assert.That(spec.MoveSpreadDegrees, Is.EqualTo(0f));
        }

        [Test]
        public void KeepsTheRecoilAndSpreadValues()
        {
            var spec = CreateWithHandling(1.5f, 0.4f, 12f, 3f, 0.5f, 2f);

            Assert.That(spec.RecoilPitchDegrees, Is.EqualTo(1.5f));
            Assert.That(spec.RecoilYawDegrees, Is.EqualTo(0.4f));
            Assert.That(spec.RecoilRecoveryPerSecond, Is.EqualTo(12f));
            Assert.That(spec.HipSpreadDegrees, Is.EqualTo(3f));
            Assert.That(spec.AdsSpreadDegrees, Is.EqualTo(0.5f));
            Assert.That(spec.MoveSpreadDegrees, Is.EqualTo(2f));
        }

        [TestCase("recoilPitchDegrees", 0f)]
        [TestCase("recoilPitchDegrees", 90f)]
        [TestCase("recoilYawDegrees", 0f)]
        [TestCase("recoilYawDegrees", 90f)]
        [TestCase("hipSpreadDegrees", 0f)]
        [TestCase("hipSpreadDegrees", 90f)]
        [TestCase("adsSpreadDegrees", 0f)]
        [TestCase("adsSpreadDegrees", 90f)]
        [TestCase("moveSpreadDegrees", 0f)]
        [TestCase("moveSpreadDegrees", 90f)]
        public void AcceptsRecoilAndSpreadAnglesAtTheBounds(string name, float degrees)
        {
            Assert.That(() => CreateWithAngle(name, degrees), Throws.Nothing);
        }

        [TestCase("recoilPitchDegrees", -0.1f)]
        [TestCase("recoilPitchDegrees", 90.1f)]
        [TestCase("recoilPitchDegrees", float.NaN)]
        [TestCase("recoilYawDegrees", -0.1f)]
        [TestCase("recoilYawDegrees", 90.1f)]
        [TestCase("recoilYawDegrees", float.PositiveInfinity)]
        [TestCase("hipSpreadDegrees", -0.1f)]
        [TestCase("hipSpreadDegrees", 90.1f)]
        [TestCase("hipSpreadDegrees", float.NaN)]
        [TestCase("adsSpreadDegrees", -0.1f)]
        [TestCase("adsSpreadDegrees", 90.1f)]
        [TestCase("adsSpreadDegrees", float.NegativeInfinity)]
        [TestCase("moveSpreadDegrees", -0.1f)]
        [TestCase("moveSpreadDegrees", 90.1f)]
        [TestCase("moveSpreadDegrees", float.NaN)]
        public void RejectsRecoilAndSpreadAnglesOutsideZeroToNinety(string name, float degrees)
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(() => CreateWithAngle(name, degrees));
            Assert.That(e.ParamName, Is.EqualTo(name));
        }

        [TestCase(-0.1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void RejectsInvalidRecoilRecovery(float perSecond)
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(() => CreateWithHandling(recoilRecoveryPerSecond: perSecond));
            Assert.That(e.ParamName, Is.EqualTo("recoilRecoveryPerSecond"));
        }

        [Test]
        public void RecoilRecoveryDelayDefaultsWhenOmitted()
        {
            Assert.That(Create().RecoilRecoveryDelaySeconds, Is.EqualTo(WeaponSpec.DefaultRecoilRecoveryDelaySeconds));
        }

        [TestCase(0f)]
        [TestCase(0.4f)]
        public void KeepsTheRecoilRecoveryDelay(float seconds)
        {
            var spec = new WeaponSpec(
                _rifleId, "Rifle", WeaponCategory.AssaultRifle, FireMode.Auto, 30, 90, 0.1f, 2f, 25, 100f, _falloff,
                recoilRecoveryDelaySeconds: seconds);

            Assert.That(spec.RecoilRecoveryDelaySeconds, Is.EqualTo(seconds));
        }

        [TestCase(-0.1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void RejectsInvalidRecoilRecoveryDelay(float seconds)
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(() => new WeaponSpec(
                _rifleId, "Rifle", WeaponCategory.AssaultRifle, FireMode.Auto, 30, 90, 0.1f, 2f, 25, 100f, _falloff,
                recoilRecoveryDelaySeconds: seconds));
            Assert.That(e.ParamName, Is.EqualTo("recoilRecoveryDelaySeconds"));
        }
    }
}
