using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class RumbleJsonTests
    {
        private static readonly string[] _expectedItemNames =
        {
            "fireLow", "fireHigh", "fireSeconds",
            "damageLow", "damageHigh", "damageSeconds",
            "explosionLow", "explosionHigh", "explosionSeconds", "explosionRadius",
        };

        [Test]
        public void ReadsEveryItem()
        {
            var spec = RumbleJson.Parse(
                "{\"fireLow\": 0.1, \"fireHigh\": 0.2, \"fireSeconds\": 0.05,"
                + " \"damageLow\": 0.7, \"damageHigh\": 0.4, \"damageSeconds\": 0.3,"
                + " \"explosionLow\": 0.9, \"explosionHigh\": 0.5, \"explosionSeconds\": 0.6, \"explosionRadius\": 40}",
                "rumble.json");

            Assert.That(spec.Fire, Is.EqualTo(new RumblePulse(0.1f, 0.2f, 0.05f)));
            Assert.That(spec.Damage, Is.EqualTo(new RumblePulse(0.7f, 0.4f, 0.3f)));
            Assert.That(spec.Explosion, Is.EqualTo(new RumblePulse(0.9f, 0.5f, 0.6f)));
            Assert.That(spec.ExplosionRadius, Is.EqualTo(40f));
        }

        [Test]
        public void LeftOutItemsUseTheDefaults()
        {
            var empty = RumbleJson.Parse("{}", "rumble.json");
            var partial = RumbleJson.Parse("{\"explosionRadius\": 10}", "rumble.json");

            Assert.That(empty.Fire, Is.EqualTo(new RumblePulse(0.15f, 0.35f, 0.08f)));
            Assert.That(empty.Damage, Is.EqualTo(new RumblePulse(0.6f, 0.3f, 0.25f)));
            Assert.That(empty.Explosion, Is.EqualTo(new RumblePulse(1f, 0.6f, 0.45f)));
            Assert.That(empty.ExplosionRadius, Is.EqualTo(25f));
            Assert.That(partial.ExplosionRadius, Is.EqualTo(10f));
            Assert.That(partial.Fire, Is.EqualTo(new RumblePulse(0.15f, 0.35f, 0.08f)));
        }

        [Test]
        public void TheDefaultSpecHoldsTheDefaultConstants()
        {
            var spec = RumbleSpec.Default;

            Assert.That(spec.Fire, Is.EqualTo(new RumblePulse(RumbleSpec.DefaultFireLow, RumbleSpec.DefaultFireHigh, RumbleSpec.DefaultFireSeconds)));
            Assert.That(spec.Damage, Is.EqualTo(new RumblePulse(RumbleSpec.DefaultDamageLow, RumbleSpec.DefaultDamageHigh, RumbleSpec.DefaultDamageSeconds)));
            Assert.That(
                spec.Explosion,
                Is.EqualTo(new RumblePulse(RumbleSpec.DefaultExplosionLow, RumbleSpec.DefaultExplosionHigh, RumbleSpec.DefaultExplosionSeconds)));
            Assert.That(spec.ExplosionRadius, Is.EqualTo(RumbleSpec.DefaultExplosionRadius));
        }

        [Test]
        public void TheItemNamesAreTheConstructorArguments()
        {
            Assert.That(RumbleJson.ItemNames, Is.EqualTo(_expectedItemNames));
        }

        [Test]
        public void ZeroTurnsAnEventOff()
        {
            var spec = RumbleJson.Parse("{\"fireLow\": 0, \"fireHigh\": 0, \"damageSeconds\": 0}", "rumble.json");

            Assert.That(spec.Fire.IsSilent, Is.True);
            Assert.That(spec.Damage.IsSilent, Is.True);
            Assert.That(spec.Explosion.IsSilent, Is.False);
        }

        [Test]
        public void AnOutOfRangeValueNamesItsItem()
        {
            var low = Assert.Throws<TuningJsonException>(() => RumbleJson.Parse("{\"fireLow\": 1.5}", "rumble.json"));
            var high = Assert.Throws<TuningJsonException>(() => RumbleJson.Parse("{\"damageHigh\": -0.1}", "rumble.json"));
            var seconds = Assert.Throws<TuningJsonException>(() => RumbleJson.Parse("{\"explosionSeconds\": -1}", "rumble.json"));
            var radius = Assert.Throws<TuningJsonException>(() => RumbleJson.Parse("{\"explosionRadius\": 0}", "rumble.json"));

            Assert.That(low.SourceName, Is.EqualTo("rumble.json"));
            Assert.That(low.Item, Is.EqualTo("fireLow"));
            Assert.That(low.Message, Does.StartWith("rumble JSON 'rumble.json' item 'fireLow' is invalid: "));
            Assert.That(low.InnerException, Is.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(high.Item, Is.EqualTo("damageHigh"));
            Assert.That(seconds.Item, Is.EqualTo("explosionSeconds"));
            Assert.That(radius.Item, Is.EqualTo("explosionRadius"));
        }

        [Test]
        public void AnUnknownItemAWrongTypeOrBrokenJsonIsRejected()
        {
            var unknown = Assert.Throws<TuningJsonException>(() => RumbleJson.Parse("{\"landingLow\": 0.5}", "rumble.json"));
            var wrongType = Assert.Throws<TuningJsonException>(() => RumbleJson.Parse("{\"fireHigh\": \"strong\"}", "rumble.json"));
            var broken = Assert.Throws<TuningJsonException>(() => RumbleJson.Parse("{\"fireHigh\": ", "rumble.json"));
            var notObject = Assert.Throws<TuningJsonException>(() => RumbleJson.Parse("[1]", "rumble.json"));

            Assert.That(unknown.Item, Is.EqualTo("landingLow"));
            Assert.That(unknown.Message, Does.Contain("explosionRadius"), "lists the known items");
            Assert.That(wrongType.Item, Is.EqualTo("fireHigh"));
            Assert.That(broken.Item, Is.Null);
            Assert.That(broken.SourceName, Is.EqualTo("rumble.json"));
            Assert.That(notObject.Item, Is.Null);
            Assert.That(
                Assert.Throws<TuningJsonException>(() => RumbleJson.Parse("{\"x\": 1}", null)).SourceName,
                Is.EqualTo("(unnamed rumble JSON)"));
            Assert.Throws<ArgumentNullException>(() => RumbleJson.Parse(null, "rumble.json"));
        }
    }
}
