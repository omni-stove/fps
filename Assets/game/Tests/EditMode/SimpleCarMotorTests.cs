using System;
using System.Collections.Generic;
using System.Linq;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // 物理は進めず、Step で WheelCollider に入れた力と、起こしたときの位置と向きを確かめる。
    public sealed class SimpleCarMotorTests
    {
        private const float Epsilon = 1e-3f;

        private readonly List<Object> _created = new List<Object>();
        private SimpleCarMotor _motor;

        [SetUp]
        public void SetUp()
        {
            _motor = TestCars.Create(_created).GetComponent<SimpleCarMotor>();
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
        public void ConfigureKeepsTheSpecAndSetsTheMass()
        {
            Assert.That(_motor.Spec, Is.SameAs(TestCars.Spec));
            Assert.That(_motor.Body.mass, Is.EqualTo(1000f));
            Assert.That(_motor.Wheels.Count, Is.EqualTo(4));
            Assert.Throws<ArgumentNullException>(() => _motor.Configure(null));
        }

        [Test]
        public void TheSpecIsBuiltFromTheDefaultsWhenNotConfigured()
        {
            var car = new GameObject("Plain");
            _created.Add(car);
            var motor = car.AddComponent<SimpleCarMotor>();

            Assert.That(motor.Spec.Mass, Is.EqualTo(CarSpec.DefaultMass));
            Assert.That(motor.Spec.MotorTorque, Is.EqualTo(CarSpec.DefaultMotorTorque));
            Assert.That(motor.Spec.FlipAngle, Is.EqualTo(CarSpec.DefaultFlipAngle));
            Assert.That(motor.Wheels, Is.Empty);
        }

        [Test]
        public void ThrottleTurnsTheDrivenWheelsAndSteeringTurnsTheFrontOnes()
        {
            _motor.SetInput(new VehicleMotorInput(1f, 0.5f, 0f, 0f));
            _motor.Step();

            var front = _motor.Wheels.Where(w => w.Steers).Select(w => w.Collider).ToArray();
            var rear = _motor.Wheels.Where(w => w.Drives).Select(w => w.Collider).ToArray();
            Assert.That(front.Length, Is.EqualTo(2));
            Assert.That(rear.Length, Is.EqualTo(2));
            foreach (var wheel in front)
            {
                Assert.That(wheel.steerAngle, Is.EqualTo(15f).Within(Epsilon));
                Assert.That(wheel.motorTorque, Is.EqualTo(0f));
                Assert.That(wheel.brakeTorque, Is.EqualTo(0f));
            }

            foreach (var wheel in rear)
            {
                Assert.That(wheel.steerAngle, Is.EqualTo(0f));
                Assert.That(wheel.motorTorque, Is.EqualTo(1000f).Within(Epsilon), "the 2000 N·m is split between the two driven wheels");
                Assert.That(wheel.brakeTorque, Is.EqualTo(0f));
            }

            Assert.That(_motor.LastCommand.MotorTorque, Is.EqualTo(2000f));
        }

        [Test]
        public void ReverseBacksUpAndTheBrakeHoldsEveryWheel()
        {
            _motor.SetInput(new VehicleMotorInput(-1f, 0f, 0f, 0f));
            _motor.Step();
            Assert.That(_motor.Wheels.Where(w => w.Drives).Select(w => w.Collider.motorTorque), Has.All.EqualTo(-1000f).Within(Epsilon));

            _motor.SetInput(new VehicleMotorInput(0f, 0f, 1f, 0f));
            _motor.Step();
            Assert.That(_motor.Wheels.Select(w => w.Collider.motorTorque), Has.All.EqualTo(0f));
            Assert.That(_motor.Wheels.Select(w => w.Collider.brakeTorque), Has.All.EqualTo(3000f).Within(Epsilon));
        }

        [Test]
        public void WithNoInputTheIdleBrakeHoldsTheCar()
        {
            _motor.SetInput(VehicleMotorInput.Idle);
            _motor.Step();

            Assert.That(_motor.Wheels.Select(w => w.Collider.brakeTorque), Has.All.EqualTo(100f).Within(Epsilon));
            Assert.That(_motor.Wheels.Select(w => w.Collider.motorTorque), Has.All.EqualTo(0f));
        }

        [Test]
        public void AnUprightCarIsNotRighted()
        {
            _motor.transform.SetPositionAndRotation(new Vector3(1f, 0f, 2f), Quaternion.Euler(0f, 0f, 30f));

            Assert.That(_motor.IsTipped, Is.False);
            Assert.That(_motor.TryRight(), Is.False);
            Assert.That(_motor.transform.position, Is.EqualTo(new Vector3(1f, 0f, 2f)));
        }

        [Test]
        public void AnUpsideDownCarIsRightedFacingItsHeadingAndLifted()
        {
            _motor.transform.SetPositionAndRotation(new Vector3(1f, 0.5f, 2f), Quaternion.Euler(0f, 90f, 180f));
            Assert.That(_motor.IsTipped, Is.True);

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' righted tilt=180");
            Assert.That(_motor.TryRight(), Is.True);

            Assert.That(Vector3.Distance(_motor.transform.position, new Vector3(1f, 1.5f, 2f)), Is.LessThan(Epsilon));
            Assert.That(Vector3.Angle(_motor.transform.up, Vector3.up), Is.LessThan(0.1f));
            Assert.That(Vector3.Angle(_motor.transform.forward, Vector3.right), Is.LessThan(0.1f));
            Assert.That(_motor.IsTipped, Is.False);
        }

        [Test]
        public void ACarOnItsNoseIsRightedFacingItsRoof()
        {
            _motor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(90f, 0f, 0f));

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' righted tilt=90");
            Assert.That(_motor.TryRight(), Is.True);

            Assert.That(Vector3.Angle(_motor.transform.forward, Vector3.forward), Is.LessThan(0.1f));
        }

        [Test]
        public void PressingUpRightsATippedCarOncePerPress()
        {
            _motor.transform.rotation = Quaternion.Euler(0f, 0f, 180f);
            LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' righted tilt=180");
            _motor.SetInput(new VehicleMotorInput(0f, 0f, 0f, 1f));
            _motor.Step();
            Assert.That(_motor.IsTipped, Is.False);

            _motor.transform.rotation = Quaternion.Euler(0f, 0f, 180f);
            _motor.SetInput(new VehicleMotorInput(0f, 0f, 0f, 1f));
            _motor.Step();
            Assert.That(_motor.IsTipped, Is.True, "holding up does not right the car again");

            _motor.SetInput(VehicleMotorInput.Idle);
            LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' righted tilt=180");
            _motor.SetInput(new VehicleMotorInput(0f, 0f, 0f, 1f));
            _motor.Step();
            Assert.That(_motor.IsTipped, Is.False);
        }

        [Test]
        public void SetWheelsRejectsAMissingCollider()
        {
            Assert.Throws<ArgumentNullException>(() => _motor.SetWheels(null));
            Assert.Throws<ArgumentException>(() => _motor.SetWheels(new CarWheel[] { null }));
            Assert.Throws<ArgumentNullException>(() => new CarWheel(null, null, true, true));
        }
    }
}
