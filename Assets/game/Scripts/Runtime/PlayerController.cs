using System;
using Fps.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fps.Runtime
{
    /// <summary>
    /// 移動入力を <see cref="PlayerMotor"/> に渡し、返ってきた変位を <see cref="CharacterController.Move"/> に適用する。
    /// 移動量の計算はすべて <see cref="PlayerMotor"/> が、しゃがみの寸法は <see cref="StanceGeometry"/> が行い、
    /// ここは入力の読み取りと結果の適用だけを受け持つ。
    /// しゃがみは Crouch を <see cref="ControlSettings"/> のモード (押している間／押すたびに切り替え) で <see cref="CrouchInput"/> が解釈し、
    /// ジャンプとダッシュの開始で解除する (頭上が塞がっていれば解除しない)。死亡・リスポーンでの解除は <see cref="ReleaseCrouch"/> を呼ぶ。
    /// しゃがむと CharacterController の height と center を縮め (足元は動かさない)、目も下げる。
    /// 立つときは立った高さのカプセルが自分以外のコライダーに重ならないときだけ立ち上がる。
    /// 参照はコードで解決するので Inspector の配線は要らない: CharacterController と <see cref="FirstPersonLook"/> は同じ
    /// GameObject、目は <see cref="ViewRig.FindEye"/> (無ければ子のカメラ)、入力はプロジェクト全体のアクション
    /// (<see cref="InputSystem.actions"/>) の Move / Sprint / Crouch / Jump。解決できなければエラーを出して自分を無効にする。
    /// Sprint は <see cref="ControlSettings"/> のモード (押している間／押すたびに切り替え) で <see cref="SprintInput"/> が解釈し、
    /// 射撃・死亡・リスポーンでの解除は <see cref="BindSprintRelease"/> で購読する。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        /// <summary>Assets/InputSystem_Actions.inputactions のアクション。</summary>
        public const string MoveActionPath = InputActionPaths.Move;
        public const string SprintActionPath = InputActionPaths.Sprint;
        public const string CrouchActionPath = InputActionPaths.Crouch;
        public const string JumpActionPath = InputActionPaths.Jump;

        public const string LogTag = "[Player]";

        public const float DefaultWalkSpeed = 5f;
        public const float DefaultSprintSpeed = 8f;
        public const float DefaultCrouchSpeed = 2.5f;
        public const float DefaultJumpHeight = 1.2f;
        public const float DefaultGravity = 20f;
        public const float DefaultAirControl = 0.1f;

        /// <summary>しゃがみのカプセルの高さ (m)。立ち 2m・半径 0.5m のプレイヤーで、目は足元から 0.8m になる。</summary>
        public const float DefaultCrouchHeight = 1.2f;

        // 自分のコライダーを除いて頭上の障害物を探すための一時バッファ。毎フレームの確保を避ける。
        private const int MaxOverlapHits = 16;

        private static readonly Collider[] _overlapBuffer = new Collider[MaxOverlapHits];

        [SerializeField, Min(0f), Tooltip("歩きの水平速度 (m/s)")]
        private float _walkSpeed = DefaultWalkSpeed;

        [SerializeField, Min(0f), Tooltip("ダッシュ中の水平速度 (m/s)")]
        private float _sprintSpeed = DefaultSprintSpeed;

        [SerializeField, Min(0f), Tooltip("しゃがみ中の水平速度 (m/s)。しゃがみ中はダッシュしない")]
        private float _crouchSpeed = DefaultCrouchSpeed;

        [SerializeField, Min(0f), Tooltip("ジャンプで到達する高さ (m)")]
        private float _jumpHeight = DefaultJumpHeight;

        [SerializeField, Min(0.01f), Tooltip("重力加速度の大きさ (m/s²)。下向きに適用する")]
        private float _gravity = DefaultGravity;

        [SerializeField, Range(0f, 1f), Tooltip("空中で水平速度を入力方向へ寄せる 1 フレームあたりの割合")]
        private float _airControl = DefaultAirControl;

        [SerializeField, Min(0.01f), Tooltip("しゃがみのカプセルの高さ (m)。半径の 2 倍以上、立ちの高さ以下")]
        private float _crouchHeight = DefaultCrouchHeight;

        [SerializeField, Tooltip("立ち上がれるかを調べるときに頭上の障害物として扱うレイヤー")]
        private LayerMask _headroomMask = Physics.DefaultRaycastLayers;

        private CharacterController _controller;
        private FirstPersonLook _look;
        private InputAction _moveAction;
        private InputAction _sprintAction;
        private InputAction _crouchAction;
        private InputAction _jumpAction;
        private PlayerMotor _motor;
        private StanceGeometry _stance;
        private Transform _eye;

        // 立ちの目の位置 (本体のローカル座標)。しゃがみでは Y だけ変える。
        private Vector3 _standingEyeLocal;

        // 立ちの center。しゃがみでは Y だけ変え、X と Z は元のまま保つ。
        private Vector3 _standingCenter;
        private bool _isCrouched;
        private bool _enabledMove;
        private bool _enabledSprint;
        private bool _enabledCrouch;
        private bool _enabledJump;
        private bool _isLive;
        private bool? _lastGrounded;
        private ControlSettings _controlSettings = ControlSettings.Default;
        private InputDeviceKind _device = InputDeviceKind.KeyboardMouse;
        private SprintInput _sprint;
        private WeaponController _releaseWeapon;
        private Damageable _releaseDamageable;
        private InputDeviceKind _crouchDevice = InputDeviceKind.KeyboardMouse;
        private CrouchInput _crouchInput;

        public CharacterController Controller => _controller;
        public FirstPersonLook Look => _look;
        public InputAction MoveAction => _moveAction;
        public InputAction SprintAction => _sprintAction;
        public InputAction CrouchAction => _crouchAction;
        public InputAction JumpAction => _jumpAction;

        /// <summary>移動計算。未初期化なら null。</summary>
        public PlayerMotor Motor => _motor;

        /// <summary>立ちとしゃがみの寸法。未初期化なら null。</summary>
        public StanceGeometry Stance => _stance;

        /// <summary>しゃがみで上下させる目。目もカメラも無ければ null (カプセルだけ縮める)。</summary>
        public Transform Eye => _eye;

        /// <summary>しゃがんでいるか。</summary>
        public bool IsCrouched => _isCrouched;

        public bool IsInitialized =>
            _controller != null
            && _look != null
            && _moveAction != null
            && _sprintAction != null
            && _crouchAction != null
            && _jumpAction != null
            && _motor != null
            && _stance != null;

        public float WalkSpeed => _walkSpeed;
        public float SprintSpeed => _sprintSpeed;
        public float CrouchSpeed => _crouchSpeed;
        public float JumpHeight => _jumpHeight;
        public float Gravity => _gravity;
        public float AirControl => _airControl;
        public float CrouchHeight => _crouchHeight;

        /// <summary>
        /// ダッシュとしゃがみのモードを決める操作設定。Awake で <see cref="ControlSettingsStore"/> から読む (Edit Mode では Awake が走らないので初期値)。
        /// 変えると次の <see cref="UpdateSprint"/> / <see cref="UpdateCrouch"/> から使う。
        /// </summary>
        public ControlSettings ControlSettings
        {
            get => _controlSettings;
            set => _controlSettings = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>最後にダッシュか移動の入力をした機器。ダッシュのモードはこの機器の設定を使う。</summary>
        public InputDeviceKind ActiveDevice => _device;

        /// <summary>ダッシュ中か (ボタンをモードどおりに解釈し、自動解除した後の値)。</summary>
        public bool IsSprinting => Sprint.IsActive;

        /// <summary>今使っているダッシュのモード。</summary>
        public HoldMode SprintMode => Sprint.Mode;

        private SprintInput Sprint
        {
            get
            {
                // Edit Mode では Awake が走らないので、初めて触れたときに作る。
                if (_sprint == null)
                {
                    _sprint = new SprintInput();
                    _sprint.Changed += OnSprintChanged;
                }

                return _sprint;
            }
        }

        /// <summary>最後にしゃがみか移動の入力をした機器。しゃがみのモードはこの機器の設定を使う。</summary>
        public InputDeviceKind CrouchDevice => _crouchDevice;

        /// <summary>
        /// しゃがみたいか (ボタンをモードどおりに解釈し、自動解除した後の値)。
        /// 頭上が塞がっていると、false でも <see cref="IsCrouched"/> は true のことがある。
        /// </summary>
        public bool IsCrouchActive => Crouch.IsActive;

        /// <summary>今使っているしゃがみのモード。</summary>
        public HoldMode CrouchMode => Crouch.Mode;

        private CrouchInput Crouch
        {
            get
            {
                // Edit Mode では Awake が走らないので、初めて触れたときに作る。
                if (_crouchInput == null)
                {
                    _crouchInput = new CrouchInput(CanStandForCrouchInput);
                    _crouchInput.Changed += OnCrouchInputChanged;
                }

                return _crouchInput;
            }
        }

        private void Awake()
        {
            _controlSettings = new ControlSettingsStore().Load();
            if (!IsInitialized && !TryInitializeFromHierarchy())
            {
                enabled = false;
            }
        }

        private void OnDestroy()
        {
            BindSprintRelease(null, null);
        }

        private void OnEnable()
        {
            if (!IsInitialized)
            {
                // Awake で解決に失敗したまま再び有効にされた場合。エラーは解決時に出している。
                enabled = false;
                return;
            }

            _isLive = true;
            AcquireActions();
        }

        private void OnDisable()
        {
            _isLive = false;
            ReleaseActions();
        }

        /// <summary>プロジェクト全体のアクションは他の部品と共有なので、自分が有効にしたものだけ後で無効に戻す。</summary>
        private void AcquireActions()
        {
            _enabledMove = EnableIfNeeded(_moveAction);
            _enabledSprint = EnableIfNeeded(_sprintAction);
            _enabledCrouch = EnableIfNeeded(_crouchAction);
            _enabledJump = EnableIfNeeded(_jumpAction);
        }

        private void ReleaseActions()
        {
            DisableIfOwned(_moveAction, ref _enabledMove);
            DisableIfOwned(_sprintAction, ref _enabledSprint);
            DisableIfOwned(_crouchAction, ref _enabledCrouch);
            DisableIfOwned(_jumpAction, ref _enabledJump);
        }

        private void Update()
        {
            var move = _moveAction.ReadValue<Vector2>();

            // 押されたダッシュボタンの機器を優先し、どちらも動いていなければ直前の機器のまま。
            var device = InputDeviceKinds.Of(_sprintAction, InputDeviceKinds.Of(_moveAction, _device));
            var sprint = UpdateSprint(move, _sprintAction.IsPressed(), _sprintAction.WasPressedThisFrame(), device);
            var jump = _jumpAction.WasPressedThisFrame();

            var crouchDevice = InputDeviceKinds.Of(_crouchAction, InputDeviceKinds.Of(_moveAction, _crouchDevice));
            // 空中で押したジャンプは跳ばない (PlayerMotor) ので、しゃがみも解かない。
            var crouch = UpdateCrouch(
                _crouchAction.IsPressed(), _crouchAction.WasPressedThisFrame(), jump && _controller.isGrounded, sprint, crouchDevice);
            Tick(move, sprint, crouch, jump, Time.deltaTime);
        }

        /// <summary>
        /// 1 フレーム分のしゃがみボタンを device の <see cref="ControlSettings"/> のモードで解釈し、しゃがみたいかを返す。
        /// しゃがんでいる間にジャンプするかダッシュを始めると、頭上が空いていれば解除する (<see cref="CrouchInput"/>)。
        /// jumpPressed は実際に跳ぶジャンプ (接地中の押下) だけを渡す。
        /// 変化は <c>[Player] crouch=..</c> でログに出す。結果は <see cref="Tick"/> の crouch に渡す。
        /// </summary>
        public bool UpdateCrouch(bool isPressed, bool wasPressedThisFrame, bool jumpPressed, bool sprinting, InputDeviceKind device)
        {
            var mode = _controlSettings.Get(device, HoldAction.Crouch);
            _crouchDevice = device;
            return Crouch.Update(mode, isPressed, wasPressedThisFrame, jumpPressed, sprinting);
        }

        /// <summary>
        /// しゃがみを解除する (死亡・リスポーンなど)。頭上は確かめないが、実際に立つのは次の <see cref="Tick"/> で頭上が空いているときだけ。
        /// Hold でもボタンを押し直すまでしゃがまない。しゃがみたい状態だったら true。
        /// </summary>
        /// <param name="reason">ログに出す理由 (<see cref="CrouchInput.ReasonDeath"/> など)。</param>
        public bool ReleaseCrouch(string reason)
        {
            return Crouch.Release(reason);
        }

        // 未初期化では頭上を調べられないので、立てるものとして解除させる (姿勢を当てる Tick も動かない)。
        private bool CanStandForCrouchInput()
        {
            return !IsInitialized || HasHeadroomToStand();
        }

        private void OnCrouchInputChanged(CrouchInput input, string reason)
        {
            Debug.Log(CrouchInput.FormatLog(input.IsActive, input.Mode, reason), this);
        }

        /// <summary>
        /// 1 フレーム分のダッシュボタンと移動入力を、device の <see cref="ControlSettings"/> のモードで解釈し、ダッシュ中かを返す。
        /// Toggle では移動入力がなくなると解除する (<see cref="SprintInput"/>)。変化は <c>[Player] sprint=..</c> でログに出す。
        /// </summary>
        public bool UpdateSprint(Vector2 move, bool isPressed, bool wasPressedThisFrame, InputDeviceKind device)
        {
            var mode = _controlSettings.Get(device, HoldAction.Sprint);
            _device = device;
            return Sprint.Update(mode, move.ToNumerics(), isPressed, wasPressedThisFrame);
        }

        /// <summary>
        /// ダッシュを解除する。Hold でもボタンを押し直すまでダッシュしない。ADS など外の処理から呼ぶ。
        /// ダッシュ中だったら true。
        /// </summary>
        /// <param name="reason">ログに出す理由 (<see cref="SprintInput.ReasonFire"/> など)。</param>
        public bool ReleaseSprint(string reason)
        {
            return Sprint.Release(reason);
        }

        /// <summary>
        /// weapon が撃ったとき、damageable が死んだとき・体力を作り直した (リスポーンした) ときにダッシュを解除するよう購読する。
        /// 前に渡したものの購読は外す。どちらも null 可 (その理由では解除しない)。
        /// </summary>
        public void BindSprintRelease(WeaponController weapon, Damageable damageable)
        {
            if (_releaseWeapon != null)
            {
                _releaseWeapon.Fired -= OnWeaponFired;
            }

            if (_releaseDamageable != null)
            {
                _releaseDamageable.Died -= OnDied;
                _releaseDamageable.HealthReset -= OnHealthReset;
            }

            _releaseWeapon = weapon;
            _releaseDamageable = damageable;

            if (_releaseWeapon != null)
            {
                _releaseWeapon.Fired += OnWeaponFired;
            }

            if (_releaseDamageable != null)
            {
                _releaseDamageable.Died += OnDied;
                _releaseDamageable.HealthReset += OnHealthReset;
            }
        }

        private void OnWeaponFired(Weapon _)
        {
            ReleaseSprint(SprintInput.ReasonFire);
        }

        private void OnDied(Damageable _)
        {
            ReleaseSprint(SprintInput.ReasonDeath);
        }

        private void OnHealthReset(Health _)
        {
            ReleaseSprint(SprintInput.ReasonRespawn);
        }

        private void OnSprintChanged(SprintChange change)
        {
            Debug.Log(SprintInput.FormatLog(change), this);
        }

        /// <summary>
        /// 1 フレーム分の入力でしゃがみの姿勢を決め、<see cref="PlayerMotor.Step"/> を呼び、変位を CharacterController に適用する。
        /// 向きは <see cref="FirstPersonLook"/> 本体 (この GameObject) の yaw を使う。
        /// </summary>
        /// <param name="crouch">しゃがみたいか (<see cref="UpdateCrouch"/> の結果)。false でも頭上が塞がっていればしゃがんだまま。</param>
        public void Tick(Vector2 move, bool sprint, bool crouch, bool jumpPressed, float deltaSeconds)
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("PlayerController is not initialized");
            }

            if (!_controller.enabled || !_controller.gameObject.activeInHierarchy)
            {
                return;
            }

            UpdateStance(crouch);

            var yawRadians = _look.transform.eulerAngles.y * Mathf.Deg2Rad;
            var displacement = _motor.Step(
                move.ToNumerics(),
                yawRadians,
                sprint,
                _isCrouched,
                jumpPressed,
                _controller.isGrounded,
                deltaSeconds);

            _controller.Move(displacement.ToUnity());
            ReportGrounded(_controller.isGrounded);
        }

        /// <summary>
        /// 立った高さのカプセルが自分以外のコライダー (トリガーを除く) に重ならないか。
        /// 床や横の壁に触れているだけで塞がっていると見なさないよう、カプセルを CharacterController の skinWidth だけ細くして調べる。
        /// </summary>
        public bool HasHeadroomToStand()
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("PlayerController is not initialized");
            }

            _stance.SphereCenters(_stance.Standing, out var bottomY, out var topY);
            var body = transform;
            var scale = body.lossyScale;
            var radius = _stance.Radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            var margin = Mathf.Min(_controller.skinWidth, radius * 0.5f);
            var bottom = body.TransformPoint(new Vector3(_standingCenter.x, bottomY, _standingCenter.z));
            var top = body.TransformPoint(new Vector3(_standingCenter.x, topY, _standingCenter.z));

            // Physics.CheckCapsule では自分の CharacterController にも当たってしまうので、重なりを列挙して自分を除く。
            var count = Physics.OverlapCapsuleNonAlloc(
                bottom, top, radius - margin, _overlapBuffer, _headroomMask, QueryTriggerInteraction.Ignore);
            var blocked = false;
            for (var i = 0; i < count; i++)
            {
                var hit = _overlapBuffer[i];
                if (hit != null && !hit.transform.IsChildOf(body))
                {
                    blocked = true;
                    break;
                }
            }

            Array.Clear(_overlapBuffer, 0, count);
            return !blocked;
        }

        /// <summary>
        /// 同じ GameObject の CharacterController と FirstPersonLook、プロジェクト全体の Move / Sprint / Crouch / Jump を探して初期化する。
        /// 見つからないか設定値が不正なら理由を Debug.LogError に出して false を返す。
        /// </summary>
        public bool TryInitializeFromHierarchy()
        {
            var controller = GetComponent<CharacterController>();
            if (controller == null)
            {
                Debug.LogError($"{LogTag} '{name}' に CharacterController がありません。", this);
                return false;
            }

            var look = GetComponent<FirstPersonLook>();
            if (look == null)
            {
                Debug.LogError(
                    $"{LogTag} '{name}' に FirstPersonLook がありません。移動の向きは FirstPersonLook 本体の yaw を使います。",
                    this);
                return false;
            }

            var actions = InputSystem.actions;
            if (actions == null)
            {
                Debug.LogError(
                    $"{LogTag} プロジェクト全体の Input Actions が設定されていません (Project Settings > Input System Package)。",
                    this);
                return false;
            }

            var move = actions.FindAction(MoveActionPath);
            var sprint = actions.FindAction(SprintActionPath);
            var crouch = actions.FindAction(CrouchActionPath);
            var jump = actions.FindAction(JumpActionPath);
            if (move == null || sprint == null || crouch == null || jump == null)
            {
                Debug.LogError(
                    $"{LogTag} Input Actions '{actions.name}' に {MissingActions(move, sprint, crouch, jump)} がありません。",
                    this);
                return false;
            }

            try
            {
                Initialize(controller, look, move, sprint, crouch, jump, CreateSpec());
            }
            catch (ArgumentOutOfRangeException e)
            {
                Debug.LogError($"{LogTag} 移動またはしゃがみの設定値が不正です: {e.Message}", this);
                return false;
            }

            return true;
        }

        /// <summary>
        /// 参照を直接渡して初期化する。controller と look はこの GameObject のものであること。
        /// 立ちの寸法は今の controller の height / center / radius と目の位置から取り、しゃがみの高さは <see cref="CrouchHeight"/>。
        /// しゃがみの高さが controller と合わなければ ArgumentOutOfRangeException。
        /// </summary>
        public void Initialize(
            CharacterController controller,
            FirstPersonLook look,
            InputAction moveAction,
            InputAction sprintAction,
            InputAction crouchAction,
            InputAction jumpAction,
            MotorSpec spec)
        {
            if (controller == null)
            {
                throw new ArgumentNullException(nameof(controller));
            }

            if (look == null)
            {
                throw new ArgumentNullException(nameof(look));
            }

            if (moveAction == null)
            {
                throw new ArgumentNullException(nameof(moveAction));
            }

            if (sprintAction == null)
            {
                throw new ArgumentNullException(nameof(sprintAction));
            }

            if (crouchAction == null)
            {
                throw new ArgumentNullException(nameof(crouchAction));
            }

            if (jumpAction == null)
            {
                throw new ArgumentNullException(nameof(jumpAction));
            }

            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (controller.gameObject != gameObject)
            {
                throw new ArgumentException($"controller must be on '{name}'", nameof(controller));
            }

            if (look.gameObject != gameObject)
            {
                throw new ArgumentException($"look must be on '{name}' so movement follows its yaw", nameof(look));
            }

            // しゃがんだまま測ると縮んだ寸法を立ちと取り違えるので、先に立ちへ戻す。
            if (_isCrouched && _controller != null)
            {
                ApplyStance(false);
            }

            var eye = ResolveEye();
            var standingEyeLocal = eye != null ? transform.InverseTransformPoint(eye.position) : Vector3.zero;
            var standingCenter = controller.center;
            var stance = new StanceGeometry(
                controller.height, standingCenter.y, standingEyeLocal.y, _crouchHeight, controller.radius);

            // OnEnable 済み (動作中) に差し替えるなら、自分が有効にした古いアクションを戻してから新しいものを有効にする。
            if (_isLive)
            {
                ReleaseActions();
            }

            _controller = controller;
            _look = look;
            _moveAction = moveAction;
            _sprintAction = sprintAction;
            _crouchAction = crouchAction;
            _jumpAction = jumpAction;
            _motor = new PlayerMotor(spec);
            _stance = stance;
            _eye = eye;
            _standingEyeLocal = standingEyeLocal;
            _standingCenter = standingCenter;
            _isCrouched = false;
            _lastGrounded = null;

            if (_isLive)
            {
                AcquireActions();
            }
        }

        /// <summary>シリアライズされた値から <see cref="MotorSpec"/> を作る。値が不正なら ArgumentOutOfRangeException。</summary>
        public MotorSpec CreateSpec()
        {
            return new MotorSpec(_walkSpeed, _sprintSpeed, _jumpHeight, _gravity, _airControl, _crouchSpeed);
        }

        /// <summary>接地状態のログ。例: <c>[Player] grounded=true</c>。</summary>
        public static string FormatGroundedLog(bool grounded)
        {
            return $"{LogTag} grounded={(grounded ? "true" : "false")}";
        }

        /// <summary>しゃがみ状態のログ。例: <c>[Player] crouched=true</c>。</summary>
        public static string FormatCrouchedLog(bool crouched)
        {
            return $"{LogTag} crouched={(crouched ? "true" : "false")}";
        }

        private void UpdateStance(bool wantsCrouch)
        {
            if (wantsCrouch == _isCrouched)
            {
                return;
            }

            if (!wantsCrouch && !HasHeadroomToStand())
            {
                return;
            }

            ApplyStance(wantsCrouch);
        }

        private void ApplyStance(bool crouched)
        {
            var shape = _stance.Shape(crouched);
            _controller.height = shape.Height;
            _controller.center = new Vector3(_standingCenter.x, shape.CenterY, _standingCenter.z);
            if (_eye != null)
            {
                _eye.position = transform.TransformPoint(new Vector3(_standingEyeLocal.x, shape.EyeY, _standingEyeLocal.z));
            }

            if (_isCrouched == crouched)
            {
                return;
            }

            _isCrouched = crouched;
            Debug.Log(FormatCrouchedLog(crouched), this);
        }

        // ViewRig の Awake より先でも決まるよう、ViewRig.FindEye も自分で階層を探す。
        private Transform ResolveEye()
        {
            var eye = ViewRig.FindEye(transform);
            if (eye != null)
            {
                return eye;
            }

            var camera = FirstPersonLook.FindChildCamera(transform);
            return camera != null ? camera.transform : null;
        }

        private void ReportGrounded(bool grounded)
        {
            if (_lastGrounded == grounded)
            {
                return;
            }

            _lastGrounded = grounded;
            Debug.Log(FormatGroundedLog(grounded), this);
        }

        private static bool EnableIfNeeded(InputAction action)
        {
            if (action.enabled)
            {
                return false;
            }

            action.Enable();
            return true;
        }

        private static void DisableIfOwned(InputAction action, ref bool owned)
        {
            if (owned && action != null)
            {
                action.Disable();
            }

            owned = false;
        }

        private static string MissingActions(InputAction move, InputAction sprint, InputAction crouch, InputAction jump)
        {
            var missing = new System.Collections.Generic.List<string>(4);
            if (move == null)
            {
                missing.Add($"'{MoveActionPath}'");
            }

            if (sprint == null)
            {
                missing.Add($"'{SprintActionPath}'");
            }

            if (crouch == null)
            {
                missing.Add($"'{CrouchActionPath}'");
            }

            if (jump == null)
            {
                missing.Add($"'{JumpActionPath}'");
            }

            return string.Join(", ", missing);
        }
    }
}
