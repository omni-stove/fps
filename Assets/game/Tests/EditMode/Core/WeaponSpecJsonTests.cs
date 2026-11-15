using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
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
                    new ChargeSpec(1.5f, 0.2f, 2.5f, firesWhenFull: true),
                    new SpinUpSpec(0.8f, 3f, 0.4f),
                    new CycleSpec(0.9f, leavesAds: true)),
                new AmmoSpec(1, 6, 0f, 0.3f, 0.2f, 0.7f, ReloadStyle.PerRound, 0.6f, AmmoKind.Heat, new HeatSpec(0.1f, 0.5f, 0.3f, 2f)),
                new RoundSpec(
                    RoundDelivery.Projectile,
                    300f,
                    pelletCount: 8,
                    pelletSpreadDegrees: 4f,
                    radius: 0.2f,
                    penetration: new PenetrationSpec(2, 0.6f, 0.5f),
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

        [TestCase("{\"category\": \"AssaultRifle\"}", "id")]
        [TestCase("{\"id\": \"" + _rifleId + "\"}", "category")]
        [TestCase("{\"id\": null, \"category\": \"AssaultRifle\"}", "id")]
        public void TheIdAndCategoryAreRequired(string text, string item)
        {
            var e = Assert.Throws<ServerDataJsonException>(() => WeaponSpecJson.Parse(text, "bad.json"));

            Assert.That(e.Item, Is.EqualTo(item));
        }

        [Test]
        public void ANullNameKeyMeansNoName()
        {
            var read = WeaponSpecJson.Parse("{\"id\": \"" + _rifleId + "\", \"nameKey\": null, \"category\": \"AssaultRifle\"}", "x.json");

            Assert.That(read.NameKey, Is.Empty);
        }
    }
}
