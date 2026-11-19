using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 動き方・武器は持たず座席の入力を IVehicleInputSink に流すだけにするのは、動き方の無い乗り物 (固定銃座など) も同じ土台で済ませるため。
    [DisallowMultipleComponent]
    public sealed partial class Vehicle : MonoBehaviour
    {
        public const string LogTag = VehicleLog.Tag;

        // 降りる位置の候補を置かなくても降りられるよう座席の横・後ろを試すのは、仮の乗り物を手早く置けるようにするため。
        public const float FallbackExitDistance = 2f;

        public const float HeightProbeDistance = 1000f;

        // 降りる場所のカプセルを床から浮かせる高さ。床そのものを塞がりと数えないため。
        private const float ExitGroundSkin = 0.05f;

        // 地面に少しめり込んでいても Ray の始点が地面の中に入って地面を外さないよう、当たり判定の底より上から飛ばす。
        private const float HeightProbeLift = 0.25f;

        private const int MaxHeightProbeHits = 16;

        // 降りる位置は乗り物の原点の高さに置かれるので、サスペンションで車体が沈むと地面の下になり、床を塞がりと数えてしまう。
        // この幅の中に地面があれば、その上に足を置き直してから塞がりを調べる。
        private const float ExitGroundSnap = 0.5f;

        private static readonly List<Vehicle> _active = new List<Vehicle>();
        private static readonly Collider[] _overlapBuffer = new Collider[32];
        private static readonly RaycastHit[] _heightProbeBuffer = new RaycastHit[MaxHeightProbeHits];

        [SerializeField, Tooltip("乗り物の値 (Id・耐久・乗り込める距離・追うカメラ)。空ならこの下の値を使う")]
        private VehicleDefinition _definition;

        [SerializeField, Tooltip("乗り物の Id")]
        private string _vehicleId = VehicleSpec.DefaultId;

        [SerializeField, Min(1), Tooltip("耐久")]
        private int _maxHealth = VehicleSpec.DefaultMaxHealth;

        [SerializeField, Min(0.01f), Tooltip("座席の乗る位置からこの距離 (m) までなら乗り込める")]
        private float _enterRange = VehicleSpec.DefaultEnterRange;

        [SerializeField, Min(0f), Tooltip("プレイヤーが乗り込みを始めてから座席に着くまでの秒数。0 ならすぐ座る")]
        private float _enterSeconds = VehicleSpec.DefaultEnterSeconds;

        [SerializeField, Min(0f), Tooltip("プレイヤーが降りると決めてから降りるまでの秒数。0 ならすぐ降りる")]
        private float _exitSeconds = VehicleSpec.DefaultExitSeconds;

        [SerializeField, Min(0f), Tooltip("追うカメラを注視点からどれだけ離すか (m)")]
        private float _chaseDistance = VehicleSpec.DefaultChaseDistance;

        [SerializeField, Tooltip("追うカメラの注視点を、乗り物の原点からどれだけ上に置くか (m)")]
        private float _chaseHeight = VehicleSpec.DefaultChaseHeight;

        [SerializeField, Tooltip("どの座席からでも使う降りる位置の候補 (足元)。座席の候補の後に試す")]
        private Transform[] _exitPoints = Array.Empty<Transform>();

        [SerializeField, Tooltip("降りる場所を塞いでいるとみなすレイヤー")]
        private LayerMask _exitBlockMask = Physics.DefaultRaycastLayers;

        [SerializeField, Tooltip("空中で降りたとき・壊れたときの脱出。定義があればその値を使う")]
        private BailOutSettings _bailOut = new BailOutSettings();

        [SerializeField, Tooltip("脱出するかを決める高さを測るときに、地面として当たるレイヤー")]
        private LayerMask _groundMask = Physics.DefaultRaycastLayers;

        private readonly List<IVehicleInputSink> _sinks = new List<IVehicleInputSink>();
        private readonly Dictionary<IVehicleRider, IgnoredCollisions> _ignored = new Dictionary<IVehicleRider, IgnoredCollisions>();

        private VehicleSeat[] _seats;
        private VehicleWeaponMount[] _mounts = Array.Empty<VehicleWeaponMount>();
        private VehicleSeating _seating;
        private VehicleSpec _spec;
        private Damageable _damageable;
        private bool _initialized;
        private bool _wrecked;

        public event Action<Vehicle, VehicleSeat, IVehicleRider> Boarded;

        public event Action<Vehicle, IVehicleRider, VehicleSeat, VehicleSeat> SeatChanged;

        public event Action<Vehicle, VehicleSeat, IVehicleRider, VehicleLeaveReason> Left;

        public event Action<Vehicle> Wrecked;

        public static IReadOnlyList<Vehicle> Active
        {
            get
            {
                _active.RemoveAll(vehicle => vehicle == null || !vehicle.IsLive);
                return _active;
            }
        }

        public IReadOnlyList<VehicleSeat> Seats
        {
            get
            {
                EnsureInitialized();
                return _seats;
            }
        }

        public IReadOnlyList<VehicleWeaponMount> WeaponMounts
        {
            get
            {
                EnsureInitialized();
                return _mounts;
            }
        }

        public VehicleSpec Spec
        {
            get
            {
                EnsureInitialized();
                return _spec ??= BuildSpec();
            }
        }

        public VehicleDefinition Definition => _definition;

        public string VehicleId => Spec.Id;

        public bool IsWrecked => _wrecked;

        // OnEnable が走らない Edit Mode でも有効かを判断できるよう、isActiveAndEnabled ではなく自分で見る。
        internal bool IsLive => enabled && gameObject.activeInHierarchy;

        public int RiderCount
        {
            get
            {
                EnsureInitialized();
                return _seating != null ? CountLiveRiders() : 0;
            }
        }

        public LayerMask ExitBlockMask
        {
            get => _exitBlockMask;
            set => _exitBlockMask = value;
        }

        public IReadOnlyList<Transform> ExitPoints => _exitPoints;

        public LayerMask GroundMask
        {
            get => _groundMask;
            set => _groundMask = value;
        }

        public Vector3 Velocity
        {
            get
            {
                var body = GetComponent<Rigidbody>();
                return body != null ? body.linearVelocity : Vector3.zero;
            }
        }

        public Vector3 ChaseFocus => transform.position + (Vector3.up * Spec.ChaseHeight);

        private void Awake()
        {
            EnsureInitialized();
            if (_seats.Length == 0)
            {
                Debug.LogError($"{LogTag} '{name}' の子に VehicleSeat がありません。座席を 1 つ以上置いてください。", this);
                enabled = false;
                return;
            }

            foreach (var mount in _mounts)
            {
                if (!mount.HasWeapon)
                {
                    Debug.LogError($"{LogTag} '{name}' の武器 '{mount.name}' に WeaponDefinition がありません。", mount);
                    enabled = false;
                    return;
                }
            }

            if (_definition != null)
            {
                Configure(Spec);
            }
            else
            {
                ConfigureDurability(Spec);
            }
        }

        private void OnEnable()
        {
            if (!_active.Contains(this))
            {
                _active.Add(this);
            }
        }

        private void OnDisable()
        {
            _active.Remove(this);
            EjectAll(VehicleLeaveReason.Disabled);
        }

        private void OnDestroy()
        {
            UnbindDamageable();
        }

        private void Update()
        {
            PruneRemovedRiders();
            TickDurability(Time.deltaTime);
        }

        public void Configure(VehicleSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            EnsureInitialized();
            if (spec.Seats.Count != _seats.Length)
            {
                throw new ArgumentException(
                    $"spec has {spec.Seats.Count} seats but '{name}' has {_seats.Length} VehicleSeat children", nameof(spec));
            }

            RequireWeaponCount(spec);
            for (var i = 0; i < _mounts.Length; i++)
            {
                if (!_mounts[i].HasWeapon || !string.Equals(_mounts[i].WeaponSpec.Id, spec.Weapons[i].WeaponId, StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        $"weapon {i} of '{name}' is not '{spec.Weapons[i].WeaponId}'; pass a WeaponCatalog to change the weapon", nameof(spec));
                }
            }

            ApplySpec(spec, null);
        }

        // 武器の Id を変える設定は、その武器の値が要るので catalog から引く。
        public void Configure(VehicleSpec spec, WeaponCatalog weapons)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (weapons == null)
            {
                throw new ArgumentNullException(nameof(weapons));
            }

            EnsureInitialized();
            if (spec.Seats.Count != _seats.Length)
            {
                throw new ArgumentException(
                    $"spec has {spec.Seats.Count} seats but '{name}' has {_seats.Length} VehicleSeat children", nameof(spec));
            }

            RequireWeaponCount(spec);
            var resolved = new WeaponSpec[_mounts.Length];
            for (var i = 0; i < resolved.Length; i++)
            {
                if (!weapons.TryGet(spec.Weapons[i].WeaponId, out resolved[i]))
                {
                    throw new ArgumentException($"weapon id '{spec.Weapons[i].WeaponId}' is not in the catalog", nameof(weapons));
                }
            }

            ApplySpec(spec, resolved);
        }

        private void RequireWeaponCount(VehicleSpec spec)
        {
            if (spec.Weapons.Count != _mounts.Length)
            {
                throw new ArgumentException(
                    $"spec has {spec.Weapons.Count} weapons but '{name}' has {_mounts.Length} VehicleWeaponMount children", nameof(spec));
            }
        }

        private void ApplySpec(VehicleSpec spec, WeaponSpec[] weapons)
        {
            for (var i = 0; i < _mounts.Length; i++)
            {
                if (weapons != null)
                {
                    _mounts[i].ApplySpec(spec.Weapons[i], weapons[i]);
                }
                else
                {
                    _mounts[i].ApplySpec(spec.Weapons[i]);
                }
            }

            for (var i = 0; i < _seats.Length; i++)
            {
                _seats[i].ApplySpec(spec.Seats[i]);
            }

            _vehicleId = spec.Id;
            _maxHealth = spec.MaxHealth;
            _enterRange = spec.EnterRange;
            _enterSeconds = spec.EnterSeconds;
            _exitSeconds = spec.ExitSeconds;
            _chaseDistance = spec.ChaseDistance;
            _chaseHeight = spec.ChaseHeight;
            (_bailOut ??= new BailOutSettings()).CopyFrom(spec.BailOut);
            _spec = spec;

            ConfigureDurability(spec);
            if (_damageable != null)
            {
                _damageable.MaxHealth = spec.MaxHealth;
                _damageable.ResetHealth();
            }
        }

        public void Configure(VehicleDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            EnsureInitialized();
            var spec = definition.ToSpec(SeatSpecs(), WeaponSpecs());
            _definition = definition;
            Configure(spec);
        }

        public void RefreshSeats()
        {
            if (_seating != null && CountLiveRiders() > 0)
            {
                throw new InvalidOperationException($"cannot refresh the seats of '{name}' while someone is aboard");
            }

            EnsureInitialized();
            CollectSeats();
        }

        public void SetExitPoints(params Transform[] points)
        {
            var list = new List<Transform>();
            if (points != null)
            {
                foreach (var point in points)
                {
                    if (point != null)
                    {
                        list.Add(point);
                    }
                }
            }

            _exitPoints = list.ToArray();
        }

        public IReadOnlyList<VehicleWeaponMount> WeaponMountsOf(int seatIndex)
        {
            EnsureInitialized();
            RequireSeatIndex(seatIndex);
            var result = new List<VehicleWeaponMount>();
            foreach (var index in Spec.WeaponsOfSeat(seatIndex))
            {
                result.Add(_mounts[index]);
            }

            return result;
        }

        public VehicleSeatFiring FiringOf(VehicleSeat seat)
        {
            if (seat == null)
            {
                throw new ArgumentNullException(nameof(seat));
            }

            if (seat.Vehicle != this)
            {
                throw new ArgumentException($"seat '{seat.name}' is not on '{name}'", nameof(seat));
            }

            return Spec.FiringOf(seat.Index);
        }

        public IVehicleRider OccupantOf(int index)
        {
            EnsureInitialized();
            if (_seating == null)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, $"'{name}' has no seats");
            }

            var rider = (IVehicleRider)_seating.OccupantOf(index);
            return IsRemoved(rider) ? null : rider;
        }

        public VehicleSeat SeatOf(IVehicleRider rider)
        {
            if (rider == null)
            {
                throw new ArgumentNullException(nameof(rider));
            }

            EnsureInitialized();
            if (_seating == null)
            {
                return null;
            }

            var index = _seating.SeatOf(rider);
            return index >= 0 ? _seats[index] : null;
        }

        public int FindBoardableSeat(Vector3 from, out float distance)
        {
            EnsureInitialized();
            distance = float.PositiveInfinity;
            if (_seating == null)
            {
                return -1;
            }

            PruneRemovedRiders();
            var distances = new float[_seats.Length];
            for (var i = 0; i < _seats.Length; i++)
            {
                // NearestFree は NaN の座席を選ばないので、破棄された座席はここで外す。
                if (_seats[i] == null)
                {
                    distances[i] = float.NaN;
                    continue;
                }

                distances[i] = _seats[i].DistanceToEnter(from);
                distance = Mathf.Min(distance, distances[i]);
            }

            if (_wrecked)
            {
                return -1;
            }

            var seat = _seating.NearestFree(distances, Spec.EnterRange);
            if (seat >= 0)
            {
                distance = distances[seat];
            }

            return seat;
        }

        public bool TryEnter(IVehicleRider rider)
        {
            if (!CanEnter(rider))
            {
                return false;
            }

            var seat = FindBoardableSeat(rider.RiderRoot.position, out var distance);
            if (seat < 0)
            {
                var reason = _seating.OccupiedCount >= _seating.SeatCount || distance <= Spec.EnterRange
                    ? VehicleLog.ReasonNoFreeSeat
                    : VehicleLog.ReasonOutOfRange;
                Debug.Log(VehicleLog.BoardRefused(name, rider.RiderName, reason), this);
                return false;
            }

            Board(rider, seat);
            return true;
        }

        public bool TryEnter(IVehicleRider rider, int seatIndex, bool requireInRange = true)
        {
            if (!CanEnter(rider))
            {
                return false;
            }

            RequireSeatIndex(seatIndex);
            if (_seats[seatIndex] == null)
            {
                Debug.Log(VehicleLog.BoardRefused(name, rider.RiderName, VehicleLog.ReasonSeatRemoved), this);
                return false;
            }

            if (_seating.IsOccupied(seatIndex))
            {
                Debug.Log(VehicleLog.BoardRefused(name, rider.RiderName, VehicleLog.ReasonSeatTaken), this);
                return false;
            }

            if (requireInRange && _seats[seatIndex].DistanceToEnter(rider.RiderRoot.position) > Spec.EnterRange)
            {
                Debug.Log(VehicleLog.BoardRefused(name, rider.RiderName, VehicleLog.ReasonOutOfRange), this);
                return false;
            }

            Board(rider, seatIndex);
            return true;
        }

        public bool TryExit(IVehicleRider rider)
        {
            var seat = RequireRiding(rider);
            if (seat == null)
            {
                return false;
            }

            if (!TryFindExit(seat, rider, out var feet, out var rotation, out var exitIndex))
            {
                Debug.Log(VehicleLog.LeaveRefused(name, rider.RiderName, VehicleLog.ReasonExitBlocked), this);
                LogExitBlockers(seat, rider);
                return false;
            }

            Leave(rider, seat, VehicleLeaveReason.Request, feet, rotation, exitIndex);
            return true;
        }

        public bool ForceExit(IVehicleRider rider, VehicleLeaveReason reason)
        {
            if (!Enum.IsDefined(typeof(VehicleLeaveReason), reason))
            {
                throw new ArgumentOutOfRangeException(nameof(reason), reason, "reason is not a defined VehicleLeaveReason");
            }

            var seat = RequireRiding(rider);
            if (seat == null)
            {
                return false;
            }

            ForceLeave(rider, seat, reason);
            return true;
        }

        // 降りる場所が塞がっていても脱出は断らない。空中で降りられないと、壊れかけの機体から出られなくなるため。
        public bool BailOut(IVehicleRider rider)
        {
            var seat = RequireRiding(rider);
            if (seat == null)
            {
                return false;
            }

            LeaveByBailOut(rider, seat);
            return true;
        }

        public BailOutVerdict EvaluateBailOut()
        {
            return EvaluateBailOut(out _);
        }

        public BailOutVerdict EvaluateBailOut(out float? heightAboveGround)
        {
            var spec = Spec.BailOut;
            heightAboveGround = spec.Enabled ? MeasureHeightAboveGround() : null;
            return spec.Evaluate(heightAboveGround);
        }

        public Vector3 OnGround(Vector3 feetPosition)
        {
            var origin = feetPosition + (Vector3.up * ExitGroundSnap);
            var count = Physics.RaycastNonAlloc(
                origin, Vector3.down, _heightProbeBuffer, ExitGroundSnap * 2f, _groundMask, QueryTriggerInteraction.Ignore);
            var nearest = float.PositiveInfinity;
            try
            {
                for (var i = 0; i < count; i++)
                {
                    var hit = _heightProbeBuffer[i];
                    if (hit.collider == null || hit.collider.transform.IsChildOf(transform) || IsRiderCollider(hit.collider))
                    {
                        continue;
                    }

                    nearest = Mathf.Min(nearest, hit.distance);
                }
            }
            finally
            {
                Array.Clear(_heightProbeBuffer, 0, count);
            }

            return float.IsPositiveInfinity(nearest) ? feetPosition : origin + (Vector3.down * nearest);
        }

        public float? MeasureHeightAboveGround()
        {
            EnsureInitialized();
            var bottom = transform.position;
            if (TryGetColliderBounds(out var bounds))
            {
                bottom = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            }

            var origin = bottom + (Vector3.up * HeightProbeLift);
            var count = Physics.RaycastNonAlloc(
                origin, Vector3.down, _heightProbeBuffer, HeightProbeDistance + HeightProbeLift, _groundMask, QueryTriggerInteraction.Ignore);
            var nearest = float.PositiveInfinity;
            try
            {
                for (var i = 0; i < count; i++)
                {
                    var hit = _heightProbeBuffer[i];
                    if (hit.collider == null || hit.collider.transform.IsChildOf(transform) || IsRiderCollider(hit.collider))
                    {
                        continue;
                    }

                    nearest = Mathf.Min(nearest, hit.distance);
                }
            }
            finally
            {
                Array.Clear(_heightProbeBuffer, 0, count);
            }

            return float.IsPositiveInfinity(nearest) ? (float?)null : Mathf.Max(0f, nearest - HeightProbeLift);
        }

        public bool TrySwitchSeat(IVehicleRider rider, int direction)
        {
            if (direction != 1 && direction != -1)
            {
                throw new ArgumentOutOfRangeException(nameof(direction), direction, "direction must be 1 or -1");
            }

            var from = RequireRiding(rider);
            if (from == null)
            {
                return false;
            }

            var to = NextLiveFreeSeat(from.Index, direction);
            if (to < 0)
            {
                Debug.Log(VehicleLog.SwitchRefused(name, rider.RiderName, VehicleLog.ReasonNoFreeSeat), this);
                return false;
            }

            MoveSeat(rider, from, _seats[to]);
            return true;
        }

        public bool TrySwitchToSeat(IVehicleRider rider, int seatIndex)
        {
            var from = RequireRiding(rider);
            if (from == null)
            {
                return false;
            }

            RequireSeatIndex(seatIndex);
            if (seatIndex == from.Index)
            {
                return false;
            }

            if (_seats[seatIndex] == null)
            {
                Debug.Log(VehicleLog.SwitchRefused(name, rider.RiderName, VehicleLog.ReasonSeatRemoved), this);
                return false;
            }

            if (_seating.IsOccupied(seatIndex))
            {
                Debug.Log(VehicleLog.SwitchRefused(name, rider.RiderName, VehicleLog.ReasonSeatTaken), this);
                return false;
            }

            MoveSeat(rider, from, _seats[seatIndex]);
            return true;
        }

        public void EjectAll(VehicleLeaveReason reason)
        {
            if (_seating == null)
            {
                return;
            }

            for (var i = 0; i < _seats.Length; i++)
            {
                var rider = (IVehicleRider)_seating.OccupantOf(i);
                if (rider == null)
                {
                    continue;
                }

                if (_seats[i] == null)
                {
                    RemoveFromDestroyedSeat(rider, i);
                }
                else if (IsRemoved(rider))
                {
                    RemoveRider(rider, _seats[i]);
                }
                else
                {
                    ForceLeave(rider, _seats[i], reason);
                }
            }
        }

        public bool Wreck()
        {
            EnsureInitialized();
            if (_wrecked)
            {
                return false;
            }

            _wrecked = true;
            Debug.Log(VehicleLog.Wrecked(name, _seating != null ? CountLiveRiders() : 0), this);
            EjectAll(VehicleLeaveReason.Wrecked);
            Wrecked?.Invoke(this);
            return true;
        }

        public bool AddInputSink(IVehicleInputSink sink)
        {
            if (sink == null)
            {
                throw new ArgumentNullException(nameof(sink));
            }

            if (_sinks.Contains(sink))
            {
                return false;
            }

            _sinks.Add(sink);
            return true;
        }

        public bool RemoveInputSink(IVehicleInputSink sink)
        {
            if (sink == null)
            {
                throw new ArgumentNullException(nameof(sink));
            }

            return _sinks.Remove(sink);
        }

        public bool TryFindExit(VehicleSeat seat, IVehicleRider rider, out Vector3 feetPosition, out Quaternion rotation, out int candidateIndex)
        {
            if (seat == null)
            {
                throw new ArgumentNullException(nameof(seat));
            }

            if (rider == null)
            {
                throw new ArgumentNullException(nameof(rider));
            }

            var candidates = ExitCandidates(seat);
            for (var i = 0; i < candidates.Count; i++)
            {
                var feet = OnGround(candidates[i].Position);
                if (!IsExitBlocked(feet, rider))
                {
                    feetPosition = feet;
                    rotation = candidates[i].Rotation;
                    candidateIndex = i;
                    return true;
                }
            }

            feetPosition = default;
            rotation = Quaternion.identity;
            candidateIndex = -1;
            return false;
        }

        public bool IsExitBlocked(Vector3 feetPosition, IVehicleRider rider)
        {
            return FindExitBlocker(feetPosition, rider) != null;
        }

        public Collider FindExitBlocker(Vector3 feetPosition, IVehicleRider rider)
        {
            if (rider == null)
            {
                throw new ArgumentNullException(nameof(rider));
            }

            var radius = Mathf.Max(0.01f, rider.ClearanceRadius);
            var height = Mathf.Max(rider.ClearanceHeight, radius * 2f);
            var bottom = feetPosition + (Vector3.up * (radius + ExitGroundSkin));
            var top = feetPosition + (Vector3.up * Mathf.Max(radius + ExitGroundSkin, height - radius));
            var count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, _overlapBuffer, _exitBlockMask, QueryTriggerInteraction.Ignore);
            var riderRoot = rider.RiderRoot;
            try
            {
                for (var i = 0; i < count; i++)
                {
                    var hit = _overlapBuffer[i];
                    if (hit == null || hit.transform.IsChildOf(transform) || (riderRoot != null && hit.transform.IsChildOf(riderRoot)))
                    {
                        continue;
                    }

                    return hit;
                }

                return null;
            }
            finally
            {
                Array.Clear(_overlapBuffer, 0, count);
            }
        }

        internal void DispatchInput(VehicleSeat seat, VehicleSeatInput input, float deltaSeconds)
        {
            // 受け手が自分の登録を外しても回り続けられるよう、写しで回す。
            foreach (var sink in _sinks.ToArray())
            {
                sink.OnSeatInput(seat, input, deltaSeconds);
            }
        }

        private bool CanEnter(IVehicleRider rider)
        {
            if (rider == null)
            {
                throw new ArgumentNullException(nameof(rider));
            }

            if (IsRemoved(rider) || rider.RiderRoot == null)
            {
                throw new ArgumentException("rider has been destroyed", nameof(rider));
            }

            EnsureInitialized();
            if (_seating == null)
            {
                Debug.LogWarning($"{LogTag} '{name}' に座席が無いので、{rider.RiderName} は乗れません。", this);
                return false;
            }

            PruneRemovedRiders();
            string reason = null;
            if (_wrecked)
            {
                reason = VehicleLog.ReasonWrecked;
            }
            else if (rider.Seat != null)
            {
                reason = VehicleLog.ReasonAlreadyRiding;
            }
            else if (!rider.CanBoard)
            {
                reason = VehicleLog.ReasonRiderUnavailable;
            }

            if (reason == null)
            {
                return true;
            }

            Debug.Log(VehicleLog.BoardRefused(name, rider.RiderName, reason), this);
            return false;
        }

        private void Board(IVehicleRider rider, int index)
        {
            if (!_seating.Board(rider, index))
            {
                return;
            }

            var seat = _seats[index];
            IgnoreCollisions(rider);
            Debug.Log(VehicleLog.Board(name, rider.RiderName, index, seat.Role), this);
            rider.OnBoarded(seat);
            Boarded?.Invoke(this, seat, rider);
        }

        private int NextLiveFreeSeat(int from, int direction)
        {
            var seat = from;
            for (var step = 0; step < _seats.Length; step++)
            {
                seat = _seating.NextFree(seat, direction);
                if (seat < 0 || _seats[seat] != null)
                {
                    return seat;
                }
            }

            return -1;
        }

        private void MoveSeat(IVehicleRider rider, VehicleSeat from, VehicleSeat to)
        {
            _seating.MoveTo(rider, to.Index);
            Debug.Log(VehicleLog.Switch(name, rider.RiderName, from.Index, to.Index, to.Role), this);
            NotifyVacated(from);
            rider.OnSeatChanged(from, to);
            SeatChanged?.Invoke(this, rider, from, to);
        }

        private void ForceLeave(IVehicleRider rider, VehicleSeat seat, VehicleLeaveReason reason)
        {
            if (reason == VehicleLeaveReason.BailOut
                || (reason == VehicleLeaveReason.Wrecked && EvaluateBailOut() == BailOutVerdict.BailOut))
            {
                LeaveByBailOut(rider, seat);
                return;
            }

            if (TryFindExit(seat, rider, out var feet, out var rotation, out var exitIndex))
            {
                Leave(rider, seat, reason, feet, rotation, exitIndex);
                return;
            }

            Leave(rider, seat, reason, AboveVehicle(seat), YawOnly(transform.rotation), -1);
        }

        // 座席の降りる位置は地上で降りる前提で置かれているので使わず、乗り物の上から試す。上なら動いている機体の翼や胴にぶつからない。
        private void LeaveByBailOut(IVehicleRider rider, VehicleSeat seat)
        {
            var candidates = BailOutCandidates(seat, rider, Spec.BailOut);
            for (var i = 0; i < candidates.Count; i++)
            {
                if (!IsExitBlocked(candidates[i].Position, rider))
                {
                    Leave(rider, seat, VehicleLeaveReason.BailOut, candidates[i].Position, candidates[i].Rotation, i);
                    return;
                }
            }

            Leave(rider, seat, VehicleLeaveReason.BailOut, candidates[0].Position, candidates[0].Rotation, -1);
        }

        private void Leave(IVehicleRider rider, VehicleSeat seat, VehicleLeaveReason reason, Vector3 feet, Quaternion rotation, int exitIndex)
        {
            _seating.Leave(rider);
            RestoreCollisions(rider);
            Debug.Log(VehicleLog.Leave(name, rider.RiderName, seat.Index, reason, exitIndex), this);
            NotifyVacated(seat);
            rider.OnLeft(seat, reason, feet, rotation);
            Left?.Invoke(this, seat, rider, reason);
        }

        private void RemoveRider(IVehicleRider rider, VehicleSeat seat)
        {
            _seating.Leave(rider);
            RestoreCollisions(rider);
            Debug.Log(VehicleLog.Leave(name, "-", seat.Index, VehicleLeaveReason.RiderRemoved, -1), this);
            NotifyVacated(seat);
            Left?.Invoke(this, seat, rider, VehicleLeaveReason.RiderRemoved);
        }

        private void NotifyVacated(VehicleSeat seat)
        {
            foreach (var sink in _sinks.ToArray())
            {
                sink.OnSeatVacated(seat);
            }
        }

        private VehicleSeat RequireRiding(IVehicleRider rider)
        {
            if (rider == null)
            {
                throw new ArgumentNullException(nameof(rider));
            }

            EnsureInitialized();
            if (_seating == null)
            {
                return null;
            }

            PruneRemovedRiders();
            var index = _seating.SeatOf(rider);
            return index >= 0 ? _seats[index] : null;
        }

        private void RequireSeatIndex(int seatIndex)
        {
            if (seatIndex < 0 || seatIndex >= _seats.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(seatIndex), seatIndex, $"'{name}' has {_seats.Length} seats");
            }
        }

        // 破棄された乗り手は OnLeft を受け取れないので、座席を空けて当たり判定を戻すだけにする。
        private void PruneRemovedRiders()
        {
            if (_seating == null)
            {
                return;
            }

            for (var i = 0; i < _seats.Length; i++)
            {
                var rider = (IVehicleRider)_seating.OccupantOf(i);
                if (rider == null)
                {
                    continue;
                }

                if (_seats[i] == null)
                {
                    RemoveFromDestroyedSeat(rider, i);
                }
                else if (IsRemoved(rider))
                {
                    RemoveRider(rider, _seats[i]);
                }
            }
        }

        // 座席が破棄されると SitPosition などを読めず降り口も探せない。乗り手は座席の破棄を自分で見て後始末するので、ここでは表と当たり判定だけ戻す。
        private void RemoveFromDestroyedSeat(IVehicleRider rider, int index)
        {
            _seating.Leave(rider);
            if (!IsRemoved(rider))
            {
                RestoreCollisions(rider);
            }

            Debug.Log(VehicleLog.Leave(name, "-", index, VehicleLeaveReason.RiderRemoved, -1), this);
        }

        private int CountLiveRiders()
        {
            var count = 0;
            for (var i = 0; i < _seats.Length; i++)
            {
                var rider = (IVehicleRider)_seating.OccupantOf(i);
                if (rider != null && !IsRemoved(rider))
                {
                    count++;
                }
            }

            return count;
        }

        private void LogExitBlockers(VehicleSeat seat, IVehicleRider rider)
        {
            var candidates = ExitCandidates(seat);
            for (var i = 0; i < candidates.Count; i++)
            {
                var blocker = FindExitBlocker(OnGround(candidates[i].Position), rider);
                Debug.Log(VehicleLog.ExitBlockedBy(name, i, blocker != null ? blocker.name : "-"), this);
            }
        }

        private List<ExitCandidate> ExitCandidates(VehicleSeat seat)
        {
            var candidates = new List<ExitCandidate>();
            AddCandidates(candidates, seat.ExitPoints);
            AddCandidates(candidates, _exitPoints);
            if (candidates.Count > 0)
            {
                return candidates;
            }

            var yaw = YawOnly(transform.rotation);
            var right = yaw * Vector3.right;
            var back = yaw * Vector3.back;
            var origin = seat.SitPosition;
            candidates.Add(new ExitCandidate(origin + (right * FallbackExitDistance), yaw));
            candidates.Add(new ExitCandidate(origin - (right * FallbackExitDistance), yaw));
            candidates.Add(new ExitCandidate(origin + (back * FallbackExitDistance), yaw));
            return candidates;
        }

        private static void AddCandidates(List<ExitCandidate> candidates, IReadOnlyList<Transform> points)
        {
            if (points == null)
            {
                return;
            }

            foreach (var point in points)
            {
                if (point != null)
                {
                    candidates.Add(new ExitCandidate(point.position, YawOnly(point.rotation)));
                }
            }
        }

        // 降りる場所がすべて塞がっていても降ろすときの置き場所。乗り物の当たり判定の上に乗せる。
        private Vector3 AboveVehicle(VehicleSeat seat)
        {
            var position = seat.SitPosition;
            if (TryGetColliderBounds(out var bounds))
            {
                position.y = bounds.max.y + ExitGroundSkin;
            }

            return position;
        }

        // 上、座席の側の横、反対の横の順に試す。横は当たり判定の端から離すので、翼の長い機体でも翼の中に出さない。
        private List<ExitCandidate> BailOutCandidates(VehicleSeat seat, IVehicleRider rider, BailOutSpec spec)
        {
            var yaw = YawOnly(transform.rotation);
            var sit = seat.SitPosition;
            var candidates = new List<ExitCandidate>();
            if (!TryGetColliderBounds(out var bounds))
            {
                var right = yaw * Vector3.right;
                var side = FallbackExitDistance + spec.ExitSideDistance;
                candidates.Add(new ExitCandidate(sit + (Vector3.up * spec.ExitHeight), yaw));
                candidates.Add(new ExitCandidate(sit + (right * side), yaw));
                candidates.Add(new ExitCandidate(sit - (right * side), yaw));
                return candidates;
            }

            var above = sit;
            above.y = bounds.max.y + ExitGroundSkin + spec.ExitHeight;
            candidates.Add(new ExitCandidate(above, yaw));

            var toSeat = Vector3.Dot(sit - bounds.center, yaw * Vector3.right);
            var near = toSeat >= 0f ? yaw * Vector3.right : yaw * Vector3.left;
            foreach (var direction in new[] { near, -near })
            {
                var halfWidth = (Mathf.Abs(direction.x) * bounds.extents.x) + (Mathf.Abs(direction.z) * bounds.extents.z);
                var fromCenter = halfWidth + Mathf.Max(0.01f, rider.ClearanceRadius) + spec.ExitSideDistance;
                var position = bounds.center + (direction * fromCenter);
                position.y = sit.y;
                candidates.Add(new ExitCandidate(position, yaw));
            }

            return candidates;
        }

        private bool TryGetColliderBounds(out Bounds bounds)
        {
            var found = false;
            bounds = default;
            foreach (var collider in GetComponentsInChildren<Collider>())
            {
                if (collider.isTrigger || !collider.enabled)
                {
                    continue;
                }

                if (found)
                {
                    bounds.Encapsulate(collider.bounds);
                }
                else
                {
                    bounds = collider.bounds;
                    found = true;
                }
            }

            return found;
        }

        private bool IsRiderCollider(Collider collider)
        {
            foreach (var ignored in _ignored.Values)
            {
                foreach (var riderCollider in ignored.Rider)
                {
                    if (ReferenceEquals(riderCollider, collider))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static Quaternion YawOnly(Quaternion rotation)
        {
            var forward = rotation * Vector3.forward;
            forward.y = 0f;
            return forward.sqrMagnitude > 1e-8f ? Quaternion.LookRotation(forward, Vector3.up) : Quaternion.identity;
        }

        private void IgnoreCollisions(IVehicleRider rider)
        {
            var riderColliders = rider.RiderRoot.GetComponentsInChildren<Collider>(true);
            var vehicleColliders = GetComponentsInChildren<Collider>(true);
            foreach (var riderCollider in riderColliders)
            {
                foreach (var vehicleCollider in vehicleColliders)
                {
                    Physics.IgnoreCollision(riderCollider, vehicleCollider, true);
                }
            }

            _ignored[rider] = new IgnoredCollisions(riderColliders, vehicleColliders);
        }

        // Collider を無効にすると IgnoreCollision の設定が消える。乗り手が CharacterController を切って置き直した後に呼び直す。
        internal void ReapplyIgnoredCollisions(IVehicleRider rider)
        {
            if (!_ignored.TryGetValue(rider, out var ignored))
            {
                return;
            }

            foreach (var riderCollider in ignored.Rider)
            {
                if (riderCollider == null)
                {
                    continue;
                }

                foreach (var vehicleCollider in ignored.Vehicle)
                {
                    if (vehicleCollider != null)
                    {
                        Physics.IgnoreCollision(riderCollider, vehicleCollider, true);
                    }
                }
            }
        }

        private void RestoreCollisions(IVehicleRider rider)
        {
            if (!_ignored.TryGetValue(rider, out var ignored))
            {
                return;
            }

            _ignored.Remove(rider);
            foreach (var riderCollider in ignored.Rider)
            {
                if (riderCollider == null)
                {
                    continue;
                }

                foreach (var vehicleCollider in ignored.Vehicle)
                {
                    if (vehicleCollider != null)
                    {
                        Physics.IgnoreCollision(riderCollider, vehicleCollider, false);
                    }
                }
            }
        }

        private void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            if (_seats == null)
            {
                CollectSeats();
            }

            BindDamageable();

            // Edit Mode やテストでは OnEnable が走らないので、使われた時点で一覧に載せる。
            if (IsLive && !_active.Contains(this))
            {
                _active.Add(this);
            }
        }

        private void CollectSeats()
        {
            if (_seats != null)
            {
                foreach (var old in _seats)
                {
                    if (old != null)
                    {
                        old.Detach();
                    }
                }
            }

            var seats = new List<VehicleSeat>();
            foreach (var seat in GetComponentsInChildren<VehicleSeat>(true))
            {
                // 入れ子の乗り物 (荷台に載せた別の乗り物など) の座席は、その乗り物のもの。
                if (seat.GetComponentInParent<Vehicle>(true) == this)
                {
                    seats.Add(seat);
                }
            }

            _seats = seats.ToArray();
            for (var i = 0; i < _seats.Length; i++)
            {
                _seats[i].AttachTo(this, i);
            }

            foreach (var old in _mounts)
            {
                if (old != null)
                {
                    old.Detach();
                    _sinks.Remove(old);
                }
            }

            var mounts = new List<VehicleWeaponMount>();
            foreach (var mount in GetComponentsInChildren<VehicleWeaponMount>(true))
            {
                if (mount.GetComponentInParent<Vehicle>(true) == this)
                {
                    mounts.Add(mount);
                }
            }

            _mounts = mounts.ToArray();
            for (var i = 0; i < _mounts.Length; i++)
            {
                _mounts[i].AttachTo(this, i);
                AddInputSink(_mounts[i]);
            }

            _seating = _seats.Length > 0 ? new VehicleSeating(_seats.Length) : null;
            _ignored.Clear();
            _spec = null;
        }

        private VehicleSpec BuildSpec()
        {
            var seats = SeatSpecs();
            if (_definition != null)
            {
                try
                {
                    return _definition.ToSpec(seats, WeaponSpecs());
                }
                catch (VehicleDefinitionException e)
                {
                    // 定義の値の誤りで乗り物ごと使えなくなるより、Inspector の値で動かして誤りをログで知らせるほうを選ぶ。
                    Debug.LogError($"{LogTag} '{name}' の定義を使えないので Inspector の値を使います: {e.Message}", this);
                }
            }

            var bailOut = (_bailOut ??= new BailOutSettings()).ToSpec();
            return new VehicleSpec(
                _vehicleId,
                _maxHealth,
                seats,
                WeaponSpecs(),
                null,
                _enterRange,
                _chaseDistance,
                _chaseHeight,
                bailOut: bailOut,
                enterSeconds: _enterSeconds,
                exitSeconds: _exitSeconds);
        }

        private VehicleWeaponSpec[] WeaponSpecs()
        {
            var weapons = new VehicleWeaponSpec[_mounts.Length];
            for (var i = 0; i < weapons.Length; i++)
            {
                weapons[i] = _mounts[i].Spec;
            }

            return weapons;
        }

        private VehicleSeatSpec[] SeatSpecs()
        {
            if (_seats.Length == 0)
            {
                throw new InvalidOperationException($"'{name}' has no VehicleSeat children");
            }

            var seats = new VehicleSeatSpec[_seats.Length];
            for (var i = 0; i < seats.Length; i++)
            {
                seats[i] = _seats[i].Spec;
            }

            return seats;
        }

        private static bool IsRemoved(IVehicleRider rider)
        {
            return rider is UnityEngine.Object unityObject && unityObject == null;
        }

        private readonly struct ExitCandidate
        {
            public ExitCandidate(Vector3 position, Quaternion rotation)
            {
                Position = position;
                Rotation = rotation;
            }

            public Vector3 Position { get; }

            public Quaternion Rotation { get; }
        }

        private readonly struct IgnoredCollisions
        {
            public IgnoredCollisions(Collider[] rider, Collider[] vehicle)
            {
                Rider = rider;
                Vehicle = vehicle;
            }

            public Collider[] Rider { get; }

            public Collider[] Vehicle { get; }
        }
    }
}
