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
            Assert.That(_motor.Spec.Aero.MaxSpeed, Is.EqualTo(AircraftAeroSpec.DefaultMaxSpeed));
            Assert.That(_motor.Spec.Aero.RollRate, Is.EqualTo(AircraftAeroSpec.DefaultRollRate));
            Assert.That(_motor.Input, Is.EqualTo(VehicleMotorInput.Idle));
        }

        [Test]
        public void ConfigureKeepsTheSpecAndSetsTheMass()
        {
            var spec = new AircraftSpec(mass: 2500f, aero: new AircraftAeroSpec(maxSpeed: 120f));

            _motor.Configure(spec);

            Assert.That(_motor.Spec, Is.SameAs(spec));
            Assert.That(_motor.Body.mass, Is.EqualTo(2500f));
            Assert.Throws<ArgumentNullException>(() => _motor.Configure(null));
        }

        [Test]
        public void TheTopSpeedIsTheDiveSpeed()
        {
            Assert.That(_motor.TopSpeed, Is.EqualTo(AircraftAero.TopSpeed(_motor.Spec, Physics.gravity.magnitude)).Within(Epsilon));
            Assert.That(_motor.TopSpeed, Is.GreaterThan(_motor.Spec.Aero.MaxSpeed));
            Assert.That(_motor, Is.InstanceOf<IVehicleTopSpeed>());
        }

        [Test]
        public void StepUsesTheInputAndTheVelocitySeenFromTheAircraft()
        {
            _motor.Configure(new AircraftSpec(maxThrust: 10f, aero: new AircraftAeroSpec(maxSpeed: 100f, liftSpeed: 40f, controlSpeed: 20f, alignment: 0f)));
            _motor.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            _motor.Body.rotation = _motor.transform.rotation;
            _motor.Body.linearVelocity = new Vector3(100f, 0f, 0f);

            _motor.SetInput(new VehicleMotorInput(1f, 1f));
            _motor.Step();

            var command = _motor.LastCommand;
            Assert.That(_motor.Input, Is.EqualTo(new VehicleMotorInput(1f, 1f)));
            Assert.That(command.LiftRatio, Is.EqualTo(0f), "100 m/s along the nose meets the wings at no angle");
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

        [Test]
        public void TheSpecIsReadFromTheDefinitionWhenOneIsSet()
        {
            var definition = ScriptableObject.CreateInstance<AircraftDefinition>();
            _created.Add(definition);
            definition.CopyFrom(new AircraftSpec(mass: 3000f, maxThrust: 12f, aero: new AircraftAeroSpec(maxSpeed: 60f)));

            _motor.Definition = definition;

            Assert.That(_motor.Spec.Mass, Is.EqualTo(3000f));
            Assert.That(_motor.Spec.MaxThrust, Is.EqualTo(12f));
            Assert.That(_motor.Spec.Aero.MaxSpeed, Is.EqualTo(60f));
            Assert.That(_motor.Body.mass, Is.EqualTo(3000f));
            Assert.That(_motor.TopSpeed, Is.LessThan(AircraftAero.TopSpeed(AircraftSpec.Default, Physics.gravity.magnitude)));
        }

        [Test]
        public void SettingTheDefinitionReplacesAConfiguredSpec()
        {
            var definition = ScriptableObject.CreateInstance<AircraftDefinition>();
            _created.Add(definition);
            definition.MaxThrust = 7f;
            _motor.Configure(new AircraftSpec(maxThrust: 20f));

            _motor.Definition = definition;

            Assert.That(_motor.Spec.MaxThrust, Is.EqualTo(7f));
            _motor.Configure(new AircraftSpec(maxThrust: 20f));
            Assert.That(_motor.Spec.MaxThrust, Is.EqualTo(20f), "Configure still wins over the asset");
        }

        [Test]
        public void StepPublishesTheFlightState()
        {
            _motor.Body.linearVelocity = new Vector3(0f, -5f, 50f);

            _motor.Step();

            Assert.That(_motor.State.Airspeed, Is.EqualTo(Mathf.Sqrt(2525f)).Within(Epsilon));
            Assert.That(_motor.State.AngleOfAttackRadians, Is.EqualTo(Mathf.Atan(0.1f)).Within(Epsilon));
            Assert.That(_motor.State.LoadFactor, Is.GreaterThan(0f), "air from below the nose lifts");
            Assert.That(
                _motor.State.LoadFactor,
                Is.EqualTo(_motor.LastCommand.LocalAcceleration.Y / Physics.gravity.magnitude).Within(Epsilon));
            Assert.That(_motor.State.Grounded, Is.False);
            Assert.That(_motor.State, Is.EqualTo(_motor.LastCommand.State));
        }

        [Test]
        public void StepAddsTheGroundAndAppliesTheConditionFromTheSameObject()
        {
            _motor.Body.isKinematic = true;
            _motor.SetInput(new VehicleMotorInput(1f));
            var ground = _motor.gameObject.AddComponent<FakeAircraftGroundSource>();
            var condition = _motor.gameObject.AddComponent<FakeAircraftConditionSource>();
            ground.Contact = new AircraftGroundContact(true, new System.Numerics.Vector3(0f, 0f, -2f), System.Numerics.Vector3.Zero);
            condition.Condition = new AircraftCondition(thrust: 0.5f);

            _motor.Step();

            Assert.That(
                _motor.LastCommand.LocalAcceleration.Z,
                Is.EqualTo((AircraftSpec.DefaultMaxThrust * 0.5f) - 2f).Within(Epsilon));
            Assert.That(_motor.State.Grounded, Is.True);
            Assert.That(ground.Calls, Is.EqualTo(1));
            Assert.That(ground.LastInput, Is.EqualTo(new VehicleMotorInput(1f)));

            Object.DestroyImmediate(ground);
            Object.DestroyImmediate(condition);
            _motor.Step();

            Assert.That(_motor.LastCommand.LocalAcceleration.Z, Is.EqualTo(AircraftSpec.DefaultMaxThrust).Within(Epsilon));
            Assert.That(_motor.State.Grounded, Is.False);
        }
    }
}
