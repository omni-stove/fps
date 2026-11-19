using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // Edit Mode では Update も FixedUpdate も走らないので、運転の部品が付く・外れる・道を受け取るところまでを確かめ、走らせるのは各部品のテストに任せる。
    public sealed class EnemyAgentVehicleControlTests
    {
        private const float Altitude = 500f;

        private readonly List<Object> _created = new List<Object>();
        private EnemyAgent _agent;
        private WeaponDefinition _rifle;

        [SetUp]
        public void SetUp()
        {
            _rifle = Track(ScriptableObject.CreateInstance<WeaponDefinition>());
            _rifle.name = "Rifle";
            WeaponAssets.Configure(_rifle, TestWeapons.RifleSpec());

            var host = Track(GameObject.CreatePrimitive(PrimitiveType.Capsule));
            host.name = "Enemy";
            host.transform.position = new Vector3(-1.3f, 1f, 0f);
            _agent = host.AddComponent<EnemyAgent>();
            _agent.WeaponDefinition = _rifle;
            _agent.Id = "enemy-1";
            host.GetComponent<Damageable>().Initialize("enemy-1", 10);
            Assert.That(_agent.TryInitialize(), Is.True);
        }

        [TearDown]
        public void TearDown()
        {
            for (var i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                {
                    Object.DestroyImmediate(_created[i]);
                }
            }

            _created.Clear();
        }

        [Test]
        public void ACarIsDrivenAndAnAircraftIsFlown()
        {
            Assert.That(EnemyAgent.FliesAsAircraft(CreateCar("Jeep")), Is.False);
            Assert.That(EnemyAgent.FliesAsAircraft(CreateAircraft("Plane")), Is.True);
            Assert.Throws<ArgumentNullException>(() => EnemyAgent.FliesAsAircraft(null));
        }

        [Test]
        public void TheOrderedRouteWinsOverTheRouteOnTheVehicle()
        {
            var car = CreateCar("Jeep");
            var onVehicle = AddRoute(car);
            var ordered = CreateRoute("Ordered");

            Assert.That(EnemyAgent.ChooseDriveRoute(ordered, car), Is.SameAs(ordered));
            Assert.That(EnemyAgent.ChooseDriveRoute(null, car), Is.SameAs(onVehicle));
            Assert.That(EnemyAgent.ChooseDriveRoute(null, CreateCar("Bare")), Is.Null);
            Assert.That(EnemyAgent.ChooseDriveRoute(null, null), Is.Null);
            Assert.That(EnemyAgent.ChooseDriveRoute(ordered, null), Is.SameAs(ordered));
        }

        [Test]
        public void ARemovedOrderedRouteFallsBackToTheRouteOnTheVehicle()
        {
            var car = CreateCar("Jeep");
            var onVehicle = AddRoute(car);
            var ordered = CreateRoute("Ordered");
            Object.DestroyImmediate(ordered.gameObject);

            Assert.That(EnemyAgent.ChooseDriveRoute(ordered, car), Is.SameAs(onVehicle));
        }

        [Test]
        public void SittingInTheDriverSeatOfACarAttachesADriverOnTheBot()
        {
            var car = CreateCar("Jeep");
            var route = AddRoute(car);

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Jeep' bot takes controls rider=enemy-1 as=driver route=Jeep");
            Assert.That(_agent.TryBoard(car, 0, requireInRange: false), Is.True);

            var driver = _agent.GetComponent<BotVehicleDriver>();
            Assert.That(driver, Is.Not.Null);
            Assert.That(_agent.VehicleDriver, Is.SameAs(driver));
            Assert.That(_agent.AircraftPilot, Is.Null);
            Assert.That(_agent.IsDrivingVehicle, Is.True);
            Assert.That(driver.Vehicle, Is.SameAs(car));
            Assert.That(driver.Rider, Is.SameAs(_agent));
            Assert.That(driver.Route, Is.SameAs(route));
            Assert.That(driver.DrivesUnmanned, Is.False, "the bot drives; no stand-in takes the seat");
            Assert.That(car.GetComponent<BotAircraftPilot>(), Is.Null);
        }

        [Test]
        public void LeavingTheCarRemovesTheDriver()
        {
            var car = CreateCar("Jeep");
            _agent.TryBoard(car, 0, requireInRange: false);

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Jeep' bot gives up controls rider=enemy-1");
            Assert.That(_agent.TryLeaveVehicle(), Is.True);

            Assert.That(_agent.GetComponent<BotVehicleDriver>(), Is.Null);
            Assert.That(_agent.VehicleDriver, Is.Null);
            Assert.That(_agent.IsDrivingVehicle, Is.False);
        }

        [Test]
        public void DyingAtTheWheelRemovesTheDriver()
        {
            var car = CreateCar("Jeep");
            _agent.TryBoard(car, 0, requireInRange: false);

            _agent.GetComponent<Damageable>().TakeDamage(10);

            Assert.That(_agent.IsRiding, Is.False);
            Assert.That(_agent.GetComponent<BotVehicleDriver>(), Is.Null);
            Assert.That(_agent.IsDrivingVehicle, Is.False);
        }

        [Test]
        public void APassengerSeatDoesNotDriveAndSwitchingSeatsTakesAndGivesUpTheWheel()
        {
            var car = CreateCar("Jeep", withPassengerSeat: true);

            Assert.That(_agent.TryBoard(car, 1, requireInRange: false), Is.True);
            Assert.That(_agent.GetComponent<BotVehicleDriver>(), Is.Null, "a passenger leaves the driving to the driver seat");
            Assert.That(_agent.IsDrivingVehicle, Is.False);

            Assert.That(_agent.TrySwitchVehicleSeat(0), Is.True);
            Assert.That(_agent.GetComponent<BotVehicleDriver>(), Is.Not.Null);
            Assert.That(_agent.VehicleDriver.Rider, Is.SameAs(_agent));

            Assert.That(_agent.TrySwitchVehicleSeat(1), Is.True);
            Assert.That(_agent.GetComponent<BotVehicleDriver>(), Is.Null);
            Assert.That(_agent.IsDrivingVehicle, Is.False);
        }

        [Test]
        public void OrderingARouteWhileDrivingSwapsTheRouteAndClearingItGoesBackToTheVehicles()
        {
            var car = CreateCar("Jeep");
            var onVehicle = AddRoute(car);
            var ordered = CreateRoute("Ordered");
            _agent.TryBoard(car, 0, requireInRange: false);

            LogAssert.Expect(LogType.Log, "[Vehicle] bot route order rider=enemy-1 route=Ordered");
            LogAssert.Expect(LogType.Log, "[Vehicle] 'Jeep' bot takes controls rider=enemy-1 as=driver route=Ordered");
            _agent.OrderRoute(ordered);
            Assert.That(_agent.OrderedRoute, Is.SameAs(ordered));
            Assert.That(_agent.VehicleDriver.Route, Is.SameAs(ordered));

            LogAssert.Expect(LogType.Log, "[Vehicle] bot route order rider=enemy-1 route=none");
            _agent.OrderRoute(null);
            Assert.That(_agent.OrderedRoute, Is.Null);
            Assert.That(_agent.VehicleDriver.Route, Is.SameAs(onVehicle));
        }

        [Test]
        public void ARouteOrderedBeforeBoardingIsDriven()
        {
            var car = CreateCar("Jeep");
            AddRoute(car);
            var ordered = CreateRoute("Ordered");
            _agent.OrderRoute(ordered);

            _agent.TryBoard(car, 0, requireInRange: false);

            Assert.That(_agent.VehicleDriver.Route, Is.SameAs(ordered));
        }

        [Test]
        public void ADriverPlacedOnTheCarIsBorrowedAndHandedBack()
        {
            var car = CreateCar("Jeep");
            var placed = car.gameObject.AddComponent<BotVehicleDriver>();
            var ownRoute = CreateRoute("Own");
            placed.SetRoute(ownRoute);
            Assert.That(placed.DrivesUnmanned, Is.True);
            var ordered = CreateRoute("Ordered");
            _agent.OrderRoute(ordered);

            _agent.TryBoard(car, 0, requireInRange: false);

            Assert.That(_agent.GetComponent<BotVehicleDriver>(), Is.Null, "no second driver on the bot");
            Assert.That(_agent.VehicleDriver, Is.SameAs(placed));
            Assert.That(placed.Rider, Is.SameAs(_agent));
            Assert.That(placed.DrivesUnmanned, Is.False);
            Assert.That(placed.Route, Is.SameAs(ordered));

            _agent.TryLeaveVehicle();

            Assert.That(placed != null, Is.True, "a borrowed driver stays");
            Assert.That(placed.Rider, Is.Null);
            Assert.That(placed.DrivesUnmanned, Is.True);
            Assert.That(placed.Route, Is.SameAs(ownRoute));
            Assert.That(placed.Vehicle, Is.SameAs(car));
        }

        [Test]
        public void SittingInTheDriverSeatOfAnAircraftAttachesAPilotOnTheAircraft()
        {
            var plane = CreateAircraft("Plane");
            var route = AddRoute(plane);
            var a = Point("A", new Vector3(1000f, Altitude + 100f, 0f));
            var b = Point("B", new Vector3(0f, Altitude + 100f, 1000f));
            route.SetPoints(a, b);

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Plane' bot takes controls rider=enemy-1 as=pilot route=Plane");
            Assert.That(_agent.TryBoard(plane, 0, requireInRange: false), Is.True);

            var pilot = plane.GetComponent<BotAircraftPilot>();
            Assert.That(pilot, Is.Not.Null);
            Assert.That(_agent.AircraftPilot, Is.SameAs(pilot));
            Assert.That(_agent.VehicleDriver, Is.Null);
            Assert.That(_agent.GetComponent<BotVehicleDriver>(), Is.Null, "an aircraft is not driven like a car");
            Assert.That(_agent.IsDrivingVehicle, Is.True);
            Assert.That(pilot.Rider, Is.SameAs(_agent));
            Assert.That(pilot.Waypoints, Is.EqualTo(new[] { a, b }));
            Assert.That(pilot.FollowWaypointHeight, Is.True);
        }

        [Test]
        public void ThePilotFliesThroughTheBotsSeatAndLeavingRemovesItAndHandsBackTheControls()
        {
            var plane = CreateAircraft("Plane");
            var link = plane.GetComponent<VehicleMotorLink>();
            var before = link.ControlScheme;
            AddRoute(plane).SetPoints(Point("A", new Vector3(1000f, Altitude, 0f)));
            _agent.TryBoard(plane, 0, requireInRange: false);
            var pilot = _agent.AircraftPilot;

            pilot.Tick(0.02f);

            Assert.That(pilot.IsFlying, Is.True);
            Assert.That(plane.Seats[0].Occupant, Is.SameAs(_agent), "the pilot does not seat a stand-in of its own");

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Plane' bot gives up controls rider=enemy-1");
            Assert.That(_agent.TryLeaveVehicle(), Is.True);

            Assert.That(plane.GetComponent<BotAircraftPilot>(), Is.Null);
            Assert.That(_agent.AircraftPilot, Is.Null);
            Assert.That(_agent.IsDrivingVehicle, Is.False);
            Assert.That(link.ControlScheme, Is.SameAs(before));
        }

        [Test]
        public void APilotPlacedOnTheAircraftIsBorrowedAndKeepsItsWaypointsWithoutARoute()
        {
            var plane = CreateAircraft("Plane");
            var placed = plane.gameObject.AddComponent<BotAircraftPilot>();
            var own = Point("Own", new Vector3(0f, Altitude, 2000f));
            placed.SetWaypoints(own);

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Plane' bot takes controls rider=enemy-1 as=pilot route=none");
            _agent.TryBoard(plane, 0, requireInRange: false);

            Assert.That(_agent.AircraftPilot, Is.SameAs(placed));
            Assert.That(placed.Rider, Is.SameAs(_agent));
            Assert.That(placed.Waypoints, Is.EqualTo(new[] { own }));
            Assert.That(placed.FollowWaypointHeight, Is.False, "a borrowed pilot keeps how it was set up");

            _agent.TryLeaveVehicle();

            Assert.That(placed != null, Is.True, "a borrowed pilot stays");
            Assert.That(placed.Rider, Is.SameAs(placed), "back to flying with its own stand-in");
            Assert.That(placed.Waypoints, Is.EqualTo(new[] { own }));
        }

        [Test]
        public void ABorrowedPilotFliesTheRouteAndGetsItsWaypointsBack()
        {
            var plane = CreateAircraft("Plane");
            var placed = plane.gameObject.AddComponent<BotAircraftPilot>();
            var own = Point("Own", new Vector3(0f, Altitude, 2000f));
            placed.SetWaypoints(own);
            var route = CreateRoute("Ordered");
            var a = Point("A", new Vector3(1000f, Altitude, 0f));
            route.SetPoints(a);
            _agent.OrderRoute(route);

            _agent.TryBoard(plane, 0, requireInRange: false);
            Assert.That(placed.Waypoints, Is.EqualTo(new[] { a }));

            _agent.TryLeaveVehicle();
            Assert.That(placed.Waypoints, Is.EqualTo(new[] { own }));
        }

        private Vehicle CreateCar(string name, bool withPassengerSeat = false)
        {
            var root = Track(new GameObject(name));
            var specs = new List<VehicleSeatSpec> { new VehicleSeatSpec(VehicleSeatRole.Driver) };
            AddSeat(root, "Driver", new Vector3(-0.5f, 0.4f, 0f));
            if (withPassengerSeat)
            {
                AddSeat(root, "Passenger", new Vector3(0.5f, 0.4f, 0f));
                specs.Add(new VehicleSeatSpec(VehicleSeatRole.Passenger));
            }

            var exit = new GameObject("Exit").transform;
            exit.SetParent(root.transform, false);
            exit.localPosition = new Vector3(-2f, 0f, 0f);
            foreach (var seat in root.GetComponentsInChildren<VehicleSeat>())
            {
                seat.SetExitPoints(exit);
            }

            var vehicle = root.AddComponent<Vehicle>();
            vehicle.Configure(new VehicleSpec(name.ToLowerInvariant(), 100, specs.ToArray()));
            Physics.SyncTransforms();
            return vehicle;
        }

        private Vehicle CreateAircraft(string name)
        {
            var root = Track(new GameObject(name));
            root.transform.position = new Vector3(0f, Altitude, 0f);
            var seat = AddSeat(root, "Pilot Seat", new Vector3(0f, 0.5f, 0.5f));
            var exit = new GameObject("Exit").transform;
            exit.SetParent(root.transform, false);
            exit.localPosition = new Vector3(-3f, 0f, 0f);
            seat.SetExitPoints(exit);
            var vehicle = root.AddComponent<Vehicle>();
            vehicle.Configure(new VehicleSpec(name.ToLowerInvariant(), 500, new[] { new VehicleSeatSpec(VehicleSeatRole.Driver) }));
            var motor = root.AddComponent<SimpleAircraftMotor>();
            motor.Body.useGravity = false;
            motor.Body.linearVelocity = new Vector3(0f, 0f, 55f);
            Assert.That(root.AddComponent<VehicleMotorLink>().Bind(), Is.True);
            Physics.SyncTransforms();
            return vehicle;
        }

        private static VehicleSeat AddSeat(GameObject root, string name, Vector3 localPosition)
        {
            var seat = new GameObject(name).AddComponent<VehicleSeat>();
            seat.transform.SetParent(root.transform, false);
            seat.transform.localPosition = localPosition;
            return seat;
        }

        // 部品の名前は GameObject の名前なので、乗り物に付けた道の名前は乗り物の名前になる。
        private static VehicleRoute AddRoute(Vehicle vehicle)
        {
            return vehicle.gameObject.AddComponent<VehicleRoute>();
        }

        private VehicleRoute CreateRoute(string name)
        {
            return Track(new GameObject(name)).AddComponent<VehicleRoute>();
        }

        private Transform Point(string name, Vector3 position)
        {
            var point = Track(new GameObject(name)).transform;
            point.position = position;
            return point;
        }

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}
