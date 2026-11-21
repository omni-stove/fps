using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class NetMapVehiclesTests
    {
        [Test]
        public void WrittenVehiclesReadBackTheSame()
        {
            var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.5f);
            var vehicles = new NetMapVehicles(new[]
            {
                TestVehicles.Jeep(new Vector3(1f, 2f, 3f), rotation, 800, new VehicleDriveSpec(25f, 2f, 0.25f)),
                TestVehicles.Jeep(new Vector3(-4f, 0f, 9f), Quaternion.Identity),
            });

            var text = NetMapVehicles.Write(vehicles);
            var read = NetMapVehicles.Parse(text, "test");

            Assert.That(read.Vehicles, Has.Count.EqualTo(2));
            var jeep = read.Vehicles[0];
            Assert.That(jeep.Spec.Id, Is.EqualTo(TestVehicles.JeepId));
            Assert.That(jeep.Spec.MaxHealth, Is.EqualTo(800));
            Assert.That(jeep.Position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(Quaternion.Dot(jeep.Rotation, rotation), Is.EqualTo(1f).Within(1e-6f));
            Assert.That(jeep.Spec.Seats[1].Role, Is.EqualTo(VehicleSeatRole.Passenger));
            Assert.That(jeep.Spec.Seats[1].OwnWeaponArcDegrees, Is.EqualTo(180f));
            Assert.That(jeep.Spec.Seats[2].Pose, Is.EqualTo(VehicleSeatPose.Standing));
            Assert.That(jeep.Seats[0].EnterPoints, Is.EqualTo(new[] { new Vector3(-1.5f, 0f, 0.3f) }));
            Assert.That(jeep.Seats[0].ExitPoints, Is.EqualTo(new[] { new Vector3(-2f, 0f, 0.3f) }));
            Assert.That(jeep.Seats[1].SitYawDegrees, Is.EqualTo(90f));
            Assert.That(jeep.ExitPoints, Is.EqualTo(new[] { new Vector3(3f, 0f, 0f) }));
            Assert.That(jeep.Spec.Weapons[0].SeatIndex, Is.EqualTo(2));
            Assert.That(jeep.Weapons[0].Id, Is.EqualTo(TestVehicles.GunId));
            Assert.That(jeep.Weapons[0].Impact.Damage, Is.EqualTo(30));
            Assert.That(jeep.Hull, Has.Count.EqualTo(2));
            Assert.That(jeep.Hull[1].PartId, Is.EqualTo("engine"));
            Assert.That(jeep.Hull[0].PartId, Is.Null);
            Assert.That(jeep.Spec.FindPart("engine").DamageMultiplier, Is.EqualTo(2f));
            Assert.That(jeep.Drive.MaxSpeed, Is.EqualTo(25f));
            Assert.That(jeep.Drive.CorrectionDistance, Is.EqualTo(2f));
            Assert.That(jeep.Drive.BurstSeconds, Is.EqualTo(0.25f));
            Assert.That(NetMapVehicles.Write(read), Is.EqualTo(text));
        }

        [Test]
        public void MissingTuningUsesTheDefaults()
        {
            var read = NetMapVehicles.Parse(
                "{ \"vehicles\": [ { \"id\": \"cart\", \"position\": [0, 0, 0], \"seats\": [ { \"role\": \"Driver\", \"sit\": [0, 1, 0] } ] } ] }",
                "test");

            var cart = read.Vehicles[0];
            Assert.That(cart.Spec.MaxHealth, Is.EqualTo(VehicleSpec.DefaultMaxHealth));
            Assert.That(cart.Spec.EnterRange, Is.EqualTo(VehicleSpec.DefaultEnterRange));
            Assert.That(cart.Rotation, Is.EqualTo(Quaternion.Identity));
            Assert.That(cart.Hull, Is.Empty);
            Assert.That(cart.Weapons, Is.Empty);
            Assert.That(cart.Drive.MaxSpeed, Is.EqualTo(VehicleDriveSpec.DefaultMaxSpeed));
            Assert.That(cart.Spec.Durability.BurnHealthFraction, Is.EqualTo(VehicleDurabilitySpec.DefaultBurnHealthFraction));
            Assert.That(cart.Spec.Durability.Explosion.Damage.MaxDamage, Is.EqualTo(VehicleDurabilitySpec.DefaultExplosionMaxDamage));
        }

        [Test]
        public void AVehicleWithoutCountermeasuresCarriesNoFlares()
        {
            var read = NetMapVehicles.Parse(
                "{ \"vehicles\": [ { \"id\": \"cart\", \"position\": [0, 0, 0], \"seats\": [ { \"role\": \"Driver\", \"sit\": [0, 1, 0] } ] } ] }",
                "test");

            Assert.That(read.Vehicles[0].Countermeasures, Is.Null);
            Assert.That(NetMapVehicles.Write(read), Does.Not.Contain(NetMapVehicles.CountermeasuresItem));
        }

        [Test]
        public void CountermeasuresReadBackTheSame()
        {
            var flares = new MapVehicleCountermeasures(
                new CountermeasureSpec(3, 0.5f, 2.5f, 20f, 8f, 0.1f), new Vector3(0f, -1f, -3f), new Vector3(1f, 0f, 0f));
            var jeep = TestVehicles.Jeep(Vector3.Zero, Quaternion.Identity);
            var heli = new MapVehicle(jeep.Spec, jeep.Position, jeep.Rotation, jeep.Seats, jeep.Hull, jeep.ExitPoints, jeep.Weapons, jeep.Drive, flares);

            var text = NetMapVehicles.Write(new NetMapVehicles(new[] { heli }));
            var read = NetMapVehicles.Parse(text, "test").Vehicles[0].Countermeasures;

            Assert.That(read, Is.Not.Null);
            Assert.That(read.Spec.Count, Is.EqualTo(3));
            Assert.That(read.Spec.IntervalSeconds, Is.EqualTo(0.5f));
            Assert.That(read.Spec.LifetimeSeconds, Is.EqualTo(2.5f));
            Assert.That(read.Spec.DecoyConeDegrees, Is.EqualTo(20f));
            Assert.That(read.Spec.EjectSpeed, Is.EqualTo(8f));
            Assert.That(read.Spec.GravityMultiplier, Is.EqualTo(0.1f));
            Assert.That(read.EjectOffset, Is.EqualTo(new Vector3(0f, -1f, -3f)));
            Assert.That(read.EjectDirection, Is.EqualTo(new Vector3(1f, 0f, 0f)));
        }

        [Test]
        public void AnEmptyCountermeasuresObjectUsesTheDefaults()
        {
            var read = NetMapVehicles.Parse(
                "{ \"vehicles\": [ { \"id\": \"heli\", \"position\": [0, 0, 0], \"seats\": [ { \"role\": \"Driver\", \"sit\": [0, 1, 0] } ], "
                + "\"countermeasures\": {} } ] }",
                "test");

            var flares = read.Vehicles[0].Countermeasures;
            Assert.That(flares, Is.Not.Null);
            Assert.That(flares.Spec.Count, Is.EqualTo(CountermeasureSpec.DefaultCount));
            Assert.That(flares.Spec.DecoyConeDegrees, Is.EqualTo(CountermeasureSpec.DefaultDecoyConeDegrees));
            Assert.That(flares.EjectOffset, Is.EqualTo(MapVehicleCountermeasures.DefaultEjectOffset));
            Assert.That(flares.EjectDirection, Is.EqualTo(MapVehicleCountermeasures.DefaultEjectDirection));
        }

        [Test]
        public void FlaresLeaveFromTheTurnedEjectPointWithTheBodyVelocity()
        {
            var flares = new MapVehicleCountermeasures(
                new CountermeasureSpec(ejectSpeed: 10f), new Vector3(0f, 0f, -2f), new Vector3(0f, 0f, -1f));
            var turned = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2f);

            var point = flares.EjectPoint(new Vector3(5f, 1f, 0f), turned);
            var velocity = flares.EjectVelocity(turned, new Vector3(0f, 0f, 3f));

            Assert.That(Vector3.Distance(point, new Vector3(3f, 1f, 0f)), Is.LessThan(1e-4f), "behind a vehicle facing +x is -x");
            Assert.That(Vector3.Distance(velocity, new Vector3(-10f, 0f, 3f)), Is.LessThan(1e-4f));
        }

        [Test]
        public void AZeroEjectDirectionDropsTheFlareStraightDown()
        {
            var flares = new MapVehicleCountermeasures(new CountermeasureSpec(ejectSpeed: 4f), Vector3.Zero, Vector3.Zero);

            Assert.That(flares.EjectVelocity(Quaternion.Identity, Vector3.Zero), Is.EqualTo(new Vector3(0f, -4f, 0f)));
        }

        [Test]
        public void AnEmptyFileHasNoVehicles()
        {
            Assert.That(NetMapVehicles.Parse("{ \"vehicles\": [] }", "test").Vehicles, Is.Empty);
            Assert.That(NetMapVehicles.None.Vehicles, Is.Empty);
        }

        [TestCase(
            "{ \"vehicles\": [ { \"id\": \"cart\", \"position\": [0, 0, 0], \"seats\": [ { \"role\": \"Driver\", \"sit\": [0, 1] } ] } ] }",
            "vehicles[0].seats[0].sit")]
        [TestCase(
            "{ \"vehicles\": [ { \"id\": \"cart\", \"position\": [0, 0, 0], \"rotation\": [0, 0, 0, 0], \"seats\": [ { \"role\": \"Driver\", \"sit\": [0, 1, 0] } ] } ] }",
            "vehicles[0].rotation")]
        [TestCase(
            "{ \"vehicles\": [ { \"id\": \"cart\", \"position\": [0, 0, 0], \"maxHealth\": 0, \"seats\": [ { \"role\": \"Driver\", \"sit\": [0, 1, 0] } ] } ] }",
            "vehicles[0].maxHealth")]
        [TestCase(
            "{ \"vehicles\": [ { \"id\": \"cart\", \"position\": [0, 0, 0], \"seats\": [ { \"role\": \"Driver\", \"sit\": [0, 1, 0] } ], \"drive\": { \"maxSpeed\": -1 } } ] }",
            "vehicles[0].drive.maxSpeed")]
        [TestCase(
            "{ \"vehicles\": [ { \"id\": \"cart\", \"position\": [0, 0, 0], \"seats\": [ { \"role\": \"Pilot\", \"sit\": [0, 1, 0] } ] } ] }",
            "vehicles[0].seats[0].role")]
        [TestCase(
            "{ \"vehicles\": [ { \"id\": \"cart\", \"position\": [0, 0, 0], \"wheels\": 4, \"seats\": [ { \"role\": \"Driver\", \"sit\": [0, 1, 0] } ] } ] }",
            "wheels")]
        [TestCase(
            "{ \"vehicles\": [ { \"id\": \"heli\", \"position\": [0, 0, 0], \"seats\": [ { \"role\": \"Driver\", \"sit\": [0, 1, 0] } ], \"countermeasures\": { \"count\": -1 } } ] }",
            "vehicles[0].countermeasures.count")]
        [TestCase(
            "{ \"vehicles\": [ { \"id\": \"heli\", \"position\": [0, 0, 0], \"seats\": [ { \"role\": \"Driver\", \"sit\": [0, 1, 0] } ], \"countermeasures\": { \"chaff\": 2 } } ] }",
            "chaff")]
        public void InvalidItemsAreNamed(string text, string item)
        {
            var error = Assert.Throws<ServerDataJsonException>(() => NetMapVehicles.Parse(text, "test"));

            Assert.That(error.Message, Does.Contain(item));
        }
    }
}
