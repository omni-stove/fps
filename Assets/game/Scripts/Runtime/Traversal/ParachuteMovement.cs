using System;
using System.Globalization;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(ExecutionOrder)]
    public sealed class ParachuteMovement : MonoBehaviour, IMovementOverride
    {
        public const string LogTag = "[Parachute]";

        // PlayerController (既定の 0) より先に Jump を読み、押したフレームの Tick から開いた動きにする。
        public const int ExecutionOrder = -10;

        // 開くときにほかの動きが移動を受け持っていれば開かないので、ほかと同時に有効になることはまず無い。
        // 開いた後に有効になる動き (乗り物の座席など) がこれより高ければ、パラシュートは閉じてそちらに任せる。
        public const int MovementPriority = 100;

        // 始点は自分の体の中になるが、Ray は始点を含むコライダーには当たらない。
        // ネットの移動 (CharacterMotorSimulation) と同じ高さで開けるかを決めるよう、Core の値を使う。
        public const float ProbeLift = ParachuteMotion.ProbeLift;

        public const float ProbeDistance = ParachuteMotion.ProbeDistance;

        private const int MaxProbeHits = 16;

        private static readonly RaycastHit[] _probeBuffer = new RaycastHit[MaxProbeHits];

        [SerializeField, Min(0.01f), Tooltip("開ける、足元から地面までの最低の高さ (m)")]
        private float _minOpenHeight = ParachuteMotion.DefaultMinOpenHeight;

        [SerializeField, Min(0.01f), Tooltip("開いている間に降りる速さ (m/s)")]
        private float _descentSpeed = ParachuteMotion.DefaultDescentSpeed;

        [SerializeField, Min(0f), Tooltip("開いている間の横の速さの上限 (m/s)")]
        private float _horizontalSpeed = ParachuteMotion.DefaultHorizontalSpeed;

        [SerializeField, Min(0.01f), Tooltip("横の速度を移動の入力の向きへ寄せる加速度 (m/s²)")]
        private float _steerAcceleration = ParachuteMotion.DefaultSteerAcceleration;

        [SerializeField, Tooltip("足元の地面として当たるレイヤー")]
        private LayerMask _groundMask = Physics.DefaultRaycastLayers;

        private PlayerController _mover;
        private ParachuteMotion _motion;
        private Parachute _rule = Parachute.Off;
        private bool _isOpen;
        private bool _stepped;
        private bool _registered;
        private bool _deployedByBailOut;

        public event Action<float> Opened;

        public event Action<ParachuteCloseReason> Closed;

        public PlayerController Mover => _mover;

        public ParachuteMotion Motion => _motion;

        public bool IsInitialized => _mover != null && _mover.IsInitialized && _motion != null;

        public bool IsOpen => _isOpen;

        public ParachuteCloseReason? LastCloseReason { get; private set; }

        public ParachuteOpenVerdict? LastVerdict { get; private set; }

        public Parachute Rule
        {
            get => _rule;
            set
            {
                if (!Enum.IsDefined(typeof(Parachute), value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "value is not a defined Parachute");
                }

                _rule = value;
                if (value != Parachute.On && !_deployedByBailOut)
                {
                    Close(ParachuteCloseReason.RuleOff);
                }
            }
        }

        public int Priority => MovementPriority;

        // 移した直後の CharacterController の接地は前の位置のままなので、着地は開いてから 1 度動かした後の接地で見る。
        public bool IsActive =>
            _isOpen && (_rule == Parachute.On || _deployedByBailOut) && enabled && !(_stepped && _mover != null && _mover.IsGrounded);

        public bool IsDeployedByBailOut => _isOpen && _deployedByBailOut;

        public PlayerActionConditions Conditions => PlayerActionConditions.Parachuting;

        public static string FormatOpenLog(ParachuteOpenVerdict verdict, float heightAboveGround)
        {
            var height = float.IsPositiveInfinity(heightAboveGround) ? "inf" : heightAboveGround.ToString("F2", CultureInfo.InvariantCulture);
            return verdict == ParachuteOpenVerdict.Open
                ? $"{LogTag} opened height={height}"
                : $"{LogTag} open={verdict} height={height}";
        }

        public static string FormatClosedLog(ParachuteCloseReason reason)
        {
            return $"{LogTag} closed reason={reason}";
        }

        private void Awake()
        {
            if (!IsInitialized && !TryInitializeFromHierarchy())
            {
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (IsInitialized)
            {
                Register();
            }
        }

        // 先に閉じてから外す。外すと PlayerController が End を呼ぶので、閉じた理由が Interrupted にならないように。
        private void OnDisable()
        {
            Close(ParachuteCloseReason.Disabled);
            Unregister();
        }

        private void OnDestroy()
        {
            Close(ParachuteCloseReason.Disabled);
            Unregister();
        }

        private void Update()
        {
            if (!IsInitialized || _isOpen || _rule != Parachute.On || _mover.IsControlHandedOff || _mover.IsGrounded)
            {
                return;
            }

            var jump = _mover.JumpAction;
            if (jump != null && jump.WasPressedThisFrame())
            {
                TryOpen();
            }
        }

        public void Configure(GameRules rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            Rule = rules.Actions.Parachute;
        }

        public bool TryInitializeFromHierarchy()
        {
            var mover = GetComponent<PlayerController>();
            if (mover == null || !mover.IsInitialized)
            {
                Debug.LogError(
                    $"{LogTag} '{name}' に初期化済みの PlayerController がありません。パラシュートは PlayerController の移動を横取りして動きます。",
                    this);
                return false;
            }

            try
            {
                Initialize(mover);
            }
            catch (ArgumentOutOfRangeException e)
            {
                Debug.LogError($"{LogTag} パラシュートの設定値が不正です: {e.Message}", this);
                return false;
            }

            return true;
        }

        public void Initialize(PlayerController mover)
        {
            if (mover == null)
            {
                throw new ArgumentNullException(nameof(mover));
            }

            if (mover.gameObject != gameObject)
            {
                throw new ArgumentException($"mover must be on '{name}'", nameof(mover));
            }

            if (!mover.IsInitialized)
            {
                throw new ArgumentException("mover must be initialized", nameof(mover));
            }

            var motion = new ParachuteMotion(_minOpenHeight, _descentSpeed, _horizontalSpeed, _steerAcceleration);
            Close(ParachuteCloseReason.Interrupted);
            Unregister();
            _mover = mover;
            _motion = motion;
            Register();
        }

        public void ApplyTuning(ParachuteTuning tuning)
        {
            if (tuning == null)
            {
                throw new ArgumentNullException(nameof(tuning));
            }

            _minOpenHeight = tuning.MinOpenHeight;
            _descentSpeed = tuning.DescentSpeed;
            _horizontalSpeed = tuning.HorizontalSpeed;
            _steerAcceleration = tuning.SteerAcceleration;

            // 同じ値で作り直すと、ルールを入れ直しただけで開いているパラシュートが閉じてしまう。
            if (_motion == null
                || (_motion.MinOpenHeight.Equals(_minOpenHeight)
                    && _motion.DescentSpeed.Equals(_descentSpeed)
                    && _motion.HorizontalSpeed.Equals(_horizontalSpeed)
                    && _motion.SteerAcceleration.Equals(_steerAcceleration)))
            {
                return;
            }

            var motion = new ParachuteMotion(_minOpenHeight, _descentSpeed, _horizontalSpeed, _steerAcceleration);
            Close(ParachuteCloseReason.Interrupted);
            _motion = motion;
        }

        public float MeasureHeightAboveGround()
        {
            return MeasureHeightAboveGround(null);
        }

        public float MeasureHeightAboveGround(Transform ignoredRoot)
        {
            RequireInitialized();
            var controller = _mover.Controller;
            var halfHeight = controller.height * 0.5f * Mathf.Abs(transform.lossyScale.y);
            var feet = transform.TransformPoint(controller.center) - (Vector3.up * halfHeight);
            var origin = feet + (Vector3.up * ProbeLift);

            var count = Physics.RaycastNonAlloc(
                origin, Vector3.down, _probeBuffer, ProbeDistance + ProbeLift, _groundMask, QueryTriggerInteraction.Ignore);
            var nearest = float.PositiveInfinity;
            for (var i = 0; i < count; i++)
            {
                var hit = _probeBuffer[i];
                if (hit.collider == null
                    || hit.collider.transform.IsChildOf(transform)
                    || (ignoredRoot != null && hit.collider.transform.IsChildOf(ignoredRoot)))
                {
                    continue;
                }

                nearest = Mathf.Min(nearest, hit.distance);
            }

            return float.IsPositiveInfinity(nearest) ? nearest : Mathf.Max(0f, nearest - ProbeLift);
        }

        public ParachuteOpenVerdict TryOpen()
        {
            RequireInitialized();
            var height = MeasureHeightAboveGround();
            var rule = enabled ? _rule : Parachute.Off;
            var busy = _mover.IsMovementOverridden && !ReferenceEquals(_mover.ActiveMovementOverride, this);
            var verdict = _motion.Evaluate(rule, _isOpen, busy, height);
            LastVerdict = verdict;
            Debug.Log(FormatOpenLog(verdict, height), this);
            if (verdict != ParachuteOpenVerdict.Open)
            {
                return verdict;
            }

            var velocity = _mover.Motor.Velocity;
            Open(new Vector3(velocity.X, velocity.Y, velocity.Z), height, false);
            return verdict;
        }

        // 乗り物から脱出したときに開く口。Rule はジャンプで開けるかのルールなので、脱出には掛けない。
        // 掛けると、パラシュートを切ったモードでは空中で降りた人がそのまま落ちて死ぬ。
        public ParachuteOpenVerdict DeployForBailOut(Vector3 inheritedVelocity, Transform vehicleRoot)
        {
            RequireInitialized();
            if (!IsFinite(inheritedVelocity.x) || !IsFinite(inheritedVelocity.y) || !IsFinite(inheritedVelocity.z))
            {
                throw new ArgumentOutOfRangeException(nameof(inheritedVelocity), inheritedVelocity, "inheritedVelocity must be finite");
            }

            var height = MeasureHeightAboveGround(vehicleRoot);
            var rule = enabled ? Parachute.On : Parachute.Off;
            var busy = _mover.IsMovementOverridden && !ReferenceEquals(_mover.ActiveMovementOverride, this);
            var verdict = _motion.Evaluate(rule, _isOpen, busy, height);
            LastVerdict = verdict;
            Debug.Log(FormatOpenLog(verdict, height), this);
            if (verdict == ParachuteOpenVerdict.Open)
            {
                Open(inheritedVelocity, height, true);
            }

            return verdict;
        }

        public ParachuteOpenVerdict TryOpenAt(Vector3 position)
        {
            RequireInitialized();
            if (!IsFinite(position.x) || !IsFinite(position.y) || !IsFinite(position.z))
            {
                throw new ArgumentOutOfRangeException(nameof(position), position, "position must be finite");
            }

            var from = transform.position;
            PlaceAt(position);
            var verdict = TryOpen();
            if (verdict != ParachuteOpenVerdict.Open)
            {
                PlaceAt(from);
            }

            return verdict;
        }

        public void Begin(PlayerController mover)
        {
        }

        public MovementOverrideStep Step(PlayerController mover, MovementOverrideInput input, float deltaSeconds)
        {
            _stepped = true;
            var yawRadians = transform.eulerAngles.y * Mathf.Deg2Rad;
            var velocity = _motion.Step(input.Move.ToNumerics(), yawRadians, deltaSeconds).ToUnity();

            // PlayerController は着地したときにこの速度で落下ダメージを決める。降りる速さを閾値より速く設定されても、
            // 開いている間はダメージを受けないという約束を守るため、渡す下向きの速さだけを閾値までに抑える (動く量はそのまま)。
            var reported = velocity;
            reported.y = -Mathf.Min(_motion.DescentSpeed, mover.Motor.Spec.SafeFallSpeed);
            return new MovementOverrideStep(velocity * deltaSeconds, reported, crouched: false);
        }

        public void End(PlayerController mover)
        {
            if (!_isOpen)
            {
                return;
            }

            if (_stepped && mover.IsGrounded)
            {
                Close(ParachuteCloseReason.Landed);
            }
            else if (_rule != Parachute.On && !_deployedByBailOut)
            {
                Close(ParachuteCloseReason.RuleOff);
            }
            else if (!enabled)
            {
                Close(ParachuteCloseReason.Disabled);
            }
            else
            {
                Close(ParachuteCloseReason.Interrupted);
            }
        }

        private void Close(ParachuteCloseReason reason)
        {
            if (!_isOpen)
            {
                return;
            }

            _isOpen = false;
            _stepped = false;
            _deployedByBailOut = false;
            _motion?.Reset();
            LastCloseReason = reason;
            Debug.Log(FormatClosedLog(reason), this);
            Closed?.Invoke(reason);
        }

        private void Open(Vector3 velocity, float height, bool byBailOut)
        {
            _motion.Open(new System.Numerics.Vector2(velocity.x, velocity.z));
            _isOpen = true;
            _stepped = false;
            _deployedByBailOut = byBailOut;
            Opened?.Invoke(height);
        }

        private void Register()
        {
            if (_mover != null && !_registered)
            {
                _mover.RegisterMovementOverride(this);
                _registered = true;
            }
        }

        private void Unregister()
        {
            if (_mover != null && _registered)
            {
                _registered = false;
                _mover.UnregisterMovementOverride(this);
            }
        }

        // CharacterController は有効な間 transform の位置を自分の位置で上書きするので、止めてから置く。
        // 続けて Raycast で高さを測るので、移した位置を物理に反映させておく。
        private void PlaceAt(Vector3 position)
        {
            var controller = _mover.Controller;
            var wasEnabled = controller.enabled;
            controller.enabled = false;
            transform.position = position;
            controller.enabled = wasEnabled;
            Physics.SyncTransforms();
        }

        private void RequireInitialized()
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException($"{LogTag} ParachuteMovement on '{name}' is not initialized");
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
