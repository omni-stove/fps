using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
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
        public void InvalidItemsAreNamed(string text, string item)
        {
            var error = Assert.Throws<ServerDataJsonException>(() => NetMapVehicles.Parse(text, "test"));

            Assert.That(error.Message, Does.Contain(item));
        }
    }
}
