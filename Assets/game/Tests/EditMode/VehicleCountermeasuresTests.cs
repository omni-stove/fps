using System.Collections.Generic;
using System.Text.RegularExpressions;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // OnEnable の走らない Edit Mode なので、入力の受け手は Bind で結び、フレアは Tick で進める。
    public sealed class VehicleCountermeasuresTests
    {
        private const float _step = 0.25f;

        private static readonly Vector3 _eye = new Vector3(0f, 1f, 0f);
        private static readonly Vector3 _vehiclePosition = new Vector3(0f, 0f, 20f);
        private static readonly Vector3 _driverSeat = new Vector3(-0.5f, 1f, 0f);
        private static readonly Vector3 _passengerSeat = new Vector3(0.5f, 1f, 0f);

        private readonly List<Object> _created = new List<Object>();
        private Vehicle _vehicle;
        private VehicleSeat _driver;
        private VehicleSeat _passenger;
        private VehicleCountermeasures _countermeasures;

        [SetUp]
        public void SetUp()
        {
            var root = Track(new GameObject("Jet"));
            root.transform.position = _vehiclePosition;
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            body.transform.localScale = new Vector3(2f, 1.2f, 4f);

            _driver = CreateSeat(root.transform, "Driver", _driverSeat);
            _passenger = CreateSeat(root.transform, "Passenger", _passengerSeat);
            _vehicle = root.AddComponent<Vehicle>();
            _vehicle.Configure(new VehicleSpec(
                "jet",
                500,
                new[] { new VehicleSeatSpec(VehicleSeatRole.Driver), new VehicleSeatSpec(VehicleSeatRole.Passenger) }));
            Assert.That(_vehicle.Damageable, Is.Not.Null);

            _countermeasures = root.AddComponent<VehicleCountermeasures>();
            _countermeasures.Configure(new CountermeasureSpec(
                count: 2, intervalSeconds: 1f, lifetimeSeconds: 2f, decoyConeDegrees: 30f, ejectSpeed: 0f, gravityMultiplier: 0f));
            Assert.That(_countermeasures.Bind(), Is.True);
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            GameSignals.ResetAll();
            foreach (var body in Object.FindObjectsByType<ProjectileBody>(FindObjectsInactive.Include))
            {
                Object.DestroyImmediate(body.gameObject);
            }

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
        public void PressingTheKeyInTheDriverSeatDeploysAGlowingFlareAndLogsWhatIsLeft()
        {
            var pilot = Board("Pilot", _driver);

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Jet' flare deployed id=0 remaining=1/2");
            Assert.That(_driver.SubmitInput(pilot, Pressed(), 0.02f), Is.True);

            Assert.That(_countermeasures.Remaining, Is.EqualTo(1));
            Assert.That(_countermeasures.Visuals, Has.Count.EqualTo(1));
            var flare = _countermeasures.Visuals[0];
            Assert.That(flare.name, Is.EqualTo(VehicleCountermeasures.FlareObjectName));
            Assert.That(flare.transform.position, Is.EqualTo(_vehicle.transform.TransformPoint(CountermeasureSettings.DefaultEjectLocalOffset)));
            Assert.That(flare.GetComponent<Light>(), Is.Not.Null, "the placeholder glows");
            Assert.That(flare.GetComponent<Renderer>(), Is.Not.Null);
            Assert.That(flare.GetComponent<Collider>().enabled, Is.False, "rounds and rays pass through the flare");
        }

        [Test]
        public void NothingIsDeployedWithoutThePressOrFromAPassengerSeat()
        {
            var pilot = Board("Pilot", _driver);
            var passenger = Board("Passenger", _passenger);

            _driver.SubmitInput(pilot, SeatInput(false), 0.02f);
            _passenger.SubmitInput(passenger, Pressed(), 0.02f);

            Assert.That(_countermeasures.Remaining, Is.EqualTo(2));
            Assert.That(_countermeasures.Visuals, Is.Empty);
        }

        [Test]
        public void AFlareCannotBeDeployedDuringTheInterval()
        {
            LogAssert.Expect(LogType.Log, "[Vehicle] 'Jet' flare deployed id=0 remaining=1/2");
            Assert.That(_countermeasures.TryDeploy(), Is.EqualTo(CountermeasureVerdict.Deployed));

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Jet' flare refused reason=cooling_down remaining=1/2 wait=1.00");
            Assert.That(_countermeasures.TryDeploy(), Is.EqualTo(CountermeasureVerdict.CoolingDown));
            Assert.That(_countermeasures.Remaining, Is.EqualTo(1));
            Assert.That(_countermeasures.Visuals, Has.Count.EqualTo(1));

            _countermeasures.Tick(1f);
            LogAssert.Expect(LogType.Log, "[Vehicle] 'Jet' flare deployed id=1 remaining=0/2");
            Assert.That(_countermeasures.TryDeploy(), Is.EqualTo(CountermeasureVerdict.Deployed));
        }

        [Test]
        public void NoFlareIsDeployedOnceTheCountRunsOut()
        {
            _countermeasures.TryDeploy();
            _countermeasures.Tick(1f);
            _countermeasures.TryDeploy();
            _countermeasures.Tick(1f);

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Jet' flare refused reason=empty remaining=0/2 wait=0.00");
            Assert.That(_countermeasures.TryDeploy(), Is.EqualTo(CountermeasureVerdict.Empty));
            Assert.That(_countermeasures.Remaining, Is.Zero);

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Jet' flare refilled remaining=2/2");
            _countermeasures.Refill();
            Assert.That(_countermeasures.TryDeploy(), Is.EqualTo(CountermeasureVerdict.Deployed));
        }

        [Test]
        public void TheFlareLooksLikeThePrefabAndGoesAwayWhenItBurnsOut()
        {
            var prefab = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            prefab.name = "FlarePrefab";
            _countermeasures.Settings.FlarePrefab = prefab;

            _countermeasures.TryDeploy();
            var flare = _countermeasures.Visuals[0];
            Assert.That(flare, Is.Not.SameAs(prefab));
            Assert.That(flare.GetComponent<BoxCollider>(), Is.Not.Null, "made from the prefab");
            Assert.That(flare.GetComponent<BoxCollider>().enabled, Is.False);
            Assert.That(flare.GetComponent<Light>(), Is.Null, "the placeholder is not used");
            Assert.That(prefab.GetComponent<BoxCollider>().enabled, Is.True, "the prefab itself is left alone");

            _countermeasures.Tick(1f);
            Assert.That(flare != null, Is.True);

            _countermeasures.Tick(1f);
            Assert.That(flare == null, Is.True);
            Assert.That(_countermeasures.Visuals, Is.Empty);
        }

        [Test]
        public void TheFlareMovesWithThePod()
        {
            _countermeasures.Configure(new CountermeasureSpec(intervalSeconds: 0f, lifetimeSeconds: 5f, ejectSpeed: 10f, gravityMultiplier: 0f));
            _countermeasures.Settings.EjectLocalDirection = Vector3.back;

            _countermeasures.TryDeploy();
            var start = _countermeasures.Visuals[0].transform.position;
            _countermeasures.Tick(0.5f);

            Assert.That(Vector3.Distance(_countermeasures.Visuals[0].transform.position, start + new Vector3(0f, 0f, -5f)), Is.LessThan(1e-4f));
        }

        [Test]
        public void TheVehicleAndItsRidersAreFoundFromTheirParts()
        {
            Assert.That(VehicleCountermeasures.For(_vehicle.Damageable), Is.SameAs(_countermeasures));
            Assert.That(VehicleCountermeasures.For(_vehicle.transform.Find("Body")), Is.SameAs(_countermeasures));
            Assert.That(VehicleCountermeasures.For(null), Is.Null);

            var stranger = Track(new GameObject("Stranger"));
            Assert.That(VehicleCountermeasures.For(stranger.transform), Is.Null);

            _countermeasures.enabled = false;
            Assert.That(VehicleCountermeasures.For(_vehicle.Damageable), Is.Null, "a switched-off pod lures nothing");
        }

        [Test]
        public void ALockOnTheVehicleBreaksWhenItDeploysAFlare()
        {
            var aim = CreateLockOn();
            LockOn(aim);
            Assert.That(aim.LockedTargetId, Is.Not.Null);

            _countermeasures.TryDeploy();
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Combat\] lock-on lost target=none$"));
            aim.Tick(_step);

            Assert.That(aim.LockedTargetId, Is.Null);
            Assert.That(aim.Progress, Is.Null);
        }

        [Test]
        public void TheVehicleCanBeLockedAgainAfterTheFlareBurnsOut()
        {
            var aim = CreateLockOn();
            _countermeasures.TryDeploy();
            aim.Tick(_step);
            Assert.That(aim.Progress, Is.Null);

            _countermeasures.Tick(2f);
            aim.Tick(_step);

            Assert.That(aim.Progress, Is.EqualTo(0.25f).Within(1e-4f), "the lock starts over");
        }

        [Test]
        public void AHomingRoundInFlightTurnsTowardTheFlare()
        {
            _countermeasures.Configure(new CountermeasureSpec(
                count: 2, intervalSeconds: 1f, lifetimeSeconds: 10f, decoyConeDegrees: 30f, ejectSpeed: 0f, gravityMultiplier: 0f));
            _countermeasures.Settings.EjectLocalOffset = new Vector3(0f, -3f, -6f);
            var aim = CreateLockOn();
            LockOn(aim);
            var request = new ShotRequest(
                null,
                null,
                null,
                Guided(),
                new ImpactSpec(10),
                "test-guided",
                Physics.DefaultRaycastLayers,
                homingTarget: aim.FollowLocked(),
                homingDecoy: aim.DecoyOfLocked());
            Assert.That(request.HomingDecoy, Is.Not.Null);
            var body = ProjectileBody.Launch(request, new PlannedRound(_eye.ToNumerics(), Vector3.forward.ToNumerics(), 0));
            body.Tick(0.02f);
            Assert.That(body.Flight.LuredDecoyId, Is.Null);

            _countermeasures.TryDeploy();
            LogAssert.Expect(LogType.Log, "[Combat] projectile weapon=test-guided lured flare=0");
            body.Tick(0.02f);

            Assert.That(body.Flight.LuredDecoyId, Is.EqualTo(0));
            Assert.That(body.Flight.Velocity.Y, Is.LessThan(0f), "turns down toward the flare below the vehicle");
        }

        private void LockOn(LockOnAim aim)
        {
            for (var i = 0; i < 4; i++)
            {
                aim.Tick(_step);
            }
        }

        private LockOnAim CreateLockOn()
        {
            var player = Track(new GameObject("player"));
            player.transform.position = _eye;
            var aim = player.AddComponent<LockOnAim>();
            var round = Guided();
            aim.RoundSource = () => round;
            aim.AimingSource = () => true;
            aim.AimSource = () => new Ray(_eye, Vector3.forward);
            aim.Targets = new HitboxLockOnTargets(player.transform, Physics.DefaultRaycastLayers);
            return aim;
        }

        private static RoundSpec Guided()
        {
            return new RoundSpec(
                RoundDelivery.Projectile,
                range: 50f,
                projectile: new ProjectileSpec(20f, 0f, 5f),
                homing: new HomingSpec(1f, 15f, 180f));
        }

        private static VehicleSeatInput Pressed()
        {
            return SeatInput(true);
        }

        private static VehicleSeatInput SeatInput(bool countermeasure)
        {
            return new VehicleSeatInput(Vector2.zero, 0f, false, false, false, false, Vector3.zero, Vector3.zero, countermeasure);
        }

        private FakeRider Board(string name, VehicleSeat seat)
        {
            var root = Track(new GameObject(name));
            root.transform.position = seat.transform.position;
            var rider = new FakeRider(root.transform);
            Assert.That(_vehicle.TryEnter(rider), Is.True);
            Assert.That(rider.Seat, Is.SameAs(seat));
            return rider;
        }

        private static VehicleSeat CreateSeat(Transform parent, string name, Vector3 localPosition)
        {
            var seat = new GameObject(name).AddComponent<VehicleSeat>();
            seat.transform.SetParent(parent, false);
            seat.transform.localPosition = localPosition;
            return seat;
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

            public string RiderName => _root.name;

            public Transform RiderRoot => _root;

            public float FeetBelowRoot => 1f;

            public float ClearanceRadius => 0.4f;

            public float ClearanceHeight => 1.8f;

            public bool CanBoard => true;

            public VehicleSeat Seat { get; private set; }

            public void OnBoarded(VehicleSeat seat)
            {
                Seat = seat;
            }

            public void OnSeatChanged(VehicleSeat from, VehicleSeat to)
            {
                Seat = to;
            }

            public void OnLeft(VehicleSeat seat, VehicleLeaveReason reason, Vector3 feetPosition, Quaternion rotation)
            {
                Seat = null;
            }
        }
    }
}
