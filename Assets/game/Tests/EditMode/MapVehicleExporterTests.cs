using System.Collections.Generic;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class MapVehicleExporterTests
    {
        private static readonly Vector3 _origin = new Vector3(-800f, 40f, 800f);

        private readonly List<Object> _created = new List<Object>();
        private GameObject _floor;
        private Vehicle _car;

        [SetUp]
        public void SetUp()
        {
            _floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _floor.name = "Floor";
            _floor.transform.position = _origin + new Vector3(0f, -0.5f, 0f);
            _floor.transform.localScale = new Vector3(30f, 1f, 30f);
            _created.Add(_floor);

            _car = TestCars.Create(_created);
            _car.transform.SetPositionAndRotation(_origin + new Vector3(2f, 0f, 3f), Quaternion.Euler(0f, 90f, 0f));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        [Test]
        public void TheVehicleIsWrittenWithItsSeatsHullAndDriveLimit()
        {
            var vehicles = MapVehicleExporter.Collect(new[] { _floor, _car.gameObject });

            Assert.That(vehicles.Vehicles, Has.Count.EqualTo(1));
            var car = vehicles.Vehicles[0];
            Assert.That(car.Spec.Id, Is.EqualTo(TestCars.VehicleId));
            Assert.That(car.Spec.MaxHealth, Is.EqualTo(500));
            AssertNear(car.Position, _origin + new Vector3(2f, 0f, 3f));
            Assert.That(car.Seats, Has.Count.EqualTo(2));
            AssertNear(car.Seats[0].Sit, new Vector3(-0.4f, 0.7f, 0.2f));
            AssertNear(car.Seats[1].Sit, new Vector3(0.4f, 0.7f, 0.2f));
            Assert.That(car.Seats[0].SitYawDegrees, Is.EqualTo(0f).Within(1e-3f));

            Assert.That(car.Hull, Has.Count.EqualTo(1), "the wheel colliders are not part of the hull");
            AssertNear(car.Hull[0].Center, new Vector3(0f, 0.9f, 0f));
            AssertNear(car.Hull[0].HalfExtents, new Vector3(0.9f, 0.4f, 2f));

            // TestCars の最高速度 20 m/秒に、既定の余裕 1.5 倍を掛ける
            Assert.That(car.Drive.MaxSpeed, Is.EqualTo(30f).Within(1e-4f));
        }

        [Test]
        public void AnAircraftsDriveLimitCoversItsDiveSpeed()
        {
            Object.DestroyImmediate(_car.GetComponent<SimpleCarMotor>());
            var aircraft = _car.gameObject.AddComponent<SimpleAircraftMotor>();
            var spec = new AircraftSpec(maxThrust: 10f, aero: new AircraftAeroSpec(maxSpeed: 100f));
            aircraft.Configure(spec);

            var vehicles = MapVehicleExporter.Collect(new[] { _car.gameObject });

            var expected = AircraftAero.TopSpeed(spec, Physics.gravity.magnitude) * VehicleDriveSpec.DefaultTopSpeedMargin;
            Assert.That(vehicles.Vehicles[0].Drive.MaxSpeed, Is.EqualTo(expected).Within(1e-3f));
            Assert.That(vehicles.Vehicles[0].Drive.MaxSpeed, Is.GreaterThan(100f * VehicleDriveSpec.DefaultTopSpeedMargin));
        }

        [Test]
        public void AVehicleWithoutATopSpeedUsesTheDefaultDriveLimit()
        {
            Object.DestroyImmediate(_car.GetComponent<SimpleCarMotor>());

            var vehicles = MapVehicleExporter.Collect(new[] { _car.gameObject });

            Assert.That(vehicles.Vehicles[0].Drive.MaxSpeed, Is.EqualTo(VehicleDriveSpec.DefaultMaxSpeed));
        }

        [Test]
        public void TheWrittenFileReadsBackTheSame()
        {
            var vehicles = MapVehicleExporter.Collect(new[] { _car.gameObject });

            var read = NetMapVehicles.Parse(NetMapVehicles.Write(vehicles), "memory");

            Assert.That(read.Vehicles, Has.Count.EqualTo(1));
            Assert.That(read.Vehicles[0].Spec.Seats[0].Role, Is.EqualTo(VehicleSeatRole.Driver));
            Assert.That(read.Vehicles[0].Spec.Seats[1].Role, Is.EqualTo(VehicleSeatRole.Passenger));
            AssertNear(read.Vehicles[0].Hull[0].HalfExtents, new Vector3(0.9f, 0.4f, 2f));
        }

        [Test]
        public void TheMapCollisionLeavesTheVehicleOut()
        {
            var map = MapCollisionExporter.Collect(new[] { _floor, _car.gameObject });

            Assert.That(map.Colliders, Has.Count.EqualTo(1), "only the floor; the server hits the vehicle through its exported hull");
        }

        [Test]
        public void ADisabledVehicleIsNotNumbered()
        {
            _car.enabled = false;

            Assert.That(MapVehicleExporter.Collect(new[] { _car.gameObject }).Vehicles, Is.Empty);
        }

        private static void AssertNear(System.Numerics.Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.X, Is.EqualTo(expected.x).Within(1e-3f));
            Assert.That(actual.Y, Is.EqualTo(expected.y).Within(1e-3f));
            Assert.That(actual.Z, Is.EqualTo(expected.z).Within(1e-3f));
        }
    }
}
