using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class WeaponSpecPartsTests
    {
        private const string _rifleId = "3f2b8c1e-0a4d-4c6e-9b1a-7d2e5f8c0b13";
        private const string _slugId = "9a8b7c6d-5e4f-4a3b-8c2d-1e0f9a8b7c6d";
        private const string _buckshotId = "0f1e2d3c-4b5a-4968-8776-655443322110";

        private static readonly DamageFalloff _falloff = new DamageFalloff(20f, 60f, 0.5f);
        private static readonly ExplosionDamage _blast = new ExplosionDamage(100, 5f, 1f);

        private static WeaponSpec CreateFlatRifle()
        {
            return new WeaponSpec(
                _rifleId, "weapon.rifle", WeaponCategory.AssaultRifle, FireMode.Semi, 20, 60, 0.15f, 2.5f, 30, 120f, _falloff,
                explosionRadius: 1.5f,
                adsFieldOfView: 40f,
                blindFireSpreadDegrees: 8f,
                activeReloadWindowStart: 0.4f,
                activeReloadWindowLength: 0.2f,
                activeReloadFailSeconds: 0.6f,
                recoilPitchDegrees: 0.5f,
                recoilYawDegrees: 0.2f,
                recoilRecoveryPerSecond: 10f,
                hipSpreadDegrees: 3f,
                adsSpreadDegrees: 0.5f,
                moveSpreadDegrees: 2f,
                recoilRecoveryDelaySeconds: 0.3f,
                shotNoiseRadius: 12f);
        }

        private static readonly float[] _rifleZoom = { 40f };
        private static readonly float[] _zoomBeyondTheProjection = { 40f, 180f };

        private static WeaponSpec CreatePartsRifle()
        {
            return new WeaponSpec(
                _rifleId,
                "weapon.rifle",
                WeaponCategory.AssaultRifle,
                new TriggerSpec(FireMode.Semi, 0.15f),
                new AmmoSpec(20, 60, 2.5f, 0.4f, 0.2f, 0.6f),
                new RoundSpec(range: 120f),
                new ImpactSpec(30, _falloff, 1.5f),
                new AimSpec(
                    hipSpreadDegrees: 3f,
                    adsSpreadDegrees: 0.5f,
                    moveSpreadDegrees: 2f,
                    recoilPitchDegrees: 0.5f,
                    recoilYawDegrees: 0.2f,
                    recoilRecoveryPerSecond: 10f,
                    recoilRecoveryDelaySeconds: 0.3f,
                    blindFireSpreadDegrees: 8f,
                    zoomLevels: _rifleZoom),
                shotNoiseRadius: 12f);
        }

        private static void AssertSameFlatValues(WeaponSpec actual, WeaponSpec expected)
        {
            Assert.That(actual.Id, Is.EqualTo(expected.Id));
            Assert.That(actual.NameKey, Is.EqualTo(expected.NameKey));
            Assert.That(actual.Category, Is.EqualTo(expected.Category));
            Assert.That(actual.FireMode, Is.EqualTo(expected.FireMode));
            Assert.That(actual.MagazineSize, Is.EqualTo(expected.MagazineSize));
            Assert.That(actual.ReserveAmmo, Is.EqualTo(expected.ReserveAmmo));
            Assert.That(actual.FireInterval, Is.EqualTo(expected.FireInterval));
            Assert.That(actual.ReloadSeconds, Is.EqualTo(expected.ReloadSeconds));
            Assert.That(actual.Damage, Is.EqualTo(expected.Damage));
            Assert.That(actual.Range, Is.EqualTo(expected.Range));
            Assert.That(actual.Falloff, Is.SameAs(expected.Falloff));
            Assert.That(actual.ExplosionRadius, Is.EqualTo(expected.ExplosionRadius));
            Assert.That(actual.AdsFieldOfView, Is.EqualTo(expected.AdsFieldOfView));
            Assert.That(actual.BlindFireSpreadDegrees, Is.EqualTo(expected.BlindFireSpreadDegrees));
            Assert.That(actual.ActiveReloadWindowStart, Is.EqualTo(expected.ActiveReloadWindowStart));
            Assert.That(actual.ActiveReloadWindowLength, Is.EqualTo(expected.ActiveReloadWindowLength));
            Assert.That(actual.ActiveReloadFailSeconds, Is.EqualTo(expected.ActiveReloadFailSeconds));
            Assert.That(actual.RecoilPitchDegrees, Is.EqualTo(expected.RecoilPitchDegrees));
            Assert.That(actual.RecoilYawDegrees, Is.EqualTo(expected.RecoilYawDegrees));
            Assert.That(actual.RecoilRecoveryPerSecond, Is.EqualTo(expected.RecoilRecoveryPerSecond));
            Assert.That(actual.HipSpreadDegrees, Is.EqualTo(expected.HipSpreadDegrees));
            Assert.That(actual.AdsSpreadDegrees, Is.EqualTo(expected.AdsSpreadDegrees));
            Assert.That(actual.MoveSpreadDegrees, Is.EqualTo(expected.MoveSpreadDegrees));
            Assert.That(actual.RecoilRecoveryDelaySeconds, Is.EqualTo(expected.RecoilRecoveryDelaySeconds));
            Assert.That(actual.ShotNoiseRadius, Is.EqualTo(expected.ShotNoiseRadius));
        }

        private static void AssertNoNestedParts(WeaponSpec spec)
        {
            Assert.That(spec.Trigger.Burst, Is.Null);
            Assert.That(spec.Trigger.Charge, Is.Null);
            Assert.That(spec.Trigger.SpinUp, Is.Null);
            Assert.That(spec.Trigger.Cycle, Is.Null);
            Assert.That(spec.Ammo.Kind, Is.EqualTo(AmmoKind.Magazine));
            Assert.That(spec.Ammo.ReloadStyle, Is.EqualTo(ReloadStyle.Whole));
            Assert.That(spec.Ammo.ReloadSecondsPerRound, Is.EqualTo(0f));
            Assert.That(spec.Ammo.Heat, Is.Null);
            Assert.That(spec.Round.Delivery, Is.EqualTo(RoundDelivery.Hitscan));
            Assert.That(spec.Round.PelletCount, Is.EqualTo(1));
            Assert.That(spec.Round.PelletSpreadDegrees, Is.EqualTo(0f));
            Assert.That(spec.Round.Radius, Is.EqualTo(0f));
            Assert.That(spec.Round.Penetration, Is.Null);
            Assert.That(spec.Round.Ricochet, Is.Null);
            Assert.That(spec.Round.Projectile, Is.Null);
            Assert.That(spec.Round.Homing, Is.Null);
            Assert.That(spec.Round.Beam, Is.Null);
            Assert.That(spec.Round.Spray, Is.Null);
            Assert.That(spec.Impact.Explosion, Is.Null);
            Assert.That(spec.Impact.StatusEffect, Is.Null);
            Assert.That(spec.Impact.AreaEffect, Is.Null);
            Assert.That(spec.Aim.ZoomLevels, Has.Count.EqualTo(1));
            Assert.That(spec.Aim.BreathHold, Is.Null);
            Assert.That(spec.Aim.SwayDegrees, Is.EqualTo(0f));
            Assert.That(spec.AmmoTypes, Is.Empty);
        }

        [Test]
        public void PartsWithoutNestedSpecsGiveTheSameValuesAsTheFlatConstructor()
        {
            var parts = CreatePartsRifle();

            AssertSameFlatValues(parts, CreateFlatRifle());
            AssertNoNestedParts(parts);
        }

        [Test]
        public void TheFlatConstructorBuildsPartsWithoutNestedSpecs()
        {
            var flat = CreateFlatRifle();

            AssertNoNestedParts(flat);
            Assert.That(flat.Aim.ZoomLevels[0], Is.EqualTo(40f));
            Assert.That(flat.Impact.StructureRadius, Is.EqualTo(1.5f));
        }

        [Test]
        public void OmittedPartsGiveTheSameValuesAsTheFlatConstructorWithTheDefaults()
        {
            var omitted = new WeaponSpec(_rifleId, "weapon.rifle", WeaponCategory.AssaultRifle);
            var flat = new WeaponSpec(
                _rifleId,
                "weapon.rifle",
                WeaponCategory.AssaultRifle,
                TriggerSpec.DefaultFireMode,
                AmmoSpec.DefaultMagazineSize,
                AmmoSpec.DefaultReserveAmmo,
                TriggerSpec.DefaultFireInterval,
                AmmoSpec.DefaultReloadSeconds,
                ImpactSpec.DefaultDamage,
                RoundSpec.DefaultRange,
                DamageFalloff.None);

            AssertSameFlatValues(omitted, flat);
            AssertNoNestedParts(omitted);
            Assert.That(omitted.AdsFieldOfView, Is.EqualTo(AdsZoom.DefaultFieldOfView));
        }

        [Test]
        public void OmittedPartsUseTheSharedDefaults()
        {
            var spec = new WeaponSpec(_rifleId, null, WeaponCategory.Handgun);

            Assert.That(spec.Trigger, Is.SameAs(TriggerSpec.Default));
            Assert.That(spec.Ammo, Is.SameAs(AmmoSpec.Default));
            Assert.That(spec.Round, Is.SameAs(RoundSpec.Default));
            Assert.That(spec.Impact, Is.SameAs(ImpactSpec.Default));
            Assert.That(spec.Aim, Is.SameAs(AimSpec.Default));
        }

        [Test]
        public void KeepsTheGivenParts()
        {
            var trigger = new TriggerSpec(FireMode.Semi, 0.5f, new BurstSpec(3, 0.05f), cycle: new CycleSpec(0.8f, leavesAds: true));
            var ammo = new AmmoSpec(6, 24, 0.5f, reloadStyle: ReloadStyle.PerRound, reloadSecondsPerRound: 0.4f);
            var round = new RoundSpec(
                RoundDelivery.Projectile,
                80f,
                pelletCount: 8,
                pelletSpreadDegrees: 6f,
                radius: 0.1f,
                projectile: new ProjectileSpec(40f, 1f, 5f, WorldHitResponse.Stick, "  arrow  "),
                homing: new HomingSpec(1f, 10f, 90f));
            var impact = new ImpactSpec(
                40,
                explosion: new ExplosionSpec(_blast, 80, DetonationTrigger.OnStop),
                statusEffect: new StatusEffectSpec(3f, 0.5f, 4),
                areaEffect: new AreaEffectSpec(4f, 6f, 1f, 5));
            var zoomLevels = new[] { 30f, 15f };
            var aim = new AimSpec(zoomLevels: zoomLevels, breathHold: new BreathHoldSpec(4f, 2f, 0.1f), swayDegrees: 1.5f);
            var slug = new AmmoTypeSpec(" " + _slugId + " ", " ammo.slug ", impact: new ImpactSpec(90));

            var spec = new WeaponSpec(_rifleId, "weapon.crossbow", WeaponCategory.Launcher, trigger, ammo, round, impact, aim, new[] { slug });

            Assert.That(spec.Trigger, Is.SameAs(trigger));
            Assert.That(spec.Ammo, Is.SameAs(ammo));
            Assert.That(spec.Round, Is.SameAs(round));
            Assert.That(spec.Impact, Is.SameAs(impact));
            Assert.That(spec.Aim, Is.SameAs(aim));
            Assert.That(spec.AmmoTypes, Is.EqualTo(new[] { slug }));
            Assert.That(spec.Trigger.Burst.RoundsPerBurst, Is.EqualTo(3));
            Assert.That(spec.Round.Projectile.VisualId, Is.EqualTo("arrow"));
            Assert.That(spec.Aim.ZoomLevels, Is.EqualTo(zoomLevels));
            Assert.That(spec.AdsFieldOfView, Is.EqualTo(30f));
            Assert.That(slug.Id, Is.EqualTo(_slugId));
            Assert.That(slug.NameKey, Is.EqualTo("ammo.slug"));
            Assert.That(slug.RoundOr(spec.Round), Is.SameAs(round));
            Assert.That(slug.ImpactOr(spec.Impact).Damage, Is.EqualTo(90));
        }

        [Test]
        public void TheAdsMoveSpreadIsTheMoveSpreadUnlessGiven()
        {
            Assert.That(new AimSpec(moveSpreadDegrees: 3f).AdsMoveSpreadDegrees, Is.EqualTo(3f));
            Assert.That(new AimSpec(moveSpreadDegrees: 3f, adsMoveSpreadDegrees: 0.9f).AdsMoveSpreadDegrees, Is.EqualTo(0.9f));
            Assert.That(new AimSpec(moveSpreadDegrees: 3f, adsMoveSpreadDegrees: 0f).AdsMoveSpreadDegrees, Is.EqualTo(0f));
            Assert.That(AimSpec.Default.AdsMoveSpreadDegrees, Is.EqualTo(0f));
        }

        [Test]
        public void CopiesTheZoomLevelsAndAmmoTypes()
        {
            var zoomLevels = new[] { 30f, 15f };
            var ammoTypes = new[] { new AmmoTypeSpec(_slugId, null) };
            var spec = new WeaponSpec(_rifleId, null, WeaponCategory.SniperRifle, aim: new AimSpec(zoomLevels: zoomLevels), ammoTypes: ammoTypes);

            zoomLevels[0] = 60f;
            ammoTypes[0] = new AmmoTypeSpec(_buckshotId, null);

            Assert.That(spec.Aim.ZoomLevels[0], Is.EqualTo(30f));
            Assert.That(spec.AmmoTypes[0].Id, Is.EqualTo(_slugId));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        public void EmptyReferenceIdsBecomeEmpty(string visualId)
        {
            Assert.That(new ProjectileSpec(10f, 0f, 1f, visualId: visualId).VisualId, Is.Empty);
            Assert.That(new BeamSpec(0.1f, visualId).VisualId, Is.Empty);
            Assert.That(new SpraySpec(0.1f, 10f, visualId).VisualId, Is.Empty);
            Assert.That(new StatusEffectSpec(1f, 0.5f, 1, visualId).VisualId, Is.Empty);
            Assert.That(new AreaEffectSpec(1f, 1f, 0.5f, 1, visualId: visualId).VisualId, Is.Empty);
        }

        [Test]
        public void AmmoTypeKeepsOnlyTheTrimmedNameKey()
        {
            Assert.That(new AmmoTypeSpec(_slugId, " ").NameKey, Is.Empty);
            Assert.That(new AmmoTypeSpec(_slugId, " ammo.slug ").NameKey, Is.EqualTo("ammo.slug"));
            Assert.That(new AmmoTypeSpec(_slugId, "ammo.slug").Id, Is.EqualTo(_slugId));
        }

        [Test]
        public void WithFireIntervalReturnsItselfWhenTheIntervalIsTheSame()
        {
            var spec = CreatePartsRifle();

            Assert.That(spec.WithFireInterval(0.15f), Is.SameAs(spec));
        }

        [Test]
        public void WithFireIntervalChangesOnlyTheInterval()
        {
            var burst = new BurstSpec(3, 0.05f);
            var spec = new WeaponSpec(
                _rifleId, "weapon.rifle", WeaponCategory.AssaultRifle,
                new TriggerSpec(FireMode.Semi, 0.4f, burst),
                ammoTypes: new[] { new AmmoTypeSpec(_slugId, null) },
                shotNoiseRadius: 7f);

            var copy = spec.WithFireInterval(0f);

            Assert.That(copy, Is.Not.SameAs(spec));
            Assert.That(copy.FireInterval, Is.EqualTo(0f));
            Assert.That(spec.FireInterval, Is.EqualTo(0.4f));
            Assert.That(copy.FireMode, Is.EqualTo(FireMode.Semi));
            Assert.That(copy.Trigger.Burst, Is.SameAs(burst));
            Assert.That(copy.Ammo, Is.SameAs(spec.Ammo));
            Assert.That(copy.Round, Is.SameAs(spec.Round));
            Assert.That(copy.Impact, Is.SameAs(spec.Impact));
            Assert.That(copy.Aim, Is.SameAs(spec.Aim));
            Assert.That(copy.AmmoTypes, Is.EqualTo(spec.AmmoTypes));
            Assert.That(copy.Id, Is.EqualTo(spec.Id));
            Assert.That(copy.NameKey, Is.EqualTo(spec.NameKey));
            Assert.That(copy.Category, Is.EqualTo(spec.Category));
            Assert.That(copy.ShotNoiseRadius, Is.EqualTo(7f));
        }

        [TestCase(-0.1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void WithFireIntervalRejectsAnInvalidInterval(float fireInterval)
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(() => CreatePartsRifle().WithFireInterval(fireInterval));
            Assert.That(e.ParamName, Is.EqualTo("fireInterval"));
        }

        [TestCase(0f)]
        [TestCase(0.5f)]
        [TestCase(1f)]
        public void AcceptsFractionsAtTheBounds(float fraction)
        {
            Assert.That(new ChargeSpec(1f, fraction, 1f).MinChargeFraction, Is.EqualTo(fraction));
            Assert.That(new PenetrationSpec(1, fraction, 0f).DamageMultiplierPerPenetration, Is.EqualTo(fraction));
            Assert.That(new RicochetSpec(1, 90f, fraction).DamageMultiplierPerBounce, Is.EqualTo(fraction));
            Assert.That(new BreathHoldSpec(1f, 0f, fraction).SwayMultiplier, Is.EqualTo(fraction));
        }

        [Test]
        public void AcceptsADetonationAtLifetimeEndForAProjectileAmmoType()
        {
            var arrow = new RoundSpec(RoundDelivery.Projectile, 50f, projectile: new ProjectileSpec(30f, 1f, 4f));
            var explosiveArrow = new AmmoTypeSpec(
                _slugId, null, arrow, new ImpactSpec(10, explosion: new ExplosionSpec(_blast, detonation: DetonationTrigger.OnLifetimeEnd)));

            Assert.That(
                () => new WeaponSpec(_rifleId, null, WeaponCategory.Launcher, ammoTypes: new[] { explosiveArrow }),
                Throws.Nothing);
        }

        private static object CreateWithInvalid(string key)
        {
            var hitscan = RoundSpec.Default;
            var onStop = new ImpactSpec(explosion: new ExplosionSpec(_blast, detonation: DetonationTrigger.OnStop));

            switch (key)
            {
                case "TriggerSpec.fireMode": return new TriggerSpec((FireMode)99);
                case "TriggerSpec.fireInterval": return new TriggerSpec(fireInterval: -0.1f);
                case "BurstSpec.roundsPerBurst": return new BurstSpec(1, 0.05f);
                case "BurstSpec.roundIntervalSeconds": return new BurstSpec(3, float.NaN);
                case "ChargeSpec.chargeSeconds": return new ChargeSpec(0f, 0f, 1f);
                case "ChargeSpec.minChargeFraction": return new ChargeSpec(1f, 1.1f, 1f);
                case "ChargeSpec.fullChargeDamageMultiplier": return new ChargeSpec(1f, 0f, float.PositiveInfinity);
                case "ChargeSpec.fullChargeSpeedMultiplier": return new ChargeSpec(1f, 0f, 1f, fullChargeSpeedMultiplier: 0f);
                case "SpinUpSpec.spinUpSeconds": return new SpinUpSpec(0f, 2f, 1f);
                case "SpinUpSpec.startIntervalMultiplier": return new SpinUpSpec(1f, 0.9f, 1f);
                case "SpinUpSpec.spinDownSeconds": return new SpinUpSpec(1f, 2f, -1f);
                case "CycleSpec.cycleSeconds": return new CycleSpec(0f);
                case "HeatSpec.heatPerShot": return new HeatSpec(0f, 1f, 0f, 1f);
                case "HeatSpec.heatPerShot.overOne": return new HeatSpec(1.1f, 1f, 0f, 1f);
                case "HeatSpec.coolPerSecond": return new HeatSpec(0.1f, 0f, 0f, 1f);
                case "HeatSpec.coolDelaySeconds": return new HeatSpec(0.1f, 1f, -1f, 1f);
                case "HeatSpec.overheatSeconds": return new HeatSpec(0.1f, 1f, 0f, float.NaN);
                case "AmmoSpec.magazineSize": return new AmmoSpec(0);
                case "AmmoSpec.reserveAmmo": return new AmmoSpec(reserveAmmo: -1);
                case "AmmoSpec.reloadSeconds": return new AmmoSpec(reloadSeconds: float.NaN);
                case "AmmoSpec.activeReloadWindowStart": return new AmmoSpec(activeReloadWindowStart: 1f);
                case "AmmoSpec.activeReloadWindowLength": return new AmmoSpec(activeReloadWindowStart: 0.9f, activeReloadWindowLength: 0.2f);
                case "AmmoSpec.activeReloadFailSeconds": return new AmmoSpec(activeReloadFailSeconds: -1f);
                case "AmmoSpec.reloadStyle": return new AmmoSpec(reloadStyle: (ReloadStyle)9);
                case "AmmoSpec.reloadSecondsPerRound": return new AmmoSpec(reloadStyle: ReloadStyle.PerRound, reloadSecondsPerRound: 0f);
                case "AmmoSpec.reloadSecondsPerRound.whole": return new AmmoSpec(reloadSecondsPerRound: -1f);
                case "AmmoSpec.kind": return new AmmoSpec(kind: (AmmoKind)9);
                case "AmmoSpec.heat.missing": return new AmmoSpec(kind: AmmoKind.Heat);
                case "AmmoSpec.heat.unused": return new AmmoSpec(heat: new HeatSpec(0.1f, 1f, 0f, 1f));
                case "RoundSpec.delivery": return new RoundSpec((RoundDelivery)9);
                case "RoundSpec.range": return new RoundSpec(range: 0f);
                case "RoundSpec.pelletCount": return new RoundSpec(pelletCount: 0);
                case "RoundSpec.pelletSpreadDegrees": return new RoundSpec(pelletSpreadDegrees: 90.1f);
                case "RoundSpec.radius": return new RoundSpec(radius: -0.1f);
                case "RoundSpec.projectile.missing": return new RoundSpec(RoundDelivery.Projectile);
                case "RoundSpec.projectile.unused": return new RoundSpec(projectile: new ProjectileSpec(10f, 0f, 1f));
                case "RoundSpec.beam.missing": return new RoundSpec(RoundDelivery.Beam);
                case "RoundSpec.beam.unused": return new RoundSpec(beam: new BeamSpec(0.1f));
                case "RoundSpec.spray.missing": return new RoundSpec(RoundDelivery.Spray);
                case "RoundSpec.spray.unused": return new RoundSpec(spray: new SpraySpec(0.1f, 10f));
                case "RoundSpec.homing": return new RoundSpec(homing: new HomingSpec(0f, 10f, 90f));
                case "PenetrationSpec.maxPenetrations": return new PenetrationSpec(0, 0.5f, 1f);
                case "PenetrationSpec.damageMultiplierPerPenetration": return new PenetrationSpec(1, 1.1f, 1f);
                case "PenetrationSpec.maxThickness": return new PenetrationSpec(1, 0.5f, float.PositiveInfinity);
                case "RicochetSpec.maxBounces": return new RicochetSpec(0, 10f, 0.5f);
                case "RicochetSpec.maxGrazingAngleDegrees": return new RicochetSpec(1, 0f, 0.5f);
                case "RicochetSpec.damageMultiplierPerBounce": return new RicochetSpec(1, 10f, -0.1f);
                case "ProjectileSpec.speed": return new ProjectileSpec(0f, 0f, 1f);
                case "ProjectileSpec.gravityMultiplier": return new ProjectileSpec(10f, -1f, 1f);
                case "ProjectileSpec.lifetimeSeconds": return new ProjectileSpec(10f, 0f, 0f);
                case "ProjectileSpec.onWorldHit": return new ProjectileSpec(10f, 0f, 1f, (WorldHitResponse)9);
                case "HomingSpec.lockOnSeconds": return new HomingSpec(-1f, 10f, 90f);
                case "HomingSpec.lockOnConeDegrees": return new HomingSpec(0f, 0f, 90f);
                case "HomingSpec.turnDegreesPerSecond": return new HomingSpec(0f, 10f, 0f);
                case "BeamSpec.tickSeconds": return new BeamSpec(0f);
                case "SpraySpec.tickSeconds": return new SpraySpec(0f, 10f);
                case "SpraySpec.coneDegrees": return new SpraySpec(0.1f, 90.1f);
                case "ImpactSpec.damage": return new ImpactSpec(-1);
                case "ImpactSpec.structureRadius": return new ImpactSpec(structureRadius: float.PositiveInfinity);
                case "ExplosionSpec.damage": return new ExplosionSpec(null);
                case "ExplosionSpec.structureDamageAtCenter": return new ExplosionSpec(_blast, -1);
                case "ExplosionSpec.detonation": return new ExplosionSpec(_blast, 0, (DetonationTrigger)9);
                case "StatusEffectSpec.durationSeconds": return new StatusEffectSpec(0f, 0.5f, 1);
                case "StatusEffectSpec.tickSeconds": return new StatusEffectSpec(1f, 0f, 1);
                case "StatusEffectSpec.damagePerTick": return new StatusEffectSpec(1f, 0.5f, -1);
                case "AreaEffectSpec.radius": return new AreaEffectSpec(0f, 1f, 0.5f, 1);
                case "AreaEffectSpec.durationSeconds": return new AreaEffectSpec(1f, 0f, 0.5f, 1);
                case "AreaEffectSpec.tickSeconds": return new AreaEffectSpec(1f, 1f, 0f, 1);
                case "AreaEffectSpec.damagePerTick": return new AreaEffectSpec(1f, 1f, 0.5f, -1);
                case "AimSpec.hipSpreadDegrees": return new AimSpec(hipSpreadDegrees: 90.1f);
                case "AimSpec.adsSpreadDegrees": return new AimSpec(adsSpreadDegrees: -0.1f);
                case "AimSpec.moveSpreadDegrees": return new AimSpec(moveSpreadDegrees: float.NaN);
                case "AimSpec.recoilPitchDegrees": return new AimSpec(recoilPitchDegrees: 90.1f);
                case "AimSpec.recoilYawDegrees": return new AimSpec(recoilYawDegrees: -0.1f);
                case "AimSpec.recoilRecoveryPerSecond": return new AimSpec(recoilRecoveryPerSecond: float.PositiveInfinity);
                case "AimSpec.recoilRecoveryDelaySeconds": return new AimSpec(recoilRecoveryDelaySeconds: -0.1f);
                case "AimSpec.blindFireSpreadDegrees": return new AimSpec(blindFireSpreadDegrees: 90.1f);
                case "AimSpec.zoomLevels": return new AimSpec(zoomLevels: _zoomBeyondTheProjection);
                case "AimSpec.zoomLevels.empty": return new AimSpec(zoomLevels: Array.Empty<float>());
                case "AimSpec.swayDegrees": return new AimSpec(swayDegrees: -1f);
                case "AimSpec.adsMoveSpreadDegrees": return new AimSpec(adsMoveSpreadDegrees: 90.1f);
                case "BreathHoldSpec.maxHoldSeconds": return new BreathHoldSpec(0f, 1f, 0.5f);
                case "BreathHoldSpec.recoverSeconds": return new BreathHoldSpec(1f, -1f, 0.5f);
                case "BreathHoldSpec.swayMultiplier": return new BreathHoldSpec(1f, 1f, 1.1f);
                case "AmmoTypeSpec.id": return new AmmoTypeSpec("slug", null);
                case "AmmoTypeSpec.id.null": return new AmmoTypeSpec(null, null);
                case "WeaponSpec.id": return new WeaponSpec("rifle", null, WeaponCategory.AssaultRifle);
                case "WeaponSpec.category": return new WeaponSpec(_rifleId, null, (WeaponCategory)99);
                case "WeaponSpec.shotNoiseRadius": return new WeaponSpec(_rifleId, null, WeaponCategory.AssaultRifle, shotNoiseRadius: -1f);
                case "WeaponSpec.impact": return new WeaponSpec(_rifleId, null, WeaponCategory.Launcher, round: hitscan, impact: onStop);
                case "WeaponSpec.ammoTypes.null":
                    return new WeaponSpec(_rifleId, null, WeaponCategory.Shotgun, ammoTypes: new AmmoTypeSpec[] { null });
                case "WeaponSpec.ammoTypes.duplicate":
                    return new WeaponSpec(
                        _rifleId, null, WeaponCategory.Shotgun,
                        ammoTypes: new[] { new AmmoTypeSpec(_slugId, null), new AmmoTypeSpec(" " + _slugId, "ammo.other") });
                case "WeaponSpec.ammoTypes.detonation":
                    return new WeaponSpec(_rifleId, null, WeaponCategory.Shotgun, ammoTypes: new[] { new AmmoTypeSpec(_buckshotId, null, impact: onStop) });
                default:
                    throw new ArgumentException("unknown case " + key, nameof(key));
            }
        }

        [TestCase("TriggerSpec.fireMode", "fireMode")]
        [TestCase("TriggerSpec.fireInterval", "fireInterval")]
        [TestCase("BurstSpec.roundsPerBurst", "roundsPerBurst")]
        [TestCase("BurstSpec.roundIntervalSeconds", "roundIntervalSeconds")]
        [TestCase("ChargeSpec.chargeSeconds", "chargeSeconds")]
        [TestCase("ChargeSpec.minChargeFraction", "minChargeFraction")]
        [TestCase("ChargeSpec.fullChargeDamageMultiplier", "fullChargeDamageMultiplier")]
        [TestCase("ChargeSpec.fullChargeSpeedMultiplier", "fullChargeSpeedMultiplier")]
        [TestCase("SpinUpSpec.spinUpSeconds", "spinUpSeconds")]
        [TestCase("SpinUpSpec.startIntervalMultiplier", "startIntervalMultiplier")]
        [TestCase("SpinUpSpec.spinDownSeconds", "spinDownSeconds")]
        [TestCase("CycleSpec.cycleSeconds", "cycleSeconds")]
        [TestCase("HeatSpec.heatPerShot", "heatPerShot")]
        [TestCase("HeatSpec.heatPerShot.overOne", "heatPerShot")]
        [TestCase("HeatSpec.coolPerSecond", "coolPerSecond")]
        [TestCase("HeatSpec.coolDelaySeconds", "coolDelaySeconds")]
        [TestCase("HeatSpec.overheatSeconds", "overheatSeconds")]
        [TestCase("AmmoSpec.magazineSize", "magazineSize")]
        [TestCase("AmmoSpec.reserveAmmo", "reserveAmmo")]
        [TestCase("AmmoSpec.reloadSeconds", "reloadSeconds")]
        [TestCase("AmmoSpec.activeReloadWindowStart", "activeReloadWindowStart")]
        [TestCase("AmmoSpec.activeReloadWindowLength", "activeReloadWindowLength")]
        [TestCase("AmmoSpec.activeReloadFailSeconds", "activeReloadFailSeconds")]
        [TestCase("AmmoSpec.reloadStyle", "reloadStyle")]
        [TestCase("AmmoSpec.reloadSecondsPerRound", "reloadSecondsPerRound")]
        [TestCase("AmmoSpec.reloadSecondsPerRound.whole", "reloadSecondsPerRound")]
        [TestCase("AmmoSpec.kind", "kind")]
        [TestCase("AmmoSpec.heat.missing", "heat")]
        [TestCase("AmmoSpec.heat.unused", "heat")]
        [TestCase("RoundSpec.delivery", "delivery")]
        [TestCase("RoundSpec.range", "range")]
        [TestCase("RoundSpec.pelletCount", "pelletCount")]
        [TestCase("RoundSpec.pelletSpreadDegrees", "pelletSpreadDegrees")]
        [TestCase("RoundSpec.radius", "radius")]
        [TestCase("RoundSpec.projectile.missing", "projectile")]
        [TestCase("RoundSpec.projectile.unused", "projectile")]
        [TestCase("RoundSpec.beam.missing", "beam")]
        [TestCase("RoundSpec.beam.unused", "beam")]
        [TestCase("RoundSpec.spray.missing", "spray")]
        [TestCase("RoundSpec.spray.unused", "spray")]
        [TestCase("RoundSpec.homing", "homing")]
        [TestCase("PenetrationSpec.maxPenetrations", "maxPenetrations")]
        [TestCase("PenetrationSpec.damageMultiplierPerPenetration", "damageMultiplierPerPenetration")]
        [TestCase("PenetrationSpec.maxThickness", "maxThickness")]
        [TestCase("RicochetSpec.maxBounces", "maxBounces")]
        [TestCase("RicochetSpec.maxGrazingAngleDegrees", "maxGrazingAngleDegrees")]
        [TestCase("RicochetSpec.damageMultiplierPerBounce", "damageMultiplierPerBounce")]
        [TestCase("ProjectileSpec.speed", "speed")]
        [TestCase("ProjectileSpec.gravityMultiplier", "gravityMultiplier")]
        [TestCase("ProjectileSpec.lifetimeSeconds", "lifetimeSeconds")]
        [TestCase("ProjectileSpec.onWorldHit", "onWorldHit")]
        [TestCase("HomingSpec.lockOnSeconds", "lockOnSeconds")]
        [TestCase("HomingSpec.lockOnConeDegrees", "lockOnConeDegrees")]
        [TestCase("HomingSpec.turnDegreesPerSecond", "turnDegreesPerSecond")]
        [TestCase("BeamSpec.tickSeconds", "tickSeconds")]
        [TestCase("SpraySpec.tickSeconds", "tickSeconds")]
        [TestCase("SpraySpec.coneDegrees", "coneDegrees")]
        [TestCase("ImpactSpec.damage", "damage")]
        [TestCase("ImpactSpec.structureRadius", "structureRadius")]
        [TestCase("ExplosionSpec.damage", "damage")]
        [TestCase("ExplosionSpec.structureDamageAtCenter", "structureDamageAtCenter")]
        [TestCase("ExplosionSpec.detonation", "detonation")]
        [TestCase("StatusEffectSpec.durationSeconds", "durationSeconds")]
        [TestCase("StatusEffectSpec.tickSeconds", "tickSeconds")]
        [TestCase("StatusEffectSpec.damagePerTick", "damagePerTick")]
        [TestCase("AreaEffectSpec.radius", "radius")]
        [TestCase("AreaEffectSpec.durationSeconds", "durationSeconds")]
        [TestCase("AreaEffectSpec.tickSeconds", "tickSeconds")]
        [TestCase("AreaEffectSpec.damagePerTick", "damagePerTick")]
        [TestCase("AimSpec.hipSpreadDegrees", "hipSpreadDegrees")]
        [TestCase("AimSpec.adsSpreadDegrees", "adsSpreadDegrees")]
        [TestCase("AimSpec.moveSpreadDegrees", "moveSpreadDegrees")]
        [TestCase("AimSpec.recoilPitchDegrees", "recoilPitchDegrees")]
        [TestCase("AimSpec.recoilYawDegrees", "recoilYawDegrees")]
        [TestCase("AimSpec.recoilRecoveryPerSecond", "recoilRecoveryPerSecond")]
        [TestCase("AimSpec.recoilRecoveryDelaySeconds", "recoilRecoveryDelaySeconds")]
        [TestCase("AimSpec.blindFireSpreadDegrees", "blindFireSpreadDegrees")]
        [TestCase("AimSpec.zoomLevels", "zoomLevels")]
        [TestCase("AimSpec.zoomLevels.empty", "zoomLevels")]
        [TestCase("AimSpec.swayDegrees", "swayDegrees")]
        [TestCase("AimSpec.adsMoveSpreadDegrees", "adsMoveSpreadDegrees")]
        [TestCase("BreathHoldSpec.maxHoldSeconds", "maxHoldSeconds")]
        [TestCase("BreathHoldSpec.recoverSeconds", "recoverSeconds")]
        [TestCase("BreathHoldSpec.swayMultiplier", "swayMultiplier")]
        [TestCase("AmmoTypeSpec.id", "id")]
        [TestCase("AmmoTypeSpec.id.null", "id")]
        [TestCase("WeaponSpec.id", "id")]
        [TestCase("WeaponSpec.category", "category")]
        [TestCase("WeaponSpec.shotNoiseRadius", "shotNoiseRadius")]
        [TestCase("WeaponSpec.impact", "impact")]
        [TestCase("WeaponSpec.ammoTypes.null", "ammoTypes")]
        [TestCase("WeaponSpec.ammoTypes.duplicate", "ammoTypes")]
        [TestCase("WeaponSpec.ammoTypes.detonation", "ammoTypes")]
        public void RejectsAnInvalidPart(string key, string paramName)
        {
            Assert.That(
                () => CreateWithInvalid(key),
                Throws.InstanceOf<ArgumentException>().With.Property("ParamName").EqualTo(paramName));
        }
    }
}
