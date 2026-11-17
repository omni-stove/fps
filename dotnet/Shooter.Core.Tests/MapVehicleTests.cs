using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class MapVehicleTests
    {
        private static readonly Quaternion _facingRight = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2f);

        [Test]
        public void SeatsAndExitsTurnWithTheVehicle()
        {
            var jeep = TestVehicles.Jeep(new Vector3(10f, 0f, 0f), _facingRight);

            // 右を向くと、乗り物の左 (-X) はワールドの +Z になる
            var feet = jeep.SeatFeet(0, jeep.Position, jeep.Rotation);
            Assert.That(feet.X, Is.EqualTo(10.3f).Within(1e-4f));
            Assert.That(feet.Y, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(feet.Z, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(jeep.ExitsOf(0), Is.EqualTo(new[] { new Vector3(-2f, 0f, 0.3f) }));
            Assert.That(jeep.ExitsOf(1), Is.EqualTo(new[] { new Vector3(3f, 0f, 0f) }), "a seat without exits uses the vehicle's");
        }

        [Test]
        public void TheEnterDistanceIsToTheNearestEnterPoint()
        {
            var jeep = TestVehicles.Jeep(Vector3.Zero, Quaternion.Identity);

            Assert.That(jeep.DistanceToEnter(0, jeep.Position, jeep.Rotation, new Vector3(-2.5f, 0f, 0.3f)), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(jeep.DistanceToEnter(2, jeep.Position, jeep.Rotation, new Vector3(0f, 1.2f, -3.2f)), Is.EqualTo(2f).Within(1e-4f), "no enter points: the seat itself");
        }

        [Test]
        public void ARayHitsTheNearestHullBoxAndItsPart()
        {
            var jeep = TestVehicles.Jeep(Vector3.Zero, Quaternion.Identity);

            Assert.That(jeep.RaycastHull(jeep.Position, jeep.Rotation, new Vector3(0f, 1f, 10f), -Vector3.UnitZ, 100f, out var distance, out var normal, out var box), Is.True);
            Assert.That(distance, Is.EqualTo(8f).Within(1e-4f), "the engine box front is at z=2");
            Assert.That(normal, Is.EqualTo(Vector3.UnitZ));
            Assert.That(box, Is.EqualTo(1));
            Assert.That(jeep.PartMultiplier(box), Is.EqualTo(2f));
            Assert.That(jeep.PartMultiplier(0), Is.EqualTo(1f));
        }

        [Test]
        public void ARayFromInsideTheHullDoesNotHitIt()
        {
            var jeep = TestVehicles.Jeep(Vector3.Zero, Quaternion.Identity);

            Assert.That(jeep.RaycastHull(jeep.Position, jeep.Rotation, new Vector3(0f, 0.5f, 0f), Vector3.UnitX, 100f, out _, out _, out _), Is.False);
        }

        [Test]
        public void ARayPastTheHullMisses()
        {
            var jeep = TestVehicles.Jeep(Vector3.Zero, _facingRight);

            // 右を向いた車の長さは X に沿うので、Z に 1.5 m 離れた線は車体の横を通る
            Assert.That(jeep.RaycastHull(jeep.Position, jeep.Rotation, new Vector3(-10f, 0.5f, 1.5f), Vector3.UnitX, 100f, out _, out _, out _), Is.False);
            Assert.That(jeep.RaycastHull(jeep.Position, jeep.Rotation, new Vector3(-10f, 0.5f, 0.5f), Vector3.UnitX, 100f, out var distance, out _, out _), Is.True);
            Assert.That(distance, Is.EqualTo(8f).Within(1e-4f));
        }

        [Test]
        public void TheBlastDistanceIsToTheClosestBox()
        {
            var jeep = TestVehicles.Jeep(Vector3.Zero, Quaternion.Identity);

            Assert.That(jeep.DistanceToHull(jeep.Position, jeep.Rotation, new Vector3(4f, 0.5f, 0f)), Is.EqualTo(3f).Within(1e-4f));
            Assert.That(jeep.DistanceToHull(jeep.Position, jeep.Rotation, new Vector3(0f, 0.5f, 0f)), Is.EqualTo(0f));
        }

        [Test]
        public void TheRiderOriginIsAboveTheSeatByTheFeetOffset()
        {
            var geometry = new StanceGeometry(2f, 0f, 0.6f, 1.2f, 0.5f);
            var feet = new Vector3(1f, 2f, 3f);

            var origin = MapVehicle.RiderOrigin(feet, geometry, crouched: true);

            Assert.That(origin.Y + geometry.Shape(true).BottomY, Is.EqualTo(2f).Within(1e-5f));
            Assert.That(origin.X, Is.EqualTo(1f));
        }

        [Test]
        public void TheWeaponsMustMatchTheMountedIds()
        {
            var other = new WeaponSpec("0a1b2c3d-4e5f-4a6b-8c7d-9e0f1a2b3c4d", "weapon.other", WeaponCategory.AssaultRifle);

            Assert.That(
                () => new MapVehicle(TestVehicles.Spec(), Vector3.Zero, Quaternion.Identity, TestVehicles.Jeep(Vector3.Zero, Quaternion.Identity).Seats, weapons: new[] { other }),
                Throws.ArgumentException.With.Message.Contains("mounts " + TestVehicles.GunId));
            Assert.That(
                () => new MapVehicle(TestVehicles.Spec(), Vector3.Zero, Quaternion.Identity, TestVehicles.Jeep(Vector3.Zero, Quaternion.Identity).Seats),
                Throws.ArgumentException.With.Message.Contains("1 weapons but 0"));
        }

        [Test]
        public void EverySeatNeedsAPosition()
        {
            Assert.That(
                () => new MapVehicle(TestVehicles.Spec(), Vector3.Zero, Quaternion.Identity, new[] { new MapVehicleSeat(Vector3.Zero) }, weapons: new[] { TestVehicles.Gun() }),
                Throws.ArgumentException.With.Message.Contains("1 seat positions but its spec has 3 seats"));
        }
    }
}
