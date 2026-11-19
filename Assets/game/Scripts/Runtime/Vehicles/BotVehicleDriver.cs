using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 舵とアクセルは座席の入力として渡し、動き方には直接触らない。人が運転するときと同じ道 (VehicleMotorLink と操作の割り当て) を通すため。
    [DisallowMultipleComponent]
    public sealed class BotVehicleDriver : MonoBehaviour
    {
        private const float StandInClearanceRadius = 0.4f;
        private const float StandInClearanceHeight = 1.8f;

        [SerializeField, Tooltip("運転する乗り物。空なら自分か親から探す")]
        private Vehicle _vehicle;

        [SerializeField, Tooltip("なぞる道")]
        private VehicleRoute _route;

        [SerializeField, Tooltip("運転席が空なら代わりの運転手を座らせて走る。切ると SetRider で渡した乗り手が運転席にいるときだけ走る")]
        private bool _driveUnmanned = true;

        [SerializeField, Min(0.01f), Tooltip("止まっていても見る道の先の距離 (m)")]
        private float _lookAheadDistance = VehicleRouteSpec.DefaultLookAheadDistance;

        [SerializeField, Min(0f), Tooltip("速さにこの秒を掛けた分だけ、見る道の先を延ばす")]
        private float _lookAheadSeconds = VehicleRouteSpec.DefaultLookAheadSeconds;

        [SerializeField, Min(0.01f), Tooltip("点にこの距離 (m) まで近づいたら、着いたとみなして次の点へ向かう")]
        private float _arriveRadius = VehicleRouteSpec.DefaultArriveRadius;

        [SerializeField, Range(1f, VehicleRouteSpec.MaxAngle), Tooltip("向かう先がこの角度 (度) ずれていたら、ハンドルを切り切る")]
        private float _fullSteerAngle = VehicleRouteSpec.DefaultFullSteerAngle;

        [SerializeField, Min(0.01f), Tooltip("まっすぐな道で出す速さ (m/s)")]
        private float _cruiseSpeed = VehicleRouteSpec.DefaultCruiseSpeed;

        [SerializeField, Min(0f), Tooltip("鋭い角を抜けるときの速さ (m/s)。まっすぐな道の速さを超える値はそこで抑える")]
        private float _cornerSpeed = VehicleRouteSpec.DefaultCornerSpeed;

        [SerializeField, Range(1f, VehicleRouteSpec.MaxAngle), Tooltip("曲がる角度 (度) がこれ以上の角は、角の速さまで落として曲がる")]
        private float _sharpCornerAngle = VehicleRouteSpec.DefaultSharpCornerAngle;

        [SerializeField, Min(0.01f), Tooltip("曲がる前や最後の点の前で落とすときに見込む減速 (m/s²)。小さいほど早くから落とす")]
        private float _deceleration = VehicleRouteSpec.DefaultDeceleration;

        [SerializeField, Min(0.01f), Tooltip("出したい速さとの差がこの値 (m/s) でアクセルを踏み切り、これより速すぎればブレーキを踏む")]
        private float _speedBand = VehicleRouteSpec.DefaultSpeedBand;

        [SerializeField, Min(0f), Tooltip("アクセルを踏んでもこの速さ (m/s) を超えないままなら、詰まったとみなす")]
        private float _stuckSpeed = VehicleRouteSpec.DefaultStuckSpeed;

        [SerializeField, Min(0.01f), Tooltip("詰まったままこの秒が過ぎたら、下がって切り返す")]
        private float _stuckSeconds = VehicleRouteSpec.DefaultStuckSeconds;

        [SerializeField, Min(0.01f), Tooltip("切り返しで下がり続ける秒")]
        private float _reverseSeconds = VehicleRouteSpec.DefaultReverseSeconds;

        [SerializeField, Range(0.01f, 1f), Tooltip("切り返しで下がるときのアクセルの強さ")]
        private float _reverseThrottle = VehicleRouteSpec.DefaultReverseThrottle;

        private readonly List<System.Numerics.Vector3> _points = new List<System.Numerics.Vector3>();

        private VehicleRouteSpec _spec;
        private VehicleRouteFollower _follower;
        private IVehicleRider _rider;
        private StandInRider _standIn;
        private VehicleRoutePhase _lastPhase = VehicleRoutePhase.Driving;
        private Vector3 _lastPosition;
        private bool _hasLastPosition;
        private bool _warnedVehicle;
        private bool _warnedSeat;
        private bool _warnedRoute;

        public Vehicle Vehicle => _vehicle != null ? _vehicle : (_vehicle = GetComponentInParent<Vehicle>());

        public VehicleRoute Route => _route;

        public VehicleRouteSpec Spec => _spec ??= BuildSpec();

        public VehicleRouteFollower Follower => _follower ??= new VehicleRouteFollower(Spec);

        public VehicleRouteCommand LastCommand { get; private set; }

        public bool DrivesUnmanned
        {
            get => _driveUnmanned;
            set
            {
                _driveUnmanned = value;
                if (!value)
                {
                    ReleaseStandIn();
                }
            }
        }

        public IVehicleRider Rider => _rider;

        public bool IsStandInSeated => _standIn != null && _standIn.Seat != null;

        private void OnValidate()
        {
            _spec = null;
            if (!_driveUnmanned)
            {
                ReleaseStandIn();
            }

            if (_follower != null)
            {
                RebuildFollower();
            }
        }

        private void Update()
        {
            Drive(Time.deltaTime);
        }

        private void OnDisable()
        {
            Release();
        }

        private void OnDestroy()
        {
            Release();
            if (_standIn != null && _standIn.Root != null)
            {
                DestroyObject(_standIn.Root.gameObject);
            }
        }

        public void Configure(VehicleRouteSpec spec)
        {
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
            _lookAheadDistance = spec.LookAheadDistance;
            _lookAheadSeconds = spec.LookAheadSeconds;
            _arriveRadius = spec.ArriveRadius;
            _fullSteerAngle = spec.FullSteerAngle;
            _cruiseSpeed = spec.CruiseSpeed;
            _cornerSpeed = spec.CornerSpeed;
            _sharpCornerAngle = spec.SharpCornerAngle;
            _deceleration = spec.Deceleration;
            _speedBand = spec.SpeedBand;
            _stuckSpeed = spec.StuckSpeed;
            _stuckSeconds = spec.StuckSeconds;
            _reverseSeconds = spec.ReverseSeconds;
            _reverseThrottle = spec.ReverseThrottle;
            RebuildFollower();
        }

        public void SetVehicle(Vehicle vehicle)
        {
            if (ReferenceEquals(vehicle, _vehicle))
            {
                return;
            }

            Release();
            _vehicle = vehicle;
            _hasLastPosition = false;
            _warnedVehicle = false;
            _warnedSeat = false;
        }

        public void SetRoute(VehicleRoute route, int startIndex = 0)
        {
            _route = route;
            _warnedRoute = false;
            Restart(startIndex);
        }

        // 渡した乗り手を運転席に座らせるのは呼ぶ側に任せる。乗り降りの順番 (歩いて近づく・降りる) は乗り手ごとに違うため。
        public void SetRider(IVehicleRider rider)
        {
            if (rider != null)
            {
                ReleaseStandIn();
            }

            _rider = rider;
        }

        public void Restart(int startIndex = 0)
        {
            Follower.Restart(startIndex);
            _lastPhase = VehicleRoutePhase.Driving;
        }

        // Update から呼ぶ。Edit Mode のテストからも 1 コマずつ進められるよう public にする。
        public bool Drive(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            var vehicle = Vehicle;
            if (vehicle == null)
            {
                WarnOnce(ref _warnedVehicle, VehicleLog.BotDriverMissingVehicle(name));
                return false;
            }

            _warnedVehicle = false;
            // 止めた乗り物は乗っている人を降ろすので、座り直して動かさないよう有効かを自分で見る。isActiveAndEnabled は Edit Mode で当てにならない。
            if (vehicle.IsWrecked || !vehicle.enabled || !vehicle.gameObject.activeInHierarchy)
            {
                return false;
            }

            var seat = DriverSeat(vehicle);
            if (seat == null)
            {
                WarnOnce(ref _warnedSeat, VehicleLog.BotDriverMissingDriverSeat(vehicle.name));
                return false;
            }

            _warnedSeat = false;
            var rider = SeatedRider(vehicle, seat);
            if (rider == null)
            {
                _hasLastPosition = false;
                return false;
            }

            var speed = ForwardSpeed(vehicle, deltaSeconds);
            if (_route != null)
            {
                _route.CopyPositions(_points);
            }
            else
            {
                _points.Clear();
            }

            if (_points.Count == 0)
            {
                WarnOnce(ref _warnedRoute, VehicleLog.BotDriverMissingRoute(vehicle.name));
                LastCommand = new VehicleRouteCommand(0f, 0f, true, Follower.TargetIndex, 0f, Follower.Phase);
                return seat.SubmitInput(rider, ToSeatInput(LastCommand), deltaSeconds);
            }

            _warnedRoute = false;
            var pose = vehicle.transform;
            var command = Follower.Tick(_points, _route.Loop, pose.position.ToNumerics(), pose.forward.ToNumerics(), speed, deltaSeconds);
            if (command.Phase != _lastPhase)
            {
                _lastPhase = command.Phase;
                Debug.Log(VehicleLog.BotDriverPhase(vehicle.name, command.Phase, command.TargetIndex), this);
            }

            LastCommand = command;
            return seat.SubmitInput(rider, ToSeatInput(command), deltaSeconds);
        }

        public static VehicleSeatInput ToSeatInput(VehicleRouteCommand command)
        {
            var move = new Vector2(Mathf.Clamp(command.Steer, -1f, 1f), Mathf.Clamp(command.Throttle, -1f, 1f));
            return new VehicleSeatInput(move, 0f, command.Brake, false, false, false, Vector3.zero, Vector3.zero);
        }

        private static VehicleSeat DriverSeat(Vehicle vehicle)
        {
            var index = vehicle.Spec.DriverSeatIndex;
            var seats = vehicle.Seats;
            return index >= 0 && index < seats.Count && seats[index] != null ? seats[index] : null;
        }

        private IVehicleRider SeatedRider(Vehicle vehicle, VehicleSeat seat)
        {
            if (_rider is UnityEngine.Object riderObject && riderObject == null)
            {
                _rider = null;
            }

            if (_rider != null)
            {
                return ReferenceEquals(seat.Occupant, _rider) ? _rider : null;
            }

            if (!_driveUnmanned)
            {
                return null;
            }

            _standIn ??= new StandInRider(this);
            if (ReferenceEquals(seat.Occupant, _standIn))
            {
                return _standIn;
            }

            if (seat.IsOccupied || _standIn.Seat != null)
            {
                return null;
            }

            return vehicle.TryEnter(_standIn, seat.Index, requireInRange: false) ? _standIn : null;
        }

        // 物理で動く車は Rigidbody の速さを使う。Rigidbody が無いか物理で動かない間は、前のコマからの動きで測る。
        private float ForwardSpeed(Vehicle vehicle, float deltaSeconds)
        {
            var pose = vehicle.transform;
            var position = pose.position;
            var forward = pose.forward;
            var hadLast = _hasLastPosition;
            var last = _lastPosition;
            _lastPosition = position;
            _hasLastPosition = true;

            var body = vehicle.GetComponent<Rigidbody>();
            if (body != null && !body.isKinematic)
            {
                return Vector3.Dot(body.linearVelocity, forward);
            }

            return hadLast && deltaSeconds > 0f ? Vector3.Dot(position - last, forward) / deltaSeconds : 0f;
        }

        private void Release()
        {
            ReleaseStandIn();
            var vehicle = _vehicle;
            if (vehicle == null || _rider == null || (_rider is UnityEngine.Object riderObject && riderObject == null))
            {
                return;
            }

            // 乗り手は座ったまま残るので、最後に踏んでいたアクセルのまま走り続けないよう止める入力を渡しておく。
            var seat = DriverSeat(vehicle);
            if (seat != null && ReferenceEquals(seat.Occupant, _rider))
            {
                seat.SubmitInput(_rider, ToSeatInput(new VehicleRouteCommand(0f, 0f, true, 0, 0f, VehicleRoutePhase.Finished)), 0f);
            }
        }

        private void ReleaseStandIn()
        {
            if (_standIn == null || _standIn.Seat == null)
            {
                return;
            }

            var vehicle = _standIn.Seat.Vehicle;
            if (vehicle != null)
            {
                vehicle.ForceExit(_standIn, VehicleLeaveReason.Forced);
            }
        }

        private void RebuildFollower()
        {
            var index = _follower != null ? _follower.TargetIndex : 0;
            _follower = new VehicleRouteFollower(Spec);
            _follower.Restart(index);
            _lastPhase = VehicleRoutePhase.Driving;
        }

        private void WarnOnce(ref bool warned, string message)
        {
            if (warned)
            {
                return;
            }

            warned = true;
            Debug.LogWarning(message, this);
        }

        private VehicleRouteSpec BuildSpec()
        {
            return new VehicleRouteSpec(
                _lookAheadDistance,
                _lookAheadSeconds,
                _arriveRadius,
                _fullSteerAngle,
                _cruiseSpeed,
                Mathf.Min(_cornerSpeed, _cruiseSpeed),
                _sharpCornerAngle,
                _deceleration,
                _speedBand,
                _stuckSpeed,
                _stuckSeconds,
                _reverseSeconds,
                _reverseThrottle);
        }

        private static void DestroyObject(GameObject target)
        {
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        // 運転席に座る人がいなくても走らせるための代わりの運転手。当たり判定を持たない子を体にして、乗り物の当たり判定を消させないようにする。
        private sealed class StandInRider : IVehicleRider
        {
            private readonly BotVehicleDriver _driver;
            private Transform _root;

            public StandInRider(BotVehicleDriver driver)
            {
                _driver = driver;
            }

            public Transform Root => _root;

            public string RiderName => _driver != null ? _driver.name : "-";

            public Transform RiderRoot
            {
                get
                {
                    if (_root == null && _driver != null)
                    {
                        _root = new GameObject(nameof(BotVehicleDriver) + "." + nameof(StandInRider)).transform;
                        _root.SetParent(_driver.transform, false);
                    }

                    return _root;
                }
            }

            public float FeetBelowRoot => 0f;

            public float ClearanceRadius => StandInClearanceRadius;

            public float ClearanceHeight => StandInClearanceHeight;

            public bool CanBoard => _driver != null && _driver.enabled && _driver.gameObject.activeInHierarchy;

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
