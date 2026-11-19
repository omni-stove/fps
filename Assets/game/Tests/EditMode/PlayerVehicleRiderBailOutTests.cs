using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // Edit Mode では物理が進まないので、機体の速さは Rigidbody に直接入れ、高さは機体を置く位置で決める。
    public sealed class PlayerVehicleRiderBailOutTests
    {
        private const float StepSeconds = 0.02f;
        private const float AirborneHeight = 50f;
        private const float BodyTop = 1.2f;

        private static readonly Vector3 _driverSeat = new Vector3(-0.5f, 0.4f, 0f);
        private static readonly Vector3 _leftExit = new Vector3(-2f, 0f, 0f);

        private readonly List<Object> _created = new List<Object>();
        private GameObject _body;
        private PlayerController _player;
        private PlayerVehicleRider _rider;
        private ParachuteMovement _parachute;
        private InputAction _move;
        private InputAction _sprint;
        private InputAction _crouch;
        private InputAction _jump;
        private GameObject _root;
        private Vehicle _vehicle;
        private Rigidbody _vehicleBody;

        [SetUp]
        public void SetUp()
        {
            var floor = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            floor.name = "Floor";
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(200f, 1f, 200f);

            _body = Track(new GameObject("Player"));
            _body.transform.position = new Vector3(-1.3f, 1f, 0f);
            var controller = _body.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.5f;
            controller.center = Vector3.zero;
            _player = _body.AddComponent<PlayerController>();
            _move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            _sprint = new InputAction("Sprint", InputActionType.Button);
            _crouch = new InputAction("Crouch", InputActionType.Button);
            _jump = new InputAction("Jump", InputActionType.Button);
            var look = _body.AddComponent<FirstPersonLook>();
            _player.Initialize(controller, look, _move, _sprint, _crouch, _jump, new MotorSpec(5f, 8f, 1.2f, 20f, 0.1f, 2.5f));

            _parachute = _body.AddComponent<ParachuteMovement>();
            _parachute.Initialize(_player);

            _rider = _body.AddComponent<PlayerVehicleRider>();
            _rider.Bind(_player, null);

            _root = Track(new GameObject("Plane"));
            var shell = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shell.name = "Body";
            shell.transform.SetParent(_root.transform, false);
            shell.transform.localPosition = new Vector3(0f, BodyTop * 0.5f, 0f);
            shell.transform.localScale = new Vector3(2f, BodyTop, 4f);

            var driver = new GameObject("Driver").AddComponent<VehicleSeat>();
            driver.transform.SetParent(_root.transform, false);
            driver.transform.localPosition = _driverSeat;
            var exit = new GameObject("ExitLeft").transform;
            exit.SetParent(_root.transform, false);
            exit.localPosition = _leftExit;
            driver.SetExitPoints(exit);

            _vehicleBody = _root.AddComponent<Rigidbody>();
            _vehicleBody.useGravity = false;
            _vehicle = _root.AddComponent<Vehicle>();
            Configure(BailOutSpec.Default);
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
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
        public void OnTheGroundLeavingStepsOutBesideTheVehicleAsBefore()
        {
            Board(Vector3.zero);
            Assert.That(_vehicle.EvaluateBailOut(out var height), Is.EqualTo(BailOutVerdict.TooLow));
            Assert.That(height, Is.EqualTo(0f).Within(1e-3f), "the vehicle rests on the floor");

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Plane' leave rider=Player seat=0 reason=Request exit=0");
            Assert.That(_rider.Exit(), Is.True);

            AssertNear(_body.transform.position, _leftExit + Vector3.up, "the seat's exit point");
            Assert.That(_parachute.IsOpen, Is.False);
            Assert.That(_player.Motor.Velocity.Length(), Is.Zero, "no momentum is carried out of the seat");
        }

        [Test]
        public void JustBelowTheMinimumHeightLeavingStillStepsOut()
        {
            Configure(new BailOutSpec(minHeight: 10f));
            Board(new Vector3(0f, 9f, 0f));
            Assert.That(_vehicle.EvaluateBailOut(out var height), Is.EqualTo(BailOutVerdict.TooLow));
            Assert.That(height, Is.EqualTo(9f).Within(1e-3f));

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Plane' leave rider=Player seat=0 reason=Request exit=0");
            Assert.That(_rider.Exit(), Is.True);
            Assert.That(_parachute.IsOpen, Is.False);
        }

        [Test]
        public void InTheAirLeavingBailsOutAboveTheVehicleAndOpensTheParachute()
        {
            Board(new Vector3(0f, AirborneHeight, 0f));
            _vehicleBody.linearVelocity = new Vector3(0f, 0f, 10f);
            Assert.That(_vehicle.EvaluateBailOut(out var height), Is.EqualTo(BailOutVerdict.BailOut));
            Assert.That(height, Is.EqualTo(AirborneHeight).Within(1e-3f));

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Plane' leave rider=Player seat=0 reason=BailOut exit=0");
            LogAssert.Expect(LogType.Log, "[Vehicle] 'Plane' bail out rider=Player speed=3.0 parachute=Open");
            Assert.That(_rider.Exit(), Is.True);

            Assert.That(_rider.IsRiding, Is.False);
            Assert.That(_player.IsControlHandedOff, Is.False);
            var feet = new Vector3(_driverSeat.x, AirborneHeight + BodyTop, 0f) + (Vector3.up * (0.05f + BailOutSpec.DefaultExitHeight));
            AssertNear(_body.transform.position, feet + Vector3.up, "above the vehicle, not at the seat's exit point");

            Assert.That(_parachute.IsOpen, Is.True);
            Assert.That(_parachute.IsDeployedByBailOut, Is.True);
            Assert.That(_parachute.LastVerdict, Is.EqualTo(ParachuteOpenVerdict.Open));
            Assert.That(_parachute.Rule, Is.EqualTo(Parachute.Off), "the bail-out does not need the jump-to-open rule");
            Assert.That(_parachute.IsActive, Is.True);
            Assert.That(_parachute.Motion.HorizontalVelocity.X, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(_parachute.Motion.HorizontalVelocity.Y, Is.EqualTo(3f).Within(1e-4f), "a third of the vehicle's speed");
            Assert.That(_player.Motor.Velocity.Z, Is.EqualTo(3f).Within(1e-4f));

            _player.Tick(Vector2.zero, false, false, false, StepSeconds);
            Assert.That(_player.ActiveMovementOverride, Is.SameAs(_parachute), "the parachute carries the rider down");

            _parachute.Rule = Parachute.Off;
            Assert.That(_parachute.IsOpen, Is.True, "setting the rule again does not fold a bail-out parachute");
        }

        [Test]
        public void ABlockedSpaceAboveBailsOutBesideTheVehicle()
        {
            Board(new Vector3(0f, AirborneHeight, 0f));
            var blocker = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            blocker.transform.position = new Vector3(_driverSeat.x, AirborneHeight + BodyTop + 2f, 0f);
            blocker.transform.localScale = new Vector3(1f, 2f, 1f);
            Physics.SyncTransforms();

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Plane' leave rider=Player seat=0 reason=BailOut exit=1");
            Assert.That(_rider.Exit(), Is.True);

            var halfWidth = 1f;
            var riderRadius = 0.5f;
            var side = -(halfWidth + riderRadius + BailOutSpec.DefaultExitSideDistance);
            AssertNear(
                _body.transform.position,
                new Vector3(side, AirborneHeight + _driverSeat.y + 1f, 0f),
                "on the seat's side of the vehicle, clear of its edge");
            Assert.That(_parachute.IsOpen, Is.True);
        }

        [Test]
        public void WreckingInTheAirBailsTheRiderOut()
        {
            Board(new Vector3(0f, AirborneHeight, 0f));
            _vehicleBody.linearVelocity = new Vector3(20f, 0f, 0f);

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Plane' wrecked riders=1");
            LogAssert.Expect(LogType.Log, "[Vehicle] 'Plane' leave rider=Player seat=0 reason=BailOut exit=0");
            LogAssert.Expect(LogType.Log, "[Vehicle] 'Plane' bail out rider=Player speed=6.0 parachute=Open");
            Assert.That(_vehicle.Wreck(), Is.True);

            Assert.That(_rider.IsRiding, Is.False);
            Assert.That(_parachute.IsOpen, Is.True);
            Assert.That(_parachute.Motion.HorizontalVelocity.X, Is.EqualTo(6f).Within(1e-4f));
        }

        [Test]
        public void WreckingInTheAirDoesNotBailOutARiderWhoDiedInTheSeat()
        {
            var damageable = _body.AddComponent<Damageable>();
            damageable.Initialize("player", 10);
            _rider.Bind(_player, null);
            Board(new Vector3(0f, AirborneHeight, 0f));
            _vehicleBody.linearVelocity = new Vector3(20f, 0f, 0f);

            LogAssert.Expect(LogType.Log, "[Combat] target died id=player");
            LogAssert.Expect(LogType.Log, "[Vehicle] 'Plane' died in seat rider=Player seat=0");
            damageable.TakeDamage(10);

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Plane' wrecked riders=1");
            LogAssert.Expect(LogType.Log, "[Vehicle] 'Plane' leave rider=Player seat=0 reason=BailOut exit=0");
            Assert.That(_vehicle.Wreck(), Is.True);

            Assert.That(_rider.IsRiding, Is.False);
            Assert.That(_parachute.IsOpen, Is.False, "the dead do not open a parachute");
            Assert.That(_player.Motor.Velocity.Length(), Is.Zero, "the dead keep none of the vehicle's speed");
        }

        [Test]
        public void WreckingOnTheGroundStillEjectsBesideTheVehicle()
        {
            Board(Vector3.zero);

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Plane' wrecked riders=1");
            LogAssert.Expect(LogType.Log, "[Vehicle] 'Plane' leave rider=Player seat=0 reason=Wrecked exit=0");
            Assert.That(_vehicle.Wreck(), Is.True);

            AssertNear(_body.transform.position, _leftExit + Vector3.up, "the seat's exit point");
            Assert.That(_parachute.IsOpen, Is.False);
        }

        [Test]
        public void ASwitchedOffBailOutStepsOutEvenInTheAir()
        {
            Configure(new BailOutSpec(enabled: false));
            Board(new Vector3(0f, AirborneHeight, 0f));
            Assert.That(_vehicle.EvaluateBailOut(), Is.EqualTo(BailOutVerdict.Off));

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Plane' leave rider=Player seat=0 reason=Request exit=0");
            Assert.That(_rider.Exit(), Is.True);
            Assert.That(_parachute.IsOpen, Is.False);
        }

        [Test]
        public void WithoutAParachuteTheRiderStillBailsOutAndKeepsTheSpeed()
        {
            // Edit Mode では OnDestroy が走らず登録が残るので、消す前に自分で外す。
            _player.UnregisterMovementOverride(_parachute);
            Object.DestroyImmediate(_parachute);
            Board(new Vector3(0f, AirborneHeight, 0f));
            _vehicleBody.linearVelocity = new Vector3(10f, 0f, 0f);

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Plane' bail out rider=Player speed=3.0 parachute=-");
            Assert.That(_rider.Exit(), Is.True);
            Assert.That(_player.Motor.Velocity.X, Is.EqualTo(3f).Within(1e-4f));
        }

        [Test]
        public void TheVehicleDefinitionCarriesTheBailOutIntoTheSpec()
        {
            var definition = ScriptableObject.CreateInstance<VehicleDefinition>();
            _created.Add(definition);
            definition.Id = "plane";
            definition.BailOut.CopyFrom(new BailOutSpec(25f, 0.5f, 2f, 3f));

            var spec = definition.ToSpec(new[] { new VehicleSeatSpec(VehicleSeatRole.Driver) });
            Assert.That(spec.BailOut.MinHeight, Is.EqualTo(25f));
            Assert.That(spec.BailOut.InheritedSpeedFraction, Is.EqualTo(0.5f));
            Assert.That(spec.BailOut.ExitHeight, Is.EqualTo(2f));
            Assert.That(spec.BailOut.ExitSideDistance, Is.EqualTo(3f));

            var copy = ScriptableObject.CreateInstance<VehicleDefinition>();
            _created.Add(copy);
            copy.CopyFrom(spec);
            Assert.That(copy.BailOut.ToSpec().MinHeight, Is.EqualTo(25f));
        }

        private void Configure(BailOutSpec bailOut)
        {
            _vehicle.Configure(new VehicleSpec("plane", 500, new[] { new VehicleSeatSpec(VehicleSeatRole.Driver) }, bailOut: bailOut));
        }

        private void Board(Vector3 vehiclePosition)
        {
            _root.transform.position = vehiclePosition;
            Physics.SyncTransforms();
            Assert.That(_vehicle.TryEnter(_rider, 0, requireInRange: false), Is.True);
            _player.Tick(Vector2.zero, false, false, false, StepSeconds);
            Physics.SyncTransforms();
        }

        private static void AssertNear(Vector3 actual, Vector3 expected, string message)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(1e-3f), $"{message}: expected {expected} but was {actual}");
        }

        private GameObject Track(GameObject gameObject)
        {
            _created.Add(gameObject);
            return gameObject;
        }
    }
}
