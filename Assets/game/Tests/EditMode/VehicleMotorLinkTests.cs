using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // OnEnable が走らないので Bind を直接呼び、座席の入力が動き方 (IVehicleMotor) に届くかを確かめる。
    public sealed class VehicleMotorLinkTests
    {
        private readonly List<Object> _created = new List<Object>();
        private Vehicle _vehicle;
        private SimpleCarMotor _motor;
        private VehicleMotorLink _link;

        [SetUp]
        public void SetUp()
        {
            _vehicle = TestCars.Create(_created);
            _motor = _vehicle.GetComponent<SimpleCarMotor>();
            _link = _vehicle.gameObject.AddComponent<VehicleMotorLink>();
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
        public void TheSeatInputMapsMoveToThrottleAndSteerAndDashToTheBrake()
        {
            var input = new VehicleSeatInput(new Vector2(-0.5f, 0.75f), 1f, true, false, false, false, Vector3.zero, Vector3.zero);

            Assert.That(VehicleMotorLink.ToMotorInput(input), Is.EqualTo(new VehicleMotorInput(0.75f, -0.5f, 1f, 1f)));
            Assert.That(VehicleMotorLink.ToMotorInput(VehicleSeatInput.Idle), Is.EqualTo(VehicleMotorInput.Idle));
        }

        [Test]
        public void TheDriversInputReachesTheMotorAndThePassengersDoesNot()
        {
            Assert.That(_link.Bind(), Is.True);
            Assert.That(_link.Motor, Is.SameAs(_motor));
            var driver = TestCars.Board(_vehicle, "Driver", 0, _created);
            var passenger = TestCars.Board(_vehicle, "Passenger", 1, _created);

            Assert.That(_vehicle.Seats[1].SubmitInput(passenger, Forward(), 0.02f), Is.True);
            Assert.That(_motor.Input, Is.EqualTo(VehicleMotorInput.Idle), "a passenger does not drive");

            Assert.That(_vehicle.Seats[0].SubmitInput(driver, Forward(), 0.02f), Is.True);
            Assert.That(_motor.Input, Is.EqualTo(new VehicleMotorInput(1f, 0f, 0f, 0f)));
        }

        [Test]
        public void TheMotorIsReleasedWhenTheDriverLeaves()
        {
            _link.Bind();
            var driver = TestCars.Board(_vehicle, "Driver", 0, _created);
            _vehicle.Seats[0].SubmitInput(driver, Forward(), 0.02f);

            _vehicle.ForceExit(driver, VehicleLeaveReason.Request);

            Assert.That(_motor.Input, Is.EqualTo(VehicleMotorInput.Idle));
        }

        [Test]
        public void UnbindingReleasesTheMotorAndStopsForwardingInput()
        {
            _link.Bind();
            var driver = TestCars.Board(_vehicle, "Driver", 0, _created);
            _vehicle.Seats[0].SubmitInput(driver, Forward(), 0.02f);

            _link.Unbind();
            Assert.That(_link.IsBound, Is.False);
            Assert.That(_motor.Input, Is.EqualTo(VehicleMotorInput.Idle));

            _vehicle.Seats[0].SubmitInput(driver, Forward(), 0.02f);
            Assert.That(_motor.Input, Is.EqualTo(VehicleMotorInput.Idle));
        }

        [Test]
        public void BindingWithoutAMotorWarnsAndDoesNothing()
        {
            var turret = new GameObject("Turret");
            _created.Add(turret);
            var seat = new GameObject("Seat").AddComponent<VehicleSeat>();
            seat.transform.SetParent(turret.transform, false);
            turret.AddComponent<Vehicle>();
            var link = turret.AddComponent<VehicleMotorLink>();

            LogAssert.Expect(LogType.Warning, VehicleLog.MissingMotor("Turret"));
            Assert.That(link.Bind(), Is.False);
            Assert.That(link.IsBound, Is.False);
        }

        private static VehicleSeatInput Forward()
        {
            return new VehicleSeatInput(new Vector2(0f, 1f), 0f, false, false, false, false, Vector3.zero, Vector3.zero);
        }
    }
}
