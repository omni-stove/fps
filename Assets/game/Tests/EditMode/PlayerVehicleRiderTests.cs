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
    // Edit Mode では Update が走らず入力も読まないので、乗り降りは PlayerVehicleRider.Interact などを直接呼び、
    // 体の動きは PlayerController.Tick を呼んで確かめる。
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

        private void UseTimedSpec(float enterSeconds, float exitSeconds)
        {
            _vehicle.Configure(new VehicleSpec(
                "car",
                500,
                new[]
                {
                    new VehicleSeatSpec(VehicleSeatRole.Driver),
                    new VehicleSeatSpec(VehicleSeatRole.Passenger, VehicleSeatPose.Hidden, VehicleCameraView.Seat),
                },
                enterSeconds: enterSeconds,
                exitSeconds: exitSeconds));
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
        public void NearAFreeSeatTheRiderShowsWhichSeatInteractBoardsAndHidesItWhenAboardOrAway()
        {
            GameSignals.ResetAll();
            KeySharing.Reset();
            var interact = new InputAction("Interact", InputActionType.Button);
            try
            {
                _rider.SetActions(interact, null, null, null, null, null);

                _rider.RefreshBoardHint();
                Assert.That(
                    GameSignals.InteractHint.Value,
                    Is.EqualTo(new InteractHint(_rider, new VehicleBoardHint(VehicleSeatRole.Driver, "Player/Interact"))));
                Assert.That(KeySharing.IsClaimed("Interact"), Is.True, "melee and reload yield the shared key while a seat is offered");

                LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' board rider=Player seat=0 role=Driver");
                Assert.That(_rider.Interact(), Is.True);
                Assert.That(GameSignals.InteractHint.Value, Is.Null, "aboard there is no seat to offer");

                LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' leave rider=Player seat=0 reason=Request exit=0");
                Assert.That(_rider.Exit(), Is.True);
                _body.transform.position = new Vector3(30f, 1f, 0f);
                _rider.RefreshBoardHint();
                Assert.That(GameSignals.InteractHint.Value, Is.Null);
                Assert.That(KeySharing.IsClaimed("Interact"), Is.False);
            }
            finally
            {
                interact.Dispose();
                GameSignals.ResetAll();
                KeySharing.Reset();
            }
        }

        [Test]
        public void WithoutAnInteractActionTheRiderOffersNoSeat()
        {
            GameSignals.ResetAll();
            try
            {
                _rider.RefreshBoardHint();
                Assert.That(GameSignals.InteractHint.Value, Is.Null);
            }
            finally
            {
                GameSignals.ResetAll();
            }
        }

        [Test]
        public void TheSeatsFiringIsPublishedWhileRiding()
        {
            GameSignals.ResetAll();
            try
            {
                _vehicle.TryEnter(_rider);
                Assert.That(GameSignals.SeatFiring.Value, Is.EqualTo(VehicleSeatFiring.None), "the driver cannot fire");

                Assert.That(_rider.Exit(), Is.True);
                Assert.That(GameSignals.SeatFiring.Value, Is.Null, "on foot there is no seat");
            }
            finally
            {
                GameSignals.ResetAll();
            }
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
        public void GettingInTakesTheVehiclesSecondsAndCarriesTheBodyToTheSeat()
        {
            UseTimedSpec(1f, 0.5f);
            var start = _body.transform.position;

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' board rider=Player seat=0 role=Driver");
            LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' enter begin rider=Player seat=0 seconds=1.00");
            Assert.That(_rider.Interact(), Is.True);

            Assert.That(_rider.IsRiding, Is.True, "the seat is taken as soon as the rider starts getting in");
            Assert.That(_vehicle.FindBoardableSeat(new Vector3(-1.3f, 1f, 0f), out _), Is.Not.EqualTo(0), "nobody else can take the seat meanwhile");
            Assert.That(_rider.Transition, Is.EqualTo(VehicleSeatTransitionKind.Entering));
            Assert.That(_rider.IsSettled, Is.False);
            Assert.That(_rider.SwitchSeat(1), Is.False, "the seat cannot be changed while getting in");
            Assert.That(_rider.Interact(), Is.False, "Interact does not leave while getting in");

            _rider.StepTransition(0.5f);
            _player.Tick(Vector2.zero, false, false, false, StepSeconds);
            var seated = _driverSeat + Vector3.up;
            var halfway = _body.transform.position;
            Assert.That(Vector3.Distance(halfway, start), Is.GreaterThan(0.1f), "the body has moved off the enter point");
            Assert.That(Vector3.Distance(halfway, seated), Is.GreaterThan(0.1f), "the body is not in the seat yet");

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' enter end rider=Player seat=0");
            _rider.StepTransition(0.5f);
            _player.Tick(Vector2.zero, false, false, false, StepSeconds);
            Assert.That(_rider.IsSettled, Is.True);
            Assert.That(_rider.Transition, Is.EqualTo(VehicleSeatTransitionKind.None));
            AssertNear(_body.transform.position, seated, "the body ends in the seat");
        }

        [Test]
        public void TheVehicleGetsNoDrivingInputUntilTheRiderIsSeated()
        {
            UseTimedSpec(1f, 0f);
            var sink = new RecordingSink();
            _vehicle.AddInputSink(sink);
            _rider.SeatInputFilter = new ForwardFireFilter();
            _vehicle.TryEnter(_rider);

            Assert.That(_rider.SubmitSeatInput(StepSeconds), Is.True);
            Assert.That(sink.Inputs[0].Move, Is.EqualTo(Vector2.zero), "getting in sends an idle input");
            Assert.That(sink.Inputs[0].FireHeld, Is.False);

            _rider.StepTransition(1f);
            Assert.That(_rider.SubmitSeatInput(StepSeconds), Is.True);
            Assert.That(sink.Inputs[1].Move, Is.EqualTo(Vector2.up), "once seated the input goes through");
            Assert.That(sink.Inputs[1].FireHeld, Is.True);
        }

        [Test]
        public void GettingOutWaitsTheVehiclesSecondsBeforeStandingAtTheExit()
        {
            UseTimedSpec(0f, 0.5f);
            _vehicle.TryEnter(_rider);
            _player.Tick(Vector2.zero, false, false, false, StepSeconds);

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' exit begin rider=Player seat=0 seconds=0.50");
            Assert.That(_rider.Interact(), Is.True);
            Assert.That(_rider.IsRiding, Is.True, "the rider stays in the seat while getting out");
            Assert.That(_rider.Transition, Is.EqualTo(VehicleSeatTransitionKind.Exiting));
            Assert.That(_rider.Interact(), Is.False, "pressing again does not restart getting out");

            _rider.StepTransition(0.25f);
            Assert.That(_rider.IsRiding, Is.True);
            Assert.That(_rider.TransitionProgress, Is.EqualTo(0.5f).Within(1e-4f));

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' exit end rider=Player seat=0");
            LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' leave rider=Player seat=0 reason=Request exit=0");
            _rider.StepTransition(0.25f);
            Assert.That(_rider.IsRiding, Is.False);
            AssertNear(_body.transform.position, _leftExit + Vector3.up, "the root is placed above the exit");
        }

        [Test]
        public void ForcedOutWhileGettingOutLeavesAtOnce()
        {
            UseTimedSpec(0f, 2f);
            _vehicle.TryEnter(_rider);
            _rider.Exit();
            Assert.That(_rider.Transition, Is.EqualTo(VehicleSeatTransitionKind.Exiting));

            Assert.That(_vehicle.ForceExit(_rider, VehicleLeaveReason.Forced), Is.True);

            Assert.That(_rider.IsRiding, Is.False);
            Assert.That(_rider.Transition, Is.EqualTo(VehicleSeatTransitionKind.None));
            _rider.StepTransition(5f);
            Assert.That(_rider.IsRiding, Is.False, "the cleared exit does not fire later");
        }

        [Test]
        public void LeavingGivesControlBackAndStandsAtTheExit()
        {
            _vehicle.TryEnter(_rider);
            _player.Tick(Vector2.zero, false, false, false, StepSeconds);

            Assert.That(_rider.Exit(), Is.True);

            Assert.That(_player.IsControlHandedOff, Is.False);
            Assert.That(_player.IsMovementOverridden, Is.False);
            Assert.That(_player.MovementOverrides, Has.No.Member(_rider));
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
        public void DyingInTheSeatKeepsTheBodySeatedAndStopsDriving()
        {
            var damageable = _body.AddComponent<Damageable>();
            damageable.Initialize("player", 10);
            _rider.Bind(_player, null);
            var sink = new RecordingSink();
            _vehicle.AddInputSink(sink);
            _rider.SeatInputFilter = new ForwardFireFilter();
            _vehicle.TryEnter(_rider);

            LogAssert.Expect(LogType.Log, "[Combat] target died id=player");
            LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' died in seat rider=Player seat=0");
            damageable.TakeDamage(10);

            Assert.That(_rider.IsRiding, Is.True, "the dead stay in the seat");
            Assert.That(_rider.IsDead, Is.True);
            Assert.That(_rider.IsSettled, Is.False);
            _player.Tick(Vector2.up, true, false, true, StepSeconds);
            AssertNear(_body.transform.position, _driverSeat + Vector3.up, "the body is still pinned to the seat");

            Assert.That(_rider.SubmitSeatInput(StepSeconds), Is.True);
            Assert.That(sink.Inputs[0].Move, Is.EqualTo(Vector2.zero), "a dead driver does not drive");
            Assert.That(sink.Inputs[0].FireHeld, Is.False);
            Assert.That(_rider.Interact(), Is.False, "the dead cannot get out");
            Assert.That(_rider.Exit(), Is.False);
            Assert.That(_rider.SwitchSeat(1), Is.False);
            Assert.That(_vehicle.FindBoardableSeat(new Vector3(-1.3f, 1f, 0f), out _), Is.Not.EqualTo(0), "the seat stays taken");
        }

        [Test]
        public void RespawningElsewhereFreesTheSeatAndKeepsTheRespawnPosition()
        {
            var damageable = _body.AddComponent<Damageable>();
            damageable.Initialize("player", 10);
            _rider.Bind(_player, null);
            _vehicle.TryEnter(_rider);
            damageable.TakeDamage(10);

            // モードの Runner (SideMember.Respawn) のように、体を移してから体力を戻す。
            var spawn = new Vector3(20f, 1f, 5f);
            _body.transform.position = spawn;
            LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' leave rider=Player seat=0 reason=Forced exit=0");
            damageable.ResetHealth();

            Assert.That(_rider.IsRiding, Is.False);
            Assert.That(_player.IsMovementOverridden, Is.False);
            AssertNear(_body.transform.position, spawn, "the respawn position is kept");
        }

        [Test]
        public void RefillingTheHealthOfALivingRiderKeepsThemSeated()
        {
            var damageable = _body.AddComponent<Damageable>();
            damageable.Initialize("player", 10);
            _rider.Bind(_player, null);
            _vehicle.TryEnter(_rider);

            damageable.ResetHealth();

            Assert.That(_rider.IsRiding, Is.True, "a round reset does not throw the living out");
        }

        [Test]
        public void RevivingInTheSeatGetsOutAtTheExit()
        {
            var damageable = _body.AddComponent<Damageable>();
            damageable.Initialize("player", 10);
            _rider.Bind(_player, null);
            _vehicle.TryEnter(_rider);
            damageable.TakeDamage(10);
            _player.Tick(Vector2.zero, false, false, false, StepSeconds);

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' leave rider=Player seat=0 reason=Forced exit=0");
            damageable.ResetHealth();

            Assert.That(_rider.IsRiding, Is.False);
            AssertNear(_body.transform.position, _leftExit + Vector3.up, "brought back where the body lay, it steps out of the vehicle");

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' board rider=Player seat=0 role=Driver");
            _body.transform.position = new Vector3(-1.3f, 1f, 0f);
            Assert.That(_vehicle.TryEnter(_rider), Is.True, "alive again, the seat can be taken");
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
        public void TheVehicleCameraWidensWithSpeedAndGivesTheFieldOfViewBack()
        {
            var camera = NewRigCamera(out var rig);
            camera.fieldOfView = 60f;
            var vehicleCamera = new VehicleCamera(rig);
            var spec = new VehicleSpeedFieldOfViewSpec(10f, 30f, 15f, 0f);

            vehicleCamera.Begin(VehicleCameraView.Chase);
            vehicleCamera.StepFieldOfView(spec, 5f, StepSeconds);
            Assert.That(camera.fieldOfView, Is.EqualTo(60f), "slower than the start speed");

            vehicleCamera.StepFieldOfView(spec, 20f, StepSeconds);
            Assert.That(camera.fieldOfView, Is.EqualTo(67.5f).Within(1e-3f));

            vehicleCamera.StepFieldOfView(spec, 50f, StepSeconds);
            Assert.That(camera.fieldOfView, Is.EqualTo(75f).Within(1e-3f));

            vehicleCamera.StepFieldOfView(spec, 0f, StepSeconds);
            Assert.That(camera.fieldOfView, Is.EqualTo(60f), "stopping gives back the field of view");

            vehicleCamera.StepFieldOfView(spec, 50f, StepSeconds);
            vehicleCamera.Restore();
            Assert.That(camera.fieldOfView, Is.EqualTo(60f), "leaving gives back the field of view");
        }

        [Test]
        public void TheVehicleCameraEasesTheFieldOfViewInsteadOfJumping()
        {
            var camera = NewRigCamera(out var rig);
            camera.fieldOfView = 60f;
            var vehicleCamera = new VehicleCamera(rig);
            var spec = new VehicleSpeedFieldOfViewSpec(10f, 30f, 15f, 0.5f);

            vehicleCamera.Begin(VehicleCameraView.Chase);
            vehicleCamera.StepFieldOfView(spec, 30f, 0.5f);

            var expected = 60f + (15f * (1f - Mathf.Exp(-1f)));
            Assert.That(camera.fieldOfView, Is.EqualTo(expected).Within(1e-3f));
            Assert.That(vehicleCamera.ExtraFieldOfView, Is.EqualTo(expected - 60f).Within(1e-3f));

            for (var i = 0; i < 200; i++)
            {
                vehicleCamera.StepFieldOfView(spec, 0f, 0.1f);
            }

            Assert.That(camera.fieldOfView, Is.EqualTo(60f), "settles back on the original");
            Assert.That(vehicleCamera.ExtraFieldOfView, Is.EqualTo(0f));
        }

        [Test]
        public void TheVehicleCameraDoesNotTouchTheFieldOfViewWhenNothingIsAdded()
        {
            var camera = NewRigCamera(out var rig);
            camera.fieldOfView = 70f;
            var vehicleCamera = new VehicleCamera(rig);

            vehicleCamera.Begin(VehicleCameraView.Chase);
            vehicleCamera.StepFieldOfView(VehicleSpeedFieldOfViewSpec.Default, 100f, StepSeconds);
            Assert.That(camera.fieldOfView, Is.EqualTo(70f));

            camera.fieldOfView = 80f;
            vehicleCamera.Restore();
            Assert.That(camera.fieldOfView, Is.EqualTo(80f), "another component's field of view is left alone");
        }

        [Test]
        public void TheVehicleCameraTakesAnotherComponentsFieldOfViewAsTheNewBase()
        {
            var camera = NewRigCamera(out var rig);
            camera.fieldOfView = 60f;
            var vehicleCamera = new VehicleCamera(rig);
            var spec = new VehicleSpeedFieldOfViewSpec(10f, 30f, 10f, 0f);

            vehicleCamera.Begin(VehicleCameraView.Chase);
            vehicleCamera.StepFieldOfView(spec, 30f, StepSeconds);
            Assert.That(camera.fieldOfView, Is.EqualTo(70f).Within(1e-3f));

            camera.fieldOfView = 90f;
            vehicleCamera.StepFieldOfView(spec, 30f, StepSeconds);
            Assert.That(camera.fieldOfView, Is.EqualTo(100f).Within(1e-3f));

            vehicleCamera.Restore();
            Assert.That(camera.fieldOfView, Is.EqualTo(90f));
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
            Assert.That(input.FireHeld || input.AimHeld || input.Boost || input.ReloadPressed || input.CountermeasurePressed, Is.False);
            Assert.That(input.HasAim, Is.True, "aims where the rider looks");
        }

        [Test]
        public void TheCountermeasureActionIsReadOnlyWhileRiding()
        {
            var countermeasure = new InputAction("Countermeasure", InputActionType.Button);
            try
            {
                _rider.SetActions(null, null, null, null, null, null, countermeasure);
                Assert.That(countermeasure.enabled, Is.False, "on foot the rider leaves it alone");

                _vehicle.TryEnter(_rider);
                Assert.That(countermeasure.enabled, Is.True, "boarding starts reading it");
                Assert.That(_rider.ReadSeatInput().CountermeasurePressed, Is.False, "nothing is pressed");

                Assert.That(_rider.Exit(), Is.True);
                Assert.That(countermeasure.enabled, Is.False, "leaving gives it back disabled");
            }
            finally
            {
                countermeasure.Dispose();
            }
        }

        [Test]
        public void TheSeatInputFilterRewritesWhatTheSeatReceives()
        {
            var sink = new RecordingSink();
            _vehicle.AddInputSink(sink);
            var filter = new ForwardFireFilter();
            _rider.SeatInputFilter = filter;

            Assert.That(_rider.SubmitSeatInput(StepSeconds), Is.False, "nothing is sent before boarding");
            Assert.That(sink.Inputs, Is.Empty);
            Assert.That(filter.Seats, Is.Empty, "the filter is not asked while the rider is on foot");

            _vehicle.TryEnter(_rider);
            Assert.That(_rider.SubmitSeatInput(StepSeconds), Is.True);

            Assert.That(filter.Seats, Is.EqualTo(new[] { _driver }));
            Assert.That(sink.Seats, Is.EqualTo(new[] { _driver }));
            Assert.That(sink.Inputs.Count, Is.EqualTo(1));
            Assert.That(sink.Inputs[0].Move, Is.EqualTo(new Vector2(0f, 1f)));
            Assert.That(sink.Inputs[0].Boost, Is.True);
            Assert.That(sink.Inputs[0].FireHeld, Is.True);
            Assert.That(sink.Inputs[0].AimDirection, Is.EqualTo(Vector3.right));
            Assert.That(sink.DeltaSeconds, Is.EqualTo(new[] { StepSeconds }));

            _rider.SeatInputFilter = null;
            Assert.That(_rider.SubmitSeatInput(StepSeconds), Is.True);
            Assert.That(sink.Inputs[1].Move, Is.EqualTo(Vector2.zero), "without the filter the idle buttons pass through");
            Assert.That(sink.Inputs[1].FireHeld, Is.False);
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

        private Camera NewRigCamera(out ViewRig rig)
        {
            var eye = new GameObject("Eye").transform;
            eye.SetParent(_body.transform, false);
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.transform.SetParent(eye, false);
            rig = _body.AddComponent<ViewRig>();
            rig.Initialize(eye, camera);
            return camera;
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

        private sealed class ForwardFireFilter : IVehicleSeatInputFilter
        {
            public List<VehicleSeat> Seats { get; } = new List<VehicleSeat>();

            public void Filter(VehicleSeat seat, ref VehicleSeatInput input)
            {
                Seats.Add(seat);
                input = new VehicleSeatInput(new Vector2(0f, 1f), 0f, true, true, false, false, input.AimOrigin, Vector3.right * 3f);
            }
        }

        private sealed class RecordingSink : IVehicleInputSink
        {
            public List<VehicleSeat> Seats { get; } = new List<VehicleSeat>();

            public List<VehicleSeatInput> Inputs { get; } = new List<VehicleSeatInput>();

            public List<float> DeltaSeconds { get; } = new List<float>();

            public void OnSeatInput(VehicleSeat seat, VehicleSeatInput input, float deltaSeconds)
            {
                Seats.Add(seat);
                Inputs.Add(input);
                DeltaSeconds.Add(deltaSeconds);
            }

            public void OnSeatVacated(VehicleSeat seat)
            {
            }
        }
    }
}
