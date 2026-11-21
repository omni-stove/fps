using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class NetMapPickupsTests
    {
        private const string _rifleId = "00000000-0000-4000-8000-000000000001";
        private const string _herbId = "0d3c6f1a-5b2e-4c7d-8a9f-1e2b3c4d5e6f";

        private static NetMapPickups Sample()
        {
            return new NetMapPickups(
                new List<NetMapPickup>
                {
                    new NetMapPickup("Pickups/Rifle", PickupSpec.Weapon(_rifleId, 30), new Vector3(1f, 0.5f, -2.25f), 1f, 20f, false),
                    new NetMapPickup("Pickups/Ammo", PickupSpec.ReserveAmmo(45), new Vector3(2f, 0.5f, 0f), 1.25f, 15f, true),
                    new NetMapPickup("Pickups/RifleAmmo", PickupSpec.ReserveAmmo(30, _rifleId), new Vector3(2f, 0.5f, 4f), 1f, 15f, true),
                    new NetMapPickup("Pickups/Medkit", PickupSpec.Health(50, PickupEligibility.Only(new Side(1))), new Vector3(-3f, 0.5f, 0f), 1f, 0f, true),
                    new NetMapPickup("Pickups/Armor", PickupSpec.Armor(25), new Vector3(-3f, 0.5f, 3f), 1f, 30f, true),
                    new NetMapPickup("Pickups/Herb", PickupSpec.Item(_herbId, 3), new Vector3(0f, 2f, 9f), 0.75f, 10f, false),
                },
                new List<NetMapAmmoCrate>
                {
                    new NetMapAmmoCrate("Crates/North", new Vector3(0f, 0.5f, 12f), 1.6f, 1f),
                });
        }

        [Test]
        public void SpawnPointsAndAmmoCratesReadBack()
        {
            var read = NetMapPickups.Parse(NetMapPickups.Write(Sample()), "NetArena01.pickups.json");

            Assert.That(read.Pickups, Has.Count.EqualTo(6));
            var rifle = read.Pickups[0];
            Assert.That(rifle.Id, Is.EqualTo("Pickups/Rifle"));
            Assert.That(rifle.Spec.Kind, Is.EqualTo(PickupKind.Weapon));
            Assert.That(rifle.Spec.WeaponId, Is.EqualTo(_rifleId));
            Assert.That(rifle.Spec.Amount, Is.EqualTo(30));
            Assert.That(rifle.Position, Is.EqualTo(new Vector3(1f, 0.5f, -2.25f)));
            Assert.That(rifle.Radius, Is.EqualTo(1f));
            Assert.That(rifle.RespawnSeconds, Is.EqualTo(20f));
            Assert.That(rifle.Touch, Is.False);
            Assert.That(read.Pickups[1].Spec.WeaponId, Is.Null, "ammo without a weapon goes to the weapon in hand");
            Assert.That(read.Pickups[1].Touch, Is.True);
            Assert.That(read.Pickups[2].Spec.WeaponId, Is.EqualTo(_rifleId));
            Assert.That(read.Pickups[3].Spec.Kind, Is.EqualTo(PickupKind.Health));
            Assert.That(read.Pickups[3].Spec.Eligibility, Is.EqualTo(PickupEligibility.Only(new Side(1))));
            Assert.That(read.Pickups[3].RespawnSeconds, Is.Zero, "0 means the pickup comes once");
            Assert.That(read.Pickups[4].Spec.Kind, Is.EqualTo(PickupKind.Armor));
            Assert.That(read.Pickups[5].Spec.ItemId, Is.EqualTo(_herbId));
            Assert.That(read.Pickups[5].Spec.Amount, Is.EqualTo(3));
            Assert.That(read.Pickups[5].Radius, Is.EqualTo(0.75f));
            Assert.That(read.AmmoCrates, Has.Count.EqualTo(1));
            Assert.That(read.AmmoCrates[0].Id, Is.EqualTo("Crates/North"));
            Assert.That(read.AmmoCrates[0].Position, Is.EqualTo(new Vector3(0f, 0.5f, 12f)));
            Assert.That(read.AmmoCrates[0].Radius, Is.EqualTo(1.6f));
            Assert.That(read.AmmoCrates[0].CooldownSeconds, Is.EqualTo(1f));
        }

        [Test]
        public void AnEmptyFileHasNoPickupsAndNoneWritesEmptyLists()
        {
            var read = NetMapPickups.Parse("{}", "Empty.pickups.json");

            Assert.That(read.Pickups, Is.Empty);
            Assert.That(read.AmmoCrates, Is.Empty);
            Assert.That(NetMapPickups.Parse(NetMapPickups.Write(NetMapPickups.None), "None.pickups.json").Pickups, Is.Empty);
        }

        [Test]
        public void TouchIsOnWhenLeftOut()
        {
            var read = NetMapPickups.Parse(
                "{ \"pickups\": [ { \"id\": \"a\", \"kind\": \"Health\", \"amount\": 10, \"position\": [0, 0, 0], \"radius\": 1, \"respawnSeconds\": 5 } ] }",
                "a.pickups.json");

            Assert.That(read.Pickups[0].Touch, Is.True);
        }

        [TestCase("{ \"pickups\": [ { \"id\": \"a\", \"kind\": \"Health\", \"amount\": 0, \"position\": [0, 0, 0], \"radius\": 1, \"respawnSeconds\": 0 } ] }", "pickups[0].amount")]
        [TestCase("{ \"pickups\": [ { \"id\": \"a\", \"kind\": \"Weapon\", \"amount\": 5, \"position\": [0, 0, 0], \"radius\": 1, \"respawnSeconds\": 0 } ] }", "pickups[0].weapon")]
        [TestCase("{ \"pickups\": [ { \"id\": \"a\", \"kind\": \"Health\", \"amount\": 5, \"weapon\": \"x\", \"position\": [0, 0, 0], \"radius\": 1, \"respawnSeconds\": 0 } ] }", "pickups[0].weapon")]
        [TestCase("{ \"pickups\": [ { \"id\": \"a\", \"kind\": \"Weapon\", \"amount\": 5, \"weapon\": \"x\", \"item\": \"y\", \"position\": [0, 0, 0], \"radius\": 1, \"respawnSeconds\": 0 } ] }", "pickups[0].item")]
        [TestCase("{ \"pickups\": [ { \"id\": \"a\", \"kind\": \"Item\", \"amount\": 5, \"position\": [0, 0, 0], \"radius\": 1, \"respawnSeconds\": 0 } ] }", "pickups[0].item")]
        [TestCase("{ \"pickups\": [ { \"id\": \"a\", \"kind\": \"Tag\", \"amount\": 1, \"position\": [0, 0, 0], \"radius\": 1, \"respawnSeconds\": 0 } ] }", "pickups[0].kind")]
        [TestCase("{ \"pickups\": [ { \"id\": \"a\", \"kind\": \"Health\", \"amount\": 5, \"position\": [0, 0, 0], \"radius\": 0, \"respawnSeconds\": 0 } ] }", "pickups[0].radius")]
        [TestCase("{ \"pickups\": [ { \"id\": \"a\", \"kind\": \"Health\", \"amount\": 5, \"position\": [0, 0, 0], \"radius\": 1, \"respawnSeconds\": -1 } ] }", "pickups[0].respawnSeconds")]
        [TestCase("{ \"pickups\": [ { \"id\": \"a\", \"kind\": \"Health\", \"amount\": 5, \"sideRule\": \"OnlySide\", \"position\": [0, 0, 0], \"radius\": 1, \"respawnSeconds\": 0 } ] }", "pickups[0].side")]
        [TestCase("{ \"pickups\": [ { \"id\": \"a\", \"kind\": \"Health\", \"amount\": 5, \"position\": [0, 0, 0], \"radius\": 1, \"respawnSeconds\": 0 }, { \"id\": \"a\", \"kind\": \"Health\", \"amount\": 5, \"position\": [0, 0, 0], \"radius\": 1, \"respawnSeconds\": 0 } ] }", "pickups[1].id")]
        [TestCase("{ \"pickups\": [ { \"id\": \"a\", \"kind\": \"Health\", \"amount\": 5, \"position\": [0, 0, 0], \"radius\": 1, \"respawnSeconds\": 0, \"color\": \"red\" } ] }", "pickups[0].color")]
        [TestCase("{ \"ammoCrates\": [ { \"id\": \"c\", \"position\": [0, 0, 0], \"radius\": 1.6, \"cooldownSeconds\": -1 } ] }", "ammoCrates[0].cooldownSeconds")]
        [TestCase("{ \"ammoCrates\": [ { \"id\": \"c\", \"position\": [0, 0, 0], \"radius\": 0, \"cooldownSeconds\": 1 } ] }", "ammoCrates[0].radius")]
        [TestCase("{ \"ammoCrates\": [ { \"id\": \"\", \"position\": [0, 0, 0], \"radius\": 1, \"cooldownSeconds\": 1 } ] }", "ammoCrates[0].id")]
        public void ABrokenEntryNamesItsItem(string text, string item)
        {
            var e = Assert.Throws<ServerDataJsonException>(() => NetMapPickups.Parse(text, "bad.pickups.json"));

            Assert.That(e.Item, Is.EqualTo(item), e.Message);
        }

        [Test]
        public void TheConstructorsRejectBadValues()
        {
            var health = PickupSpec.Health(10);
            Assert.Throws<ArgumentException>(() => new NetMapPickup(" ", health, Vector3.Zero, 1f, 0f, true));
            Assert.Throws<ArgumentNullException>(() => new NetMapPickup("a", null, Vector3.Zero, 1f, 0f, true));
            Assert.Throws<ArgumentException>(() => new NetMapPickup("a", PickupSpec.Tag(1, null), Vector3.Zero, 1f, 0f, true));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NetMapPickup("a", health, new Vector3(float.NaN, 0f, 0f), 1f, 0f, true));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NetMapPickup("a", health, Vector3.Zero, float.PositiveInfinity, 0f, true));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NetMapPickup("a", health, Vector3.Zero, 1f, float.NaN, true));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NetMapAmmoCrate("c", Vector3.Zero, -1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NetMapAmmoCrate("c", Vector3.Zero, 1f, float.PositiveInfinity));

            var pickup = new NetMapPickup("a", health, Vector3.Zero, 1f, 0f, true);
            Assert.Throws<ArgumentException>(() => new NetMapPickups(new[] { pickup, pickup }, null));
            Assert.Throws<ArgumentException>(() => new NetMapPickups(new NetMapPickup[] { null }, null));
            Assert.Throws<ArgumentNullException>(() => NetMapPickups.Write(null));
        }
    }
}
