using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class TuningJsonTests
    {
        private static readonly string[] _expectedGrenadeItemNames =
        {
            "fuseSeconds", "throwSeconds", "throwSpeed", "throwLiftSpeed",
            "maxDamage", "damageRadius", "fullDamageRadius", "structureDamageAtCenter",
        };

        private static readonly string[] _expectedPingItemNames =
        {
            "lifetimeSeconds", "maxMarkers", "maxDistance", "enemyAimToleranceDegrees", "minIntervalSeconds",
        };

        private static readonly string[] _expectedKnockbackItemNames =
        {
            "explosionSpeed", "explosionUpwardRatio", "explosionWeightFactor",
            "meleeSpeed", "meleeUpwardRatio", "meleeWeightFactor",
            "chargeSpeed", "chargeUpwardRatio", "chargeWeightFactor",
            "minSpeed", "minAirSeconds", "maxAirSeconds", "airControl", "gravity", "recoverSeconds", "landingSnapRadius",
            "playerWeight", "enemyWeight",
        };

        [Test]
        public void ReadsEveryGrenadeItem()
        {
            var spec = TuningJson.ParseGrenade(
                "{\"fuseSeconds\": 1.5, \"throwSeconds\": 0.4, \"throwSpeed\": 20, \"throwLiftSpeed\": 2.5,"
                + " \"maxDamage\": 90, \"damageRadius\": 8, \"fullDamageRadius\": 2, \"structureDamageAtCenter\": 60}",
                "grenade.json");

            Assert.That(spec.FuseSeconds, Is.EqualTo(1.5f));
            Assert.That(spec.ThrowSeconds, Is.EqualTo(0.4f));
            Assert.That(spec.ThrowSpeed, Is.EqualTo(20f));
            Assert.That(spec.ThrowLiftSpeed, Is.EqualTo(2.5f));
            Assert.That(spec.MaxDamage, Is.EqualTo(90));
            Assert.That(spec.DamageRadius, Is.EqualTo(8f));
            Assert.That(spec.FullDamageRadius, Is.EqualTo(2f));
            Assert.That(spec.StructureDamageAtCenter, Is.EqualTo(60));
        }

        [Test]
        public void OmittedGrenadeItemsUseTheDefaults()
        {
            var spec = TuningJson.ParseGrenade("{\"damageRadius\": 9}", "grenade.json");

            Assert.That(spec.DamageRadius, Is.EqualTo(9f));
            Assert.That(spec.FuseSeconds, Is.EqualTo(GrenadeSpec.DefaultFuseSeconds));
            Assert.That(spec.ThrowSpeed, Is.EqualTo(GrenadeSpec.DefaultThrowSpeed));
            Assert.That(spec.MaxDamage, Is.EqualTo(GrenadeSpec.DefaultMaxDamage));
            Assert.That(spec.FullDamageRadius, Is.EqualTo(GrenadeSpec.DefaultFullDamageRadius));
        }

        [Test]
        public void AnEmptyObjectIsTheDefaultSpec()
        {
            var grenade = TuningJson.ParseGrenade("﻿{ }", "grenade.json");
            var ping = TuningJson.ParsePing("{}", "ping.json");

            Assert.That(grenade.FuseSeconds, Is.EqualTo(GrenadeSpec.DefaultFuseSeconds));
            Assert.That(grenade.StructureDamageAtCenter, Is.EqualTo(GrenadeSpec.DefaultStructureDamageAtCenter));
            Assert.That(ping.LifetimeSeconds, Is.EqualTo(PingSpec.DefaultLifetimeSeconds));
            Assert.That(ping.MinIntervalSeconds, Is.EqualTo(PingSpec.DefaultMinIntervalSeconds));
        }

        [Test]
        public void ReadsEveryPingItem()
        {
            var spec = TuningJson.ParsePing(
                "{\"lifetimeSeconds\": 10, \"maxMarkers\": 5, \"maxDistance\": 80.5, \"enemyAimToleranceDegrees\": 4, \"minIntervalSeconds\": 1.25}",
                "ping.json");

            Assert.That(spec.LifetimeSeconds, Is.EqualTo(10f));
            Assert.That(spec.MaxMarkers, Is.EqualTo(5));
            Assert.That(spec.MaxDistance, Is.EqualTo(80.5f));
            Assert.That(spec.EnemyAimToleranceDegrees, Is.EqualTo(4f));
            Assert.That(spec.MinIntervalSeconds, Is.EqualTo(1.25f));
        }

        [Test]
        public void TheItemNamesAreTheConstructorArguments()
        {
            Assert.That(TuningJson.GrenadeItemNames, Is.EqualTo(_expectedGrenadeItemNames));
            Assert.That(TuningJson.PingItemNames, Is.EqualTo(_expectedPingItemNames));
            Assert.That(TuningJson.KnockbackItemNames, Is.EqualTo(_expectedKnockbackItemNames));
        }

        [Test]
        public void ReadsEveryKnockbackItem()
        {
            var spec = TuningJson.ParseKnockback(
                "{\"explosionSpeed\": 12, \"explosionUpwardRatio\": 0.6, \"explosionWeightFactor\": 0.5,"
                + " \"meleeSpeed\": 3, \"meleeUpwardRatio\": 0.1, \"meleeWeightFactor\": 0,"
                + " \"chargeSpeed\": 9, \"chargeUpwardRatio\": 0.3, \"chargeWeightFactor\": 2,"
                + " \"minSpeed\": 2, \"minAirSeconds\": 0.2, \"maxAirSeconds\": 4, \"airControl\": 0.5,"
                + " \"gravity\": 15, \"recoverSeconds\": 0.8, \"landingSnapRadius\": 6,"
                + " \"playerWeight\": 1.5, \"enemyWeight\": 3}",
                "knockback.json");

            Assert.That(spec.Explosion.Speed, Is.EqualTo(12f));
            Assert.That(spec.Explosion.UpwardRatio, Is.EqualTo(0.6f));
            Assert.That(spec.Explosion.WeightFactor, Is.EqualTo(0.5f));
            Assert.That(spec.Melee.Speed, Is.EqualTo(3f));
            Assert.That(spec.Melee.UpwardRatio, Is.EqualTo(0.1f));
            Assert.That(spec.Melee.WeightFactor, Is.EqualTo(0f));
            Assert.That(spec.Charge.Speed, Is.EqualTo(9f));
            Assert.That(spec.Charge.UpwardRatio, Is.EqualTo(0.3f));
            Assert.That(spec.Charge.WeightFactor, Is.EqualTo(2f));
            Assert.That(spec.MinSpeed, Is.EqualTo(2f));
            Assert.That(spec.MinAirSeconds, Is.EqualTo(0.2f));
            Assert.That(spec.MaxAirSeconds, Is.EqualTo(4f));
            Assert.That(spec.AirControl, Is.EqualTo(0.5f));
            Assert.That(spec.Gravity, Is.EqualTo(15f));
            Assert.That(spec.RecoverSeconds, Is.EqualTo(0.8f));
            Assert.That(spec.LandingSnapRadius, Is.EqualTo(6f));
            Assert.That(spec.PlayerWeight, Is.EqualTo(1.5f));
            Assert.That(spec.EnemyWeight, Is.EqualTo(3f));
        }

        [Test]
        public void OmittedKnockbackItemsUseTheDefaults()
        {
            var spec = TuningJson.ParseKnockback("{\"explosionSpeed\": 10, \"enemyWeight\": 2}", "knockback.json");

            Assert.That(spec.Explosion.Speed, Is.EqualTo(10f));
            Assert.That(spec.Explosion.UpwardRatio, Is.EqualTo(KnockbackSpec.DefaultExplosionUpwardRatio));
            Assert.That(spec.Explosion.WeightFactor, Is.EqualTo(KnockbackSpec.DefaultExplosionWeightFactor));
            Assert.That(spec.Melee.Speed, Is.EqualTo(KnockbackSpec.DefaultMeleeSpeed));
            Assert.That(spec.Charge.Speed, Is.EqualTo(KnockbackSpec.DefaultChargeSpeed));
            Assert.That(spec.Gravity, Is.EqualTo(KnockbackSpec.DefaultGravity));
            Assert.That(spec.PlayerWeight, Is.EqualTo(KnockbackSpec.DefaultWeight));
            Assert.That(spec.EnemyWeight, Is.EqualTo(2f));

            var empty = TuningJson.ParseKnockback("{}", "knockback.json");
            Assert.That(empty.Explosion.Speed, Is.EqualTo(KnockbackSpec.DefaultExplosionSpeed));
            Assert.That(empty.LandingSnapRadius, Is.EqualTo(KnockbackSpec.DefaultLandingSnapRadius));
        }

        [Test]
        public void AnInvalidForceNamesItsKindAndPart()
        {
            var speed = Assert.Throws<TuningJsonException>(() => TuningJson.ParseKnockback("{\"meleeSpeed\": -1}", "knockback.json"));
            var upward = Assert.Throws<TuningJsonException>(() => TuningJson.ParseKnockback("{\"chargeUpwardRatio\": 1.5}", "knockback.json"));
            var factor = Assert.Throws<TuningJsonException>(() => TuningJson.ParseKnockback("{\"explosionWeightFactor\": -0.5}", "knockback.json"));

            Assert.That(speed.Item, Is.EqualTo("meleeSpeed"));
            Assert.That(speed.SourceName, Is.EqualTo("knockback.json"));
            Assert.That(speed.Message, Does.Contain("knockback.json").And.Contain("meleeSpeed"));
            Assert.That(speed.InnerException, Is.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(upward.Item, Is.EqualTo("chargeUpwardRatio"));
            Assert.That(factor.Item, Is.EqualTo("explosionWeightFactor"));
        }

        [Test]
        public void AnInvalidKnockbackValueNamesTheItem()
        {
            Assert.That(
                Assert.Throws<TuningJsonException>(() => TuningJson.ParseKnockback("{\"playerWeight\": 0}", "k")).Item,
                Is.EqualTo("playerWeight"));
            Assert.That(
                Assert.Throws<TuningJsonException>(() => TuningJson.ParseKnockback("{\"gravity\": -1}", "k")).Item,
                Is.EqualTo("gravity"));
            Assert.That(
                Assert.Throws<TuningJsonException>(() => TuningJson.ParseKnockback("{\"minAirSeconds\": 5}", "k")).Item,
                Is.EqualTo("minAirSeconds"));
            Assert.That(
                Assert.Throws<TuningJsonException>(() => TuningJson.ParseKnockback("{\"enemyWeight\": \"2\"}", "k")).Item,
                Is.EqualTo("enemyWeight"));
            Assert.That(
                Assert.Throws<TuningJsonException>(() => TuningJson.ParseKnockback("{\"weight\": 2}", "k")).Item,
                Is.EqualTo("weight"));
            Assert.Throws<ArgumentNullException>(() => TuningJson.ParseKnockback(null, "k"));
        }

        [Test]
        public void AnInvalidValueNamesTheFileAndTheItem()
        {
            var e = Assert.Throws<TuningJsonException>(() => TuningJson.ParseGrenade("{\"damageRadius\": 0}", "grenade.json"));

            Assert.That(e.SourceName, Is.EqualTo("grenade.json"));
            Assert.That(e.Item, Is.EqualTo("damageRadius"));
            Assert.That(e.Message, Does.Contain("grenade.json").And.Contain("damageRadius"));
            Assert.That(e.InnerException, Is.InstanceOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void AFullDamageRadiusOutsideTheDefaultRadiusIsRejected()
        {
            var e = Assert.Throws<TuningJsonException>(() => TuningJson.ParseGrenade("{\"fullDamageRadius\": 7}", "grenade.json"));

            Assert.That(e.Item, Is.EqualTo("fullDamageRadius"));
        }

        [Test]
        public void AnInvalidPingValueNamesTheItem()
        {
            var e = Assert.Throws<TuningJsonException>(() => TuningJson.ParsePing("{\"maxMarkers\": 0}", "ping.json"));

            Assert.That(e.Item, Is.EqualTo("maxMarkers"));
            Assert.That(e.Message, Does.Contain("ping.json"));
        }

        [Test]
        public void AnUnknownItemIsRejected()
        {
            var e = Assert.Throws<TuningJsonException>(() => TuningJson.ParsePing("{\"lifetime\": 3}", "ping.json"));

            Assert.That(e.Item, Is.EqualTo("lifetime"));
            Assert.That(e.Message, Does.Contain("lifetimeSeconds"), "the message lists the known items");
        }

        [Test]
        public void WrongTypesAreRejected()
        {
            Assert.That(
                Assert.Throws<TuningJsonException>(() => TuningJson.ParseGrenade("{\"fuseSeconds\": \"2\"}", "g")).Item,
                Is.EqualTo("fuseSeconds"));
            Assert.That(
                Assert.Throws<TuningJsonException>(() => TuningJson.ParseGrenade("{\"maxDamage\": 1.5}", "g")).Item,
                Is.EqualTo("maxDamage"));
            Assert.That(
                Assert.Throws<TuningJsonException>(() => TuningJson.ParseGrenade("{\"maxDamage\": 1e2}", "g")).Item,
                Is.EqualTo("maxDamage"));
            Assert.That(
                Assert.Throws<TuningJsonException>(() => TuningJson.ParsePing("{\"maxMarkers\": 99999999999}", "p")).Item,
                Is.EqualTo("maxMarkers"));
            Assert.That(
                Assert.Throws<TuningJsonException>(() => TuningJson.ParsePing("{\"lifetimeSeconds\": null}", "p")).Item,
                Is.EqualTo("lifetimeSeconds"));
        }

        [Test]
        public void ATooLargeNumberIsRejectedWithTheItem()
        {
            var e = Assert.Throws<TuningJsonException>(() => TuningJson.ParseGrenade("{\"throwSpeed\": 1e999}", "g"));

            Assert.That(e.Item, Is.EqualTo("throwSpeed"));
        }

        [Test]
        public void BrokenJsonIsRejectedWithoutAnItem()
        {
            foreach (var text in new[] { "", "{", "{\"fuseSeconds\": 1,}", "[]", "3", "{\"fuseSeconds\": 1, \"fuseSeconds\": 2}" })
            {
                var e = Assert.Throws<TuningJsonException>(() => TuningJson.ParseGrenade(text, "grenade.json"));
                Assert.That(e.Item, Is.Null, text);
                Assert.That(e.SourceName, Is.EqualTo("grenade.json"), text);
            }
        }

        [Test]
        public void AMissingSourceNameIsDescribed()
        {
            var e = Assert.Throws<TuningJsonException>(() => TuningJson.ParsePing("{", null));

            Assert.That(e.SourceName, Does.Contain("ping JSON"));
            Assert.Throws<ArgumentNullException>(() => TuningJson.ParseGrenade(null, "g"));
            Assert.Throws<ArgumentNullException>(() => TuningJson.ParsePing(null, "p"));
        }
    }
}
