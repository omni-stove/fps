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
    /// 参照はコードで解決するので Inspector の配線は要らない: CharacterController と <see cref="FirstPersonLook"/> は同じ
    /// GameObject、目は <see cref="ViewRig.FindEye"/> (無ければ子のカメラ)、入力はプロジェクト全体のアクション
    /// (<see cref="InputSystem.actions"/>) の Move / Sprint / Crouch / Jump。解決できなければエラーを出して自分を無効にする
    /// (Dodge と Prone は無くても警告だけで、回避・伏せを入力からできなくなる)。
    /// 乗り物の座席などが <see cref="Handoff"/> で操作を引き取っている間は、入力を読まず何も押していないものとして動く。
    /// 姿勢 (.Stance.cs)・伏せ (.Prone.cs)・ダッシュ (.Sprint.cs)・着地と落下ダメージ (.Landing.cs)・回避 (.Dodge.cs)・乗り越え (.Vault.cs)・
    /// 崖のぼり (.Mantle.cs)・移動の横取り (.MovementOverride.cs) は、<see cref="Tick"/> から呼ぶメソッドとして別のファイルに置く。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed partial class PlayerController : MonoBehaviour, ICharacterMotionSource
    {
        /// <summary>Assets/InputSystem_Actions.inputactions のアクション。</summary>
        public const string MoveActionPath = InputActionPaths.Move;
        public const string SprintActionPath = InputActionPaths.Sprint;
        public const string CrouchActionPath = InputActionPaths.Crouch;
        public const string JumpActionPath = InputActionPaths.Jump;
        public const string DodgeActionPath = InputActionPaths.Dodge;

        public const string LogTag = "[Player]";

        public const float DefaultWalkSpeed = 5f;
        public const float DefaultSprintSpeed = 8f;
        public const float DefaultCrouchSpeed = 2.5f;
        public const float DefaultJumpHeight = 1.2f;
        public const float DefaultGravity = 20f;
        public const float DefaultAirControl = 0.1f;

        /// <summary>しゃがみのカプセルの高さ (m)。立ち 2m・半径 0.5m のプレイヤーで、目は足元から 0.8m になる。</summary>
        public const float DefaultCrouchHeight = 1.2f;

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

        [SerializeField, Tooltip("立ち上がれるか・乗り越えられるかを調べるときに障害物として扱うレイヤー")]
        private LayerMask _headroomMask = Physics.DefaultRaycastLayers;

        private readonly ControlHandoff _handoff = new ControlHandoff();
        private CharacterController _controller;
        private FirstPersonLook _look;
        private InputAction _moveAction;
        private InputAction _sprintAction;
        private InputAction _crouchAction;
        private InputAction _jumpAction;
        private PlayerMotor _motor;
        private bool _enabledMove;
        private bool _enabledSprint;
        private bool _enabledCrouch;
        private bool _enabledJump;
        private bool _isLive;
        private bool? _lastGrounded;
        private ControlSettings _controlSettings = ControlSettings.Default;
        private bool _listeningToSettings;

        public CharacterController Controller => _controller;
        public FirstPersonLook Look => _look;
        public InputAction MoveAction => _moveAction;
        public InputAction SprintAction => _sprintAction;
        public InputAction CrouchAction => _crouchAction;
        public InputAction JumpAction => _jumpAction;

        /// <summary>移動計算。未初期化なら null。</summary>
        public PlayerMotor Motor => _motor;

        /// <summary>直前の <see cref="CharacterController.Move"/> で接地していたか。未初期化なら false。</summary>
        public bool IsGrounded => _controller != null && _controller.isGrounded;

        /// <summary>
        /// <see cref="Tick"/> が姿勢を決める前に、移動・ダッシュ・しゃがみ・ジャンプの入力を書き換える差し込み口 (カバーなど)。
        /// null なら入力をそのまま使う。
        /// </summary>
        public IPlayerMoveFilter MoveFilter { get; set; }

        /// <summary>
        /// 操作を手放す口。乗り物の座席などが <see cref="ControlHandoff.Claim"/> している間、<see cref="ApplyInput"/> は入力を何も押していないものとして扱う。
        /// 同じプレイヤーの <see cref="WeaponController.Handoff"/> にも同じものを配線する (オフラインは <see cref="GameBootstrap"/>)。
        /// </summary>
        public ControlHandoff Handoff => _handoff;

        /// <summary>操作を手放しているか (<see cref="Handoff"/> を誰かが宣言しているか)。</summary>
        public bool IsControlHandedOff => _handoff.IsHandedOff;

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
        /// ダッシュとしゃがみのモードを決める操作設定。Awake で <see cref="AppliedControlSettings.Current"/> を読み、設定画面で変わるたびに入れ直す
        /// (Edit Mode では Awake が走らないので初期値)。
        /// 変えると次の <see cref="UpdateSprint"/> / <see cref="UpdateCrouch"/> から使う。
        /// </summary>
        public ControlSettings ControlSettings
        {
            get => _controlSettings;
            set => _controlSettings = value ?? throw new ArgumentNullException(nameof(value));
        }

        private void Awake()
        {
            _controlSettings = AppliedControlSettings.Current;
            AppliedControlSettings.Changed += HandleSettingsChanged;
            _listeningToSettings = true;
            if (!IsInitialized && !TryInitializeFromHierarchy())
            {
                enabled = false;
            }
        }

        private void OnDestroy()
        {
            BindSprintRelease(null, null);
            if (_listeningToSettings)
            {
                AppliedControlSettings.Changed -= HandleSettingsChanged;
                _listeningToSettings = false;
            }
        }

        private void HandleSettingsChanged(ControlSettings settings)
        {
            _controlSettings = settings;
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
            CancelDodge();
            CancelVault();
            CancelDive();
            CancelMantle();
            EndMovementOverride();
        }

        /// <summary>プロジェクト全体のアクションは他の部品と共有なので、自分が有効にしたものだけ後で無効に戻す。</summary>
        private void AcquireActions()
        {
            _enabledMove = EnableIfNeeded(_moveAction);
            _enabledSprint = EnableIfNeeded(_sprintAction);
            _enabledCrouch = EnableIfNeeded(_crouchAction);
            _enabledJump = EnableIfNeeded(_jumpAction);
            AcquireDodgeAction();
            AcquireProneAction();
        }

        private void ReleaseActions()
        {
            DisableIfOwned(_moveAction, ref _enabledMove);
            DisableIfOwned(_sprintAction, ref _enabledSprint);
            DisableIfOwned(_crouchAction, ref _enabledCrouch);
            DisableIfOwned(_jumpAction, ref _enabledJump);
            ReleaseDodgeAction();
            ReleaseProneAction();
        }

        private void Update()
        {
            ApplyInput(ReadInput(), Time.deltaTime);
        }

        // 手放している間はアクションに触れない。押されたままのボタンの機器でモードが切り替わるのも避ける。
        private PlayerMoveInput ReadInput()
        {
            if (IsControlHandedOff)
            {
                return PlayerMoveInput.Idle(_device, _crouchDevice);
            }

            // 押されたダッシュボタンの機器を優先し、どちらも動いていなければ直前の機器のまま。
            var device = InputDeviceKinds.Of(_sprintAction, InputDeviceKinds.Of(_moveAction, _device));
            var crouchDevice = InputDeviceKinds.Of(_crouchAction, InputDeviceKinds.Of(_moveAction, _crouchDevice));
            return new PlayerMoveInput(
                _moveAction.ReadValue<Vector2>(),
                HeldInput.IsHeld(_sprintAction),
                _sprintAction.WasPressedThisFrame(),
                device,
                _crouchAction.IsPressed(),
                _crouchAction.WasPressedThisFrame(),
                crouchDevice,
                _jumpAction.WasPressedThisFrame(),
                _dodgeAction != null && _dodgeAction.WasPressedThisFrame(),
                _proneAction != null && _proneAction.WasPressedThisFrame());
        }

        /// <summary>
        /// 1 フレーム分の入力で、回避の開始・ダッシュと伏せとしゃがみの解釈 (<see cref="UpdateSprint"/> / <see cref="UpdateProne"/> / <see cref="UpdateCrouch"/>) をしてから
        /// <see cref="Tick"/> する。伏せから起き上がるのに押した Jump では跳ばない。
        /// Play 中は Update が Input Actions から読んだ入力で毎フレーム呼ぶ。
        /// <see cref="IsControlHandedOff"/> の間は input を捨て、何も押していない入力 (<see cref="PlayerMoveInput.Idle"/>) として扱う。
        /// </summary>
        public void ApplyInput(PlayerMoveInput input, float deltaSeconds)
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("PlayerController is not initialized");
            }

            if (IsControlHandedOff)
            {
                input = PlayerMoveInput.Idle(_device, _crouchDevice);
            }

            // ダッシュとしゃがみを解釈する前に始める。回避が解いたしゃがみとダッシュを、同じフレームの押下で戻さないため。
            if (input.DodgePressed)
            {
                TryStartDodge(input.Move);
            }

            var sprint = UpdateSprint(input.Move, input.SprintHeld, input.SprintPressed, input.SprintDevice);

            // しゃがみより先に解釈する。伏せている間のしゃがみの押下を、伏せを解いてからしゃがみとして読ませるため。
            var wasProne = IsProneActive || IsProne;
            UpdateProne(input.PronePressed, input.CrouchPressed, input.JumpPressed, sprint, input.Move);
            sprint = IsSprinting;
            var jumpPressed = input.JumpPressed && !wasProne;

            // 空中で押したジャンプは跳ばない (PlayerMotor) ので、しゃがみも解かない。
            var crouch = UpdateCrouch(
                input.CrouchHeld, input.CrouchPressed, jumpPressed && _controller.isGrounded, sprint, input.CrouchDevice);
            Tick(input.Move, sprint, crouch, jumpPressed, deltaSeconds);
        }

        /// <summary>
        /// 1 フレーム分の入力で姿勢 (立ち・しゃがみ・伏せ) を決め、<see cref="PlayerMotor.Step"/> を呼び、変位を CharacterController に適用する。
        /// 伏せたいか (<see cref="IsProneActive"/>) は <see cref="UpdateProne"/> が覚えたものを使い、しゃがみより優先する。
        /// 伏せている間は <see cref="ProneSpec.Speed"/> で動き、ダッシュもジャンプもしない。
        /// 向きは <see cref="FirstPersonLook"/> 本体 (この GameObject) の yaw を使う。
        /// <see cref="MoveFilter"/> があれば、姿勢を決める前に入力を書き換えさせる。
        /// <see cref="VaultRule"/> が On なら、Jump で前方の低い障害物を乗り越える (乗り越えられなければふつうにジャンプする)。
        /// <see cref="MantleRule"/> が On なら、乗り越えなかった Jump で、それより高い段によじ登る (よじ登れなければふつうにジャンプする)。
        /// 登録した <see cref="IMovementOverride"/> のどれかが有効なら、姿勢と移動はそれに任せる (<see cref="ActiveMovementOverride"/>)。
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

            MoveFilter?.Filter(ref move, ref sprint, ref crouch, ref jumpPressed);
            FilterInputForDodge(ref sprint, ref crouch, ref jumpPressed);
            FilterInputForVault(ref sprint, ref crouch, ref jumpPressed);
            FilterInputForProne(ref sprint, ref jumpPressed);
            FilterInputForMantle(ref sprint, ref crouch, ref jumpPressed);

            Vector3 displacement;
            var movementOverride = SelectMovementOverride();
            if (movementOverride != null)
            {
                displacement = StepMovementOverride(movementOverride, move, sprint, crouch, jumpPressed, deltaSeconds);
            }
            else
            {
                UpdateStance(crouch, IsProneActive);
                if (!TryOverrideMove(move, jumpPressed, deltaSeconds, out displacement))
                {
                    var yawRadians = _look.transform.eulerAngles.y * Mathf.Deg2Rad;
                    displacement = IsProne
                        ? _motor.StepAtSpeed(
                            move.ToNumerics(), yawRadians, _proneSpec.Speed, false, _controller.isGrounded, deltaSeconds).ToUnity()
                        : _motor.Step(
                            move.ToNumerics(),
                            yawRadians,
                            sprint,
                            IsCrouched,
                            jumpPressed,
                            _controller.isGrounded,
                            deltaSeconds).ToUnity();
                }
            }

            _controller.Move(displacement);
            var grounded = _controller.isGrounded;
            ReportLanding(grounded);
            ReportGrounded(grounded);
        }

        /// <summary>
        /// 乗り越え・崖のぼり・回避・飛び込みの最中なら、<see cref="PlayerMotor.Step"/> の代わりに動かす変位を displacement に入れて true を返す。
        /// どれも動いていなければ false (displacement は 0)。
        /// </summary>
        private bool TryOverrideMove(Vector2 move, bool jumpPressed, float deltaSeconds, out Vector3 displacement)
        {
            var overridden = false;
            displacement = Vector3.zero;

            // 乗り越えは足場の縁までの決まった軌道で、途中で回避に切り替えると縁にめり込むので先に聞く
            TryOverrideMoveForVault(move, jumpPressed, deltaSeconds, ref overridden, ref displacement);

            // 同じ Jump で乗り越えが始まらなかったときだけよじ登る。低い障害物は今までどおり乗り越えになる。
            if (!overridden)
            {
                TryOverrideMoveForMantle(jumpPressed, deltaSeconds, ref overridden, ref displacement);
            }

            if (!overridden)
            {
                TryOverrideMoveForDodge(move, jumpPressed, deltaSeconds, ref overridden, ref displacement);
            }

            if (!overridden)
            {
                TryOverrideMoveForDive(deltaSeconds, ref overridden, ref displacement);
            }

            return overridden;
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

            AppliedInputBindings.EnsureLoaded();
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

            ResolveDodgeAction(actions);
            ResolveProneAction(actions);
            return true;
        }

        /// <summary>
        /// 参照を直接渡して初期化する。controller と look はこの GameObject のものであること。
        /// 立ちの寸法は今の controller の height / center / radius と目の位置から取り、しゃがみの高さは <see cref="CrouchHeight"/>、
        /// 伏せの高さは <see cref="ProneSpec"/> の <see cref="ProneSpec.Height"/> (合わなければ合う範囲に収める)。
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

            // しゃがんだまま (伏せたまま) 測ると縮んだ寸法を立ちと取り違えるので、先に立ちへ戻す。
            if (_currentStance != PlayerStance.Standing && _controller != null)
            {
                ApplyStance(PlayerStance.Standing);
            }

            var eye = ResolveEye();
            var standingEyeLocal = eye != null ? transform.InverseTransformPoint(eye.position) : Vector3.zero;
            var standingCenter = controller.center;

            // しゃがみの高さが合わないときは、伏せの高さに関わらずしゃがみの理由で投げさせる。
            var proneHeight = _crouchHeight >= controller.radius * 2f && _crouchHeight <= controller.height
                ? FitProneHeight(_proneSpec.Height, _crouchHeight, controller.radius)
                : _crouchHeight;
            var stance = new StanceGeometry(
                controller.height, standingCenter.y, standingEyeLocal.y, _crouchHeight, proneHeight, controller.radius);

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
            CancelDodge();
            CancelVault();
            CancelDive();
            CancelMantle();
            EndMovementOverride();
            _motor = new PlayerMotor(spec);
            _dodgeMotion = new DodgeMotion(spec);
            _dive = new ProneDive(_proneSpec);
            _stance = stance;
            _eye = eye;
            _standingEyeLocal = standingEyeLocal;
            _standingCenter = standingCenter;
            _currentStance = PlayerStance.Standing;
            _lastGrounded = null;
            ApplyProneLook();

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
