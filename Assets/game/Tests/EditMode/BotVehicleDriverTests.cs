using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // Update が走らないので Drive を直接呼び、座席に渡した入力が動き方 (SimpleCarMotor) に届くかを見る。
    public sealed class BotVehicleDriverTests
    {
        private readonly List<Object> _created = new List<Object>();
        private Vehicle _vehicle;
        private SimpleCarMotor _motor;
        private BotVehicleDriver _driver;

        [SetUp]
        public void SetUp()
        {
            _vehicle = TestCars.Create(_created);
            _motor = _vehicle.GetComponent<SimpleCarMotor>();
            _vehicle.gameObject.AddComponent<VehicleMotorLink>().Bind();
            _driver = _vehicle.gameObject.AddComponent<BotVehicleDriver>();
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
        public void TheDriverFindsTheVehicleItSitsOn()
        {
            Assert.That(_driver.Vehicle, Is.SameAs(_vehicle));
        }

        [Test]
        public void WithAnEmptyDriverSeatAStandInTakesItAndDrivesTowardTheRoute()
        {
            _driver.SetRoute(Route(false, new Vector3(0f, 0f, 30f), new Vector3(0f, 0f, 60f)));

            Assert.That(_driver.Drive(0.02f), Is.True);

            Assert.That(_driver.IsStandInSeated, Is.True);
            Assert.That(_vehicle.Seats[0].IsOccupied, Is.True);
            Assert.That(_vehicle.Seats[1].IsOccupied, Is.False, "only the driver seat");
            Assert.That(_motor.Input[CarDrive.ThrottleAxis], Is.EqualTo(1f));
            Assert.That(_motor.Input[CarDrive.SteerAxis], Is.EqualTo(0f).Within(1e-3f));
            Assert.That(_motor.Input[CarDrive.BrakeAxis], Is.EqualTo(0f));
        }

        [Test]
        public void APointToTheRightTurnsTheWheelsRight()
        {
            _driver.SetRoute(Route(false, new Vector3(20f, 0f, 20f)));

            _driver.Drive(0.02f);

            Assert.That(_motor.Input[CarDrive.SteerAxis], Is.EqualTo(1f));
            Assert.That(_driver.LastCommand.Steer, Is.EqualTo(1f));
        }

        [Test]
        public void AtTheLastPointItHoldsTheBrake()
        {
            _driver.SetRoute(Route(false, new Vector3(0f, 0f, 1f)));

            _driver.Drive(0.02f);

            Assert.That(_driver.LastCommand.Phase, Is.EqualTo(VehicleRoutePhase.Finished));
            Assert.That(_motor.Input[CarDrive.ThrottleAxis], Is.EqualTo(0f));
            Assert.That(_motor.Input[CarDrive.BrakeAxis], Is.EqualTo(1f));
        }

        [Test]
        public void ALoopKeepsDrivingPastTheLastPoint()
        {
            _driver.SetRoute(Route(true, new Vector3(0f, 0f, 20f), new Vector3(20f, 0f, 20f), new Vector3(20f, 0f, 0f), new Vector3(0f, 0f, 0f)), 3);

            _driver.Drive(0.02f);

            Assert.That(_driver.LastCommand.Phase, Is.EqualTo(VehicleRoutePhase.Driving));
            Assert.That(_driver.Follower.TargetIndex, Is.EqualTo(0));
        }

        [Test]
        public void ItLeavesASeatSomeoneElseIsDrivingAlone()
        {
            _driver.SetRoute(Route(false, new Vector3(0f, 0f, 30f)));
            var person = TestCars.Board(_vehicle, "Person", 0, _created);

            Assert.That(_driver.Drive(0.02f), Is.False);

            Assert.That(_vehicle.Seats[0].Occupant, Is.SameAs(person));
            Assert.That(_driver.IsStandInSeated, Is.False);
            Assert.That(_motor.Input, Is.EqualTo(VehicleMotorInput.Idle));
        }

        [Test]
        public void AnAssignedRiderDrivesOnlyWhileSeatedInTheDriverSeat()
        {
            _driver.SetRoute(Route(false, new Vector3(0f, 0f, 30f)));
            var rider = new GameObject("Rider").AddComponent<VehicleTestRider>();
            _created.Add(rider.gameObject);
            _driver.SetRider(rider);

            Assert.That(_driver.Drive(0.02f), Is.False, "not seated yet");
            Assert.That(_vehicle.Seats[0].IsOccupied, Is.False, "the stand-in does not take the seat meant for the rider");

            _vehicle.TryEnter(rider, 0, requireInRange: false);
            Assert.That(_driver.Drive(0.02f), Is.True);
            Assert.That(_motor.Input[CarDrive.ThrottleAxis], Is.EqualTo(1f));
            Assert.That(_driver.Rider, Is.SameAs(rider));
        }

        [Test]
        public void AssigningARiderSendsTheStandInOut()
        {
            _driver.SetRoute(Route(false, new Vector3(0f, 0f, 30f)));
            _driver.Drive(0.02f);
            Assert.That(_driver.IsStandInSeated, Is.True);

            _driver.SetRider(new GameObject("Rider").AddComponent<VehicleTestRider>());
            _created.Add(((VehicleTestRider)_driver.Rider).gameObject);

            Assert.That(_driver.IsStandInSeated, Is.False);
            Assert.That(_vehicle.Seats[0].IsOccupied, Is.False);
            Assert.That(_motor.Input, Is.EqualTo(VehicleMotorInput.Idle), "vacating the driver seat idles the motor");
        }

        [Test]
        public void TurningOffUnmannedDrivingFreesTheSeat()
        {
            _driver.SetRoute(Route(false, new Vector3(0f, 0f, 30f)));
            _driver.Drive(0.02f);

            _driver.DrivesUnmanned = false;

            Assert.That(_vehicle.Seats[0].IsOccupied, Is.False);
            Assert.That(_driver.Drive(0.02f), Is.False);
            Assert.That(_vehicle.Seats[0].IsOccupied, Is.False);
        }

        [Test]
        public void WithoutARouteItBrakesAndWarns()
        {
            LogAssert.Expect(LogType.Warning, VehicleLog.BotDriverMissingRoute(_vehicle.name));

            Assert.That(_driver.Drive(0.02f), Is.True);
            Assert.That(_driver.Drive(0.02f), Is.True);

            Assert.That(_motor.Input[CarDrive.BrakeAxis], Is.EqualTo(1f));
            Assert.That(_motor.Input[CarDrive.ThrottleAxis], Is.EqualTo(0f));
        }

        [Test]
        public void AWreckedVehicleIsNotDriven()
        {
            _driver.SetRoute(Route(false, new Vector3(0f, 0f, 30f)));
            _vehicle.Wreck();

            Assert.That(_driver.Drive(0.02f), Is.False);
            Assert.That(_driver.IsStandInSeated, Is.False);
        }

        [Test]
        public void ConfigureCopiesTheSpecAndKeepsTheProgress()
        {
            _driver.SetRoute(Route(true, new Vector3(0f, 0f, 20f), new Vector3(20f, 0f, 20f), new Vector3(20f, 0f, 0f), new Vector3(0f, 0f, 0f)), 2);
            var spec = new VehicleRouteSpec(cruiseSpeed: 8f, cornerSpeed: 3f, reverseSeconds: 2f);

            _driver.Configure(spec);

            Assert.That(_driver.Spec, Is.SameAs(spec));
            Assert.That(_driver.Follower.Spec, Is.SameAs(spec));
            Assert.That(_driver.Follower.TargetIndex, Is.EqualTo(2));
        }

        [Test]
        public void TheDefaultSpecUsesTheDefaults()
        {
            Assert.That(_driver.Spec.CruiseSpeed, Is.EqualTo(VehicleRouteSpec.DefaultCruiseSpeed));
            Assert.That(_driver.Spec.LookAheadDistance, Is.EqualTo(VehicleRouteSpec.DefaultLookAheadDistance));
            Assert.That(_driver.Spec.ReverseThrottle, Is.EqualTo(VehicleRouteSpec.DefaultReverseThrottle));
        }

        [Test]
        public void SeatInputCarriesSteerThrottleAndBrake()
        {
            var input = BotVehicleDriver.ToSeatInput(new VehicleRouteCommand(-0.5f, -0.8f, true, 0, 0f, VehicleRoutePhase.Reversing));

            Assert.That(input.Move, Is.EqualTo(new Vector2(-0.5f, -0.8f)));
            Assert.That(input.Boost, Is.True);
            Assert.That(input.FireHeld, Is.False);
            Assert.That(input.HasAim, Is.False);
        }

        [Test]
        public void DriveRejectsABadDeltaTime()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => _driver.Drive(-1f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => _driver.Drive(float.NaN));
        }

        private VehicleRoute Route(bool loop, params Vector3[] positions)
        {
            var route = new GameObject("Route").AddComponent<VehicleRoute>();
            _created.Add(route.gameObject);
            route.Loop = loop;
            foreach (var position in positions)
            {
                var point = new GameObject("Point").transform;
                point.SetParent(route.transform, false);
                point.position = position;
            }

            return route;
        }
    }
}
