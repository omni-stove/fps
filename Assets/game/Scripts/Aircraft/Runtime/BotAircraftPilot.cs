using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 舵の値を動き方 (IVehicleMotor) へ直に渡さず操縦席を通すのは、人が操縦するときと同じく、座席の受け手 (VehicleMotorLink や武器) を通り、
    // 操縦席に座っていなければ飛ばせないようにするため。
    // 座席の入力 (VehicleSeatInput) にはピッチ・ロール・ヨー・スロットルの欄が無いので、操縦している間はこの部品が当て方 (IVehicleControlScheme) を受け持ち、
    // 座席から届いた入力の代わりに AircraftAutopilot の値を返す。
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Vehicle))]
    public sealed class BotAircraftPilot : MonoBehaviour, IVehicleRider, IVehicleControlScheme, IBotPilot
    {
        public const string LogTag = "[BotAircraftPilot]";

        private const int MaxGroundHits = 8;
        private const string StandInName = "Bot Pilot";
        private const float StandInClearanceRadius = 0.4f;
        private const float StandInClearanceHeight = 1.8f;

        private static readonly RaycastHit[] _groundBuffer = new RaycastHit[MaxGroundHits];

        [SerializeField, Tooltip("飛ばし方の値 (Create > Shooter > Bot Aircraft Pilot Settings)。空なら既定値で飛ぶ")]
        private BotAircraftPilotSettings _settings;

        [SerializeField, Tooltip("回る点の並び。最後の点に着いたら最初の点へ戻る。点どうしは、傾け切って回る輪の直径 (巡航の速さで 600 m ほど) より離す")]
        private Transform[] _waypoints = Array.Empty<Transform>();

        [SerializeField, Tooltip("オンなら点ごとの高さをなぞる。オフなら置いたときの高さを保ち、点の高さは見ない")]
        private bool _followWaypointHeight;

        [SerializeField, Tooltip("地面の近さを測るために、真下へレイを撃つレイヤー")]
        private LayerMask _groundMask = Physics.DefaultRaycastLayers;

        private AircraftAutopilotSpec _configuredSpec;
        private AircraftAutopilotSpec _settingsSpec;
        private Vehicle _vehicle;
        private VehicleMotorLink _link;
        private IVehicleControlScheme _previousScheme;
        private Transform _standIn;
        private VehicleSeat _seat;
        private IVehicleRider _rider;
        private int _waypointIndex;
        private float _holdAltitude;
        private bool _hasHoldAltitude;
        private bool _wantsSeat = true;
        private bool _warnedNoWaypoints;
        private bool _warnedNoMotor;
        private bool _warnedNoLink;
        private bool _warnedNoDriverSeat;
        private VehicleMotorInput _lastInput;

        public BotAircraftPilotSettings Settings
        {
            get => _settings;
            set
            {
                _settings = value;
                _configuredSpec = null;
                _settingsSpec = null;
            }
        }

        // Configure で渡した値をアセットより優先するのは、テストやコードから組んだ Bot がアセット無しでも値を差し替えられるようにするため。
        public AircraftAutopilotSpec Spec => _configuredSpec ?? (_settingsSpec ??= BuildSpec());

        public Vehicle Vehicle => _vehicle != null ? _vehicle : (_vehicle = GetComponent<Vehicle>());

        public IReadOnlyList<Transform> Waypoints => _waypoints;

        public int WaypointIndex => _waypointIndex;

        public bool FollowWaypointHeight
        {
            get => _followWaypointHeight;
            set => _followWaypointHeight = value;
        }

        public LayerMask GroundMask
        {
            get => _groundMask;
            set => _groundMask = value;
        }

        // 置いたときの高さを既定にするのは、空中に置いた機体へ付けるだけで、その高さを保って回れるようにするため。
        public float HoldAltitude
        {
            get => _hasHoldAltitude ? _holdAltitude : transform.position.y;
            set
            {
                if (!float.IsFinite(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "hold altitude must be finite");
                }

                _holdAltitude = value;
                _hasHoldAltitude = true;
            }
        }

        // 空のときは自分が操縦席に座るのは、試走場などで機体に付けるだけで飛ばせるようにするため。
        // 敵や Bot が自分で座って操縦するときは、その乗り手を入れる。座っているかどうかは入れた側が受け持つ。
        public IVehicleRider Rider
        {
            get => IsAssigned(_rider) ? _rider : this;
            set
            {
                var next = ReferenceEquals(value, this) ? null : value;
                if (ReferenceEquals(next, _rider))
                {
                    return;
                }

                if (IsAssigned(next))
                {
                    LeaveOwnSeat();
                }

                _rider = next;
                _wantsSeat = !IsAssigned(next);
            }
        }

        public VehicleMotorInput LastInput => _lastInput;

        public bool IsFlying => _link != null && ReferenceEquals(_link.ControlScheme, this);

        string IVehicleRider.RiderName => name;

        Transform IVehicleRider.RiderRoot => StandIn;

        float IVehicleRider.FeetBelowRoot => 0f;

        float IVehicleRider.ClearanceRadius => StandInClearanceRadius;

        float IVehicleRider.ClearanceHeight => StandInClearanceHeight;

        // isActiveAndEnabled で見ないのは、OnEnable の走らない Edit Mode や Awake の途中では、有効でも false になるため。
        bool IVehicleRider.CanBoard => this != null && enabled && gameObject.activeInHierarchy;

        VehicleSeat IVehicleRider.Seat => _seat;

        // 乗り手の根を機体そのものにしないのは、乗るときに乗り手と機体の当たりを切るため、機体の当たり同士まで切ってしまうため。
        private Transform StandIn
        {
            get
            {
                if (_standIn == null)
                {
                    _standIn = new GameObject(StandInName).transform;
                    _standIn.SetParent(transform, false);
                }

                return _standIn;
            }
        }

        private void Awake()
        {
            if (!_hasHoldAltitude)
            {
                HoldAltitude = transform.position.y;
            }
        }

        private void OnEnable()
        {
            _wantsSeat = !IsAssigned(_rider);
        }

        private void OnDisable()
        {
            Release();
        }

        // 遅らせて消すのは、機体ごと消えている最中に子をすぐ消すと、Unity が消している途中の物を二重に消そうとするため。
        private void OnDestroy()
        {
            if (_standIn != null && Application.isPlaying)
            {
                Destroy(_standIn.gameObject);
            }
        }

        private void OnValidate()
        {
            _settingsSpec = null;
        }

        private void FixedUpdate()
        {
            Tick(Time.fixedDeltaTime);
        }

        public void Configure(AircraftAutopilotSpec spec)
        {
            _configuredSpec = spec ?? throw new ArgumentNullException(nameof(spec));
        }

        public void SetWaypoints(params Transform[] waypoints)
        {
            _waypoints = waypoints != null ? (Transform[])waypoints.Clone() : Array.Empty<Transform>();
            _waypointIndex = 0;
            _warnedNoWaypoints = false;
        }

        // FixedUpdate から呼ぶ。物理の進まない Edit Mode のテストからも直接呼べるよう public にする。
        public void Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            var vehicle = Vehicle;
            if (vehicle == null || vehicle.IsWrecked)
            {
                return;
            }

            if (!TakeControls())
            {
                return;
            }

            // 座り直すのを有効になったときの 1 度だけにするのは、降ろされたり座席を取られたりしたときに、毎ステップ乗り込みを断られて記録が流れ続けないようにするため。
            if (_wantsSeat && !IsAssigned(_rider))
            {
                _wantsSeat = false;
                BoardOwnSeat(vehicle);
            }

            var rider = Rider;
            var seat = rider.Seat;
            if (seat == null || seat.Vehicle != vehicle || seat.Role != VehicleSeatRole.Driver)
            {
                return;
            }

            AdvanceWaypoint();
            seat.SubmitInput(rider, VehicleSeatInput.Idle, deltaSeconds);
        }

        // 操縦をやめて、座っていた操縦席と、受け持っていた当て方を返す。OnDisable の走らない Edit Mode のテストからも呼べるよう public にする。
        public void Release()
        {
            LeaveOwnSeat();
            if (_link != null && ReferenceEquals(_link.ControlScheme, this))
            {
                _link.ControlScheme = _previousScheme;
            }

            _previousScheme = null;
            _wantsSeat = !IsAssigned(_rider);
        }

        public VehicleMotorInput ToMotorInput(VehicleSeat seat, VehicleSeatInput input, float deltaSeconds)
        {
            _lastInput = Steer();
            return _lastInput;
        }

        void IVehicleRider.OnBoarded(VehicleSeat seat)
        {
            _seat = seat;
        }

        void IVehicleRider.OnSeatChanged(VehicleSeat from, VehicleSeat to)
        {
            _seat = to;
        }

        // 代わりの乗り手を降りる場所へ動かさないのは、機体の子として付いたまま、次に座るときも機体から乗れるようにするため。
        void IVehicleRider.OnLeft(VehicleSeat seat, VehicleLeaveReason reason, Vector3 feetPosition, Quaternion rotation)
        {
            _seat = null;
        }

        private bool TakeControls()
        {
            if (_link == null)
            {
                _link = GetComponent<VehicleMotorLink>();
            }

            if (_link == null)
            {
                if (!_warnedNoLink)
                {
                    _warnedNoLink = true;
                    Debug.LogWarning(BotAircraftPilotLog.NoMotorLink(name), this);
                }

                return false;
            }

            if (!ReferenceEquals(_link.ControlScheme, this))
            {
                _previousScheme = _link.ControlScheme;
                _link.ControlScheme = this;
            }

            return true;
        }

        private void BoardOwnSeat(Vehicle vehicle)
        {
            if (_seat != null)
            {
                return;
            }

            var seats = vehicle.Seats;
            for (var i = 0; i < seats.Count; i++)
            {
                if (seats[i] != null && seats[i].Role == VehicleSeatRole.Driver)
                {
                    // 乗り込める距離を見ないのは、代わりの乗り手が機体の中心にいて、操縦席の乗り口から離れていることがあるため。
                    vehicle.TryEnter(this, i, requireInRange: false);
                    return;
                }
            }

            if (!_warnedNoDriverSeat)
            {
                _warnedNoDriverSeat = true;
                Debug.LogWarning(BotAircraftPilotLog.NoDriverSeat(name), this);
            }
        }

        private void LeaveOwnSeat()
        {
            if (_seat == null)
            {
                return;
            }

            var vehicle = _seat.Vehicle;
            if (vehicle != null)
            {
                vehicle.ForceExit(this, VehicleLeaveReason.Forced);
            }

            _seat = null;
        }

        private void AdvanceWaypoint()
        {
            var count = _waypoints.Length;
            for (var step = 0; step < count; step++)
            {
                var current = Waypoint(_waypointIndex);
                if (current != null && !AircraftAutopilot.HasArrived(Spec, transform.position.ToNumerics(), current.position.ToNumerics()))
                {
                    return;
                }

                _waypointIndex = (_waypointIndex + 1) % count;
                if (current != null)
                {
                    return;
                }
            }
        }

        private Transform Waypoint(int index)
        {
            return index >= 0 && index < _waypoints.Length ? _waypoints[index] : null;
        }

        private VehicleMotorInput Steer()
        {
            if (!TryGetComponent<SimpleAircraftMotor>(out var motor))
            {
                if (!_warnedNoMotor)
                {
                    _warnedNoMotor = true;
                    Debug.LogWarning(BotAircraftPilotLog.NoMotor(name), this);
                }

                return VehicleMotorInput.Idle;
            }

            var spec = Spec;
            var velocity = motor.Body.linearVelocity;
            var position = transform.position;
            var situation = new AircraftAutopilotSituation(
                position.ToNumerics(),
                transform.forward.ToNumerics(),
                transform.up.ToNumerics(),
                velocity.ToNumerics(),
                motor.State,
                GroundDistance(spec, velocity.y),
                Physics.gravity.magnitude);

            var waypoint = Waypoint(_waypointIndex);
            Vector3 target;
            if (waypoint != null)
            {
                target = waypoint.position;
            }
            else
            {
                if (!_warnedNoWaypoints)
                {
                    _warnedNoWaypoints = true;
                    Debug.LogWarning(BotAircraftPilotLog.NoWaypoints(name), this);
                }

                // 点が無いときに今の向きの先を狙うのは、舵を止めるより、高さと速さを保ったまままっすぐ飛ぶほうが落ちないため。
                target = position + Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            }

            var altitude = _followWaypointHeight && waypoint != null ? target.y : HoldAltitude;
            return AircraftAutopilot.Compute(spec, situation, target.ToNumerics(), altitude);
        }

        // 地面に近いかの判定に届く距離までしか測らないのは、それより遠い地面は上昇の判断に関わらず、長いレイを毎回撃たずに済むため。
        private float GroundDistance(AircraftAutopilotSpec spec, float climbRate)
        {
            var reach = spec.MinGroundClearance + (Mathf.Max(0f, -climbRate) * spec.GroundLookAheadSeconds);
            if (!(reach > 0f))
            {
                return float.PositiveInfinity;
            }

            var count = Physics.RaycastNonAlloc(transform.position, Vector3.down, _groundBuffer, reach, _groundMask, QueryTriggerInteraction.Ignore);
            var nearest = float.PositiveInfinity;
            for (var i = 0; i < count; i++)
            {
                var hit = _groundBuffer[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform) || hit.distance >= nearest)
                {
                    continue;
                }

                nearest = hit.distance;
            }

            Array.Clear(_groundBuffer, 0, count);
            return nearest;
        }

        private AircraftAutopilotSpec BuildSpec()
        {
            return _settings != null ? _settings.ToSpec() : AircraftAutopilotSpec.Default;
        }

        // interface への null 判定は破棄済みの MonoBehaviour を見逃すので、Unity の null で見る。
        private static bool IsAssigned(IVehicleRider rider)
        {
            return rider is UnityEngine.Object unityRider ? unityRider != null : rider != null;
        }

        private void OnDrawGizmosSelected()
        {
            if (_waypoints == null || _waypoints.Length == 0)
            {
                return;
            }

            Gizmos.color = Color.cyan;
            for (var i = 0; i < _waypoints.Length; i++)
            {
                var from = _waypoints[i];
                var to = _waypoints[(i + 1) % _waypoints.Length];
                if (from != null && to != null)
                {
                    Gizmos.DrawLine(from.position, to.position);
                }
            }
        }
    }
}
