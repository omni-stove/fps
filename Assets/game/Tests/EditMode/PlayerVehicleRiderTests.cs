using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// プレイヤーが乗り物に乗っている間、操作を手放して体を座席に留め、降りたら戻ること (<see cref="PlayerVehicleRider"/>)。
    /// Edit Mode では Update が走らず入力も読まないので、乗り降りは <see cref="PlayerVehicleRider.Interact"/> などを直接呼び、
    /// 体の動きは <see cref="PlayerController.Tick"/> を呼んで確かめる。
    /// </summary>
    public sealed class PlayerVehicleRiderTests
    {
        private const float StepSeconds = 0.02f;

        private static readonly Vector3 _driverSeat = new Vector3(-0.5f, 0.4f, 0f);
        private static readonly Vector3 _passengerSeat = new Vector3(0.5f, 0.4f, 0f);
        private static readonly Vector3 _leftExit = new Vector3(-2f, 0f, 0f);
        private static readonly Vector3 _rightExit = new Vector3(2f, 0f, 0f);

        private readonly List<Object> _created = new List<Object>();
        private GameObject _body;
        private PlayerController _player;
        private PlayerVehicleRider _rider;
        private InputAction _move;
        private InputAction _sprint;
        private InputAction _crouch;
        private InputAction _jump;
        private Vehicle _vehicle;
        private VehicleSeat _driver;
        private VehicleSeat _passenger;

        [SetUp]
        public void SetUp()
        {
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

            _rider = _body.AddComponent<PlayerVehicleRider>();
            _rider.Bind(_player, null);

            var root = Track(new GameObject("Car"));
            var shell = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shell.name = "Body";
            shell.transform.SetParent(root.transform, false);
            shell.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            shell.transform.localScale = new Vector3(2f, 1.2f, 4f);

            _driver = CreateSeat(root.transform, "Driver", _driverSeat);
            _driver.SetEnterPoints(CreatePoint(root.transform, "EnterDriver", new Vector3(-1.3f, 0f, 0f)));
            _driver.SetExitPoints(CreatePoint(root.transform, "ExitLeft", _leftExit), CreatePoint(root.transform, "ExitRight", _rightExit));
            _passenger = CreateSeat(root.transform, "Passenger", _passengerSeat);
            _passenger.SetExitPoints(CreatePoint(root.transform, "ExitRight2", _rightExit));

            _vehicle = root.AddComponent<Vehicle>();
            _vehicle.Configure(new VehicleSpec(
                "car",
                500,
                new[]
                {
                    new VehicleSeatSpec(VehicleSeatRole.Driver),
                    new VehicleSeatSpec(VehicleSeatRole.Passenger, VehicleSeatPose.Hidden, VehicleCameraView.Seat),
                }));
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
        public void InteractBoardsTheNearestVehicleAndInteractingAgainLeaves()
        {
            LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' board rider=Player seat=0 role=Driver");
            Assert.That(_rider.Interact(), Is.True);
            Assert.That(_rider.IsRiding, Is.True);
            Assert.That(_rider.Vehicle, Is.SameAs(_vehicle));
            Assert.That(_rider.Seat, Is.SameAs(_driver));

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' leave rider=Player seat=0 reason=Request exit=0");
            Assert.That(_rider.Interact(), Is.True);
            Assert.That(_rider.IsRiding, Is.False);
        }

        [Test]
        public void InteractFarFromAnyVehicleDoesNothing()
        {
            _body.transform.position = new Vector3(30f, 1f, 0f);

            Assert.That(_rider.Interact(), Is.False);
            Assert.That(_rider.IsRiding, Is.False);
        }

        [Test]
        public void RidingHandsOffControlAndPinsTheBodyToTheSeat()
        {
            _vehicle.TryEnter(_rider);

            Assert.That(_player.IsControlHandedOff, Is.True);
            Assert.That(_player.Handoff.IsHeldBy(_rider), Is.True);

            for (var i = 0; i < 10; i++)
            {
                _player.Tick(Vector2.up, true, false, true, StepSeconds);
            }

            Assert.That(_player.ActiveMovementOverride, Is.SameAs(_rider));
            Assert.That(_player.MovementOverrideConditions, Is.EqualTo(PlayerActionConditions.InVehicle));
            Assert.That(_player.IsCrouched, Is.True, "a seated rider takes the crouched height");
            Assert.That(_rider.FeetBelowRoot, Is.EqualTo(1f).Within(1e-4f), "crouching keeps the feet where they were");
            AssertNear(_body.transform.position, _driverSeat + Vector3.up, "the root stays above the seat despite the input");
            Assert.That(Mathf.DeltaAngle(_body.transform.eulerAngles.y, 0f), Is.EqualTo(0f).Within(0.01f), "faces the seat's forward");
        }

        [Test]
        public void LeavingGivesControlBackAndStandsAtTheExit()
        {
            _vehicle.TryEnter(_rider);
            _player.Tick(Vector2.zero, false, false, false, StepSeconds);

            Assert.That(_rider.Exit(), Is.True);

            Assert.That(_player.IsControlHandedOff, Is.False);
            Assert.That(_player.IsMovementOverridden, Is.False);
            Assert.That(_player.MovementOverrides, Does.Not.Contain(_rider));
            AssertNear(_body.transform.position, _leftExit + Vector3.up, "the root is placed above the exit");
            Assert.That(_player.Motor.Velocity.Length(), Is.Zero, "no momentum is carried out of the seat");
        }

        [Test]
        public void ABlockedExitPutsThePlayerOnTheOtherSide()
        {
            var blocker = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            blocker.transform.position = _leftExit + new Vector3(0f, 1f, 0f);
            blocker.transform.localScale = new Vector3(1f, 2f, 1f);
            Physics.SyncTransforms();
            _vehicle.TryEnter(_rider);

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' leave rider=Player seat=0 reason=Request exit=1");
            Assert.That(_rider.Exit(), Is.True);
            AssertNear(_body.transform.position, _rightExit + Vector3.up, "the next candidate");
        }

        [Test]
        public void SwitchingSeatsMovesTheBodyAndAppliesTheNewPose()
        {
            var mesh = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Object.DestroyImmediate(mesh.GetComponent<Collider>());
            mesh.transform.SetParent(_body.transform, false);
            var meshRenderer = mesh.GetComponent<Renderer>();
            _vehicle.TryEnter(_rider);

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' switch rider=Player from=0 to=1 role=Passenger");
            Assert.That(_rider.SwitchSeat(1), Is.True);

            Assert.That(_rider.Seat, Is.SameAs(_passenger));
            AssertNear(_body.transform.position, _passengerSeat + Vector3.up, "moved to the new seat at once");
            Assert.That(meshRenderer.enabled, Is.False, "the passenger seat hides the rider");

            Assert.That(_rider.Exit(), Is.True);
            Assert.That(meshRenderer.enabled, Is.True, "shown again after leaving");
        }

        [Test]
        public void ComponentsThatDoNotWorkInASeatAreSuspendedWhileRiding()
        {
            var extra = _body.AddComponent<VehicleTestRider>();
            var disabled = _body.AddComponent<AudioSource>();
            disabled.enabled = false;
            Assert.That(_rider.SuspendWhileRiding(extra), Is.True);
            Assert.That(_rider.SuspendWhileRiding(extra), Is.False);
            Assert.That(_rider.SuspendWhileRiding(disabled), Is.True);
            Assert.That(_rider.SuspendWhileRiding(null), Is.False);

            _vehicle.TryEnter(_rider);
            Assert.That(extra.enabled, Is.False);
            Assert.That(_rider.SuspendedComponents, Is.EqualTo(new Behaviour[] { extra }), "only what was running");

            _rider.Exit();
            Assert.That(extra.enabled, Is.True);
            Assert.That(disabled.enabled, Is.False, "what was off stays off");
        }

        [Test]
        public void DyingInTheSeatLeavesTheVehicle()
        {
            var damageable = _body.AddComponent<Damageable>();
            damageable.Initialize("player", 10);
            _rider.Bind(_player, null);
            _vehicle.TryEnter(_rider);

            LogAssert.Expect(LogType.Log, "[Combat] target died id=player");
            LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' leave rider=Player seat=0 reason=RiderDied exit=0");
            damageable.TakeDamage(10);

            Assert.That(_rider.IsRiding, Is.False);
            Assert.That(_player.IsControlHandedOff, Is.False);

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' board refused rider=Player reason=rider_unavailable");
            Assert.That(_vehicle.TryEnter(_rider), Is.False, "the dead do not board");
        }

        [Test]
        public void TheChaseCameraFollowsBehindTheVehicleAndIsRestoredAfterwards()
        {
            var eye = new GameObject("Eye").transform;
            eye.SetParent(_body.transform, false);
            eye.localPosition = new Vector3(0f, 0.6f, 0f);
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.transform.SetParent(eye, false);
            var rig = _body.AddComponent<ViewRig>();
            rig.Initialize(eye, camera);
            _rider.Bind(_player, null);
            var savedLocal = camera.transform.localPosition;

            _vehicle.TryEnter(_rider);

            Assert.That(rig.enabled, Is.False, "the rider places the camera itself");
            Assert.That(_rider.CameraView, Is.EqualTo(VehicleCameraView.Chase));
            var expected = _vehicle.ChaseFocus + (Vector3.back * _vehicle.Spec.ChaseDistance);
            AssertNear(camera.transform.position, expected, "behind the vehicle, looking where the rider looks");

            _rider.SetCameraView(VehicleCameraView.Seat);
            Assert.That(rig.Mode, Is.EqualTo(ViewMode.FirstPerson));
            AssertNear(camera.transform.position, eye.position, "the seat view without a view point is the eye");

            _rider.Exit();
            Assert.That(rig.enabled, Is.True);
            Assert.That(camera.transform.localPosition, Is.EqualTo(savedLocal));
        }

        [Test]
        public void ThePassengerSeatUsesItsOwnViewPoint()
        {
            var eye = new GameObject("Eye").transform;
            eye.SetParent(_body.transform, false);
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.transform.SetParent(eye, false);
            var rig = _body.AddComponent<ViewRig>();
            rig.Initialize(eye, camera);
            _rider.Bind(_player, null);
            var viewPoint = CreatePoint(_passenger.transform, "View", new Vector3(0f, 0.9f, 0f));
            _passenger.SetViewPoint(viewPoint);

            Assert.That(_vehicle.TryEnter(_rider, 1, requireInRange: false), Is.True);

            Assert.That(_rider.CameraView, Is.EqualTo(VehicleCameraView.Seat));
            AssertNear(camera.transform.position, viewPoint.position, "the seat's view point");
        }

        [Test]
        public void ReadSeatInputIsIdleWithoutPressedButtons()
        {
            _vehicle.TryEnter(_rider);

            var input = _rider.ReadSeatInput();

            Assert.That(input.Move, Is.EqualTo(Vector2.zero));
            Assert.That(input.Vertical, Is.Zero);
            Assert.That(input.FireHeld || input.AimHeld || input.Boost || input.ReloadPressed, Is.False);
            Assert.That(input.HasAim, Is.True, "aims where the rider looks");
        }

        [Test]
        public void TheRiderCannotBeReboundWhileRiding()
        {
            _vehicle.TryEnter(_rider);

            Assert.Throws<System.InvalidOperationException>(() => _rider.Bind(_player, null));
            Assert.Throws<System.ArgumentNullException>(() => _rider.Bind(null, null));
        }

        private static void AssertNear(Vector3 actual, Vector3 expected, string message)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(1e-3f), $"{message}: expected {expected} but was {actual}");
        }

        private VehicleSeat CreateSeat(Transform parent, string name, Vector3 localPosition)
        {
            var seat = new GameObject(name).AddComponent<VehicleSeat>();
            seat.transform.SetParent(parent, false);
            seat.transform.localPosition = localPosition;
            return seat;
        }

        private static Transform CreatePoint(Transform parent, string name, Vector3 localPosition)
        {
            var point = new GameObject(name).transform;
            point.SetParent(parent, false);
            point.localPosition = localPosition;
            return point;
        }

        private GameObject Track(GameObject gameObject)
        {
            _created.Add(gameObject);
            return gameObject;
        }
    }
}
