using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // Edit Mode では FixedUpdate も OnEnable・OnDisable も走らないので、Tick と Release を直接呼んで確かめる。
    public sealed class BotAircraftPilotTests
    {
        private const float Epsilon = 1e-3f;
        private const float Altitude = 500f;

        private readonly List<Object> _created = new List<Object>();
        private Vehicle _vehicle;
        private SimpleAircraftMotor _motor;
        private VehicleMotorLink _link;
        private BotAircraftPilot _pilot;

        [SetUp]
        public void SetUp()
        {
            _vehicle = CreateAircraft("Plane", withLink: true);
            _motor = _vehicle.GetComponent<SimpleAircraftMotor>();
            _link = _vehicle.GetComponent<VehicleMotorLink>();
            _pilot = _vehicle.gameObject.AddComponent<BotAircraftPilot>();
            _pilot.Configure(Spec());
            _pilot.HoldAltitude = Altitude;
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
        public void ThePilotSitsInTheDriverSeatAndItsControlsReachTheMotorThroughIt()
        {
            _pilot.SetWaypoints(Point("A", 1000f, 0f));

            _pilot.Tick(0.02f);

            Assert.That(((IVehicleRider)_pilot).Seat, Is.SameAs(_vehicle.Seats[0]));
            Assert.That(_vehicle.Seats[0].Occupant, Is.SameAs(_pilot));
            Assert.That(_link.ControlScheme, Is.SameAs(_pilot));
            Assert.That(_pilot.IsFlying, Is.True);
            Assert.That(_motor.Input, Is.EqualTo(_pilot.LastInput));
            Assert.That(_motor.Input[AircraftFlight.RollAxis], Is.EqualTo(0.8f).Within(Epsilon), "banks right toward the point");
            Assert.That(_motor.Input[AircraftFlight.YawAxis], Is.EqualTo(0.45f).Within(Epsilon));
            Assert.That(_motor.Input[AircraftFlight.ThrottleAxis], Is.EqualTo(0.5f).Within(Epsilon));
            Assert.That(_motor.Input[AircraftFlight.PitchAxis], Is.EqualTo(0f).Within(Epsilon), "already at the altitude it holds");
        }

        [Test]
        public void TheAircraftMotorNamesThisPilotForBots()
        {
            Assert.That(_motor, Is.InstanceOf<IBotPilotedMotor>());
            Assert.That(_motor.BotPilotType, Is.EqualTo(typeof(BotAircraftPilot)));
            Assert.That(_pilot, Is.InstanceOf<IBotPilot>());
        }

        [Test]
        public void BelowTheHeldAltitudeItClimbs()
        {
            _pilot.SetWaypoints(Point("A", 0f, 2000f));
            _pilot.HoldAltitude = Altitude + 50f;

            _pilot.Tick(0.02f);

            Assert.That(_motor.Input[AircraftFlight.PitchAxis], Is.EqualTo(0.6f).Within(Epsilon));
        }

        [Test]
        public void FollowingTheWaypointHeightAimsForEachPointsAltitude()
        {
            var point = Point("A", 0f, 2000f);
            point.position = new Vector3(0f, Altitude - 100f, 2000f);
            _pilot.SetWaypoints(point);
            _pilot.FollowWaypointHeight = true;

            _pilot.Tick(0.02f);

            Assert.That(_motor.Input[AircraftFlight.PitchAxis], Is.EqualTo(-0.6f).Within(Epsilon));
        }

        [Test]
        public void ReachingAPointHeadsForTheNextAndTheLastLeadsBackToTheFirst()
        {
            var a = Point("A", 0f, 50f);
            var b = Point("B", 2000f, 0f);
            var c = Point("C", 0f, 3000f);
            _pilot.SetWaypoints(a, b, c);

            _pilot.Tick(0.02f);
            Assert.That(_pilot.WaypointIndex, Is.EqualTo(1), "A is within the arrival radius across the ground");

            _pilot.Tick(0.02f);
            Assert.That(_pilot.WaypointIndex, Is.EqualTo(1), "B is still far away");

            _vehicle.transform.position = new Vector3(2000f, Altitude, 30f);
            _pilot.Tick(0.02f);
            Assert.That(_pilot.WaypointIndex, Is.EqualTo(2));

            _vehicle.transform.position = new Vector3(40f, Altitude, 3000f);
            _pilot.Tick(0.02f);
            Assert.That(_pilot.WaypointIndex, Is.EqualTo(0), "after the last point it loops");
        }

        [Test]
        public void MissingWaypointsAreSkipped()
        {
            _pilot.SetWaypoints(null, Point("B", 2000f, 0f));

            _pilot.Tick(0.02f);

            Assert.That(_pilot.WaypointIndex, Is.EqualTo(1));
        }

        [Test]
        public void WithoutWaypointsItWarnsAndFliesStraightOnAtItsAltitude()
        {
            LogAssert.Expect(LogType.Warning, BotAircraftPilotLog.NoWaypoints("Plane"));

            _pilot.Tick(0.02f);

            Assert.That(_motor.Input[AircraftFlight.RollAxis], Is.EqualTo(0f).Within(Epsilon));
            Assert.That(_motor.Input[AircraftFlight.YawAxis], Is.EqualTo(0f).Within(Epsilon));
            Assert.That(_motor.Input[AircraftFlight.PitchAxis], Is.EqualTo(0f).Within(Epsilon));
            Assert.That(_motor.Input[AircraftFlight.ThrottleAxis], Is.EqualTo(0.5f).Within(Epsilon));
        }

        [Test]
        public void ReleasingLeavesTheSeatAndGivesBackTheSchemeItReplaced()
        {
            var previous = new FixedScheme();
            _link.ControlScheme = previous;
            _pilot.SetWaypoints(Point("A", 1000f, 0f));
            _pilot.Tick(0.02f);
            Assert.That(_link.ControlScheme, Is.SameAs(_pilot));

            _pilot.Release();

            Assert.That(_link.ControlScheme, Is.SameAs(previous));
            Assert.That(_vehicle.Seats[0].IsOccupied, Is.False);
            Assert.That(((IVehicleRider)_pilot).Seat, Is.Null);
            Assert.That(_motor.Input, Is.EqualTo(VehicleMotorInput.Idle));
            Assert.That(_pilot.IsFlying, Is.False);
        }

        [Test]
        public void AnotherRiderInTheDriverSeatIsFlownForInsteadOfSittingThere()
        {
            var rider = Track(new GameObject("Bot")).AddComponent<VehicleTestRider>();
            Assert.That(_vehicle.TryEnter(rider, 0, false), Is.True);
            _pilot.Rider = rider;
            _pilot.SetWaypoints(Point("A", 1000f, 0f));

            _pilot.Tick(0.02f);

            Assert.That(((IVehicleRider)_pilot).Seat, Is.Null);
            Assert.That(_vehicle.Seats[0].Occupant, Is.SameAs(rider));
            Assert.That(_motor.Input[AircraftFlight.RollAxis], Is.EqualTo(0.8f).Within(Epsilon));
        }

        [Test]
        public void AnAssignedRiderOutOfTheDriverSeatDoesNotFly()
        {
            var rider = Track(new GameObject("Bot")).AddComponent<VehicleTestRider>();
            _pilot.Rider = rider;
            _pilot.SetWaypoints(Point("A", 1000f, 0f));

            _pilot.Tick(0.02f);

            Assert.That(_vehicle.Seats[0].IsOccupied, Is.False, "the pilot does not take the seat for itself");
            Assert.That(_motor.Input, Is.EqualTo(VehicleMotorInput.Idle));
        }

        [Test]
        public void AssigningARiderGivesUpTheSeatThePilotHeld()
        {
            _pilot.SetWaypoints(Point("A", 1000f, 0f));
            _pilot.Tick(0.02f);
            var rider = Track(new GameObject("Bot")).AddComponent<VehicleTestRider>();

            _pilot.Rider = rider;

            Assert.That(_vehicle.Seats[0].IsOccupied, Is.False);
            Assert.That(_pilot.Rider, Is.SameAs(rider));
        }

        [Test]
        public void ATakenDriverSeatLeavesTheControlsIdle()
        {
            var someone = Track(new GameObject("Someone")).AddComponent<VehicleTestRider>();
            Assert.That(_vehicle.TryEnter(someone, 0, false), Is.True);
            _pilot.SetWaypoints(Point("A", 1000f, 0f));

            _pilot.Tick(0.02f);

            Assert.That(((IVehicleRider)_pilot).Seat, Is.Null);
            Assert.That(_motor.Input, Is.EqualTo(VehicleMotorInput.Idle));
        }

        [Test]
        public void AWreckedAircraftIsNotFlown()
        {
            _pilot.SetWaypoints(Point("A", 1000f, 0f));
            _vehicle.Wreck();

            _pilot.Tick(0.02f);

            Assert.That(((IVehicleRider)_pilot).Seat, Is.Null);
            Assert.That(_motor.Input, Is.EqualTo(VehicleMotorInput.Idle));
        }

        [Test]
        public void WithoutAMotorLinkItWarnsThatTheControlsCannotReachTheMotor()
        {
            var bare = CreateAircraft("Bare", withLink: false);
            var pilot = bare.gameObject.AddComponent<BotAircraftPilot>();
            LogAssert.Expect(LogType.Warning, BotAircraftPilotLog.NoMotorLink("Bare"));

            pilot.Tick(0.02f);

            Assert.That(bare.Seats[0].IsOccupied, Is.False);
        }

        [Test]
        public void ANegativeStepIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _pilot.Tick(-0.02f));
        }

        [Test]
        public void TheSettingsAssetDefaultsToTheSpecDefaultsAndRoundTrips()
        {
            var settings = Track(ScriptableObject.CreateInstance<BotAircraftPilotSettings>());
            var defaults = settings.ToSpec();
            Assert.That(defaults.CruiseSpeed, Is.EqualTo(AircraftAutopilotSpec.DefaultCruiseSpeed));
            Assert.That(defaults.MaxBankAngle, Is.EqualTo(AircraftAutopilotSpec.DefaultMaxBankAngle));
            Assert.That(defaults.CriticalAngleOfAttack, Is.EqualTo(AircraftAutopilotSpec.DefaultCriticalAngleOfAttack));
            Assert.That(defaults.MinGroundClearance, Is.EqualTo(AircraftAutopilotSpec.DefaultMinGroundClearance));
            Assert.That(defaults.ArrivalRadius, Is.EqualTo(AircraftAutopilotSpec.DefaultArrivalRadius));

            settings.CopyFrom(Spec());
            var copied = settings.ToSpec();
            Assert.That(copied.CruiseSpeed, Is.EqualTo(55f));
            Assert.That(copied.RollGain, Is.EqualTo(0.02f));
            Assert.That(copied.GroundLookAheadSeconds, Is.EqualTo(3f));
            Assert.That(copied.ArrivalRadius, Is.EqualTo(80f));
        }

        [Test]
        public void TheSettingsAssetIsReadWhenNothingIsConfigured()
        {
            var settings = Track(ScriptableObject.CreateInstance<BotAircraftPilotSettings>());
            settings.CopyFrom(new AircraftAutopilotSpec(arrivalRadius: 123f));
            var pilot = CreateAircraft("Other", withLink: true).gameObject.AddComponent<BotAircraftPilot>();

            pilot.Settings = settings;

            Assert.That(pilot.Spec.ArrivalRadius, Is.EqualTo(123f));
        }

        [Test]
        public void AnInvalidSettingsAssetNamesItselfInTheError()
        {
            var settings = Track(ScriptableObject.CreateInstance<BotAircraftPilotSettings>());
            settings.name = "Broken Pilot";
            var serialized = new SerializedObject(settings);
            serialized.FindProperty("_angleOfAttackMargin").floatValue = 30f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var error = Assert.Throws<ArgumentException>(() => settings.ToSpec());

            Assert.That(error.Message, Does.Contain("Broken Pilot"));
            Assert.That(error.Message, Does.Contain("angleOfAttackMargin"));
        }

        private Vehicle CreateAircraft(string name, bool withLink)
        {
            var root = Track(new GameObject(name));
            root.transform.position = new Vector3(0f, Altitude, 0f);
            var seat = new GameObject("Pilot Seat").AddComponent<VehicleSeat>();
            seat.transform.SetParent(root.transform, false);
            seat.transform.localPosition = new Vector3(0f, 0.5f, 0.5f);
            var vehicle = root.AddComponent<Vehicle>();
            vehicle.Configure(new VehicleSpec("plane", 500, new[] { new VehicleSeatSpec(VehicleSeatRole.Driver) }));
            var motor = root.AddComponent<SimpleAircraftMotor>();
            motor.Body.useGravity = false;
            motor.Body.linearVelocity = new Vector3(0f, 0f, 55f);
            if (withLink)
            {
                Assert.That(root.AddComponent<VehicleMotorLink>().Bind(), Is.True);
            }

            return vehicle;
        }

        private Transform Point(string name, float x, float z)
        {
            var point = Track(new GameObject(name)).transform;
            point.position = new Vector3(x, 0f, z);
            return point;
        }

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }

        private static AircraftAutopilotSpec Spec()
        {
            return new AircraftAutopilotSpec(
                cruiseSpeed: 55f,
                cruiseThrottle: 0.5f,
                speedGain: 0.05f,
                maxBankAngle: 40f,
                headingGain: 1.5f,
                rollGain: 0.02f,
                yawGain: 0.005f,
                maxClimbAngle: 15f,
                altitudeGain: 0.3f,
                pitchGain: 0.04f,
                maxLoadFactor: 5f,
                loadFactorMargin: 1.5f,
                criticalAngleOfAttack: 15f,
                angleOfAttackMargin: 5f,
                minGroundClearance: 40f,
                groundLookAheadSeconds: 3f,
                arrivalRadius: 80f);
        }

        private sealed class FixedScheme : IVehicleControlScheme
        {
            public VehicleMotorInput ToMotorInput(VehicleSeat seat, VehicleSeatInput input, float deltaSeconds) => VehicleMotorInput.Idle;
        }
    }
}
