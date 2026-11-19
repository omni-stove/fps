using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // 物理は進めず、Step が機体から見た速さで AircraftFlight を引いた結果を確かめる。
    public sealed class SimpleAircraftMotorTests
    {
        private const float Epsilon = 1e-3f;

        private readonly List<Object> _created = new List<Object>();
        private SimpleAircraftMotor _motor;

        [SetUp]
        public void SetUp()
        {
            var aircraft = new GameObject("Aircraft");
            _created.Add(aircraft);
            _motor = aircraft.AddComponent<SimpleAircraftMotor>();
            _motor.Body.useGravity = false;
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
        public void TheSpecIsBuiltFromTheDefaultsWhenNotConfigured()
        {
            Assert.That(_motor.Spec.Mass, Is.EqualTo(AircraftSpec.DefaultMass));
            Assert.That(_motor.Spec.MaxThrust, Is.EqualTo(AircraftSpec.DefaultMaxThrust));
            Assert.That(_motor.Spec.MaxSpeed, Is.EqualTo(AircraftSpec.DefaultMaxSpeed));
            Assert.That(_motor.Spec.RollRate, Is.EqualTo(AircraftSpec.DefaultRollRate));
            Assert.That(_motor.Input, Is.EqualTo(VehicleMotorInput.Idle));
        }

        [Test]
        public void ConfigureKeepsTheSpecAndSetsTheMass()
        {
            var spec = new AircraftSpec(mass: 2500f, maxSpeed: 120f);

            _motor.Configure(spec);

            Assert.That(_motor.Spec, Is.SameAs(spec));
            Assert.That(_motor.Body.mass, Is.EqualTo(2500f));
            Assert.Throws<ArgumentNullException>(() => _motor.Configure(null));
        }

        [Test]
        public void TheTopSpeedIsTheDiveSpeed()
        {
            Assert.That(_motor.TopSpeed, Is.EqualTo(AircraftFlight.TopSpeed(_motor.Spec, Physics.gravity.magnitude)).Within(Epsilon));
            Assert.That(_motor.TopSpeed, Is.GreaterThan(_motor.Spec.MaxSpeed));
            Assert.That(_motor, Is.InstanceOf<IVehicleTopSpeed>());
        }

        [Test]
        public void StepUsesTheInputAndTheVelocitySeenFromTheAircraft()
        {
            _motor.Configure(new AircraftSpec(maxThrust: 10f, maxSpeed: 100f, liftSpeed: 40f, controlSpeed: 20f, alignment: 0f));
            _motor.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            _motor.Body.rotation = _motor.transform.rotation;
            _motor.Body.linearVelocity = new Vector3(100f, 0f, 0f);

            _motor.SetInput(new VehicleMotorInput(1f, 1f));
            _motor.Step();

            var command = _motor.LastCommand;
            Assert.That(_motor.Input, Is.EqualTo(new VehicleMotorInput(1f, 1f)));
            Assert.That(command.LiftRatio, Is.EqualTo(1f), "100 m/s along the nose is past the lift speed");
            Assert.That(command.Control, Is.EqualTo(1f));
            Assert.That(command.LocalAcceleration.Z, Is.EqualTo(0f).Within(Epsilon), "full thrust at top speed");
            Assert.That(command.LocalAngularAcceleration.X, Is.LessThan(0f), "pitching up turns about -x");
        }

        [Test]
        public void StepOnAKinematicBodyComputesButDoesNotPush()
        {
            _motor.Body.isKinematic = true;
            _motor.SetInput(new VehicleMotorInput(1f));

            _motor.Step();

            Assert.That(_motor.LastCommand.LocalAcceleration.Z, Is.EqualTo(AircraftSpec.DefaultMaxThrust).Within(Epsilon));
            Assert.That(_motor.Body.linearVelocity, Is.EqualTo(Vector3.zero));
        }
    }
}
