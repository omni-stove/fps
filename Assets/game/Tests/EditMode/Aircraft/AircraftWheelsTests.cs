using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // Edit Mode では物理が進まず WheelCollider が接地しないので、車輪へ当てる値は Apply を直接呼んで確かめ、高さは Raycast が当たるよう SyncTransforms してから測る。
    public sealed class AircraftWheelsTests
    {
        private const float _epsilon = 1e-3f;

        private readonly List<Object> _created = new List<Object>();

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
        public void TheWheelsAheadOfTheCenterOfMassAreTheFrontWheels()
        {
            var plane = NewAircraft(out var nose, out var left, out var right);

            Assert.That(plane.Wheels, Is.EquivalentTo(new[] { nose, left, right }));
            Assert.That(plane.FrontWheels, Is.EquivalentTo(new[] { nose }));
            Assert.That(plane.IsFrontWheel(nose), Is.True);
            Assert.That(plane.IsFrontWheel(left), Is.False);
            Assert.That(plane.IsFrontWheel(null), Is.False);
        }

        [Test]
        public void TheFrontIsDecidedByPositionNotByName()
        {
            var plane = NewAircraft(out var nose, out var left, out var right);
            nose.name = "MainGearRear";
            left.name = "NoseGear";

            plane.Body.centerOfMass = new Vector3(0f, 0f, -1f);
            plane.CollectWheels();

            Assert.That(plane.FrontWheels, Is.EquivalentTo(new[] { nose, left, right }), "with the weight further back, the mains are ahead of it too");

            plane.Body.centerOfMass = new Vector3(0f, 0f, 5f);
            plane.CollectWheels();

            Assert.That(plane.FrontWheels, Is.Empty);
        }

        [Test]
        public void ATurnedChildStillCountsByWhereItsWheelSits()
        {
            var plane = NewAircraft(out _, out _, out _);
            var strut = new GameObject("Strut");
            strut.transform.SetParent(plane.transform, false);
            strut.transform.localPosition = new Vector3(0f, -1f, -3f);
            strut.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var tail = strut.AddComponent<WheelCollider>();
            tail.center = new Vector3(0f, 0f, -5f);

            plane.CollectWheels();

            Assert.That(plane.IsFrontWheel(tail), Is.True, "the strut faces backwards, so its -z center sits at +2 on the aircraft");
        }

        [Test]
        public void WheelsUnderAnotherRigidbodyAreNotCollected()
        {
            var plane = NewAircraft(out _, out _, out _);
            var loose = new GameObject("LooseGear");
            loose.transform.SetParent(plane.transform, false);
            loose.AddComponent<Rigidbody>();
            var child = new GameObject("Wheel");
            child.transform.SetParent(loose.transform, false);
            var looseWheel = child.AddComponent<WheelCollider>();

            plane.CollectWheels();

            Assert.That(plane.Wheels, Has.No.Member(looseWheel));
            Assert.That(plane.Wheels.Count, Is.EqualTo(3));
        }

        [Test]
        public void ApplyTurnsOnlyTheFrontWheelsAndBrakesEveryWheel()
        {
            var plane = NewAircraft(out var nose, out var left, out var right);

            plane.Apply(new AircraftGroundCommand(15f, 800f));

            Assert.That(nose.steerAngle, Is.EqualTo(15f).Within(_epsilon));
            Assert.That(left.steerAngle, Is.EqualTo(0f));
            Assert.That(right.steerAngle, Is.EqualTo(0f));
            Assert.That(nose.brakeTorque, Is.EqualTo(800f).Within(_epsilon));
            Assert.That(left.brakeTorque, Is.EqualTo(800f).Within(_epsilon));
            Assert.That(right.brakeTorque, Is.EqualTo(800f).Within(_epsilon));
            Assert.That(plane.LastCommand, Is.EqualTo(new AircraftGroundCommand(15f, 800f)));
        }

        [Test]
        public void TheIdleCommandFromTheSpecBrakesEveryWheel()
        {
            var plane = NewAircraft(out var nose, out var left, out var right);
            plane.Configure(new AircraftGroundSpec(idleBrakeTorque: 450f));

            plane.Apply(AircraftGround.Compute(plane.Spec, VehicleMotorInput.Idle, 0f, true));

            Assert.That(nose.brakeTorque, Is.EqualTo(450f).Within(_epsilon));
            Assert.That(left.brakeTorque, Is.EqualTo(450f).Within(_epsilon));
            Assert.That(right.brakeTorque, Is.EqualTo(450f).Within(_epsilon));
        }

        [Test]
        public void OffTheGroundTheContactCentersTheNoseAndReleasesTheBrakes()
        {
            var plane = NewAircraft(out var nose, out var left, out _);
            plane.Apply(new AircraftGroundCommand(20f, 1000f));

            var contact = plane.GroundContact(new VehicleMotorInput(-1f, 0f, 0f, 1f), new Vector3(0f, 0f, 5f), Vector3.zero);

            Assert.That(contact.Grounded, Is.False);
            Assert.That(contact.LocalAcceleration, Is.EqualTo(System.Numerics.Vector3.Zero));
            Assert.That(contact.LocalAngularAcceleration, Is.EqualTo(System.Numerics.Vector3.Zero));
            Assert.That(nose.steerAngle, Is.EqualTo(0f));
            Assert.That(left.brakeTorque, Is.EqualTo(0f));
            Assert.That(plane.LastContact, Is.EqualTo(contact));
        }

        [Test]
        public void TheContactReportsTheHeightFromTheLowestWheelToTheGround()
        {
            var plane = NewAircraft(out _, out _, out _);
            NewGround(topY: -10f);
            Physics.SyncTransforms();

            var contact = plane.GroundContact(VehicleMotorInput.Idle, Vector3.zero, Vector3.zero);

            // 車輪の中心は y = -1、底はそこから脚 0.3 と半径 0.5 だけ下の y = -1.8。地面の上は y = -10。
            Assert.That(contact.HeightAboveGround.HasValue, Is.True);
            Assert.That(contact.HeightAboveGround.Value, Is.EqualTo(8.2f).Within(_epsilon));
        }

        [Test]
        public void TheAircraftsOwnCollidersAreNotTakenForTheGround()
        {
            var plane = NewAircraft(out _, out _, out _);
            var belly = new GameObject("Belly");
            belly.transform.SetParent(plane.transform, false);
            belly.transform.localPosition = new Vector3(0f, -3f, 0f);
            belly.AddComponent<BoxCollider>().size = new Vector3(6f, 0.2f, 12f);
            NewGround(topY: -10f);
            Physics.SyncTransforms();

            Assert.That(plane.MeasureHeight(), Is.EqualTo(8.2f).Within(_epsilon));
        }

        [Test]
        public void NoGroundWithinTheProbeLeavesTheHeightUnknown()
        {
            var plane = NewAircraft(out _, out _, out _);
            plane.Configure(new AircraftGroundSpec(heightProbeDistance: 5f));
            NewGround(topY: -10f);
            Physics.SyncTransforms();

            var contact = plane.GroundContact(VehicleMotorInput.Idle, Vector3.zero, Vector3.zero);

            Assert.That(contact.HeightAboveGround.HasValue, Is.False);
        }

        [Test]
        public void TheGroundLayersLimitWhatIsMeasured()
        {
            var plane = NewAircraft(out _, out _, out _);
            var ground = NewGround(topY: -10f);
            ground.layer = 2;
            plane.GroundLayers = 1 << 0;
            Physics.SyncTransforms();

            Assert.That(plane.MeasureHeight().HasValue, Is.False);
        }

        [Test]
        public void TheSpecComesFromTheDefinitionsGroundBlockUnlessConfigured()
        {
            var plane = NewAircraft(out _, out _, out _);
            var definition = ScriptableObject.CreateInstance<AircraftDefinition>();
            definition.name = "Trainer";
            _created.Add(definition);
            definition.Ground.CopyFrom(new AircraftGroundSpec(maxSteerAngle: 25f, highSpeedSteerAngle: 4f));

            Assert.That(plane.Spec.MaxSteerAngle, Is.EqualTo(AircraftGroundSpec.DefaultMaxSteerAngle), "no asset uses the defaults");

            plane.Definition = definition;
            Assert.That(plane.Spec.MaxSteerAngle, Is.EqualTo(25f));
            Assert.That(plane.Spec.HighSpeedSteerAngle, Is.EqualTo(4f));

            plane.Configure(new AircraftGroundSpec(maxSteerAngle: 12f, highSpeedSteerAngle: 2f));
            Assert.That(plane.Spec.MaxSteerAngle, Is.EqualTo(12f));
            Assert.Throws<ArgumentNullException>(() => plane.Configure(null));
        }

        [Test]
        public void AnInvalidGroundBlockNamesTheAsset()
        {
            var definition = ScriptableObject.CreateInstance<AircraftDefinition>();
            definition.name = "BrokenGear";
            _created.Add(definition);
            definition.Ground.CopyFrom(new AircraftGroundSpec(maxSteerAngle: 10f, highSpeedSteerAngle: 10f));
            var settings = definition.Ground;
            typeof(AircraftGroundSettings)
                .GetField("_highSpeedSteerAngle", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(settings, 30f);

            var error = Assert.Throws<ArgumentException>(() => definition.ToGroundSpec());

            Assert.That(error.Message, Does.Contain("BrokenGear"));
            Assert.That(error.InnerException, Is.InstanceOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void TheGroundBlockSurvivesARoundTrip()
        {
            var definition = ScriptableObject.CreateInstance<AircraftDefinition>();
            _created.Add(definition);
            var original = new AircraftGroundSpec(
                maxSteerAngle: 50f,
                highSpeedSteerAngle: 8f,
                steerFadeSpeed: 15f,
                brakeTorque: 9000f,
                idleBrakeTorque: 1200f,
                heightProbeDistance: 80f);

            definition.Ground.CopyFrom(original);
            var spec = definition.ToGroundSpec();

            Assert.That(spec.MaxSteerAngle, Is.EqualTo(50f));
            Assert.That(spec.HighSpeedSteerAngle, Is.EqualTo(8f));
            Assert.That(spec.SteerFadeSpeed, Is.EqualTo(15f));
            Assert.That(spec.BrakeTorque, Is.EqualTo(9000f));
            Assert.That(spec.IdleBrakeTorque, Is.EqualTo(1200f));
            Assert.That(spec.HeightProbeDistance, Is.EqualTo(80f));
        }

        [Test]
        public void TheMotorReadsTheWheelsAsItsGroundSource()
        {
            var plane = NewAircraft(out var nose, out _, out _);
            var motor = plane.gameObject.AddComponent<SimpleAircraftMotor>();
            plane.Body.isKinematic = true;
            plane.Apply(new AircraftGroundCommand(20f, 1000f));

            motor.SetInput(VehicleMotorInput.Idle.With(AircraftFlight.YawAxis, 1f));
            motor.Step();

            Assert.That(motor.State.Grounded, Is.False);
            Assert.That(nose.steerAngle, Is.EqualTo(0f), "the motor's step went through the wheels");
        }

        private AircraftWheels NewAircraft(out WheelCollider nose, out WheelCollider left, out WheelCollider right)
        {
            var root = new GameObject("Aircraft");
            _created.Add(root);
            var body = root.AddComponent<Rigidbody>();
            body.useGravity = false;

            nose = NewWheel(root.transform, "Nose", new Vector3(0f, -1f, 4f));
            left = NewWheel(root.transform, "Left", new Vector3(-2f, -1f, -0.5f));
            right = NewWheel(root.transform, "Right", new Vector3(2f, -1f, -0.5f));

            // 車輪を足した後に重心を置くのは、Unity が当たり判定から求めた重心で前後の分かれ目が動かないようにするため。
            body.centerOfMass = Vector3.zero;

            var wheels = root.AddComponent<AircraftWheels>();
            wheels.CollectWheels();
            return wheels;
        }

        private static WheelCollider NewWheel(Transform parent, string wheelName, Vector3 localPosition)
        {
            var go = new GameObject(wheelName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var wheel = go.AddComponent<WheelCollider>();
            wheel.radius = 0.5f;
            wheel.suspensionDistance = 0.3f;
            wheel.center = Vector3.zero;
            return wheel;
        }

        private GameObject NewGround(float topY)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            _created.Add(ground);
            ground.transform.position = new Vector3(0f, topY - 0.5f, 0f);
            ground.transform.localScale = new Vector3(100f, 1f, 100f);
            return ground;
        }
    }
}
