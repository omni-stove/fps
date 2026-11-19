using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // 物理は進めず、Step が体の向き・速さと真下の地面から HelicopterFlight を引いた結果を確かめる。
    public sealed class SimpleHelicopterMotorTests
    {
        private const float Epsilon = 1e-3f;

        private static readonly VehicleMotorInput Neutral = new VehicleMotorInput(0f, 0f, 0f, 0f, 1f);

        private readonly List<Object> _created = new List<Object>();
        private SimpleHelicopterMotor _motor;

        [SetUp]
        public void SetUp()
        {
            var helicopter = new GameObject("Helicopter");
            _created.Add(helicopter);
            helicopter.transform.position = new Vector3(0f, 1000f, 0f);
            _motor = helicopter.AddComponent<SimpleHelicopterMotor>();
            _motor.Body.isKinematic = true;
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
            Assert.That(_motor.Spec.Mass, Is.EqualTo(HelicopterSpec.DefaultMass));
            Assert.That(_motor.Spec.MaxSpeed, Is.EqualTo(HelicopterSpec.DefaultMaxSpeed));
            Assert.That(_motor.Spec.SpoolSeconds, Is.EqualTo(HelicopterSpec.DefaultSpoolSeconds));
            Assert.That(_motor.Input, Is.EqualTo(VehicleMotorInput.Idle));
            Assert.That(_motor.Rotor, Is.EqualTo(0f));
        }

        [Test]
        public void ItComesWithItsControlScheme()
        {
            Assert.That(_motor.GetComponent<HelicopterControlScheme>(), Is.Not.Null);
            Assert.That(VehicleControlSchemes.For(_motor.gameObject), Is.InstanceOf<HelicopterControlScheme>());
            Assert.That(_motor, Is.InstanceOf<IVehicleMotor>());
        }

        [Test]
        public void ConfigureKeepsTheSpecAndSetsTheMass()
        {
            var spec = new HelicopterSpec(mass: 1500f, maxSpeed: 25f);

            _motor.Configure(spec);

            Assert.That(_motor.Spec, Is.SameAs(spec));
            Assert.That(_motor.Body.mass, Is.EqualTo(1500f));
            Assert.That(_motor.TopSpeed, Is.EqualTo(25f));
            Assert.That(_motor, Is.InstanceOf<IVehicleTopSpeed>());
            Assert.Throws<ArgumentNullException>(() => _motor.Configure(null));
        }

        [Test]
        public void NeutralWithTheRotorUpHoversAgainstGravity()
        {
            _motor.Configure(new HelicopterSpec(spoolSeconds: 0f));
            _motor.SetInput(Neutral);

            _motor.Step(0.02f);

            var command = _motor.LastCommand;
            Assert.That(_motor.Rotor, Is.EqualTo(1f));
            Assert.That(command.Acceleration.X, Is.EqualTo(0f).Within(Epsilon));
            Assert.That(command.Acceleration.Y, Is.EqualTo(Physics.gravity.magnitude).Within(Epsilon));
            Assert.That(command.Acceleration.Z, Is.EqualTo(0f).Within(Epsilon));
            Assert.That(command.AngularAcceleration.Length(), Is.EqualTo(0f).Within(Epsilon));
        }

        [Test]
        public void WithoutGravityNeutralPushesNothing()
        {
            _motor.Configure(new HelicopterSpec(spoolSeconds: 0f));
            _motor.Body.useGravity = false;
            _motor.SetInput(Neutral);

            _motor.Step(0.02f);

            Assert.That(_motor.LastCommand.Acceleration.Y, Is.EqualTo(0f).Within(Epsilon));
        }

        [Test]
        public void TheRotorSpoolsUpWhileFlownAndDownWhenLeft()
        {
            _motor.Configure(new HelicopterSpec(spoolSeconds: 2f));
            _motor.SetInput(Neutral);

            _motor.Step(0.5f);
            Assert.That(_motor.Rotor, Is.EqualTo(0.25f).Within(Epsilon));
            Assert.That(_motor.LastCommand.Acceleration.Y, Is.EqualTo(Physics.gravity.magnitude * 0.25f).Within(Epsilon));

            _motor.SetInput(VehicleMotorInput.Idle);
            _motor.Step(0.5f);
            Assert.That(_motor.Rotor, Is.EqualTo(0f));
            Assert.That(_motor.LastCommand.Acceleration.Y, Is.EqualTo(0f), "an empty helicopter falls");
        }

        [Test]
        public void StepUsesTheBodysHeadingAndVelocity()
        {
            _motor.Configure(new HelicopterSpec(maxSpeed: 20f, horizontalResponse: 1f, maxHorizontalAcceleration: 100f, spoolSeconds: 0f));
            var east = Quaternion.Euler(0f, 90f, 0f);
            _motor.transform.rotation = east;
            _motor.Body.rotation = east;
            _motor.Body.isKinematic = false;
            _motor.Body.linearVelocity = new Vector3(5f, 0f, 2f);
            _motor.SetInput(new VehicleMotorInput(0f, 1f, 0f, 0f, 1f));

            _motor.Step(0.02f);

            Assert.That(_motor.LastCommand.Acceleration.X, Is.EqualTo(15f).Within(Epsilon), "20 m/s east wanted, 5 m/s already");
            Assert.That(_motor.LastCommand.Acceleration.Z, Is.EqualTo(-2f).Within(Epsilon), "the northward drift is braked");
        }

        [Test]
        public void StepOnAKinematicBodyComputesButDoesNotPush()
        {
            _motor.Configure(new HelicopterSpec(spoolSeconds: 0f));
            _motor.SetInput(new VehicleMotorInput(1f, 1f, 0f, 0f, 1f));

            _motor.Step(0.02f);

            Assert.That(_motor.LastCommand.Acceleration.Z, Is.GreaterThan(0f));
            Assert.That(_motor.Body.linearVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(_motor.Body.angularVelocity, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void TheGroundBelowIsMeasuredWithoutItsOwnColliders()
        {
            _motor.Configure(new HelicopterSpec(groundEffectHeight: 4f, spoolSeconds: 0f));
            var skid = GameObject.CreatePrimitive(PrimitiveType.Cube);
            skid.transform.SetParent(_motor.transform, false);
            skid.transform.localPosition = new Vector3(0f, -0.6f, 0f);
            skid.transform.localScale = new Vector3(1f, 0.2f, 1f);
            Physics.SyncTransforms();

            _motor.Step(0.02f);
            Assert.That(_motor.GroundHeight, Is.EqualTo(float.PositiveInfinity), "its own skid is not the ground");
            Assert.That(_motor.LastCommand.GroundEffect, Is.EqualTo(0f));

            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _created.Add(ground);
            ground.transform.position = new Vector3(0f, 998.5f, 0f);
            ground.transform.localScale = new Vector3(200f, 1f, 200f);
            Physics.SyncTransforms();

            _motor.Step(0.02f);
            Assert.That(_motor.GroundHeight, Is.EqualTo(1f).Within(Epsilon));
            Assert.That(_motor.LastCommand.GroundEffect, Is.EqualTo(0.75f).Within(Epsilon));

            _motor.GroundMask = 0;
            _motor.Step(0.02f);
            Assert.That(_motor.GroundHeight, Is.EqualTo(float.PositiveInfinity), "layers outside the mask are not the ground");
        }

        [Test]
        public void TheSpecIsReadFromTheDefinitionWhenOneIsSet()
        {
            var definition = ScriptableObject.CreateInstance<HelicopterDefinition>();
            _created.Add(definition);
            definition.CopyFrom(new HelicopterSpec(mass: 2000f, maxSpeed: 40f));
            _motor.Configure(new HelicopterSpec(maxSpeed: 10f));

            _motor.Definition = definition;

            Assert.That(_motor.Spec.Mass, Is.EqualTo(2000f));
            Assert.That(_motor.Spec.MaxSpeed, Is.EqualTo(40f));
            Assert.That(_motor.Body.mass, Is.EqualTo(2000f));
            Assert.That(_motor.TopSpeed, Is.EqualTo(40f));
        }

        [Test]
        public void BadValuesAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _motor.Rotor = 1.5f);
            Assert.Throws<ArgumentOutOfRangeException>(() => _motor.Rotor = -0.1f);
            Assert.Throws<ArgumentOutOfRangeException>(() => _motor.Step(-0.02f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _motor.Step(float.NaN));
        }
    }
}
