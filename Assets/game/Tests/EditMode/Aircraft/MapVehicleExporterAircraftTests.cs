using System.Collections.Generic;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // 書き出しは IVehicleTopSpeed しか見ないので、飛行機がその口で急降下の速さまで渡すことをここで確かめる。
    public sealed class MapVehicleExporterAircraftTests
    {
        private readonly List<Object> _created = new List<Object>();

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
        public void AnAircraftsDriveLimitCoversItsDiveSpeed()
        {
            var root = new GameObject("Plane");
            _created.Add(root);
            root.transform.position = new Vector3(-800f, 40f, 800f);
            var seat = new GameObject("Pilot Seat").AddComponent<VehicleSeat>();
            seat.transform.SetParent(root.transform, false);
            var vehicle = root.AddComponent<Vehicle>();
            vehicle.Configure(new VehicleSpec("plane", 500, new[] { new VehicleSeatSpec(VehicleSeatRole.Driver) }));
            var aircraft = root.AddComponent<SimpleAircraftMotor>();
            var spec = new AircraftSpec(maxThrust: 10f, aero: new AircraftAeroSpec(maxSpeed: 100f));
            aircraft.Configure(spec);

            var vehicles = MapVehicleExporter.Collect(new[] { root });

            Assert.That(vehicles.Vehicles, Has.Count.EqualTo(1));
            var expected = AircraftAero.TopSpeed(spec, Physics.gravity.magnitude) * VehicleDriveSpec.DefaultTopSpeedMargin;
            Assert.That(vehicles.Vehicles[0].Drive.MaxSpeed, Is.EqualTo(expected).Within(1e-3f));
            Assert.That(vehicles.Vehicles[0].Drive.MaxSpeed, Is.GreaterThan(100f * VehicleDriveSpec.DefaultTopSpeedMargin));
        }
    }
}
