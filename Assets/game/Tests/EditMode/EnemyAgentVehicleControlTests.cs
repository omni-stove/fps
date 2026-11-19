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
        public void OnlyAVehicleWithABotPilotedMotorNeedsABotPilot()
        {
            Assert.That(EnemyAgent.NeedsBotPilot(CreateCar("Jeep")), Is.False);
            Assert.That(EnemyAgent.NeedsBotPilot(CreatePiloted("Glider")), Is.True);
            Assert.Throws<ArgumentNullException>(() => EnemyAgent.NeedsBotPilot(null));
        }

        [Test]
        public void OnlyAConcreteMonoBehaviourWithIBotPilotIsABotPilotType()
        {
            Assert.That(EnemyAgent.IsBotPilotType(typeof(FakeBotPilot)), Is.True);
            Assert.That(EnemyAgent.IsBotPilotType(typeof(FakeBotPilotedMotor)), Is.False, "a MonoBehaviour without IBotPilot");
            Assert.That(EnemyAgent.IsBotPilotType(typeof(IBotPilot)), Is.False, "an interface cannot be added");
            Assert.That(EnemyAgent.IsBotPilotType(typeof(string)), Is.False);
            Assert.That(EnemyAgent.IsBotPilotType(null), Is.False);
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
            Assert.That(_agent.VehiclePilot, Is.Null);
            Assert.That(_agent.IsDrivingVehicle, Is.True);
            Assert.That(driver.Vehicle, Is.SameAs(car));
            Assert.That(driver.Rider, Is.SameAs(_agent));
            Assert.That(driver.Route, Is.SameAs(route));
            Assert.That(driver.DrivesUnmanned, Is.False, "the bot drives; no stand-in takes the seat");
            Assert.That(car.GetComponent<IBotPilot>(), Is.Null);
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
        public void SittingInTheDriverSeatOfAPilotedVehicleAttachesThePilotTypeItNames()
        {
            var glider = CreatePiloted("Glider");
            var route = AddRoute(glider);
            var a = Point("A", new Vector3(1000f, Altitude + 100f, 0f));
            var b = Point("B", new Vector3(0f, Altitude + 100f, 1000f));
            route.SetPoints(a, b);

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Glider' bot takes controls rider=enemy-1 as=pilot route=Glider");
            Assert.That(_agent.TryBoard(glider, 0, requireInRange: false), Is.True);

            var pilot = glider.GetComponent<FakeBotPilot>();
            Assert.That(pilot, Is.Not.Null);
            Assert.That(_agent.VehiclePilot, Is.SameAs(pilot));
            Assert.That(_agent.VehicleDriver, Is.Null);
            Assert.That(_agent.GetComponent<BotVehicleDriver>(), Is.Null, "a piloted vehicle is not driven like a car");
            Assert.That(glider.GetComponent<BotVehicleDriver>(), Is.Null);
            Assert.That(_agent.IsDrivingVehicle, Is.True);
            Assert.That(pilot.Rider, Is.SameAs(_agent));
            Assert.That(pilot.Waypoints, Is.EqualTo(new[] { a, b }));
            Assert.That(pilot.FollowWaypointHeight, Is.True);
        }

        [Test]
        public void LeavingAPilotedVehicleRemovesTheAttachedPilot()
        {
            var glider = CreatePiloted("Glider");
            _agent.TryBoard(glider, 0, requireInRange: false);
            Assert.That(glider.GetComponent<FakeBotPilot>(), Is.Not.Null);

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Glider' bot gives up controls rider=enemy-1");
            Assert.That(_agent.TryLeaveVehicle(), Is.True);

            Assert.That(glider.GetComponent<FakeBotPilot>(), Is.Null);
            Assert.That(_agent.VehiclePilot, Is.Null);
            Assert.That(_agent.IsDrivingVehicle, Is.False);
        }

        [Test]
        public void APilotPlacedOnTheVehicleIsBorrowedAndHandedBackAsItWas()
        {
            var glider = CreatePiloted("Glider");
            var placed = glider.gameObject.AddComponent<FakeBotPilot>();
            var ownRider = Track(new GameObject("Own Rider")).AddComponent<VehicleTestRider>();
            var own = Point("Own", new Vector3(0f, Altitude, 2000f));
            placed.Rider = ownRider;
            placed.SetWaypoints(own);
            placed.enabled = false;

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Glider' bot takes controls rider=enemy-1 as=pilot route=none");
            _agent.TryBoard(glider, 0, requireInRange: false);

            Assert.That(glider.GetComponents<FakeBotPilot>(), Has.Length.EqualTo(1), "no second pilot on the vehicle");
            Assert.That(_agent.VehiclePilot, Is.SameAs(placed));
            Assert.That(placed.enabled, Is.True);
            Assert.That(placed.Rider, Is.SameAs(_agent));
            Assert.That(placed.Waypoints, Is.EqualTo(new[] { own }), "without a route it keeps its own points");
            Assert.That(placed.FollowWaypointHeight, Is.False, "a borrowed pilot keeps how it was set up");

            _agent.TryLeaveVehicle();

            Assert.That(placed != null, Is.True, "a borrowed pilot stays");
            Assert.That(placed.ReleaseCount, Is.EqualTo(1));
            Assert.That(placed.Rider, Is.SameAs(ownRider));
            Assert.That(placed.Waypoints, Is.EqualTo(new[] { own }));
            Assert.That(placed.enabled, Is.False);
            Assert.That(_agent.VehiclePilot, Is.Null);
        }

        [Test]
        public void ABorrowedPilotFliesTheRouteAndGetsItsWaypointsBack()
        {
            var glider = CreatePiloted("Glider");
            var placed = glider.gameObject.AddComponent<FakeBotPilot>();
            var own = Point("Own", new Vector3(0f, Altitude, 2000f));
            placed.SetWaypoints(own);
            var route = CreateRoute("Ordered");
            var a = Point("A", new Vector3(1000f, Altitude, 0f));
            route.SetPoints(a);
            _agent.OrderRoute(route);

            _agent.TryBoard(glider, 0, requireInRange: false);
            Assert.That(placed.Waypoints, Is.EqualTo(new[] { a }));

            _agent.TryLeaveVehicle();
            Assert.That(placed.Waypoints, Is.EqualTo(new[] { own }));
        }

        [Test]
        public void APilotTypeWithoutIBotPilotFallsBackToTheDriver()
        {
            var glider = CreatePiloted("Glider");
            glider.GetComponent<FakeBotPilotedMotor>().PilotType = typeof(VehicleTestRider);
            AddRoute(glider);

            LogAssert.Expect(
                LogType.Warning,
                "[Vehicle] 'Glider' bot pilot type 'Shooter.Tests.EditMode.VehicleTestRider' is not a MonoBehaviour with IBotPilot; the bot drives it as a car");
            LogAssert.Expect(LogType.Log, "[Vehicle] 'Glider' bot takes controls rider=enemy-1 as=driver route=Glider");
            Assert.That(_agent.TryBoard(glider, 0, requireInRange: false), Is.True);

            Assert.That(glider.GetComponent<VehicleTestRider>(), Is.Null, "the wrong type is not added");
            Assert.That(_agent.VehiclePilot, Is.Null);
            Assert.That(_agent.VehicleDriver, Is.SameAs(_agent.GetComponent<BotVehicleDriver>()));
            Assert.That(_agent.VehicleDriver.Vehicle, Is.SameAs(glider));
            Assert.That(_agent.IsDrivingVehicle, Is.True);
        }

        [Test]
        public void NoPilotTypeFallsBackToTheDriver()
        {
            var glider = CreatePiloted("Glider");
            glider.GetComponent<FakeBotPilotedMotor>().PilotType = null;

            LogAssert.Expect(
                LogType.Warning,
                "[Vehicle] 'Glider' bot pilot type 'none' is not a MonoBehaviour with IBotPilot; the bot drives it as a car");
            _agent.TryBoard(glider, 0, requireInRange: false);

            Assert.That(_agent.VehiclePilot, Is.Null);
            Assert.That(_agent.VehicleDriver, Is.Not.Null);
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

        // 本物の飛行機を使わないのは、Bot が操縦の部品を選ぶのが動き方の名指す型だけで決まり、飛行機の部品に頼らないことを確かめるため。
        private Vehicle CreatePiloted(string name)
        {
            var vehicle = CreateCar(name);
            vehicle.gameObject.AddComponent<FakeBotPilotedMotor>();
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
        private VehicleRoute AddRoute(Vehicle vehicle)
        {
            var route = vehicle.gameObject.AddComponent<VehicleRoute>();
            route.SetPoints(Point("RoutePoint", vehicle.transform.position + new Vector3(50f, 0f, 0f)));
            return route;
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
