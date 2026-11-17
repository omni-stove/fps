using System.Linq;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Tests.EditMode
{
    public sealed class CarRangeFixtureTests
    {
        private const float Epsilon = 1e-3f;

        private Scene _scene;

        [SetUp]
        public void SetUp()
        {
            _scene = TestScenes.NewAdditive();
        }

        [TearDown]
        public void TearDown()
        {
            if (_scene.IsValid())
            {
                EditorSceneManager.CloseScene(_scene, true);
            }
        }

        [Test]
        public void TheRangeFindsTheLane()
        {
            Assert.That(TpsRangeSceneBuilder.Fixtures().OfType<CarRangeFixture>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void PlacesADrivableCarFacingATarget()
        {
            var root = new CarRangeFixture().Place(_scene, GameRulesAssetBuilder.TpsRangeRules());

            Assert.That(root.name, Is.EqualTo(CarRangeFixture.RootName));
            Assert.That(root.scene, Is.EqualTo(_scene));
            Assert.That(root.transform.parent, Is.Null);

            var vehicle = root.GetComponentInChildren<Vehicle>();
            Assert.That(vehicle.name, Is.EqualTo(CarRangeFixture.CarName));
            Assert.That(vehicle.VehicleId, Is.EqualTo(CarFixtureBuilder.VehicleId));
            Assert.That(Vector3.Distance(vehicle.transform.position, CarRangeFixture.CarOrigin), Is.LessThan(Epsilon));
            Assert.That(Vector3.Angle(vehicle.transform.forward, Vector3.right), Is.LessThan(0.1f));
            Assert.That(vehicle.Seats.Select(s => s.Role), Is.EqualTo(new[] { VehicleSeatRole.Driver, VehicleSeatRole.Passenger }));

            var motor = vehicle.GetComponent<SimpleCarMotor>();
            Assert.That(motor, Is.Not.Null);
            Assert.That(motor.Spec.MotorTorque, Is.EqualTo(CarSpec.DefaultMotorTorque));
            Assert.That(motor.Body.mass, Is.EqualTo(CarSpec.DefaultMass));
            Assert.That(motor.Body.automaticCenterOfMass, Is.False);
            Assert.That(Vector3.Distance(motor.Body.centerOfMass, CarFixtureBuilder.CenterOfMass), Is.LessThan(Epsilon));
            Assert.That(motor.Wheels.Count, Is.EqualTo(4));
            Assert.That(motor.Wheels.Count(w => w.Steers), Is.EqualTo(2));
            Assert.That(motor.Wheels.Count(w => w.Drives), Is.EqualTo(2));
            Assert.That(motor.Wheels.All(w => !w.Steers || w.Collider.transform.localPosition.z > 0f), Is.True, "the front wheels steer");
            Assert.That(motor.Wheels.All(w => !w.Drives || w.Collider.transform.localPosition.z < 0f), Is.True, "the rear wheels drive");
            Assert.That(motor.Wheels.All(w => w.Collider.radius == CarFixtureBuilder.WheelRadius), Is.True);
            Assert.That(motor.Wheels.All(w => w.Visual != null && w.Visual.GetComponentInChildren<Collider>() == null), Is.True, "the tires do not collide");

            Assert.That(vehicle.GetComponent<VehicleMotorLink>(), Is.Not.Null);
            var impact = vehicle.GetComponent<VehicleImpactDamage>();
            Assert.That(impact, Is.Not.Null);
            Assert.That(impact.Spec.MinSpeed, Is.EqualTo(VehicleImpactSpec.DefaultMinSpeed));

            var dummy = root.GetComponentInChildren<TargetDummy>();
            Assert.That(dummy.Damageable.Id, Is.EqualTo(CarRangeFixture.TargetId));
            Assert.That(Vector3.Distance(dummy.transform.position, CarRangeFixture.TargetGround), Is.LessThan(Epsilon));
            Assert.That(Vector3.Angle(dummy.transform.forward, Vector3.left), Is.LessThan(0.1f), "the target faces the car");
        }

        [Test]
        public void TheFootprintCoversTheCarAndTheTarget()
        {
            var footprint = new CarRangeFixture().Footprint;
            var car = CarFixtureBuilder.BodyBounds(CarRangeFixture.CarOrigin, CarRangeFixture.CarRotation);

            Assert.That(footprint.min.x, Is.LessThanOrEqualTo(car.min.x + Epsilon));
            Assert.That(footprint.min.z, Is.LessThanOrEqualTo(car.min.z + Epsilon));
            Assert.That(footprint.max.y, Is.GreaterThanOrEqualTo(car.max.y - Epsilon));
            Assert.That(footprint.max.z, Is.GreaterThanOrEqualTo(car.max.z - Epsilon));
            Assert.That(footprint.max.x, Is.GreaterThan(CarRangeFixture.TargetGround.x));
            Assert.That(footprint.max.y, Is.GreaterThanOrEqualTo(TargetDummy.HeadLocalPosition.y));
            Assert.That(footprint.min.y, Is.EqualTo(0f).Within(Epsilon));
            Assert.That(footprint.size.z, Is.EqualTo(CarRangeFixture.LaneHalfWidth * 2f).Within(Epsilon));
        }

        [Test]
        public void AGunnerWeaponAddsAStandingGunnerSeatWithARoofGun()
        {
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            try
            {
                weapon.name = "Rifle";
                WeaponAssetBuilder.Configure(weapon, WeaponAssetBuilder.RifleSpec());

                var vehicle = CarFixtureBuilder.Create(_scene, null, "GunCar", Vector3.zero, Quaternion.identity, weapon);

                Assert.That(vehicle.VehicleId, Is.EqualTo(CarFixtureBuilder.GunnerVehicleId));
                Assert.That(vehicle.Seats.Select(s => s.Role), Is.EqualTo(new[] { VehicleSeatRole.Driver, VehicleSeatRole.Passenger, VehicleSeatRole.Gunner }));
                var gunner = vehicle.Seats[2];
                Assert.That(gunner.name, Is.EqualTo(CarFixtureBuilder.GunnerSeatName));
                Assert.That(gunner.Pose, Is.EqualTo(VehicleSeatPose.Standing));
                Assert.That(gunner.transform.localPosition.y, Is.EqualTo(1.3f).Within(Epsilon), "the gunner stands on the roof");
                Assert.That(gunner.EnterPoints.Single().localPosition, Is.EqualTo(CarFixtureBuilder.GunnerEnterLocal));
                Assert.That(vehicle.FiringOf(vehicle.Seats[0]), Is.EqualTo(VehicleSeatFiring.None));
                Assert.That(vehicle.FiringOf(gunner), Is.EqualTo(VehicleSeatFiring.VehicleWeapon));

                var mount = vehicle.WeaponMounts.Single();
                Assert.That(mount.name, Is.EqualTo(CarFixtureBuilder.GunName));
                Assert.That(mount.SeatIndex, Is.EqualTo(2));
                Assert.That(mount.WeaponSpec.Id, Is.EqualTo(WeaponAssetBuilder.RifleId));
                Assert.That(mount.Spec.PitchMinDegrees, Is.EqualTo(CarFixtureBuilder.GunPitchMinDegrees));
                Assert.That(mount.Spec.TurnSpeedDegreesPerSecond, Is.EqualTo(CarFixtureBuilder.GunTurnSpeedDegreesPerSecond));
                Assert.That(mount.transform.localPosition, Is.EqualTo(new Vector3(0f, 2.3f, -0.6f)));
                Assert.That(mount.Muzzle.name, Is.EqualTo(CarFixtureBuilder.MuzzleName));
                Assert.That(mount.GetComponentInChildren<Collider>(), Is.Null, "the barrel does not block the rider or the rounds");
                Assert.That(vehicle.GetComponent<SimpleCarMotor>(), Is.Not.Null, "the gun car still drives");
            }
            finally
            {
                Object.DestroyImmediate(weapon);
            }
        }

        [Test]
        public void WithoutAGunnerWeaponTheCarKeepsTwoSeatsAndItsId()
        {
            var vehicle = CarFixtureBuilder.Create(_scene, null, "Car", Vector3.zero, Quaternion.identity);

            Assert.That(vehicle.VehicleId, Is.EqualTo(CarFixtureBuilder.VehicleId));
            Assert.That(vehicle.Seats.Count, Is.EqualTo(2));
            Assert.That(vehicle.WeaponMounts, Is.Empty);
            Assert.That(CarFixtureBuilder.Spec().Weapons, Is.Empty);
            Assert.That(CarFixtureBuilder.Spec("rifle").Seats.Count, Is.EqualTo(3));
        }
    }
}
