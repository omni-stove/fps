using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using Shooter.Runtime.Traversal;
using UnityEngine;
using Object = UnityEngine.Object;
using WaterComponent = Shooter.Runtime.Traversal.Water;

namespace Shooter.Tests.EditMode
{
    // 物理は進めず、Step が船の姿勢と水から BoatHull を引いた結果を確かめる。
    public sealed class SimpleBoatMotorTests
    {
        private const float Epsilon = 1e-3f;

        // 水面を高さ 0 に置いた広い水。既定の船は原点が 0.2 m の高さで喫水まで沈む。
        private static readonly WaterVolumes _sea = new WaterVolumes(new[]
        {
            new WaterBox(new System.Numerics.Vector3(-50f, -10f, -50f), new System.Numerics.Vector3(50f, 5f, 50f), 0f),
        });

        private static readonly float _restHeight = -BoatSpec.DefaultDraft - BoatSpec.DefaultHullBottom;

        private readonly List<Object> _created = new List<Object>();
        private readonly List<WaterComponent> _waters = new List<WaterComponent>();
        private SimpleBoatMotor _motor;

        [SetUp]
        public void SetUp()
        {
            var boat = new GameObject("Boat");
            _created.Add(boat);
            _motor = boat.AddComponent<SimpleBoatMotor>();
            Place(new Vector3(0f, _restHeight, 0f), Quaternion.identity);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var water in _waters)
            {
                if (water != null)
                {
                    water.Unregister();
                }
            }

            _waters.Clear();
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
            Assert.That(_motor.Spec.Mass, Is.EqualTo(BoatSpec.DefaultMass));
            Assert.That(_motor.Spec.Draft, Is.EqualTo(BoatSpec.DefaultDraft));
            Assert.That(_motor.Spec.Thrust, Is.EqualTo(BoatSpec.DefaultThrust));
            Assert.That(_motor.Input, Is.EqualTo(VehicleMotorInput.Idle));
            Assert.That(_motor, Is.InstanceOf<IVehicleMotor>());
        }

        [Test]
        public void ConfigureKeepsTheSpecAndSetsTheMass()
        {
            var spec = new BoatSpec(mass: 1500f, maxForwardSpeed: 9f);

            _motor.Configure(spec);

            Assert.That(_motor.Spec, Is.SameAs(spec));
            Assert.That(_motor.Body.mass, Is.EqualTo(1500f));
            Assert.That(_motor.TopSpeed, Is.EqualTo(9f));
            Assert.That(_motor, Is.InstanceOf<IVehicleTopSpeed>());
            Assert.Throws<ArgumentNullException>(() => _motor.Configure(null));
        }

        [Test]
        public void TheSpecIsReadFromTheDefinitionWhenOneIsSet()
        {
            var definition = ScriptableObject.CreateInstance<BoatDefinition>();
            _created.Add(definition);
            definition.CopyFrom(new BoatSpec(mass: 1200f, thrust: 3f, maxForwardSpeed: 8f));
            _motor.Configure(new BoatSpec(thrust: 20f));

            _motor.Definition = definition;

            Assert.That(_motor.Spec.Mass, Is.EqualTo(1200f));
            Assert.That(_motor.Spec.Thrust, Is.EqualTo(3f), "setting the asset replaces a configured spec");
            Assert.That(_motor.Body.mass, Is.EqualTo(1200f));
            Assert.That(_motor.TopSpeed, Is.EqualTo(8f));
            _motor.Configure(new BoatSpec(thrust: 20f));
            Assert.That(_motor.Spec.Thrust, Is.EqualTo(20f), "Configure still wins over the asset");
        }

        [Test]
        public void RestingOnTheWaterAtTheDraftItFloatsWithoutMovingOn()
        {
            _motor.Water = _sea;

            _motor.Step();

            var command = _motor.LastCommand;
            Assert.That(_motor.IsAfloat, Is.True);
            Assert.That(command.WetFraction, Is.EqualTo(1f).Within(Epsilon));
            Assert.That(command.Force.Y, Is.EqualTo(_motor.Spec.Mass * Physics.gravity.magnitude).Within(1f));
            Assert.That(command.Force.X, Is.EqualTo(0f).Within(Epsilon));
            Assert.That(command.Force.Z, Is.EqualTo(0f).Within(Epsilon));
            Assert.That(command.AngularAcceleration.Y, Is.EqualTo(0f).Within(Epsilon));
        }

        [Test]
        public void ThrottleAndRudderPushTheBowForwardAndTurnIt()
        {
            _motor.Water = _sea;
            Place(new Vector3(0f, _restHeight, 0f), Quaternion.Euler(0f, 90f, 0f));
            var input = CarControlScheme.Instance.ToMotorInput(null, new VehicleSeatInput(new Vector2(1f, 1f), 0f, false, false, false, false, Vector3.zero, Vector3.forward), 0.02f);

            _motor.SetInput(input);
            _motor.Step();

            var command = _motor.LastCommand;
            Assert.That(_motor.Input, Is.EqualTo(input));
            Assert.That(command.ThrustAcceleration, Is.EqualTo(BoatSpec.DefaultThrust).Within(Epsilon));
            Assert.That(command.Force.X, Is.EqualTo(BoatSpec.DefaultMass * BoatSpec.DefaultThrust).Within(1f), "the bow faces +x");
            Assert.That(command.AngularAcceleration.Y, Is.GreaterThan(0f), "steering right turns about +y");
        }

        [Test]
        public void OnLandTheThrottleDoesNotPush()
        {
            _motor.Water = NoWaterVolumes.Instance;
            _motor.SetInput(new VehicleMotorInput(1f, 1f));

            _motor.Step();

            Assert.That(_motor.IsAfloat, Is.False);
            Assert.That(_motor.LastCommand.ThrustAcceleration, Is.EqualTo(0f));
            Assert.That(_motor.LastCommand.Force, Is.EqualTo(System.Numerics.Vector3.Zero));
            Assert.That(_motor.LastCommand.AngularAcceleration, Is.EqualTo(System.Numerics.Vector3.Zero));
        }

        [Test]
        public void WithoutAnAssignedWaterTheSceneWaterIsRead()
        {
            Assert.That(_motor.Water, Is.SameAs(WaterVolumes.Scene));
            _motor.Step();
            Assert.That(_motor.IsAfloat, Is.False, "no water in the scene yet");

            var lake = new GameObject("Lake");
            _created.Add(lake);
            lake.transform.position = new Vector3(0f, -2f, 0f);
            var water = lake.AddComponent<WaterComponent>();
            water.Size = new Vector3(20f, 3f, 20f);
            water.SurfaceHeight = 2f;
            water.Register();
            _waters.Add(water);

            _motor.Step();

            Assert.That(_motor.IsAfloat, Is.True);
            Assert.That(_motor.LastCommand.WetFraction, Is.EqualTo(1f).Within(Epsilon));
        }

        [Test]
        public void WithGravityOffTheWaterDoesNotLiftTheBoat()
        {
            _motor.Water = _sea;
            _motor.Body.useGravity = false;

            _motor.Step();

            Assert.That(_motor.LastCommand.Force.Y, Is.EqualTo(0f).Within(Epsilon));
        }

        [Test]
        public void StepOnAKinematicBodyComputesButDoesNotPush()
        {
            _motor.Water = _sea;
            _motor.Body.isKinematic = true;
            _motor.SetInput(new VehicleMotorInput(1f));

            _motor.Step();

            Assert.That(_motor.LastCommand.ThrustAcceleration, Is.EqualTo(BoatSpec.DefaultThrust).Within(Epsilon));
            Assert.That(_motor.Body.linearVelocity, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void TheBoatIsDrivenThroughTheCarControlScheme()
        {
            Assert.That(VehicleControlSchemes.For(_motor.gameObject), Is.SameAs(CarControlScheme.Instance));
            Assert.That(BoatHull.ThrottleAxis, Is.EqualTo(CarDrive.ThrottleAxis));
            Assert.That(BoatHull.SteerAxis, Is.EqualTo(CarDrive.SteerAxis));
        }

        private void Place(Vector3 position, Quaternion rotation)
        {
            _motor.transform.SetPositionAndRotation(position, rotation);
            _motor.Body.position = position;
            _motor.Body.rotation = rotation;
        }
    }
}
