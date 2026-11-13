using System;
using Fps.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fps.Runtime
{
    /// <summary>
    /// 移動入力を <see cref="PlayerMotor"/> に渡し、返ってきた変位を <see cref="CharacterController.Move"/> に適用する。
    /// 移動量の計算はすべて <see cref="PlayerMotor"/> が行い、ここは入力の読み取りと結果の適用だけを受け持つ。
    /// 参照はコードで解決するので Inspector の配線は要らない: CharacterController と <see cref="FirstPersonLook"/> は同じ
    /// GameObject、入力はプロジェクト全体のアクション (<see cref="InputSystem.actions"/>) の Move / Sprint / Jump。
    /// 解決できなければエラーを出して自分を無効にする。
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
        public const string JumpActionPath = InputActionPaths.Jump;

        public const string LogTag = "[Player]";

        public const float DefaultWalkSpeed = 5f;
        public const float DefaultSprintSpeed = 8f;
        public const float DefaultJumpHeight = 1.2f;
        public const float DefaultGravity = 20f;
        public const float DefaultAirControl = 0.1f;

        [SerializeField, Min(0f), Tooltip("歩きの水平速度 (m/s)")]
        private float _walkSpeed = DefaultWalkSpeed;

        [SerializeField, Min(0f), Tooltip("ダッシュ中の水平速度 (m/s)")]
        private float _sprintSpeed = DefaultSprintSpeed;

        [SerializeField, Min(0f), Tooltip("ジャンプで到達する高さ (m)")]
        private float _jumpHeight = DefaultJumpHeight;

        [SerializeField, Min(0.01f), Tooltip("重力加速度の大きさ (m/s²)。下向きに適用する")]
        private float _gravity = DefaultGravity;

        [SerializeField, Range(0f, 1f), Tooltip("空中で水平速度を入力方向へ寄せる 1 フレームあたりの割合")]
        private float _airControl = DefaultAirControl;

        private CharacterController _controller;
        private FirstPersonLook _look;
        private InputAction _moveAction;
        private InputAction _sprintAction;
        private InputAction _jumpAction;
        private PlayerMotor _motor;
        private bool _enabledMove;
        private bool _enabledSprint;
        private bool _enabledJump;
        private bool _isLive;
        private bool? _lastGrounded;
        private ControlSettings _controlSettings = ControlSettings.Default;
        private InputDeviceKind _device = InputDeviceKind.KeyboardMouse;
        private SprintInput _sprint;
        private WeaponController _releaseWeapon;
        private Damageable _releaseDamageable;

        public CharacterController Controller => _controller;
        public FirstPersonLook Look => _look;
        public InputAction MoveAction => _moveAction;
        public InputAction SprintAction => _sprintAction;
        public InputAction JumpAction => _jumpAction;

        /// <summary>移動計算。未初期化なら null。</summary>
        public PlayerMotor Motor => _motor;

        public bool IsInitialized =>
            _controller != null && _look != null && _moveAction != null && _sprintAction != null && _jumpAction != null && _motor != null;

        public float WalkSpeed => _walkSpeed;
        public float SprintSpeed => _sprintSpeed;
        public float JumpHeight => _jumpHeight;
        public float Gravity => _gravity;
        public float AirControl => _airControl;

        /// <summary>
        /// ダッシュのモードを決める操作設定。Awake で <see cref="ControlSettingsStore"/> から読む (Edit Mode では Awake が走らないので初期値)。
        /// 変えると次の <see cref="UpdateSprint"/> から使う。
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
            _enabledJump = EnableIfNeeded(_jumpAction);
        }

        private void ReleaseActions()
        {
            DisableIfOwned(_moveAction, ref _enabledMove);
            DisableIfOwned(_sprintAction, ref _enabledSprint);
            DisableIfOwned(_jumpAction, ref _enabledJump);
        }

        private void Update()
        {
            var move = _moveAction.ReadValue<Vector2>();

            // 押されたダッシュボタンの機器を優先し、どちらも動いていなければ直前の機器のまま。
            var device = InputDeviceKinds.Of(_sprintAction, InputDeviceKinds.Of(_moveAction, _device));
            var sprint = UpdateSprint(move, _sprintAction.IsPressed(), _sprintAction.WasPressedThisFrame(), device);
            Tick(move, sprint, _jumpAction.WasPressedThisFrame(), Time.deltaTime);
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
        /// 1 フレーム分の入力で <see cref="PlayerMotor.Step"/> を呼び、変位を CharacterController に適用する。
        /// 向きは <see cref="FirstPersonLook"/> 本体 (この GameObject) の yaw を使う。
        /// </summary>
        public void Tick(Vector2 move, bool sprint, bool jumpPressed, float deltaSeconds)
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("PlayerController is not initialized");
            }

            if (!_controller.enabled || !_controller.gameObject.activeInHierarchy)
            {
                return;
            }

            var yawRadians = _look.transform.eulerAngles.y * Mathf.Deg2Rad;
            var displacement = _motor.Step(
                move.ToNumerics(),
                yawRadians,
                sprint,
                jumpPressed,
                _controller.isGrounded,
                deltaSeconds);

            _controller.Move(displacement.ToUnity());
            ReportGrounded(_controller.isGrounded);
        }

        /// <summary>
        /// 同じ GameObject の CharacterController と FirstPersonLook、プロジェクト全体の Move / Sprint / Jump を探して初期化する。
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
            var jump = actions.FindAction(JumpActionPath);
            if (move == null || sprint == null || jump == null)
            {
                Debug.LogError(
                    $"{LogTag} Input Actions '{actions.name}' に {MissingActions(move, sprint, jump)} がありません。",
                    this);
                return false;
            }

            MotorSpec spec;
            try
            {
                spec = CreateSpec();
            }
            catch (ArgumentOutOfRangeException e)
            {
                Debug.LogError($"{LogTag} 移動の設定値が不正です: {e.Message}", this);
                return false;
            }

            Initialize(controller, look, move, sprint, jump, spec);
            return true;
        }

        /// <summary>参照を直接渡して初期化する。controller と look はこの GameObject のものであること。</summary>
        public void Initialize(
            CharacterController controller,
            FirstPersonLook look,
            InputAction moveAction,
            InputAction sprintAction,
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

            // OnEnable 済み (動作中) に差し替えるなら、自分が有効にした古いアクションを戻してから新しいものを有効にする。
            if (_isLive)
            {
                ReleaseActions();
            }

            _controller = controller;
            _look = look;
            _moveAction = moveAction;
            _sprintAction = sprintAction;
            _jumpAction = jumpAction;
            _motor = new PlayerMotor(spec);
            _lastGrounded = null;

            if (_isLive)
            {
                AcquireActions();
            }
        }

        /// <summary>シリアライズされた値から <see cref="MotorSpec"/> を作る。値が不正なら ArgumentOutOfRangeException。</summary>
        public MotorSpec CreateSpec()
        {
            return new MotorSpec(_walkSpeed, _sprintSpeed, _jumpHeight, _gravity, _airControl);
        }

        /// <summary>接地状態のログ。例: <c>[Player] grounded=true</c>。</summary>
        public static string FormatGroundedLog(bool grounded)
        {
            return $"{LogTag} grounded={(grounded ? "true" : "false")}";
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

        private static string MissingActions(InputAction move, InputAction sprint, InputAction jump)
        {
            var missing = new System.Collections.Generic.List<string>(3);
            if (move == null)
            {
                missing.Add($"'{MoveActionPath}'");
            }

            if (sprint == null)
            {
                missing.Add($"'{SprintActionPath}'");
            }

            if (jump == null)
            {
                missing.Add($"'{JumpActionPath}'");
            }

            return string.Join(", ", missing);
        }
    }
}
