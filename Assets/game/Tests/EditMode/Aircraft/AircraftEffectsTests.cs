using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // 物理もフレームも進めず、Show に入力と飛び方の状態を渡して、参照がどう動くかを確かめる。
    public sealed class AircraftEffectsTests
    {
        private const float Epsilon = 1e-3f;

        private readonly List<Object> _created = new List<Object>();
        private SimpleAircraftMotor _motor;
        private AircraftEffects _effects;

        [SetUp]
        public void SetUp()
        {
            var aircraft = new GameObject("Aircraft");
            _created.Add(aircraft);
            _motor = aircraft.AddComponent<SimpleAircraftMotor>();
            _motor.Body.useGravity = false;
            _effects = aircraft.AddComponent<AircraftEffects>();
            _effects.Bind(_motor);
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
        public void VaporIsOnlyOutWhileTheLoadFactorIsPastTheThreshold()
        {
            _effects.Configure(new AircraftEffectsSpec(vaporLoadFactor: 3f));

            _effects.Show(VehicleMotorInput.Idle, Flying(loadFactor: 1f));
            Assert.That(_effects.IsVaporOn, Is.False, "level flight is 1 G");
            Assert.That(_effects.VaporInstances, Is.Empty, "nothing is built before the first pull");

            _effects.Show(VehicleMotorInput.Idle, Flying(loadFactor: 5f));
            Assert.That(_effects.IsVaporOn, Is.True);
            Assert.That(Trails(), Is.Not.Empty);
            Assert.That(Trails(), Has.All.Matches<TrailRenderer>(trail => trail.emitting));

            _effects.Show(VehicleMotorInput.Idle, Flying(loadFactor: 2.9f));
            Assert.That(_effects.IsVaporOn, Is.False);
            Assert.That(Trails(), Has.None.Matches<TrailRenderer>(trail => trail.emitting));

            _effects.Show(VehicleMotorInput.Idle, Flying(loadFactor: -4f));
            Assert.That(_effects.IsVaporOn, Is.True, "a hard push counts too");

            _effects.Show(VehicleMotorInput.Idle, new AircraftFlightState(30f, 0f, 0f, 5f, true));
            Assert.That(_effects.IsVaporOn, Is.False, "no vapor on the ground");
        }

        [Test]
        public void TheVaporThresholdIsReadFromTheDefinition()
        {
            var definition = ScriptableObject.CreateInstance<AircraftDefinition>();
            _created.Add(definition);
            definition.Effects.CopyFrom(new AircraftEffectsSpec(vaporLoadFactor: 1.5f));

            _motor.Definition = definition;
            _effects.Bind(_motor);
            _effects.Show(VehicleMotorInput.Idle, Flying(loadFactor: 2f));

            Assert.That(_effects.Spec.VaporLoadFactor, Is.EqualTo(1.5f), "the motor's asset is used when the effects have none");
            Assert.That(_effects.IsVaporOn, Is.True);

            var own = ScriptableObject.CreateInstance<AircraftDefinition>();
            _created.Add(own);
            own.Effects.CopyFrom(new AircraftEffectsSpec(vaporLoadFactor: 6f));
            _effects.Definition = own;
            _effects.Show(VehicleMotorInput.Idle, Flying(loadFactor: 2f));

            Assert.That(_effects.IsVaporOn, Is.False, "the effects' own asset wins");
        }

        [Test]
        public void OpeningTheThrottleRaisesTheEnginePitch()
        {
            var clip = AudioClip.Create("Engine", 441, 1, 44100, false);
            _created.Add(clip);
            _effects.EngineClip = clip;
            var spec = new AircraftEffectsSpec(minEnginePitch: 0.5f, maxEnginePitch: 2f, minEngineVolume: 0.2f, maxEngineVolume: 1f, engineSpeedShare: 0.25f);
            _effects.Configure(spec);

            _effects.Show(VehicleMotorInput.Idle, Flying(airspeed: 0f));
            var idlePitch = _effects.EngineSource.pitch;
            var idleVolume = _effects.EngineSource.volume;

            _effects.Show(new VehicleMotorInput(1f), Flying(airspeed: 0f));

            Assert.That(_effects.EngineSource.clip, Is.SameAs(clip));
            Assert.That(_effects.EngineSource.loop, Is.True);
            Assert.That(idlePitch, Is.EqualTo(0.5f).Within(Epsilon));
            Assert.That(idleVolume, Is.EqualTo(0.2f).Within(Epsilon));
            Assert.That(_effects.EngineSource.pitch, Is.GreaterThan(idlePitch));
            Assert.That(_effects.EngineSource.pitch, Is.EqualTo(0.5f + (1.5f * 0.75f)).Within(Epsilon), "throttle carries 75% of the level");
            Assert.That(_effects.EngineSource.volume, Is.EqualTo(0.2f + (0.8f * 0.75f)).Within(Epsilon));

            _effects.Show(new VehicleMotorInput(1f), Flying(airspeed: _motor.Spec.Aero.MaxSpeed));
            Assert.That(_effects.EngineSource.pitch, Is.EqualTo(2f).Within(Epsilon), "full throttle at top speed is the top pitch");
        }

        [Test]
        public void EmptyReferencesDoNotThrow()
        {
            _effects.SetSurfaces(new[] { new AircraftControlSurface(null, AircraftControlSurfaceRole.Elevator, Vector3.right) });
            _effects.SetVaporAnchors(new Transform[] { null });

            Assert.DoesNotThrow(() => _effects.Show(new VehicleMotorInput(1f, 1f, -1f, 1f), Flying(loadFactor: 9f)));

            Assert.That(_effects.EngineSource, Is.Null, "no clip, no sound");
            Assert.That(_effects.IsVaporOn, Is.True);
            Assert.That(Trails(), Has.Count.EqualTo(1), "an aircraft with no look gets one code-built trail at its origin");
            Assert.That(Trails()[0].sharedMaterial, Is.Not.Null);
            Assert.DoesNotThrow(() => _effects.Show(VehicleMotorInput.Idle, Flying(loadFactor: 1f)));
        }

        [Test]
        public void ShowWithoutAMotorStillWorks()
        {
            var bare = new GameObject("Bare");
            _created.Add(bare);
            var effects = bare.AddComponent<AircraftEffects>();

            Assert.DoesNotThrow(() => effects.Show(new VehicleMotorInput(1f), Flying(loadFactor: 9f)));
            Assert.That(effects.Spec.VaporLoadFactor, Is.EqualTo(AircraftEffectsSpec.DefaultVaporLoadFactor));
            Assert.That(effects.IsVaporOn, Is.True);
        }

        [Test]
        public void ControlSurfacesTurnByTheInputAngleAndReturnAtRest()
        {
            var elevator = Child("Elevator");
            var leftAileron = Child("AileronL");
            var rightAileron = Child("AileronR");
            var rudder = Child("Rudder");
            rudder.localRotation = Quaternion.Euler(0f, 0f, 10f);
            var rudderRest = rudder.localRotation;
            _effects.Configure(new AircraftEffectsSpec(elevatorAngle: 30f, aileronAngle: 20f, rudderAngle: 15f));
            _effects.SetSurfaces(new[]
            {
                new AircraftControlSurface(elevator, AircraftControlSurfaceRole.Elevator, Vector3.right),
                new AircraftControlSurface(leftAileron, AircraftControlSurfaceRole.Aileron, Vector3.right),
                new AircraftControlSurface(rightAileron, AircraftControlSurfaceRole.Aileron, Vector3.right, reversed: true),
                new AircraftControlSurface(rudder, AircraftControlSurfaceRole.Rudder, Vector3.up),
            });

            _effects.Show(new VehicleMotorInput(0f, 0.5f, 1f, -1f), Flying());

            AssertRotation(elevator.localRotation, Quaternion.AngleAxis(15f, Vector3.right));
            AssertRotation(leftAileron.localRotation, Quaternion.AngleAxis(20f, Vector3.right));
            AssertRotation(rightAileron.localRotation, Quaternion.AngleAxis(-20f, Vector3.right));
            AssertRotation(rudder.localRotation, rudderRest * Quaternion.AngleAxis(-15f, Vector3.up));

            _effects.Show(VehicleMotorInput.Idle, Flying());

            AssertRotation(elevator.localRotation, Quaternion.identity);
            AssertRotation(rudder.localRotation, rudderRest);
        }

        [Test]
        public void AVaporPrefabIsPlacedAtEachAnchorAndSwitched()
        {
            var prefab = new GameObject("VaporPrefab");
            _created.Add(prefab);
            prefab.AddComponent<TrailRenderer>().emitting = true;
            var left = Child("TipL");
            var right = Child("TipR");
            _effects.VaporPrefab = prefab;
            _effects.SetVaporAnchors(new[] { left, right });

            _effects.Show(VehicleMotorInput.Idle, Flying(loadFactor: 9f));

            Assert.That(_effects.VaporInstances, Has.Count.EqualTo(2));
            Assert.That(_effects.VaporInstances[0].transform.parent, Is.SameAs(left));
            Assert.That(_effects.VaporInstances[1].transform.parent, Is.SameAs(right));
            Assert.That(Trails(), Has.All.Matches<TrailRenderer>(trail => trail.emitting));

            _effects.Show(VehicleMotorInput.Idle, Flying(loadFactor: 1f));
            Assert.That(Trails(), Has.None.Matches<TrailRenderer>(trail => trail.emitting));
        }

        [Test]
        public void TheCodeBuiltVaporSitsAtTheWingEnds()
        {
            var wing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wing.transform.SetParent(_motor.transform, false);
            wing.transform.localScale = new Vector3(8f, 0.2f, 1f);

            _effects.Show(VehicleMotorInput.Idle, Flying(loadFactor: 9f));

            var trails = Trails();
            Assert.That(trails, Has.Count.EqualTo(2));
            var xs = new[] { trails[0].transform.position.x, trails[1].transform.position.x };
            Array.Sort(xs);
            Assert.That(xs[0], Is.EqualTo(-4f).Within(Epsilon));
            Assert.That(xs[1], Is.EqualTo(4f).Within(Epsilon));
        }

        [Test]
        public void SetupRejectsMissingArguments()
        {
            Assert.Throws<ArgumentNullException>(() => _effects.Bind(null));
            Assert.Throws<ArgumentNullException>(() => _effects.Configure(null));
            Assert.Throws<ArgumentNullException>(() => _effects.SetSurfaces(null));
            Assert.Throws<ArgumentNullException>(() => _effects.SetVaporAnchors(null));
            Assert.Throws<ArgumentException>(() => _effects.SetSurfaces(new AircraftControlSurface[] { null }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftControlSurface(null, AircraftControlSurfaceRole.Rudder, Vector3.zero));
        }

        private static AircraftFlightState Flying(float airspeed = 50f, float loadFactor = 1f)
        {
            return new AircraftFlightState(airspeed, 0f, 0f, loadFactor, false);
        }

        private Transform Child(string childName)
        {
            var child = new GameObject(childName).transform;
            child.SetParent(_motor.transform, false);
            return child;
        }

        private List<TrailRenderer> Trails()
        {
            var trails = new List<TrailRenderer>();
            foreach (var instance in _effects.VaporInstances)
            {
                trails.AddRange(instance.GetComponentsInChildren<TrailRenderer>(true));
            }

            return trails;
        }

        private static void AssertRotation(Quaternion actual, Quaternion expected)
        {
            Assert.That(Quaternion.Angle(actual, expected), Is.LessThan(0.01f), $"expected {expected.eulerAngles} but was {actual.eulerAngles}");
        }
    }
}
