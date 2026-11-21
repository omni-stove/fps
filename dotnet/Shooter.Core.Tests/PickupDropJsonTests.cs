using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class PickupDropJsonTests
    {
        [Test]
        public void TheDefaultsMatchTheOfflineDropOnDeath()
        {
            var spec = PickupDropSpec.Default;

            Assert.That(spec.DropsWeapon, Is.True);
            Assert.That(spec.ReserveAmmo, Is.EqualTo(30));
            Assert.That(spec.LifetimeSeconds, Is.EqualTo(30f));
            Assert.That(spec.Reach, Is.EqualTo(1f));
            Assert.That(spec.SpreadRadius, Is.EqualTo(0.75f));
            Assert.That(spec.DropsAnything, Is.True);
        }

        [Test]
        public void AnEmptyFileKeepsEveryDefault()
        {
            var spec = PickupDropJson.Parse("{}", "pickup-drops.json");

            Assert.That(spec.DropsWeapon, Is.EqualTo(PickupDropSpec.DefaultDropsWeapon));
            Assert.That(spec.ReserveAmmo, Is.EqualTo(PickupDropSpec.DefaultReserveAmmo));
            Assert.That(spec.LifetimeSeconds, Is.EqualTo(PickupDropSpec.DefaultLifetimeSeconds));
            Assert.That(spec.Reach, Is.EqualTo(PickupDropSpec.DefaultReach));
            Assert.That(spec.SpreadRadius, Is.EqualTo(PickupDropSpec.DefaultSpreadRadius));
        }

        [Test]
        public void EveryItemReadsBack()
        {
            var written = PickupDropJson.Write(new PickupDropSpec(false, 0, 0f, 1.5f, 0f));
            var spec = PickupDropJson.Parse(written, "pickup-drops.json");

            Assert.That(spec.DropsWeapon, Is.False);
            Assert.That(spec.ReserveAmmo, Is.Zero);
            Assert.That(spec.LifetimeSeconds, Is.Zero, "0 keeps drops until someone takes them");
            Assert.That(spec.Reach, Is.EqualTo(1.5f));
            Assert.That(spec.SpreadRadius, Is.Zero);
            Assert.That(spec.DropsAnything, Is.False);
        }

        [TestCase("{\"reserveAmmo\": -1}", "reserveAmmo")]
        [TestCase("{\"lifetimeSeconds\": -1}", "lifetimeSeconds")]
        [TestCase("{\"reach\": 0}", "reach")]
        [TestCase("{\"spreadRadius\": -0.5}", "spreadRadius")]
        [TestCase("{\"dropsWeapon\": 1}", "dropsWeapon")]
        [TestCase("{\"dropWeapon\": true}", "dropWeapon")]
        public void ABrokenItemIsNamed(string text, string item)
        {
            var e = Assert.Throws<TuningJsonException>(() => PickupDropJson.Parse(text, "pickup-drops.json"));

            Assert.That(e.Item, Is.EqualTo(item), e.Message);
        }

        [Test]
        public void WriteRejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => PickupDropJson.Write(null));
        }
    }
}
