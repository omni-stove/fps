using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class WeaponSpecJsonTests
    {
        private const string _rifleId = "6c1f3b2a-8d4e-4f7a-9b0c-2e5d7a1f4c83";
        private const string _handgunId = "b4e2a9d7-3c16-4a58-8f2e-7d0b9c6a1e45";
        private const string _launcherId = "e7a3c5d1-2f84-4b69-a0d3-5c8e1b7f2a96";
        private const string _standardId = "1a2b3c4d-5e6f-4a7b-8c9d-0e1f2a3b4c5d";
        private const string _piercingId = "2b3c4d5e-6f7a-4b8c-9d0e-1f2a3b4c5d6e";

        // 部品に分ける前のサンプルの武器の JSON (Samples~/Demo/Weapons/Json の Rifle・Handgun・Launcher) をそのまま写したもの。
        private const string _legacyRifle = @"{
  ""id"": ""6c1f3b2a-8d4e-4f7a-9b0c-2e5d7a1f4c83"",
  ""nameKey"": ""weapon.rifle"",
  ""category"": ""AssaultRifle"",
  ""fireMode"": ""Auto"",
  ""magazineSize"": 30,
  ""reserveAmmo"": 90,
  ""fireInterval"": 0.1,
  ""reloadSeconds"": 2,
  ""damage"": 25,
  ""range"": 100,
  ""falloff"": {
    ""startDistance"": 20,
    ""endDistance"": 60,
    ""minMultiplier"": 0.5
  },
  ""explosionRadius"": 0,
  ""adsFieldOfView"": 45,
  ""blindFireSpreadDegrees"": 8,
  ""activeReloadWindowStart"": 0.5,
  ""activeReloadWindowLength"": 0.15,
  ""activeReloadFailSeconds"": 0.6,
  ""recoilPitchDegrees"": 0.5,
  ""recoilYawDegrees"": 0.2,
  ""recoilRecoveryPerSecond"": 10,
  ""hipSpreadDegrees"": 3,
  ""adsSpreadDegrees"": 0.5,
  ""moveSpreadDegrees"": 3
}";

        private const string _legacyHandgun = @"{
  ""id"": ""b4e2a9d7-3c16-4a58-8f2e-7d0b9c6a1e45"",
  ""nameKey"": ""weapon.handgun"",
  ""category"": ""Handgun"",
  ""fireMode"": ""Semi"",
  ""magazineSize"": 12,
  ""reserveAmmo"": 48,
  ""fireInterval"": 0.2,
  ""reloadSeconds"": 1.5,
  ""damage"": 34,
  ""range"": 60,
  ""falloff"": {
    ""startDistance"": 15,
    ""endDistance"": 40,
    ""minMultiplier"": 0.6
  },
  ""explosionRadius"": 0,
  ""adsFieldOfView"": 55,
  ""blindFireSpreadDegrees"": 12,
  ""activeReloadWindowStart"": 0.45,
  ""activeReloadWindowLength"": 0.2,
  ""activeReloadFailSeconds"": 0.4,
  ""recoilPitchDegrees"": 2.5,
  ""recoilYawDegrees"": 0.6,
  ""recoilRecoveryPerSecond"": 60,
  ""hipSpreadDegrees"": 2,
  ""adsSpreadDegrees"": 0.8,
  ""moveSpreadDegrees"": 1.5
}";

        private const string _legacyLauncher = @"{
  ""id"": ""e7a3c5d1-2f84-4b69-a0d3-5c8e1b7f2a96"",
  ""nameKey"": ""weapon.launcher"",
  ""category"": ""Launcher"",
  ""fireMode"": ""Semi"",
  ""magazineSize"": 1,
  ""reserveAmmo"": 4,
  ""fireInterval"": 0.5,
  ""reloadSeconds"": 3,
  ""damage"": 120,
  ""range"": 150,
  ""falloff"": ""None"",
  ""explosionRadius"": 4,
  ""adsFieldOfView"": null,
  ""blindFireSpreadDegrees"": 0,
  ""activeReloadWindowStart"": 0.5,
  ""activeReloadWindowLength"": 0.15,
  ""activeReloadFailSeconds"": 0.5,
  ""recoilPitchDegrees"": 0,
  ""recoilYawDegrees"": 0,
  ""recoilRecoveryPerSecond"": 0,
  ""hipSpreadDegrees"": 0,
  ""adsSpreadDegrees"": 0,
  ""moveSpreadDegrees"": 0
}";

        private static readonly float[] _everyPartZoom = { 30f, 15f };

        private static WeaponSpec Rifle() => new WeaponSpec(
            _rifleId, "weapon.rifle", WeaponCategory.AssaultRifle, FireMode.Auto, 30, 90, 0.1f, 2f, 25, 100f, new DamageFalloff(20f, 60f, 0.5f),
            adsFieldOfView: 45f, blindFireSpreadDegrees: 8f, activeReloadWindowStart: 0.5f, activeReloadWindowLength: 0.15f,
            activeReloadFailSeconds: 0.6f, recoilPitchDegrees: 0.5f, recoilYawDegrees: 0.2f, recoilRecoveryPerSecond: 10f,
            hipSpreadDegrees: 3f, adsSpreadDegrees: 0.5f, moveSpreadDegrees: 3f);

        private static WeaponSpec Handgun() => new WeaponSpec(
            _handgunId, "weapon.handgun", WeaponCategory.Handgun, FireMode.Semi, 12, 48, 0.2f, 1.5f, 34, 60f, new DamageFalloff(15f, 40f, 0.6f),
            adsFieldOfView: 55f, blindFireSpreadDegrees: 12f, activeReloadWindowStart: 0.45f, activeReloadWindowLength: 0.2f,
            activeReloadFailSeconds: 0.4f, recoilPitchDegrees: 2.5f, recoilYawDegrees: 0.6f, recoilRecoveryPerSecond: 60f,
            hipSpreadDegrees: 2f, adsSpreadDegrees: 0.8f, moveSpreadDegrees: 1.5f);

        private static WeaponSpec Launcher() => new WeaponSpec(
            _launcherId, "weapon.launcher", WeaponCategory.Launcher, FireMode.Semi, 1, 4, 0.5f, 3f, 120, 150f, DamageFalloff.None,
            explosionRadius: 4f);

        // どの部品も省略せずに書いた武器。Write が書く項目をすべて通す。
        private static WeaponSpec EveryPart()
        {
            var burning = new StatusEffectSpec(3f, 0.5f, 4, "fx.burning", "napalm");
            return new WeaponSpec(
                _rifleId,
                "weapon.everything",
                WeaponCategory.Launcher,
                new TriggerSpec(
                    FireMode.Semi,
                    0.25f,
                    new BurstSpec(3, 0.05f),
                    new ChargeSpec(1.5f, 0.2f, 2.5f, firesWhenFull: true, firesBelowMin: true, fullChargeSpeedMultiplier: 1.75f),
                    new SpinUpSpec(0.8f, 3f, 0.4f),
                    new CycleSpec(0.9f, leavesAds: true)),
                new AmmoSpec(1, 6, 0f, 0.3f, 0.2f, 0.7f, ReloadStyle.PerRound, 0.6f, AmmoKind.Heat, new HeatSpec(0.1f, 0.5f, 0.3f, 2f)),
                new RoundSpec(
                    RoundDelivery.Projectile,
                    300f,
                    pelletCount: 8,
                    pelletSpreadDegrees: 4f,
                    radius: 0.2f,
                    penetration: new PenetrationSpec(
                        2, 0.6f, 0.5f, new[] { new PenetrationMaterialSpec("metal", 0f), new PenetrationMaterialSpec("wood", 2f) }),
                    ricochet: new RicochetSpec(1, 20f, 0.5f),
                    projectile: new ProjectileSpec(40f, 0.5f, 6f, WorldHitResponse.Stick, "fx.bolt"),
                    homing: new HomingSpec(1f, 10f, 90f)),
                new ImpactSpec(
                    90,
                    new DamageFalloff(10f, 30f, 0.25f),
                    3f,
                    new ExplosionSpec(new ExplosionDamage(150, 6f, 1.5f), 200, DetonationTrigger.OnLifetimeEnd),
                    burning,
                    new AreaEffectSpec(4f, 8f, 1f, 5, burning, "fx.fire")),
                new AimSpec(1f, 0.2f, 0.5f, 3f, 1f, 20f, 0.25f, 6f, _everyPartZoom, new BreathHoldSpec(4f, 2f, 0.1f), 1.5f),
                new[]
                {
                    new AmmoTypeSpec(_standardId, "ammo.everything.standard"),
                    new AmmoTypeSpec(
                        _piercingId,
                        "ammo.everything.piercing",
                        new RoundSpec(RoundDelivery.Beam, 80f, beam: new BeamSpec(0.1f, "fx.beam")),
                        new ImpactSpec(40, structureRadius: 1f)),
                },
                shotNoiseRadius: 12f);
        }

        [Test]
        public void TheFlatSampleJsonReadsAsTheSameWeapons()
        {
            Assert.That(WeaponSpecJson.Write(WeaponSpecJson.Parse(_legacyRifle, "Rifle.json")), Is.EqualTo(WeaponSpecJson.Write(Rifle())));
            Assert.That(WeaponSpecJson.Write(WeaponSpecJson.Parse(_legacyHandgun, "Handgun.json")), Is.EqualTo(WeaponSpecJson.Write(Handgun())));
            Assert.That(WeaponSpecJson.Write(WeaponSpecJson.Parse(_legacyLauncher, "Launcher.json")), Is.EqualTo(WeaponSpecJson.Write(Launcher())));
        }

        [TestCase(_legacyRifle)]
        [TestCase(_legacyHandgun)]
        [TestCase(_legacyLauncher)]
        public void TheFlatSampleJsonIsWrittenBackInPartsAndReadsBackTheSame(string legacy)
        {
            var read = WeaponSpecJson.Parse(legacy, "legacy.json");

            var written = WeaponSpecJson.Write(read);
            var reread = WeaponSpecJson.Parse(written, "parts.json");

            Assert.That(written, Does.Contain("\"trigger\": {").And.Contain("\"ammo\": {").And.Contain("\"round\": {"));
            Assert.That(written, Does.Contain("\"impact\": {").And.Contain("\"aim\": {"));
            Assert.That(written, Does.Not.Contain("\"explosionRadius\"").And.Not.Contain("\"adsFieldOfView\""));
            Assert.That(WeaponSpecJson.Write(reread), Is.EqualTo(written));
            Assert.That(reread.Damage, Is.EqualTo(read.Damage));
            Assert.That(reread.Range, Is.EqualTo(read.Range));
            Assert.That(reread.MagazineSize, Is.EqualTo(read.MagazineSize));
            Assert.That(reread.FireInterval, Is.EqualTo(read.FireInterval));
            Assert.That(reread.ExplosionRadius, Is.EqualTo(read.ExplosionRadius));
            Assert.That(reread.AdsFieldOfView, Is.EqualTo(read.AdsFieldOfView));
            Assert.That(reread.Falloff.MinMultiplier, Is.EqualTo(read.Falloff.MinMultiplier));
        }

        [Test]
        public void NoFalloffIsWrittenAsNoneAndReadAsTheSharedValue()
        {
            var written = WeaponSpecJson.Write(Launcher());

            Assert.That(written, Does.Contain("\"falloff\": \"None\""));
            Assert.That(WeaponSpecJson.Parse(written, "Launcher.json").Falloff, Is.SameAs(DamageFalloff.None));
        }

        [Test]
        public void EveryPartReadsBackTheSame()
        {
            var written = WeaponSpecJson.Write(EveryPart());
            var read = WeaponSpecJson.Parse(written, "Everything.json");

            Assert.That(WeaponSpecJson.Write(read), Is.EqualTo(written));
            Assert.That(read.Trigger.Burst.RoundsPerBurst, Is.EqualTo(3));
            Assert.That(read.Trigger.Charge.FiresWhenFull, Is.True);
            Assert.That(read.Trigger.Charge.FiresBelowMin, Is.True);
            Assert.That(read.Trigger.Charge.FullChargeSpeedMultiplier, Is.EqualTo(1.75f));
            Assert.That(read.Trigger.SpinUp.StartIntervalMultiplier, Is.EqualTo(3f));
            Assert.That(read.Trigger.Cycle.LeavesAds, Is.True);
            Assert.That(read.Ammo.Kind, Is.EqualTo(AmmoKind.Heat));
            Assert.That(read.Ammo.Heat.OverheatSeconds, Is.EqualTo(2f));
            Assert.That(read.Ammo.ReloadStyle, Is.EqualTo(ReloadStyle.PerRound));
            Assert.That(read.Round.Delivery, Is.EqualTo(RoundDelivery.Projectile));
            Assert.That(read.Round.Projectile.OnWorldHit, Is.EqualTo(WorldHitResponse.Stick));
            Assert.That(read.Round.Projectile.VisualId, Is.EqualTo("fx.bolt"));
            Assert.That(read.Round.Homing.TurnDegreesPerSecond, Is.EqualTo(90f));
            Assert.That(read.Round.Penetration.MaxPenetrations, Is.EqualTo(2));
            Assert.That(read.Round.Penetration.Materials.Count, Is.EqualTo(2));
            Assert.That(read.Round.Penetration.PenetrabilityOf("metal"), Is.EqualTo(0f));
            Assert.That(read.Round.Penetration.PenetrabilityOf("wood"), Is.EqualTo(2f));
            Assert.That(read.Round.Ricochet.MaxGrazingAngleDegrees, Is.EqualTo(20f));
            Assert.That(read.Impact.Explosion.Damage.FullDamageRadius, Is.EqualTo(1.5f));
            Assert.That(read.Impact.Explosion.Detonation, Is.EqualTo(DetonationTrigger.OnLifetimeEnd));
            Assert.That(read.Impact.AreaEffect.StatusEffect.DamagePerTick, Is.EqualTo(4));
            Assert.That(read.Impact.StatusEffect.KindId, Is.EqualTo("napalm"));
            Assert.That(read.Impact.AreaEffect.StatusEffect.KindId, Is.EqualTo("napalm"));
            Assert.That(read.Aim.ZoomLevels, Is.EqualTo(_everyPartZoom));
            Assert.That(read.Aim.BreathHold.SwayMultiplier, Is.EqualTo(0.1f));
            Assert.That(read.AmmoTypes, Has.Count.EqualTo(2));
            Assert.That(read.AmmoTypes[0].Round, Is.Null);
            Assert.That(read.AmmoTypes[1].Round.Beam.VisualId, Is.EqualTo("fx.beam"));
            Assert.That(read.AmmoTypes[1].Impact.Damage, Is.EqualTo(40));
            Assert.That(read.ShotNoiseRadius, Is.EqualTo(12f));
        }

        [Test]
        public void TheDescriptionReadsBackAfterTheNameKey()
        {
            const string json = "{\"id\": \"" + _rifleId + "\", \"nameKey\": \"weapon.rifle\", "
                + "\"description\": \"AK 系の \\\"7.62 mm\\\" 小銃\", \"category\": \"AssaultRifle\"}";

            var read = WeaponSpecJson.Parse(json, "described.json");
            var written = WeaponSpecJson.Write(read);

            Assert.That(read.Description, Is.EqualTo("AK 系の \"7.62 mm\" 小銃"));
            Assert.That(written, Does.Contain("  \"nameKey\": \"weapon.rifle\",\n  \"description\": \"AK 系の \\\"7.62 mm\\\" 小銃\",\n  \"category\""));
            Assert.That(WeaponSpecJson.Parse(written, "described.json").Description, Is.EqualTo("AK 系の \"7.62 mm\" 小銃"));
        }

        [Test]
        public void AWeaponWithoutADescriptionIsWrittenWithoutTheItem()
        {
            var read = WeaponSpecJson.Parse("{\"id\": \"" + _rifleId + "\", \"category\": \"AssaultRifle\"}", "bare.json");

            Assert.That(read.Description, Is.Empty);
            Assert.That(WeaponSpecJson.Write(read), Does.Not.Contain("\"description\""));
        }

        [Test]
        public void AStatusEffectWithoutAKindBurns()
        {
            var read = WeaponSpecJson.Parse(
                "{\"id\": \"" + _rifleId + "\", \"category\": \"AssaultRifle\", "
                + "\"impact\": {\"statusEffect\": {\"durationSeconds\": 4, \"tickSeconds\": 0.5, \"damagePerTick\": 4}}}",
                "burn.json");

            Assert.That(read.Impact.StatusEffect.KindId, Is.EqualTo(StatusEffectKinds.Burn));
            Assert.That(read.Impact.StatusEffect.DurationSeconds, Is.EqualTo(4f));
        }

        [Test]
        public void LeftOutPartsTakeTheDefaultsOfTheParts()
        {
            var read = WeaponSpecJson.Parse("{\"id\": \"" + _rifleId + "\", \"category\": \"AssaultRifle\"}", "bare.json");

            Assert.That(read.FireMode, Is.EqualTo(TriggerSpec.DefaultFireMode));
            Assert.That(read.MagazineSize, Is.EqualTo(AmmoSpec.DefaultMagazineSize));
            Assert.That(read.Range, Is.EqualTo(RoundSpec.DefaultRange));
            Assert.That(read.Damage, Is.EqualTo(ImpactSpec.DefaultDamage));
            Assert.That(read.AdsFieldOfView, Is.EqualTo(AdsZoom.DefaultFieldOfView));
            Assert.That(read.NameKey, Is.Empty);
            Assert.That(read.AmmoTypes, Is.Empty);
        }

        [TestCase("{\"damage\": 10, \"impact\": {\"damage\": 20}}", "damage")]
        [TestCase("{\"explosionRadius\": 1, \"impact\": {\"structureRadius\": 2}}", "explosionRadius")]
        [TestCase("{\"adsFieldOfView\": 40, \"aim\": {\"zoomLevels\": [40]}}", "adsFieldOfView")]
        [TestCase("{\"ammo\": {\"magazineSize\": 0}}", "ammo.magazineSize")]
        [TestCase("{\"magazineSize\": 0}", "magazineSize")]
        [TestCase("{\"explosionRadius\": -1}", "explosionRadius")]
        [TestCase("{\"adsFieldOfView\": 180}", "adsFieldOfView")]
        [TestCase("{\"aim\": {\"zoomLevels\": [40, 0]}}", "aim.zoomLevels")]
        [TestCase("{\"aim\": {\"zoomLevels\": [40, \"x\"]}}", "aim.zoomLevels[1]")]
        [TestCase("{\"trigger\": {\"burst\": {\"roundsPerBurst\": 1, \"roundIntervalSeconds\": 0.1}}}", "trigger.burst.roundsPerBurst")]
        [TestCase("{\"trigger\": {\"burst\": {\"roundsPerBurst\": 3}}}", "trigger.burst.roundIntervalSeconds")]
        [TestCase("{\"ammo\": {\"kind\": \"Heat\"}}", "ammo.heat")]
        [TestCase("{\"round\": {\"delivery\": \"Projectile\"}}", "round.projectile")]
        [TestCase("{\"round\": {\"delivery\": \"Projectile\", \"projectile\": {\"speed\": 0, \"gravityMultiplier\": 1, \"lifetimeSeconds\": 3}}}", "round.projectile.speed")]
        [TestCase("{\"round\": {\"beam\": {\"tickSeconds\": 0.1}}}", "round.beam")]
        [TestCase("{\"round\": {\"pelets\": 8}}", "round.pelets")]
        [TestCase("{\"impact\": {\"falloff\": \"none\"}}", "impact.falloff")]
        [TestCase("{\"impact\": {\"falloff\": {\"startDistance\": 10, \"endDistance\": 5, \"minMultiplier\": 0.5}}}", "impact.falloff.endDistance")]
        [TestCase("{\"impact\": {\"explosion\": {\"maxDamage\": 10, \"radius\": 2, \"fullDamageRadius\": 3}}}", "impact.explosion.fullDamageRadius")]
        [TestCase("{\"impact\": {\"explosion\": {\"maxDamage\": 10, \"radius\": 2, \"detonation\": \"OnStop\"}}}", "impact")]
        [TestCase("{\"impact\": {\"areaEffect\": {\"radius\": 2, \"durationSeconds\": 3, \"tickSeconds\": 1, \"damagePerTick\": 1, \"statusEffect\": {\"durationSeconds\": 0, \"tickSeconds\": 1, \"damagePerTick\": 1}}}}", "impact.areaEffect.statusEffect.durationSeconds")]
        [TestCase("{\"impact\": {\"blind\": {\"radius\": 10, \"durationSeconds\": 3, \"backFraction\": 0.2}}}", "impact.blind")]
        [TestCase("{\"round\": {\"penetration\": {\"maxPenetrations\": 1, \"damageMultiplierPerPenetration\": 0.5, \"maxThickness\": 0.3, \"materials\": [{\"materialId\": \"metal\", \"penetrability\": -1}]}}}", "round.penetration.materials[0].penetrability")]
        [TestCase("{\"round\": {\"penetration\": {\"maxPenetrations\": 1, \"damageMultiplierPerPenetration\": 0.5, \"maxThickness\": 0.3, \"materials\": [{\"materialId\": \" \", \"penetrability\": 1}]}}}", "round.penetration.materials[0].materialId")]
        [TestCase("{\"round\": {\"penetration\": {\"maxPenetrations\": 1, \"damageMultiplierPerPenetration\": 0.5, \"maxThickness\": 0.3, \"materials\": [{\"materialId\": \"metal\"}]}}}", "round.penetration.materials[0].penetrability")]
        [TestCase("{\"round\": {\"penetration\": {\"maxPenetrations\": 1, \"damageMultiplierPerPenetration\": 0.5, \"maxThickness\": 0.3, \"materials\": [{\"materialId\": \"metal\", \"penetrability\": 0}, {\"materialId\": \"metal\", \"penetrability\": 1}]}}}", "round.penetration.materials")]
        [TestCase("{\"aim\": {\"sight\": {\"aimedInProgress\": 1.5}}}", "aim.sight.aimedInProgress")]
        [TestCase("{\"aim\": {\"adsMoveSpreadDegrees\": 90.5}}", "aim.adsMoveSpreadDegrees")]
        [TestCase("{\"aim\": {\"adsMoveSpreadDegrees\": \"x\"}}", "aim.adsMoveSpreadDegrees")]
        [TestCase("{\"aim\": {\"sight\": {\"overlay\": true}}}", "aim.sight.overlay")]
        [TestCase("{\"aim\": {\"sight\": {\"visionMode\": \"Infrared\"}}}", "aim.sight.visionMode")]
        [TestCase("{\"aim\": {\"sight\": {\"visionMode\": 2}}}", "aim.sight.visionMode")]
        [TestCase("{\"weight\": -1}", "weight")]
        [TestCase("{\"moveSpeedMultiplier\": -0.5}", "moveSpeedMultiplier")]
        [TestCase("{\"drawSeconds\": -0.5}", "drawSeconds")]
        [TestCase("{\"holsterSeconds\": -0.5}", "holsterSeconds")]
        [TestCase("{\"ammoTypes\": [{\"id\": \"x\"}]}", "ammoTypes[0].id")]
        [TestCase("{\"ammoTypes\": [{\"id\": \"" + _standardId + "\", \"round\": {\"range\": 0}}]}", "ammoTypes[0].round.range")]
        [TestCase("{\"ammoTypes\": [{\"id\": \"" + _standardId + "\"}, {\"id\": \"" + _standardId + "\"}]}", "ammoTypes")]
        public void ProblemsNameTheItem(string body, string item)
        {
            var text = "{\"id\": \"" + _rifleId + "\", \"category\": \"AssaultRifle\", " + body.TrimStart('{');

            var e = Assert.Throws<ServerDataJsonException>(() => WeaponSpecJson.Parse(text, "bad.json"));

            Assert.That(e.Item, Is.EqualTo(item));
            Assert.That(e.Message, Does.Contain("bad.json"));
        }

        [Test]
        public void TheSightReadsBackAndADefaultSightIsNotWritten()
        {
            var scoped = new WeaponSpec(
                _rifleId,
                "weapon.scopedRifle",
                WeaponCategory.MarksmanRifle,
                aim: new AimSpec(sight: new SightSpec("mildot", true, true, 0.75f)));

            var written = WeaponSpecJson.Write(scoped);
            var read = WeaponSpecJson.Parse(written, "scoped.json");

            Assert.That(written, Does.Contain("\"sight\""));
            Assert.That(read.Aim.Sight.ReticleId, Is.EqualTo("mildot"));
            Assert.That(read.Aim.Sight.ScopeOverlay, Is.True);
            Assert.That(read.Aim.Sight.EyeViewInThirdPerson, Is.True);
            Assert.That(read.Aim.Sight.AimedInProgress, Is.EqualTo(0.75f));

            var plain = new WeaponSpec(_rifleId, "weapon.rifle", WeaponCategory.AssaultRifle);
            Assert.That(WeaponSpecJson.Write(plain), Does.Not.Contain("\"sight\""));
            Assert.That(WeaponSpecJson.Parse(WeaponSpecJson.Write(plain), "plain.json").Aim.Sight, Is.SameAs(SightSpec.Default));
        }

        [Test]
        public void ALeftOutSightItemTakesItsDefault()
        {
            var read = WeaponSpecJson.Parse(
                "{\"id\": \"" + _rifleId + "\", \"category\": \"AssaultRifle\", \"aim\": {\"sight\": {\"reticleId\": \"redDot\"}}}", "dot.json");

            Assert.That(read.Aim.Sight.ReticleId, Is.EqualTo("redDot"));
            Assert.That(read.Aim.Sight.ScopeOverlay, Is.False);
            Assert.That(read.Aim.Sight.EyeViewInThirdPerson, Is.False);
            Assert.That(read.Aim.Sight.AimedInProgress, Is.EqualTo(SightSpec.DefaultAimedInProgress));
            Assert.That(read.Aim.Sight.VisionMode, Is.EqualTo(VisionMode.None));
        }

        [TestCase(VisionMode.Night, "\"Night\"")]
        [TestCase(VisionMode.Thermal, "\"Thermal\"")]
        public void TheSightsVisionModeReadsBack(VisionMode mode, string written)
        {
            var spec = new WeaponSpec(
                _rifleId,
                "weapon.nightRifle",
                WeaponCategory.MarksmanRifle,
                aim: new AimSpec(sight: new SightSpec(visionMode: mode)));

            var text = WeaponSpecJson.Write(spec);
            var read = WeaponSpecJson.Parse(text, "night.json");

            Assert.That(text, Does.Contain("\"visionMode\": " + written));
            Assert.That(read.Aim.Sight.VisionMode, Is.EqualTo(mode));
            Assert.That(WeaponSpecJson.Write(read), Is.EqualTo(text));
        }

        [Test]
        public void ASightWithoutVisionDoesNotWriteIt()
        {
            var scoped = new WeaponSpec(
                _rifleId, "weapon.scopedRifle", WeaponCategory.MarksmanRifle, aim: new AimSpec(sight: new SightSpec("mildot", true)));

            Assert.That(WeaponSpecJson.Write(scoped), Does.Not.Contain("visionMode"));
        }

        [TestCase("{\"category\": \"AssaultRifle\"}", "id")]
        [TestCase("{\"id\": \"" + _rifleId + "\"}", "category")]
        [TestCase("{\"id\": null, \"category\": \"AssaultRifle\"}", "id")]
        public void TheIdAndCategoryAreRequired(string text, string item)
        {
            var e = Assert.Throws<ServerDataJsonException>(() => WeaponSpecJson.Parse(text, "bad.json"));

            Assert.That(e.Item, Is.EqualTo(item));
        }

        [Test]
        public void WeightAndMoveSpeedMultiplierReadBackAndDefaultWhenLeftOut()
        {
            var read = WeaponSpecJson.Parse(
                "{\"id\": \"" + _rifleId + "\", \"category\": \"AssaultRifle\", \"weight\": 3.5, \"moveSpeedMultiplier\": 0.9}", "x.json");
            var bare = WeaponSpecJson.Parse("{\"id\": \"" + _rifleId + "\", \"category\": \"AssaultRifle\"}", "x.json");
            var again = WeaponSpecJson.Parse(WeaponSpecJson.Write(read), "again.json");

            Assert.That(read.Weight, Is.EqualTo(3.5f));
            Assert.That(read.MoveSpeedMultiplier, Is.EqualTo(0.9f));
            Assert.That(bare.Weight, Is.EqualTo(WeaponSpec.DefaultWeight));
            Assert.That(bare.MoveSpeedMultiplier, Is.EqualTo(WeaponSpec.DefaultMoveSpeedMultiplier));
            Assert.That(again.Weight, Is.EqualTo(3.5f));
            Assert.That(again.MoveSpeedMultiplier, Is.EqualTo(0.9f));
        }

        [Test]
        public void DrawSecondsReadBackAndAreLeftOutWhenUnset()
        {
            var read = WeaponSpecJson.Parse(
                "{\"id\": \"" + _rifleId + "\", \"category\": \"AssaultRifle\", \"drawSeconds\": 0.3}", "x.json");
            var zero = WeaponSpecJson.Parse(
                "{\"id\": \"" + _rifleId + "\", \"category\": \"AssaultRifle\", \"drawSeconds\": 0}", "x.json");
            var bare = WeaponSpecJson.Parse("{\"id\": \"" + _rifleId + "\", \"category\": \"AssaultRifle\"}", "x.json");
            var nulled = WeaponSpecJson.Parse(
                "{\"id\": \"" + _rifleId + "\", \"category\": \"AssaultRifle\", \"drawSeconds\": null}", "x.json");

            Assert.That(read.DrawSeconds, Is.EqualTo(0.3f));
            Assert.That(zero.DrawSeconds, Is.EqualTo(0f));
            Assert.That(bare.DrawSeconds, Is.Null);
            Assert.That(nulled.DrawSeconds, Is.Null);
            Assert.That(WeaponSpecJson.Parse(WeaponSpecJson.Write(read), "again.json").DrawSeconds, Is.EqualTo(0.3f));
            Assert.That(WeaponSpecJson.Write(bare), Does.Not.Contain(WeaponSpecJson.DrawSecondsItem), "a weapon without a draw time writes the same JSON as before");
            Assert.That(read.WithFireInterval(read.FireInterval * 2f).DrawSeconds, Is.EqualTo(0.3f));
        }

        [Test]
        public void TheAdsMoveSpreadReadsBackAndIsLeftOutWhenItIsTheMoveSpread()
        {
            var read = WeaponSpecJson.Parse(
                "{\"id\": \"" + _rifleId + "\", \"category\": \"AssaultRifle\", \"aim\": {\"moveSpreadDegrees\": 3, \"adsMoveSpreadDegrees\": 0.9}}",
                "x.json");
            var zero = WeaponSpecJson.Parse(
                "{\"id\": \"" + _rifleId + "\", \"category\": \"AssaultRifle\", \"aim\": {\"moveSpreadDegrees\": 3, \"adsMoveSpreadDegrees\": 0}}",
                "x.json");
            var bare = WeaponSpecJson.Parse(
                "{\"id\": \"" + _rifleId + "\", \"category\": \"AssaultRifle\", \"aim\": {\"moveSpreadDegrees\": 3}}", "x.json");

            Assert.That(read.AdsMoveSpreadDegrees, Is.EqualTo(0.9f));
            Assert.That(zero.AdsMoveSpreadDegrees, Is.EqualTo(0f));
            Assert.That(bare.AdsMoveSpreadDegrees, Is.EqualTo(3f));
            Assert.That(WeaponSpecJson.Write(read), Does.Contain("\"adsMoveSpreadDegrees\": 0.9"));
            Assert.That(WeaponSpecJson.Parse(WeaponSpecJson.Write(read), "again.json").AdsMoveSpreadDegrees, Is.EqualTo(0.9f));
            Assert.That(WeaponSpecJson.Parse(WeaponSpecJson.Write(zero), "again.json").AdsMoveSpreadDegrees, Is.EqualTo(0f));
            Assert.That(WeaponSpecJson.Write(bare), Does.Not.Contain(WeaponSpecJson.AdsMoveSpreadDegreesItem), "a weapon without an aimed move spread writes the same JSON as before");
            Assert.That(read.WithFireInterval(read.FireInterval * 2f).AdsMoveSpreadDegrees, Is.EqualTo(0.9f));
        }

        [Test]
        public void HolsterSecondsReadBackAndAreLeftOutWhenZero()
        {
            var read = WeaponSpecJson.Parse(
                "{\"id\": \"" + _rifleId + "\", \"category\": \"AssaultRifle\", \"holsterSeconds\": 0.4}", "x.json");
            var bare = WeaponSpecJson.Parse("{\"id\": \"" + _rifleId + "\", \"category\": \"AssaultRifle\"}", "x.json");

            Assert.That(read.HolsterSeconds, Is.EqualTo(0.4f));
            Assert.That(bare.HolsterSeconds, Is.EqualTo(WeaponSpec.DefaultHolsterSeconds));
            Assert.That(WeaponSpec.DefaultHolsterSeconds, Is.Zero);
            Assert.That(WeaponSpecJson.Parse(WeaponSpecJson.Write(read), "again.json").HolsterSeconds, Is.EqualTo(0.4f));
            Assert.That(WeaponSpecJson.Write(bare), Does.Not.Contain(WeaponSpecJson.HolsterSecondsItem), "a weapon without a holster time writes the same JSON as before");
            Assert.That(read.WithFireInterval(read.FireInterval * 2f).HolsterSeconds, Is.EqualTo(0.4f));
        }

        [Test]
        public void TheShieldReadsBackAndANonShieldWeaponWritesNoShield()
        {
            var shield = new WeaponSpec(
                _rifleId,
                "weapon.riotShield",
                WeaponCategory.Handgun,
                shield: new RiotShieldSpec(100f, 0.7f, 1.4f, 0.6f, new MeleeSpec(45, 1.5f, 80f, 0.7f, 0.6f, 0.3f, 0.4f)));

            var written = WeaponSpecJson.Write(shield);
            var read = WeaponSpecJson.Parse(written, "shield.json");

            Assert.That(written, Does.Contain("\"shield\""));
            Assert.That(read.IsShield, Is.True);
            Assert.That(read.Shield.BlockArcDegrees, Is.EqualTo(100f));
            Assert.That(read.Shield.Width, Is.EqualTo(0.7f));
            Assert.That(read.Shield.Height, Is.EqualTo(1.4f));
            Assert.That(read.Shield.MoveSpeedMultiplier, Is.EqualTo(0.6f));
            Assert.That(read.Shield.Bash.Damage, Is.EqualTo(45));
            Assert.That(read.Shield.Bash.Reach, Is.EqualTo(1.5f));
            Assert.That(read.Shield.Bash.ArcDegrees, Is.EqualTo(80f));
            Assert.That(read.Shield.Bash.SwingSeconds, Is.EqualTo(0.7f));
            Assert.That(read.Shield.Bash.CooldownSeconds, Is.EqualTo(0.6f));
            Assert.That(read.Shield.Bash.ImpactSeconds, Is.EqualTo(0.3f));
            Assert.That(read.Shield.Bash.LungeDistance, Is.EqualTo(0.4f));
            Assert.That(read.WithFireInterval(read.FireInterval * 2f).Shield, Is.SameAs(read.Shield));

            var plain = new WeaponSpec(_rifleId, "weapon.rifle", WeaponCategory.AssaultRifle);
            Assert.That(WeaponSpecJson.Write(plain), Does.Not.Contain("\"shield\""), "a weapon without a shield writes the same JSON as before");
            Assert.That(WeaponSpecJson.Parse(WeaponSpecJson.Write(plain), "plain.json").Shield, Is.Null);
        }

        [Test]
        public void ALeftOutShieldItemTakesItsDefault()
        {
            var read = WeaponSpecJson.Parse(
                "{\"id\": \"" + _rifleId + "\", \"category\": \"Handgun\", \"shield\": {\"bash\": {\"damage\": 60}}}", "shield.json");

            Assert.That(read.Shield.BlockArcDegrees, Is.EqualTo(RiotShieldSpec.DefaultBlockArcDegrees));
            Assert.That(read.Shield.Width, Is.EqualTo(RiotShieldSpec.DefaultWidth));
            Assert.That(read.Shield.Height, Is.EqualTo(RiotShieldSpec.DefaultHeight));
            Assert.That(read.Shield.MoveSpeedMultiplier, Is.EqualTo(RiotShieldSpec.DefaultMoveSpeedMultiplier));
            Assert.That(read.Shield.Bash.Damage, Is.EqualTo(60));
            Assert.That(read.Shield.Bash.Reach, Is.EqualTo(RiotShieldSpec.DefaultBashReach));
            Assert.That(read.Shield.Bash.SwingSeconds, Is.EqualTo(RiotShieldSpec.DefaultBashSwingSeconds));

            var empty = WeaponSpecJson.Parse("{\"id\": \"" + _rifleId + "\", \"category\": \"Handgun\", \"shield\": {}}", "shield.json");
            Assert.That(empty.Shield.Bash, Is.SameAs(RiotShieldSpec.DefaultBash));
        }

        [TestCase("{\"blockArcDegrees\": 0}", "shield.blockArcDegrees")]
        [TestCase("{\"blockArcDegrees\": 361}", "shield.blockArcDegrees")]
        [TestCase("{\"width\": 0}", "shield.width")]
        [TestCase("{\"height\": -1}", "shield.height")]
        [TestCase("{\"moveSpeedMultiplier\": 1.5}", "shield.moveSpeedMultiplier")]
        [TestCase("{\"bash\": {\"damage\": 0}}", "shield.bash.damage")]
        [TestCase("{\"bash\": {\"impactSeconds\": 5}}", "shield.bash.impactSeconds")]
        [TestCase("{\"bash\": {\"finisherSeconds\": 1}}", "shield.bash.finisherSeconds")]
        [TestCase("{\"angle\": 90}", "shield.angle")]
        public void ABadShieldValueNamesItsItem(string shield, string item)
        {
            var text = "{\"id\": \"" + _rifleId + "\", \"category\": \"Handgun\", \"shield\": " + shield + "}";

            var e = Assert.Throws<ServerDataJsonException>(() => WeaponSpecJson.Parse(text, "bad.json"));

            Assert.That(e.Item, Is.EqualTo(item));
        }

        [Test]
        public void TheMeleePartReadsBackAndAGunWritesNoMeleePart()
        {
            var melee = new MeleeWeaponSpec(
                new MeleeSpec(30, 2f, 70f, 0.35f, 0.05f, 0.12f, 0.25f),
                new MeleeSpec(75, 2.2f, 80f, 0.55f, 0.15f, 0.2f, 0.6f),
                0.25f,
                0.7f,
                new MeleeThrowSpec(50, 18f, 4f, 900f, 0.12f, 6f));
            var crowbar = new WeaponSpec(_rifleId, "weapon.crowbar", WeaponCategory.Melee, melee: melee);

            var written = WeaponSpecJson.Write(crowbar);
            var read = WeaponSpecJson.Parse(written, "crowbar.json");

            Assert.That(written, Does.Contain("\"melee\""));
            Assert.That(read.Category, Is.EqualTo(WeaponCategory.Melee));
            Assert.That(read.IsMelee, Is.True);
            Assert.That(read.Melee.Swing.Damage, Is.EqualTo(30));
            Assert.That(read.Melee.Swing.Reach, Is.EqualTo(2f));
            Assert.That(read.Melee.Swing.ArcDegrees, Is.EqualTo(70f));
            Assert.That(read.Melee.Swing.SwingSeconds, Is.EqualTo(0.35f));
            Assert.That(read.Melee.Swing.CooldownSeconds, Is.EqualTo(0.05f));
            Assert.That(read.Melee.Swing.ImpactSeconds, Is.EqualTo(0.12f));
            Assert.That(read.Melee.Swing.LungeDistance, Is.EqualTo(0.25f));
            Assert.That(read.Melee.HeavySwing.Damage, Is.EqualTo(75));
            Assert.That(read.Melee.HeavySwing.Reach, Is.EqualTo(2.2f));
            Assert.That(read.Melee.HeavySwing.SwingSeconds, Is.EqualTo(0.55f));
            Assert.That(read.Melee.HeavySwing.LungeDistance, Is.EqualTo(0.6f));
            Assert.That(read.Melee.ChargeDelaySeconds, Is.EqualTo(0.25f));
            Assert.That(read.Melee.ChargeSeconds, Is.EqualTo(0.7f));
            Assert.That(read.Melee.Throw.Damage, Is.EqualTo(50));
            Assert.That(read.Melee.Throw.Speed, Is.EqualTo(18f));
            Assert.That(read.Melee.Throw.UpwardDegrees, Is.EqualTo(4f));
            Assert.That(read.Melee.Throw.SpinDegreesPerSecond, Is.EqualTo(900f));
            Assert.That(read.Melee.Throw.Radius, Is.EqualTo(0.12f));
            Assert.That(read.Melee.Throw.MaxFlightSeconds, Is.EqualTo(6f));

            var plain = new WeaponSpec(_rifleId, "weapon.rifle", WeaponCategory.AssaultRifle);
            Assert.That(WeaponSpecJson.Write(plain), Does.Not.Contain("\"melee\""), "a gun writes the same JSON as before");
            Assert.That(WeaponSpecJson.Parse(WeaponSpecJson.Write(plain), "plain.json").Melee, Is.Null);
        }

        [Test]
        public void ALeftOutMeleeItemTakesItsDefaultAndTheHeavySwingFillsFromTheSwing()
        {
            var read = WeaponSpecJson.Parse(
                "{\"id\": \"" + _rifleId + "\", \"category\": \"Melee\", \"melee\": {\"swing\": {\"damage\": 40}, \"heavySwing\": {\"damage\": 100}}}",
                "melee.json");

            Assert.That(read.Melee.Swing.Damage, Is.EqualTo(40));
            Assert.That(read.Melee.Swing.Reach, Is.EqualTo(MeleeWeaponSpec.DefaultSwingReach));
            Assert.That(read.Melee.HeavySwing.Damage, Is.EqualTo(100));
            Assert.That(read.Melee.HeavySwing.Reach, Is.EqualTo(MeleeWeaponSpec.DefaultSwingReach), "the heavy swing takes what it leaves out from the swing");
            Assert.That(read.Melee.ChargeSeconds, Is.EqualTo(MeleeWeaponSpec.DefaultChargeSeconds));
            Assert.That(read.Melee.CanThrow, Is.False);

            var empty = WeaponSpecJson.Parse("{\"id\": \"" + _rifleId + "\", \"category\": \"Melee\", \"melee\": {}}", "melee.json");
            Assert.That(empty.Melee.Swing, Is.SameAs(MeleeWeaponSpec.DefaultSwing));
            Assert.That(empty.Melee.CanCharge, Is.False);
            Assert.That(empty.Melee.CanThrow, Is.False);

            var thrown = WeaponSpecJson.Parse("{\"id\": \"" + _rifleId + "\", \"category\": \"Melee\", \"melee\": {\"throw\": {}}}", "melee.json");
            Assert.That(thrown.Melee.Throw.Damage, Is.EqualTo(MeleeThrowSpec.DefaultDamage));
            Assert.That(thrown.Melee.Throw.Speed, Is.EqualTo(MeleeThrowSpec.DefaultSpeed));
        }

        [TestCase("{\"swing\": {\"damage\": 0}}", "melee.swing.damage")]
        [TestCase("{\"heavySwing\": {\"impactSeconds\": 5}}", "melee.heavySwing.impactSeconds")]
        [TestCase("{\"swing\": {\"finisherSeconds\": 1}}", "melee.swing.finisherSeconds")]
        [TestCase("{\"chargeSeconds\": 0}", "melee.chargeSeconds")]
        [TestCase("{\"throw\": {\"speed\": -1}}", "melee.throw.speed")]
        [TestCase("{\"throw\": {\"upwardDegrees\": 120}}", "melee.throw.upwardDegrees")]
        [TestCase("{\"blade\": true}", "melee.blade")]
        public void ABadMeleeValueNamesItsItem(string melee, string item)
        {
            var text = "{\"id\": \"" + _rifleId + "\", \"category\": \"Melee\", \"melee\": " + melee + "}";

            var e = Assert.Throws<ServerDataJsonException>(() => WeaponSpecJson.Parse(text, "bad.json"));

            Assert.That(e.Item, Is.EqualTo(item));
        }

        [Test]
        public void AShieldWithAMeleePartIsRefused()
        {
            var text = "{\"id\": \"" + _rifleId + "\", \"category\": \"Melee\", \"shield\": {}, \"melee\": {}}";

            var e = Assert.Throws<ServerDataJsonException>(() => WeaponSpecJson.Parse(text, "bad.json"));

            Assert.That(e.Item, Is.EqualTo("melee"));
        }

        [Test]
        public void ANullNameKeyMeansNoName()
        {
            var read = WeaponSpecJson.Parse("{\"id\": \"" + _rifleId + "\", \"nameKey\": null, \"category\": \"AssaultRifle\"}", "x.json");

            Assert.That(read.NameKey, Is.Empty);
        }
    }
}
