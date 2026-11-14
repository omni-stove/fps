using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// 乗り物の土台 (<see cref="Vehicle"/>・<see cref="VehicleSeat"/>) の乗る・移る・降りる・壊れる と、座席の入力の口。
    /// 乗る側は入力を持たない仮のもの (<see cref="FakeRider"/>) で確かめる。降りる場所の塞がりは Collider に問い合わせるので、置いたあとに SyncTransforms する。
    /// </summary>
    public sealed class VehicleTests
    {
        private static readonly Vector3 _driverSeat = new Vector3(-0.5f, 0.4f, 0f);
        private static readonly Vector3 _passengerSeat = new Vector3(0.5f, 0.4f, 0f);
        private static readonly Vector3 _leftExit = new Vector3(-2f, 0f, 0f);
        private static readonly Vector3 _rightExit = new Vector3(2f, 0f, 0f);
        private static readonly Vector3 _rearExit = new Vector3(0f, 0f, -3f);

        private readonly List<Object> _created = new List<Object>();
        private Vehicle _vehicle;
        private VehicleSeat _driver;
        private VehicleSeat _passenger;

        [SetUp]
        public void SetUp()
        {
            var root = Track(new GameObject("Car"));
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            body.transform.localScale = new Vector3(2f, 1.2f, 4f);

            _driver = CreateSeat(root.transform, "Driver", _driverSeat);
            _driver.SetEnterPoints(CreatePoint(root.transform, "EnterDriver", new Vector3(-1.3f, 0f, 0f)));
            _driver.SetExitPoints(CreatePoint(root.transform, "ExitLeft", _leftExit), CreatePoint(root.transform, "ExitRight", _rightExit));

            _passenger = CreateSeat(root.transform, "Passenger", _passengerSeat);
            _passenger.SetEnterPoints(CreatePoint(root.transform, "EnterPassenger", new Vector3(1.3f, 0f, 0f)));

            _vehicle = root.AddComponent<Vehicle>();
            _vehicle.SetExitPoints(CreatePoint(root.transform, "ExitRear", _rearExit));
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
        public void SeatsAreNumberedInChildOrderAndTakeTheConfiguredSpec()
        {
            Assert.That(_vehicle.Seats, Is.EqualTo(new[] { _driver, _passenger }));
            Assert.That(_driver.Index, Is.EqualTo(0));
            Assert.That(_passenger.Index, Is.EqualTo(1));
            Assert.That(_driver.Vehicle, Is.SameAs(_vehicle));
            Assert.That(_driver.Role, Is.EqualTo(VehicleSeatRole.Driver));
            Assert.That(_passenger.Pose, Is.EqualTo(VehicleSeatPose.Hidden));
            Assert.That(_passenger.CameraView, Is.EqualTo(VehicleCameraView.Seat));
            Assert.That(_vehicle.VehicleId, Is.EqualTo("car"));
            Assert.That(_vehicle.Spec.MaxHealth, Is.EqualTo(500));
            Assert.That(Vehicle.Active, Does.Contain(_vehicle));
        }

        [Test]
        public void TheSpecIsBuiltFromTheSeatsWhenNotConfigured()
        {
            var root = Track(new GameObject("Turret"));
            CreateSeat(root.transform, "Gun", Vector3.zero);
            var turret = root.AddComponent<Vehicle>();

            Assert.That(turret.Spec.Seats, Has.Count.EqualTo(1));
            Assert.That(turret.Spec.Seats[0].Role, Is.EqualTo(VehicleSeatRole.Passenger));
            Assert.That(turret.Spec.MaxHealth, Is.EqualTo(VehicleSpec.DefaultMaxHealth));
            Assert.That(turret.Spec.EnterRange, Is.EqualTo(VehicleSpec.DefaultEnterRange));
        }

        [Test]
        public void ConfigureRejectsNullAndAMismatchedSeatCount()
        {
            Assert.Throws<ArgumentNullException>(() => _vehicle.Configure(null));
            Assert.Throws<ArgumentException>(
                () => _vehicle.Configure(new VehicleSpec("car", 100, new[] { new VehicleSeatSpec(VehicleSeatRole.Driver) })));
        }

        [Test]
        public void EnteringTakesTheNearestFreeSeat()
        {
            var rider = NewRider("Rider", new Vector3(1.5f, 1f, 0f));

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' board rider=Rider seat=1 role=Passenger");
            Assert.That(_vehicle.TryEnter(rider), Is.True);

            Assert.That(rider.Seat, Is.SameAs(_passenger));
            Assert.That(_passenger.Occupant, Is.SameAs(rider));
            Assert.That(_vehicle.SeatOf(rider), Is.SameAs(_passenger));
            Assert.That(_vehicle.RiderCount, Is.EqualTo(1));
            Assert.That(rider.Calls, Is.EqualTo(new[] { "board 1" }));
        }

        [Test]
        public void TheNextRiderTakesTheRemainingSeat()
        {
            var first = NewRider("First", new Vector3(1.5f, 1f, 0f));
            var second = NewRider("Second", Vector3.zero);
            _vehicle.TryEnter(first);

            Assert.That(_vehicle.TryEnter(second), Is.True);
            Assert.That(second.Seat, Is.SameAs(_driver), "both seats are equally close and the passenger seat is taken");
        }

        [Test]
        public void EnteringIsRefusedFarAwayWhenFullWhenWreckedAndWhenAlreadyRiding()
        {
            var far = NewRider("Far", new Vector3(10f, 1f, 0f));
            LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' board refused rider=Far reason=out_of_range");
            Assert.That(_vehicle.TryEnter(far), Is.False);

            var a = NewRider("A", Vector3.zero);
            var b = NewRider("B", Vector3.zero);
            var c = NewRider("C", Vector3.zero);
            _vehicle.TryEnter(a);
            _vehicle.TryEnter(b);

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' board refused rider=C reason=no_free_seat");
            Assert.That(_vehicle.TryEnter(c), Is.False);

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' board refused rider=A reason=already_riding");
            Assert.That(_vehicle.TryEnter(a), Is.False);

            var unavailable = NewRider("Down", Vector3.zero);
            unavailable.CanBoard = false;
            _vehicle.TryExit(a);
            LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' board refused rider=Down reason=rider_unavailable");
            Assert.That(_vehicle.TryEnter(unavailable), Is.False);

            _vehicle.Wreck();
            LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' board refused rider=C reason=wrecked");
            Assert.That(_vehicle.TryEnter(c), Is.False);
        }

        [Test]
        public void EnteringAChosenSeatChecksTheRangeUnlessToldNotTo()
        {
            var rider = NewRider("Bot", new Vector3(10f, 1f, 0f));

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' board refused rider=Bot reason=out_of_range");
            Assert.That(_vehicle.TryEnter(rider, 0), Is.False);
            Assert.That(_vehicle.TryEnter(rider, 0, requireInRange: false), Is.True);
            Assert.That(rider.Seat, Is.SameAs(_driver));

            var other = NewRider("Other", Vector3.zero);
            LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' board refused rider=Other reason=seat_taken");
            Assert.That(_vehicle.TryEnter(other, 0), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => _vehicle.TryEnter(other, 2));
        }

        [Test]
        public void ExitingUsesTheFirstOpenCandidate()
        {
            var rider = NewRider("Rider", Vector3.zero);
            _vehicle.TryEnter(rider, 0);

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' leave rider=Rider seat=0 reason=Request exit=0");
            Assert.That(_vehicle.TryExit(rider), Is.True);

            Assert.That(rider.Seat, Is.Null);
            Assert.That(rider.LastFeet, Is.EqualTo(_leftExit));
            Assert.That(rider.LastReason, Is.EqualTo(VehicleLeaveReason.Request));
            Assert.That(_driver.IsOccupied, Is.False);
        }

        [Test]
        public void ABlockedExitFallsBackToTheNextCandidate()
        {
            Block(_leftExit);
            var rider = NewRider("Rider", Vector3.zero);
            _vehicle.TryEnter(rider, 0);

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' leave rider=Rider seat=0 reason=Request exit=1");
            Assert.That(_vehicle.TryExit(rider), Is.True);
            Assert.That(rider.LastFeet, Is.EqualTo(_rightExit));
        }

        [Test]
        public void TheVehicleCandidatesComeAfterTheSeatCandidates()
        {
            Block(_leftExit);
            Block(_rightExit);
            var rider = NewRider("Rider", Vector3.zero);
            _vehicle.TryEnter(rider, 0);

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' leave rider=Rider seat=0 reason=Request exit=2");
            Assert.That(_vehicle.TryExit(rider), Is.True);
            Assert.That(rider.LastFeet, Is.EqualTo(_rearExit));
        }

        [Test]
        public void EveryExitBlockedKeepsTheRiderAboardUnlessForced()
        {
            Block(_leftExit);
            Block(_rightExit);
            Block(_rearExit);
            var rider = NewRider("Rider", Vector3.zero);
            _vehicle.TryEnter(rider, 0);

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' leave refused rider=Rider reason=exit_blocked");
            Assert.That(_vehicle.TryExit(rider), Is.False);
            Assert.That(rider.Seat, Is.SameAs(_driver));

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' leave rider=Rider seat=0 reason=Forced exit=-");
            Assert.That(_vehicle.ForceExit(rider, VehicleLeaveReason.Forced), Is.True);
            Assert.That(rider.Seat, Is.Null);
            Assert.That(rider.LastFeet.y, Is.EqualTo(1.2f).Within(0.1f), "set on the roof");
        }

        [Test]
        public void TheVehicleItselfDoesNotBlockTheExit()
        {
            var rider = NewRider("Rider", Vector3.zero);

            Assert.That(_vehicle.IsExitBlocked(_driverSeat, rider), Is.False, "only colliders outside the vehicle and the rider count");
            Block(_leftExit);
            Assert.That(_vehicle.IsExitBlocked(_leftExit, rider), Is.True);
        }

        [Test]
        public void SwitchingSeatsMovesToTheNextFreeSeat()
        {
            var rider = NewRider("Rider", Vector3.zero);
            _vehicle.TryEnter(rider, 0);

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' switch rider=Rider from=0 to=1 role=Passenger");
            Assert.That(_vehicle.TrySwitchSeat(rider, 1), Is.True);
            Assert.That(rider.Seat, Is.SameAs(_passenger));
            Assert.That(rider.Calls, Is.EqualTo(new[] { "board 0", "switch 0>1" }));

            var other = NewRider("Other", Vector3.zero);
            _vehicle.TryEnter(other, 0);
            LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' switch refused rider=Rider reason=no_free_seat");
            Assert.That(_vehicle.TrySwitchSeat(rider, -1), Is.False);

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' switch refused rider=Rider reason=seat_taken");
            Assert.That(_vehicle.TrySwitchToSeat(rider, 0), Is.False);
            Assert.That(_vehicle.TrySwitchToSeat(rider, 1), Is.False, "already there");
            Assert.Throws<ArgumentOutOfRangeException>(() => _vehicle.TrySwitchSeat(rider, 2));
        }

        [Test]
        public void ARiderNotAboardCannotExitOrSwitch()
        {
            var rider = NewRider("Rider", Vector3.zero);

            Assert.That(_vehicle.TryExit(rider), Is.False);
            Assert.That(_vehicle.ForceExit(rider, VehicleLeaveReason.Forced), Is.False);
            Assert.That(_vehicle.TrySwitchSeat(rider, 1), Is.False);
            Assert.That(_vehicle.TrySwitchToSeat(rider, 1), Is.False);
            Assert.That(rider.Calls, Is.Empty);
        }

        [Test]
        public void WreckingEjectsEveryoneEvenOnBlockedExitsAndRefusesNewRiders()
        {
            var a = NewRider("A", Vector3.zero);
            var b = NewRider("B", Vector3.zero);
            _vehicle.TryEnter(a, 0);
            _vehicle.TryEnter(b, 1);
            var wrecked = 0;
            _vehicle.Wrecked += _ => wrecked++;

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' wrecked riders=2");
            LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' leave rider=A seat=0 reason=Wrecked exit=0");
            LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' leave rider=B seat=1 reason=Wrecked exit=0");
            Assert.That(_vehicle.Wreck(), Is.True);

            Assert.That(_vehicle.IsWrecked, Is.True);
            Assert.That(_vehicle.RiderCount, Is.Zero);
            Assert.That(a.LastReason, Is.EqualTo(VehicleLeaveReason.Wrecked));
            Assert.That(b.LastReason, Is.EqualTo(VehicleLeaveReason.Wrecked));
            Assert.That(wrecked, Is.EqualTo(1));
            Assert.That(_vehicle.Wreck(), Is.False, "only once");
            Assert.That(_vehicle.FindBoardableSeat(Vector3.zero, out _), Is.EqualTo(-1));
        }

        [Test]
        public void TheVehiclesDamageableDyingWrecksIt()
        {
            var root = Track(new GameObject("Tank"));
            CreateSeat(root.transform, "Seat", Vector3.zero);
            var damageable = root.AddComponent<Damageable>();
            damageable.Initialize("tank", 10);
            var tank = root.AddComponent<Vehicle>();
            var rider = NewRider("Crew", Vector3.zero);
            tank.TryEnter(rider);

            LogAssert.Expect(LogType.Log, "[Combat] target died id=tank");
            damageable.TakeDamage(10);

            Assert.That(tank.IsWrecked, Is.True);
            Assert.That(rider.Seat, Is.Null);
            Assert.That(rider.LastReason, Is.EqualTo(VehicleLeaveReason.Wrecked));
        }

        [Test]
        public void ADestroyedRiderFreesItsSeat()
        {
            var rider = Track(new GameObject("Doomed")).AddComponent<VehicleTestRider>();
            _vehicle.TryEnter(rider, 0, requireInRange: false);
            Object.DestroyImmediate(rider);

            Assert.That(_driver.Occupant, Is.Null);
            LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' leave rider=- seat=0 reason=RiderRemoved exit=-");
            Assert.That(_vehicle.FindBoardableSeat(new Vector3(-1.3f, 0f, 0f), out _), Is.EqualTo(0));
        }

        [Test]
        public void SeatInputReachesTheSinksOnlyFromTheOccupant()
        {
            var sink = new RecordingSink();
            Assert.That(_vehicle.AddInputSink(sink), Is.True);
            Assert.That(_vehicle.AddInputSink(sink), Is.False);
            var rider = NewRider("Driver", Vector3.zero);
            var stranger = NewRider("Stranger", Vector3.zero);
            _vehicle.TryEnter(rider, 0);
            var input = new VehicleSeatInput(new Vector2(0f, 1f), 0f, true, false, false, false, Vector3.zero, Vector3.forward * 2f);

            Assert.That(_driver.SubmitInput(rider, input, 0.02f), Is.True);
            Assert.That(_driver.SubmitInput(stranger, input, 0.02f), Is.False);
            Assert.That(_passenger.SubmitInput(rider, input, 0.02f), Is.False, "not the rider's seat");

            Assert.That(sink.Inputs, Has.Count.EqualTo(1));
            Assert.That(sink.Inputs[0].Seat, Is.SameAs(_driver));
            Assert.That(sink.Inputs[0].Input.Move, Is.EqualTo(new Vector2(0f, 1f)));
            Assert.That(sink.Inputs[0].Input.Boost, Is.True);
            Assert.That(sink.Inputs[0].Input.AimDirection, Is.EqualTo(Vector3.forward), "normalized");

            _vehicle.TrySwitchSeat(rider, 1);
            _vehicle.TryExit(rider);
            Assert.That(sink.Vacated, Is.EqualTo(new[] { _driver, _passenger }));

            Assert.That(_vehicle.RemoveInputSink(sink), Is.True);
            Assert.That(_vehicle.RemoveInputSink(sink), Is.False);
        }

        [Test]
        public void SeatInputRejectsOutOfRangeValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new VehicleSeatInput(new Vector2(2f, 0f), 0f, false, false, false, false, Vector3.zero, Vector3.zero));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new VehicleSeatInput(Vector2.zero, -1.5f, false, false, false, false, Vector3.zero, Vector3.zero));
            Assert.Throws<ArgumentException>(
                () => new VehicleSeatInput(Vector2.zero, 0f, false, false, false, false, new Vector3(float.NaN, 0f, 0f), Vector3.zero));
            Assert.That(VehicleSeatInput.Idle.HasAim, Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _driver.SubmitInput(NewRider("R", Vector3.zero), VehicleSeatInput.Idle, -1f));
        }

        [Test]
        public void WithoutExitPointsTheRiderLeavesToTheSideOfTheSeat()
        {
            var root = Track(new GameObject("Bare"));
            root.transform.position = new Vector3(20f, 0f, 0f);
            CreateSeat(root.transform, "Seat", Vector3.zero);
            var bare = root.AddComponent<Vehicle>();
            var rider = NewRider("Rider", new Vector3(20f, 0f, 0f));
            bare.TryEnter(rider);

            Assert.That(bare.TryExit(rider), Is.True);
            Assert.That(rider.LastFeet, Is.EqualTo(new Vector3(20f + Vehicle.FallbackExitDistance, 0f, 0f)));
        }

        private void Block(Vector3 feet)
        {
            var box = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            box.name = "Blocker";
            box.transform.position = feet + new Vector3(0f, 1f, 0f);
            box.transform.localScale = new Vector3(1f, 2f, 1f);
            Physics.SyncTransforms();
        }

        private FakeRider NewRider(string name, Vector3 position)
        {
            var root = Track(new GameObject(name));
            root.transform.position = position;
            return new FakeRider(root.transform);
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

        private sealed class FakeRider : IVehicleRider
        {
            private readonly Transform _root;

            public FakeRider(Transform root)
            {
                _root = root;
            }

            public List<string> Calls { get; } = new List<string>();

            public Vector3 LastFeet { get; private set; }

            public VehicleLeaveReason LastReason { get; private set; }

            public bool CanBoard { get; set; } = true;

            public string RiderName => _root.name;

            public Transform RiderRoot => _root;

            public float FeetBelowRoot => 1f;

            public float ClearanceRadius => 0.4f;

            public float ClearanceHeight => 1.8f;

            public VehicleSeat Seat { get; private set; }

            public void OnBoarded(VehicleSeat seat)
            {
                Seat = seat;
                Calls.Add($"board {seat.Index}");
            }

            public void OnSeatChanged(VehicleSeat from, VehicleSeat to)
            {
                Seat = to;
                Calls.Add($"switch {from.Index}>{to.Index}");
            }

            public void OnLeft(VehicleSeat seat, VehicleLeaveReason reason, Vector3 feetPosition, Quaternion rotation)
            {
                Seat = null;
                LastFeet = feetPosition;
                LastReason = reason;
                Calls.Add($"leave {seat.Index}");
                _root.position = feetPosition + Vector3.up;
            }
        }

        private sealed class RecordingSink : IVehicleInputSink
        {
            public List<(VehicleSeat Seat, VehicleSeatInput Input)> Inputs { get; } = new List<(VehicleSeat, VehicleSeatInput)>();

            public List<VehicleSeat> Vacated { get; } = new List<VehicleSeat>();

            public void OnSeatInput(VehicleSeat seat, VehicleSeatInput input, float deltaSeconds)
            {
                Inputs.Add((seat, input));
            }

            public void OnSeatVacated(VehicleSeat seat)
            {
                Vacated.Add(seat);
            }
        }
    }
}
