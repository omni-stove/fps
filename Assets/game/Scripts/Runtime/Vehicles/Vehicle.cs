using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 乗り物の土台。子の <see cref="VehicleSeat"/> を並び順に座席の番号として集め、誰がどこに座っているか (<see cref="VehicleSeating"/>) を持ち、
    /// 乗る・移る・降りるを決めて乗る側 (<see cref="IVehicleRider"/>) に知らせる。動き方・武器・耐久は持たず、
    /// 座席の入力を <see cref="IVehicleInputSink"/> に流すだけにする。
    /// <list type="bullet">
    /// <item>乗る: 空いている座席のうち、乗る位置が <see cref="VehicleSpec.EnterRange"/> 以内で一番近いもの (<see cref="TryEnter(IVehicleRider)"/>)。</item>
    /// <item>降りる: 座席の降りる位置、乗り物の降りる位置の順に試し、塞がっていれば次へ。どれも塞がっていれば降りない (<see cref="TryExit"/>)。
    /// 候補が 1 つも無ければ、座席の右・左・後ろに <see cref="FallbackExitDistance"/> 離した位置を試す。</item>
    /// <item>壊れる: 同じ GameObject の <see cref="Damageable"/> が倒れたか <see cref="Wreck"/> で、全員を降ろす (降りる場所が塞がっていても降ろす)。</item>
    /// </list>
    /// 乗っている間は、乗っている人と乗り物の Collider を互いにぶつからないようにする。
    /// 設定は <see cref="Spec"/> (シリアライズした値と座席の値から作る) で、ゲームのデータで上書きするときは <see cref="Configure"/> を使う。
    /// 乗り降りと座席の移動は <see cref="VehicleLog.Tag"/> 付きでログに出す。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Vehicle : MonoBehaviour
    {
        public const string LogTag = VehicleLog.Tag;

        /// <summary>降りる位置の候補が 1 つも無いときに、座席から横・後ろへ離して試す距離 (m)。仮の乗り物を手早く置くためのもの。</summary>
        public const float FallbackExitDistance = 2f;

        // 降りる場所のカプセルを床から浮かせる高さ。床そのものを塞がりと数えないため。
        private const float ExitGroundSkin = 0.05f;

        private static readonly List<Vehicle> _active = new List<Vehicle>();
        private static readonly Collider[] _overlapBuffer = new Collider[32];

        [SerializeField, Tooltip("乗り物の Id")]
        private string _vehicleId = VehicleSpec.DefaultId;

        [SerializeField, Min(1), Tooltip("耐久")]
        private int _maxHealth = VehicleSpec.DefaultMaxHealth;

        [SerializeField, Min(0.01f), Tooltip("座席の乗る位置からこの距離 (m) までなら乗り込める")]
        private float _enterRange = VehicleSpec.DefaultEnterRange;

        [SerializeField, Min(0f), Tooltip("追うカメラを注視点からどれだけ離すか (m)")]
        private float _chaseDistance = VehicleSpec.DefaultChaseDistance;

        [SerializeField, Tooltip("追うカメラの注視点を、乗り物の原点からどれだけ上に置くか (m)")]
        private float _chaseHeight = VehicleSpec.DefaultChaseHeight;

        [SerializeField, Tooltip("どの座席からでも使う降りる位置の候補 (足元)。座席の候補の後に試す")]
        private Transform[] _exitPoints = Array.Empty<Transform>();

        [SerializeField, Tooltip("降りる場所を塞いでいるとみなすレイヤー")]
        private LayerMask _exitBlockMask = Physics.DefaultRaycastLayers;

        private readonly List<IVehicleInputSink> _sinks = new List<IVehicleInputSink>();
        private readonly Dictionary<IVehicleRider, IgnoredCollisions> _ignored = new Dictionary<IVehicleRider, IgnoredCollisions>();

        private VehicleSeat[] _seats;
        private VehicleSeating _seating;
        private VehicleSpec _spec;
        private Damageable _damageable;
        private bool _initialized;
        private bool _wrecked;

        /// <summary>rider が seat に乗った。</summary>
        public event Action<Vehicle, VehicleSeat, IVehicleRider> Boarded;

        /// <summary>rider が from から to へ移った。</summary>
        public event Action<Vehicle, IVehicleRider, VehicleSeat, VehicleSeat> SeatChanged;

        /// <summary>rider が seat から降りた。</summary>
        public event Action<Vehicle, VehicleSeat, IVehicleRider, VehicleLeaveReason> Left;

        /// <summary>壊れた (乗っている人を降ろした後)。</summary>
        public event Action<Vehicle> Wrecked;

        /// <summary>有効な乗り物。プレイヤーが Interact で乗る乗り物を探すのに使う。</summary>
        public static IReadOnlyList<Vehicle> Active
        {
            get
            {
                _active.RemoveAll(vehicle => vehicle == null || !vehicle.IsLive);
                return _active;
            }
        }

        /// <summary>座席。番号の順 (子の並び順)。</summary>
        public IReadOnlyList<VehicleSeat> Seats
        {
            get
            {
                EnsureInitialized();
                return _seats;
            }
        }

        /// <summary>今の設定。座席が無ければ <see cref="InvalidOperationException"/>。</summary>
        public VehicleSpec Spec
        {
            get
            {
                EnsureInitialized();
                return _spec ??= BuildSpecFromFields();
            }
        }

        public string VehicleId => Spec.Id;

        public bool IsWrecked => _wrecked;

        // OnEnable が走らない Edit Mode でも有効かを判断できるよう、isActiveAndEnabled ではなく自分で見る。
        internal bool IsLive => enabled && gameObject.activeInHierarchy;

        /// <summary>乗っている人数。</summary>
        public int RiderCount
        {
            get
            {
                EnsureInitialized();
                return _seating != null ? CountLiveRiders() : 0;
            }
        }

        /// <summary>降りる場所を塞いでいるとみなすレイヤー。</summary>
        public LayerMask ExitBlockMask
        {
            get => _exitBlockMask;
            set => _exitBlockMask = value;
        }

        public IReadOnlyList<Transform> ExitPoints => _exitPoints;

        /// <summary>追うカメラの注視点 (ワールド)。</summary>
        public Vector3 ChaseFocus => transform.position + (Vector3.up * Spec.ChaseHeight);

        private void Awake()
        {
            EnsureInitialized();
            if (_seats.Length == 0)
            {
                Debug.LogError($"{LogTag} '{name}' の子に VehicleSeat がありません。座席を 1 つ以上置いてください。", this);
                enabled = false;
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
            if (_damageable != null)
            {
                _damageable.Died -= OnDied;
            }
        }

        private void Update()
        {
            PruneRemovedRiders();
        }

        /// <summary>
        /// 設定を差し替える。座席の数は子の <see cref="VehicleSeat"/> の数と同じであること (違えば <see cref="ArgumentException"/>)。
        /// 座席ごとの設定は同じ番号の座席に渡す。乗っている人はそのまま。
        /// </summary>
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

            for (var i = 0; i < _seats.Length; i++)
            {
                _seats[i].ApplySpec(spec.Seats[i]);
            }

            _vehicleId = spec.Id;
            _maxHealth = spec.MaxHealth;
            _enterRange = spec.EnterRange;
            _chaseDistance = spec.ChaseDistance;
            _chaseHeight = spec.ChaseHeight;
            _spec = spec;
        }

        /// <summary>
        /// 子の座席を集め直す (コードで座席を足したあとなど)。誰かが乗っている間は <see cref="InvalidOperationException"/>。
        /// 番号は子の並び順で、<see cref="Configure"/> で渡した設定は捨てて座席の値から作り直す。
        /// </summary>
        public void RefreshSeats()
        {
            if (_seating != null && CountLiveRiders() > 0)
            {
                throw new InvalidOperationException($"cannot refresh the seats of '{name}' while someone is aboard");
            }

            EnsureInitialized();
            CollectSeats();
        }

        /// <summary>
        /// 降りる位置の候補 (どの座席からでも使う) を差し替える。null なら無し。null の要素は飛ばす。
        /// </summary>
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

        /// <summary>index の座席に座っている人。空いていれば (破棄されていても) null。</summary>
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

        /// <summary>rider が座っているこの乗り物の座席。座っていなければ null。</summary>
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

        /// <summary>
        /// from から乗り込める座席の番号 (空いていて、乗る位置が <see cref="VehicleSpec.EnterRange"/> 以内で一番近いもの)。
        /// 無ければ -1。distance にはその座席の乗る位置までの距離 (無ければ一番近い座席までの距離) を入れる。
        /// 壊れていれば -1。
        /// </summary>
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

        /// <summary>rider を、乗り込める座席 (<see cref="FindBoardableSeat"/>) に乗せる。乗せられなければ理由をログに出して false。</summary>
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

        /// <summary>
        /// rider を seatIndex の座席に乗せる (敵や Bot が座席を選ぶとき)。requireInRange が true なら、その座席の乗る位置が
        /// <see cref="VehicleSpec.EnterRange"/> 以内でなければ乗せない (モードが出現と同時に座らせるときは false)。
        /// 乗せられなければ理由をログに出して false。
        /// </summary>
        public bool TryEnter(IVehicleRider rider, int seatIndex, bool requireInRange = true)
        {
            if (!CanEnter(rider))
            {
                return false;
            }

            RequireSeatIndex(seatIndex);
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

        /// <summary>
        /// rider を降ろす。降りる場所の候補を順に試し、塞がっていない最初の場所に置く。すべて塞がっていれば降ろさずにログを出して false。
        /// 乗っていなければ false。
        /// </summary>
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
                return false;
            }

            Leave(rider, seat, VehicleLeaveReason.Request, feet, rotation, exitIndex);
            return true;
        }

        /// <summary>
        /// rider を、降りる場所が塞がっていても降ろす。塞がっていない候補があればそこ、無ければ乗り物の上に置く。乗っていなければ false。
        /// </summary>
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

        /// <summary>
        /// rider を、今の座席から direction (1 で番号の大きいほう、-1 で小さいほう) へ巡って最初の空いている座席に移す。
        /// 空きが無ければログを出して false。乗っていなければ false。
        /// </summary>
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

            var to = _seating.NextFree(from.Index, direction);
            if (to < 0)
            {
                Debug.Log(VehicleLog.SwitchRefused(name, rider.RiderName, VehicleLog.ReasonNoFreeSeat), this);
                return false;
            }

            MoveSeat(rider, from, _seats[to]);
            return true;
        }

        /// <summary>rider を seatIndex の座席に移す。今の座席か、塞がっていれば false (塞がっていればログを出す)。乗っていなければ false。</summary>
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

            if (_seating.IsOccupied(seatIndex))
            {
                Debug.Log(VehicleLog.SwitchRefused(name, rider.RiderName, VehicleLog.ReasonSeatTaken), this);
                return false;
            }

            MoveSeat(rider, from, _seats[seatIndex]);
            return true;
        }

        /// <summary>全員を、降りる場所が塞がっていても降ろす。</summary>
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

        /// <summary>壊す。乗っている人を全員降ろし、以後は乗れない。もう壊れていれば何もせず false。</summary>
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

        /// <summary>座席の入力の受け手を登録する。既に登録されていれば false。</summary>
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

        /// <summary>座席の入力の受け手の登録を外す。登録されていなければ false。</summary>
        public bool RemoveInputSink(IVehicleInputSink sink)
        {
            if (sink == null)
            {
                throw new ArgumentNullException(nameof(sink));
            }

            return _sinks.Remove(sink);
        }

        /// <summary>
        /// seat の降りる場所の候補を順に試し、rider が塞がれずに立てる最初の場所を返す。無ければ false。
        /// 候補は座席の降りる位置、乗り物の降りる位置の順で、どちらも無ければ座席の右・左・後ろ。
        /// </summary>
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
                if (!IsExitBlocked(candidates[i].Position, rider))
                {
                    feetPosition = candidates[i].Position;
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

        /// <summary>足元を feetPosition に置いた rider のカプセルが、乗り物と rider 自身のほかの Collider に重なるか。</summary>
        public bool IsExitBlocked(Vector3 feetPosition, IVehicleRider rider)
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

                    return true;
                }

                return false;
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
            if (TryFindExit(seat, rider, out var feet, out var rotation, out var exitIndex))
            {
                Leave(rider, seat, reason, feet, rotation, exitIndex);
                return;
            }

            Leave(rider, seat, reason, AboveVehicle(seat), YawOnly(transform.rotation), -1);
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
            var found = false;
            var bounds = default(Bounds);
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

            var position = seat.SitPosition;
            if (found)
            {
                position.y = bounds.max.y + ExitGroundSkin;
            }

            return position;
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

            _damageable = GetComponent<Damageable>();
            if (_damageable != null)
            {
                _damageable.Died += OnDied;
            }

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

            _seating = _seats.Length > 0 ? new VehicleSeating(_seats.Length) : null;
            _ignored.Clear();
            _spec = null;
        }

        private VehicleSpec BuildSpecFromFields()
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

            return new VehicleSpec(_vehicleId, _maxHealth, seats, null, null, _enterRange, _chaseDistance, _chaseHeight);
        }

        private void OnDied(Damageable _)
        {
            Wreck();
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
