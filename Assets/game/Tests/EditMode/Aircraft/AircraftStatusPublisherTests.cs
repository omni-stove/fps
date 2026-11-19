using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // Edit Mode では LateUpdate も物理も走らないので、機体の Step と Refresh を直接呼んで確かめる。
    public sealed class AircraftStatusPublisherTests
    {
        private const float Epsilon = 1e-3f;
        private const float Altitude = 120f;

        private readonly List<Object> _created = new List<Object>();
        private GameObject _body;
        private PlayerVehicleRider _rider;
        private InputAction _move;
        private InputAction _sprint;
        private InputAction _crouch;
        private InputAction _jump;
        private Vehicle _vehicle;
        private SimpleAircraftMotor _motor;
        private AircraftStatusPublisher _publisher;

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();

            _body = Track(new GameObject("Pilot"));
            var controller = _body.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.5f;
            controller.center = Vector3.zero;
            var player = _body.AddComponent<PlayerController>();
            _move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            _sprint = new InputAction("Sprint", InputActionType.Button);
            _crouch = new InputAction("Crouch", InputActionType.Button);
            _jump = new InputAction("Jump", InputActionType.Button);
            var look = _body.AddComponent<FirstPersonLook>();
            player.Initialize(controller, look, _move, _sprint, _crouch, _jump, new MotorSpec(5f, 8f, 1.2f, 20f, 0.1f, 2.5f));
            _rider = _body.AddComponent<PlayerVehicleRider>();
            _rider.Bind(player, null);

            var root = Track(new GameObject("Plane"));
            root.transform.position = new Vector3(0f, Altitude, 0f);
            CreateSeat(root.transform, "Pilot Seat", new Vector3(0f, 0.5f, 0.5f));
            CreateSeat(root.transform, "Rear Seat", new Vector3(0f, 0.5f, -0.5f));
            _vehicle = root.AddComponent<Vehicle>();
            _vehicle.Configure(new VehicleSpec(
                "plane",
                500,
                new[]
                {
                    new VehicleSeatSpec(VehicleSeatRole.Driver),
                    new VehicleSeatSpec(VehicleSeatRole.Passenger),
                }));
            _motor = root.AddComponent<SimpleAircraftMotor>();
            _motor.Body.useGravity = false;

            _publisher = Track(new GameObject("Publisher")).AddComponent<AircraftStatusPublisher>();
            _publisher.Rider = _rider;
        }

        [TearDown]
        public void TearDown()
        {
            GameSignals.ResetAll();
            _move.Dispose();
            _sprint.Dispose();
            _crouch.Dispose();
            _jump.Dispose();
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
        public void NothingIsPublishedWhileNotRiding()
        {
            Fly(new Vector3(0f, 0f, 50f), 1f);

            Assert.That(_publisher.Refresh(), Is.Null);
            Assert.That(_publisher.Published, Is.Null);
            Assert.That(AircraftSignals.Status.HasValue, Is.False);
        }

        [Test]
        public void DrivingPublishesTheAircraftsReadings()
        {
            Assert.That(_vehicle.TryEnter(_rider, 0, false), Is.True);
            Fly(new Vector3(0f, 0f, 50f), 0.5f);

            var status = _publisher.Refresh();

            Assert.That(status.HasValue, Is.True);
            Assert.That(AircraftSignals.Status.Value, Is.EqualTo(status));
            Assert.That(status.Value.Speed, Is.EqualTo(50f).Within(Epsilon));
            Assert.That(status.Value.Altitude, Is.EqualTo(Altitude).Within(Epsilon));
            Assert.That(status.Value.Throttle, Is.EqualTo(0.5f));
            Assert.That(status.Value.AngleOfAttackDegrees, Is.EqualTo(0f).Within(Epsilon));
            Assert.That(status.Value.LoadFactor, Is.EqualTo(_motor.State.LoadFactor));
            Assert.That(status.Value.StallWarning, Is.False, "50 m/s along the nose is above the default stall speed");
            Assert.That(status.Value.Grounded, Is.False);
        }

        [Test]
        public void FlyingTooSlowRaisesTheStallWarning()
        {
            _vehicle.TryEnter(_rider, 0, false);
            Fly(new Vector3(0f, 0f, AircraftWarningSpec.DefaultStallSpeed - 10f), 1f);

            Assert.That(_publisher.Refresh().Value.StallWarning, Is.True);
        }

        [Test]
        public void TheWarningIsReadFromTheAircraftsDefinition()
        {
            var definition = ScriptableObject.CreateInstance<AircraftDefinition>();
            _created.Add(definition);
            definition.Warning.CopyFrom(new AircraftWarningSpec(stallSpeed: 60f));
            _motor.Definition = definition;
            _vehicle.TryEnter(_rider, 0, false);
            Fly(new Vector3(0f, 0f, 50f), 1f);

            Assert.That(_publisher.Refresh().Value.StallWarning, Is.True, "50 m/s is below this aircraft's 60 m/s");
        }

        [Test]
        public void ARiderInAnotherSeatPublishesNothing()
        {
            Assert.That(_vehicle.TryEnter(_rider, 1, false), Is.True);
            Fly(new Vector3(0f, 0f, 50f), 1f);

            Assert.That(_publisher.Refresh(), Is.Null);
            Assert.That(AircraftSignals.Status.HasValue, Is.False);
        }

        [Test]
        public void LeavingTheAircraftClearsTheSignal()
        {
            _vehicle.TryEnter(_rider, 0, false);
            Fly(new Vector3(0f, 0f, 50f), 1f);
            _publisher.Refresh();
            Assert.That(AircraftSignals.Status.HasValue, Is.True);

            Assert.That(_vehicle.ForceExit(_rider, VehicleLeaveReason.Forced), Is.True);

            Assert.That(_publisher.Refresh(), Is.Null);
            Assert.That(_publisher.Published, Is.Null);
            Assert.That(AircraftSignals.Status.Value, Is.Null);
        }

        [Test]
        public void LeavingDoesNotClearAValueAnotherPartPublished()
        {
            _vehicle.TryEnter(_rider, 0, false);
            Fly(new Vector3(0f, 0f, 50f), 1f);
            _publisher.Refresh();
            var other = new AircraftStatus(10f, 5f, 0f, 0f, 1f, false, true);
            AircraftSignals.Status.Publish(other);

            _vehicle.ForceExit(_rider, VehicleLeaveReason.Forced);
            _publisher.Refresh();

            Assert.That(AircraftSignals.Status.Value, Is.EqualTo(other));
        }

        [Test]
        public void ResetAllEmptiesTheAircraftSignalAndDropsItsSubscribers()
        {
            var heard = 0;
            AircraftSignals.Status.Changed += _ => heard++;
            _vehicle.TryEnter(_rider, 0, false);
            Fly(new Vector3(0f, 0f, 50f), 1f);
            _publisher.Refresh();
            Assert.That(AircraftSignals.Status.HasValue, Is.True);
            heard = 0;

            GameSignals.ResetAll();

            Assert.That(AircraftSignals.Status.Value, Is.Null);
            Assert.That(AircraftSignals.Status.HasValue, Is.False);
            AircraftSignals.Status.Publish(new AircraftStatus(10f, 5f, 0f, 0f, 1f, false, true));
            Assert.That(heard, Is.Zero, "subscribers from before the reset are gone");
        }

        [Test]
        public void WithoutAnInjectedRiderTheLocalPlayersRiderIsUsed()
        {
            _publisher.Rider = null;
            _vehicle.TryEnter(_rider, 0, false);
            Fly(new Vector3(0f, 0f, 50f), 1f);

            Assert.That(_publisher.Refresh(), Is.Null, "no local player yet");

            GameSignals.LocalPlayer.Publish(new LocalPlayerSignal(_body, null, null, null, null, null, null, null));

            Assert.That(_publisher.Refresh().HasValue, Is.True);
            Assert.That(AircraftStatusPublisher.DrivenAircraft(_rider), Is.SameAs(_motor));
        }

        [Test]
        public void AVehicleWithoutAnAircraftMotorIsNotAnAircraft()
        {
            Object.DestroyImmediate(_motor);
            _vehicle.TryEnter(_rider, 0, false);

            Assert.That(AircraftStatusPublisher.DrivenAircraft(_rider), Is.Null);
            Assert.That(AircraftStatusPublisher.DrivenAircraft(null), Is.Null);
            Assert.That(_publisher.Refresh(), Is.Null);
        }

        private void Fly(Vector3 velocity, float throttle)
        {
            _motor.Body.linearVelocity = velocity;
            _motor.SetInput(new VehicleMotorInput(throttle));
            _motor.Step();
        }

        private void CreateSeat(Transform parent, string name, Vector3 localPosition)
        {
            var seat = new GameObject(name).AddComponent<VehicleSeat>();
            seat.transform.SetParent(parent, false);
            seat.transform.localPosition = localPosition;
        }

        private GameObject Track(GameObject gameObject)
        {
            _created.Add(gameObject);
            return gameObject;
        }
    }
}
