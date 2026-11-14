using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class TuningJsonTests
    {
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
            Assert.That(TuningJson.GrenadeItemNames, Is.EqualTo(ExpectedGrenadeItemNames));
            Assert.That(TuningJson.PingItemNames, Is.EqualTo(ExpectedPingItemNames));
        }

        static readonly string[] ExpectedGrenadeItemNames =
        {
            "fuseSeconds", "throwSeconds", "throwSpeed", "throwLiftSpeed",
            "maxDamage", "damageRadius", "fullDamageRadius", "structureDamageAtCenter",
        };

        static readonly string[] ExpectedPingItemNames =
        {
            "lifetimeSeconds", "maxMarkers", "maxDistance", "enemyAimToleranceDegrees", "minIntervalSeconds",
        };

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
