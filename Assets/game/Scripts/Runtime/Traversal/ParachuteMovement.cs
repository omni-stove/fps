using System;
using System.Globalization;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 高い所から落ちている間にパラシュートを開いて降りる、プレイヤーのコンポーネント。<see cref="PlayerController"/> の移動の横取り (<see cref="IMovementOverride"/>) として動く。
    /// 空中で Jump を押すと、ルールの <see cref="Parachute"/> が On で足元の地面まで <see cref="ParachuteMotion.MinOpenHeight"/> 以上あれば開き、
    /// 開いている間は <see cref="ParachuteMotion"/> の速さで降り、移動の入力の向きへ横に流れる。着地すると閉じる。
    /// 開いている間に着地しても落下ダメージは受けない (<see cref="Step"/> が渡す速度を <see cref="MotorSpec.SafeFallSpeed"/> までに抑える)。
    /// 開く・開けない・閉じるたびに <c>[Parachute]</c> のログを出す。
    /// 参照はコードで解決するので Inspector の配線は要らない: <see cref="PlayerController"/> (と、その Jump) は同じ GameObject。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(ExecutionOrder)]
    public sealed class ParachuteMovement : MonoBehaviour, IMovementOverride
    {
        public const string LogTag = "[Parachute]";

        /// <summary>
        /// Update の実行順。<see cref="PlayerController"/> (既定の 0) より先に Jump を読み、押したフレームの Tick から開いた動きにする。
        /// </summary>
        public const int ExecutionOrder = -10;

        /// <summary>
        /// <see cref="IMovementOverride.Priority"/>。開くときにほかの動きが移動を受け持っていれば開かないので、ほかと同時に有効になることはまず無い。
        /// 開いた後に有効になる動き (乗り物の座席など) がこれより高ければ、パラシュートは閉じてそちらに任せる。
        /// </summary>
        public const int MovementPriority = 100;

        /// <summary>
        /// 足元の高さを測る Ray を、足元からこれだけ上から飛ばす (m)。接地して少しめり込んでいても、始点が地面の中に入って地面を外さないため。
        /// 始点は自分の体の中になるが、Ray は始点を含むコライダーには当たらない。
        /// </summary>
        public const float ProbeLift = 0.25f;

        /// <summary>足元の高さを測る Ray の長さ (m)。これより下に地面が無ければ高さは正の無限大。</summary>
        public const float ProbeDistance = 1000f;

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

        /// <summary>開いたときに、足元から地面までの高さ (m) を渡して呼ばれる。</summary>
        public event Action<float> Opened;

        /// <summary>閉じたときに、閉じた理由を渡して呼ばれる。</summary>
        public event Action<ParachuteCloseReason> Closed;

        /// <summary>動かしている <see cref="PlayerController"/>。未初期化なら null。</summary>
        public PlayerController Mover => _mover;

        /// <summary>降りる動きの値。未初期化なら null。</summary>
        public ParachuteMotion Motion => _motion;

        public bool IsInitialized => _mover != null && _mover.IsInitialized && _motion != null;

        /// <summary>開いているか (開いてから閉じるまで)。</summary>
        public bool IsOpen => _isOpen;

        /// <summary>最後に閉じた理由。まだ閉じたことが無ければ null。</summary>
        public ParachuteCloseReason? LastCloseReason { get; private set; }

        /// <summary>最後に <see cref="TryOpen"/> した結果。まだ試していなければ null。</summary>
        public ParachuteOpenVerdict? LastVerdict { get; private set; }

        /// <summary>
        /// パラシュートを使うか。既定は <see cref="Parachute.Off"/>。<see cref="PlayerActions.Attach"/> がルールの <see cref="ActionRules.Parachute"/> を入れる。
        /// Off にすると、開いていればその場で閉じる。
        /// </summary>
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
                if (value != Parachute.On)
                {
                    Close(ParachuteCloseReason.RuleOff);
                }
            }
        }

        public int Priority => MovementPriority;

        /// <summary>
        /// 開いていて、ルールが On で、このコンポーネントが有効で、まだ着地していない間。
        /// 着地は開いてから 1 度動かした後の接地で見る (移した直後の CharacterController の接地は前の位置のままなので)。
        /// </summary>
        public bool IsActive => _isOpen && _rule == Parachute.On && enabled && !(_stepped && _mover != null && _mover.IsGrounded);

        public PlayerActionConditions Conditions => PlayerActionConditions.Parachuting;

        /// <summary>開いた・開けなかったときのログ。例: <c>[Parachute] opened height=12.00</c>、<c>[Parachute] open=TooLow height=1.20</c>。</summary>
        public static string FormatOpenLog(ParachuteOpenVerdict verdict, float heightAboveGround)
        {
            var height = float.IsPositiveInfinity(heightAboveGround) ? "inf" : heightAboveGround.ToString("F2", CultureInfo.InvariantCulture);
            return verdict == ParachuteOpenVerdict.Open
                ? $"{LogTag} opened height={height}"
                : $"{LogTag} open={verdict} height={height}";
        }

        /// <summary>閉じたときのログ。例: <c>[Parachute] closed reason=Landed</c>。</summary>
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

        /// <summary>rules の <see cref="ActionRules.Parachute"/> を <see cref="Rule"/> に入れる。</summary>
        public void Configure(GameRules rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            Rule = rules.Actions.Parachute;
        }

        /// <summary>
        /// 同じ GameObject の初期化済みの <see cref="PlayerController"/> を探して初期化する。
        /// 見つからないか設定値が不正なら理由を Debug.LogError に出して false を返す。
        /// </summary>
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

        /// <summary>
        /// mover (この GameObject の初期化済みのもの) の移動の横取りとして登録し、Inspector の値から <see cref="ParachuteMotion"/> を作る。
        /// 開いていれば閉じてから付け替える。値が不正なら <see cref="ArgumentOutOfRangeException"/>。
        /// </summary>
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

        /// <summary>
        /// 足元から下へ Ray を飛ばし、自分の体を除いて最も近い地面までの高さ (m) を測る。
        /// <see cref="ProbeDistance"/> の中に無ければ正の無限大。
        /// </summary>
        public float MeasureHeightAboveGround()
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
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                nearest = Mathf.Min(nearest, hit.distance);
            }

            return float.IsPositiveInfinity(nearest) ? nearest : Mathf.Max(0f, nearest - ProbeLift);
        }

        /// <summary>
        /// 開けるかを <see cref="ParachuteMotion.Evaluate"/> で決め、開けるなら開く。開いた後の動きは次の <see cref="PlayerController.Tick"/> から。
        /// 結果を <see cref="FormatOpenLog"/> でログに出して返し、<see cref="LastVerdict"/> にも残す。開いたら <see cref="Opened"/> を呼ぶ。
        /// このコンポーネントが無効なときはルールが Off のときと同じく開かない。
        /// </summary>
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
            _motion.Open(new System.Numerics.Vector2(velocity.X, velocity.Z));
            _isOpen = true;
            _stepped = false;
            Opened?.Invoke(height);
            return verdict;
        }

        /// <summary>
        /// 体を position (CharacterController の中心ではなく Transform の位置) に移してから <see cref="TryOpen"/> する。
        /// 開けなければ元の位置に戻す。上空から降ろして始めるとき (<see cref="BattleRoyaleRunner"/>) に使う。
        /// </summary>
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
            else if (_rule != Parachute.On)
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
            _motion?.Reset();
            LastCloseReason = reason;
            Debug.Log(FormatClosedLog(reason), this);
            Closed?.Invoke(reason);
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
