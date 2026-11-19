using System;
using Shooter.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed partial class PlayerController : MonoBehaviour, ICharacterMotionSource
    {
        public const string MoveActionPath = InputActionPaths.Move;
        public const string SprintActionPath = InputActionPaths.Sprint;
        public const string CrouchActionPath = InputActionPaths.Crouch;
        public const string JumpActionPath = InputActionPaths.Jump;
        public const string DodgeActionPath = InputActionPaths.Dodge;

        public const string LogTag = "[Player]";

        public const float DefaultWalkSpeed = MotorSpec.DefaultWalkSpeed;
        public const float DefaultSprintSpeed = MotorSpec.DefaultSprintSpeed;
        public const float DefaultCrouchSpeed = MotorSpec.DefaultCrouchSpeed;
        public const float DefaultJumpHeight = MotorSpec.DefaultJumpHeight;
        public const float DefaultGravity = MotorSpec.DefaultGravity;
        public const float DefaultAirControl = MotorSpec.DefaultAirControl;

        // 立ち 2m・半径 0.5m のプレイヤーで、目が足元から 0.8m になる高さにする。
        public const float DefaultCrouchHeight = 1.2f;

        [SerializeField, Tooltip("移動の調整値。空なら Resources の MotorSettings、それも無ければ既定の値。ネット対戦の NetworkPlayerMotor と同じものを使う")]
        private MotorSettings _motorSettings;

        [SerializeField, Min(0.01f), Tooltip("しゃがみのカプセルの高さ (m)。半径の 2 倍以上、立ちの高さ以下")]
        private float _crouchHeight = DefaultCrouchHeight;

        [SerializeField, Tooltip("立ち上がれるか・乗り越えられるかを調べるときに障害物として扱うレイヤー")]
        private LayerMask _headroomMask = Physics.DefaultRaycastLayers;

        private readonly ControlHandoff _handoff = new ControlHandoff();
        private CharacterController _controller;
        private FirstPersonLook _look;
        // シーンに置いたまま再コンパイルすると、Unity が private の値も保存し直し、null の InputAction を空のアクションとして戻してしまう (再生でシーンを読み直さないので、そのまま使われる)。
        [NonSerialized]
        private InputAction _moveAction;
        [NonSerialized]
        private InputAction _sprintAction;
        [NonSerialized]
        private InputAction _crouchAction;
        [NonSerialized]
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

        public PlayerMotor Motor => _motor;

        public bool IsGrounded => _controller != null && _controller.isGrounded;

        public IPlayerMoveFilter MoveFilter { get; set; }

        public IPlayerCarryLoad CarryLoad { get; set; }

        public float CarrySpeedMultiplier => CarryLoad != null ? CarryLoad.CarrySpeedMultiplier(Spec.CarryLoad) : 1f;

        private Jump _jumpRule = Jump.On;

        // Off で止めるのはふつうのジャンプだけで、Jump は乗り越え・崖のぼり・伏せから立つ・移動の横取り (はしご・ジップラインなど) には渡す。
        public Jump JumpRule
        {
            get => _jumpRule;
            set
            {
                if (!Enum.IsDefined(typeof(Jump), value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "value is not a defined Jump");
                }

                _jumpRule = value;
            }
        }

        public ControlHandoff Handoff => _handoff;

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

        public MotorSettings MotorSettings
        {
            get => _motorSettings;
            set => _motorSettings = value;
        }

        // setter で作り直さないのは、初期化の前に値だけ置く使い方 (Inspector・テスト) で、置くたびに初期化が走らないようにするため。
        // GameBootstrap のように付けた時点の Awake で既に初期化された後に設定を替えるときは、こちらで PlayerMotor から作り直す。
        public void UseMotorSettings(MotorSettings settings)
        {
            if (!IsInitialized)
            {
                _motorSettings = settings;
                return;
            }

            // 不正な値で投げたときに、設定だけ替わって動きは前の値のままという食い違いを残さないよう、作れることを確かめてから置く。
            var spec = MotorSettings.Resolve(settings);
            var prone = _proneSpecAssigned ? _proneSpec : MotorSettings.ResolveProne(settings);
            _motorSettings = settings;
            _proneSpec = prone;
            Initialize(_controller, _look, _moveAction, _sprintAction, _crouchAction, _jumpAction, spec);
        }

        // 初期化の前でも、初期化したときに使われる値を返すため設定から作る。
        public MotorSpec Spec => _motor != null ? _motor.Spec : MotorSettings.ResolveOrDefault(_motorSettings);

        public float WalkSpeed => Spec.WalkSpeed;
        public float SprintSpeed => Spec.SprintSpeed;
        public float CrouchSpeed => Spec.CrouchSpeed;
        public float JumpHeight => Spec.JumpHeight;
        public float Gravity => Spec.Gravity;
        public float AirControl => Spec.AirControl;
        public float CrouchHeight => _crouchHeight;

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
            ResetTraversal();
            ResetPlatform();
        }

        // プロジェクト全体のアクションは他の部品と共有なので、自分が有効にしたものだけ後で無効に戻す。
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
                _proneAction != null && _proneAction.WasPressedThisFrame(),
                ReadLean(),
                _jumpAction.IsPressed());
        }

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

            // 潜る入力をしゃがみの切り替え (Toggle) に通すと、水から上がったときにしゃがんだままになるので、ボタンを押している間だけ潜らせる。
            if (IsSwimming)
            {
                ReleaseSprint(SprintInput.ReasonSwim);
                _ascendHeld = input.JumpHeld;
                try
                {
                    Tick(input.Move, false, input.CrouchHeld, input.JumpPressed, input.Lean, deltaSeconds);
                }
                finally
                {
                    _ascendHeld = false;
                }

                return;
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

            // 空中で押したジャンプは地上のジャンプにならない (PlayerMotor) ので、しゃがみも解かない。猶予の間は地上のジャンプなので解く。
            var crouch = UpdateCrouch(
                input.CrouchHeld, input.CrouchPressed, jumpPressed && (_controller.isGrounded || IsInJumpGrace), sprint, input.CrouchDevice);
            Tick(input.Move, sprint, crouch, jumpPressed, input.Lean, deltaSeconds);
        }

        public void Tick(Vector2 move, bool sprint, bool crouch, bool jumpPressed, float deltaSeconds)
        {
            Tick(move, sprint, crouch, jumpPressed, 0, deltaSeconds);
        }

        public void Tick(Vector2 move, bool sprint, bool crouch, bool jumpPressed, int lean, float deltaSeconds)
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("PlayerController is not initialized");
            }

            if (lean < -1 || lean > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(lean), lean, "lean must be -1, 0 or 1");
            }

            if (!_controller.enabled || !_controller.gameObject.activeInHierarchy)
            {
                StepStamina(false, deltaSeconds);
                return;
            }

            MoveFilter?.Filter(ref move, ref sprint, ref crouch, ref jumpPressed);
            FilterInputForDodge(ref sprint, ref crouch, ref jumpPressed);
            FilterInputForVault(ref sprint, ref crouch, ref jumpPressed);
            FilterInputForProne(ref sprint, ref jumpPressed);
            FilterInputForMantle(ref sprint, ref crouch, ref jumpPressed);
            FilterInputForStamina(ref sprint);
            var sprinted = false;

            var wasSwimming = _swim.IsSwimming;
            UpdateSwim(deltaSeconds);

            // CharacterMotorSimulation と同じく、猶予を持ち越すのはふつうの移動の道だけにする。横取り・泳ぎ・壁・回避などで宙に出たときまで残すと、歩いて縁を離れたのでないのに地上のジャンプができるため。
            var jumpGraceLeft = _jumpGraceLeft;
            _jumpGraceLeft = 0f;

            Vector3 displacement;
            var paddled = false;
            var movementOverride = SelectMovementOverride();

            // 横取り (はしご・ジップラインなど) は体を自分で置き、泳いでいる間は床に立っていないので運ばない。視点を回すので移動の向きを読む前に呼ぶ。
            var canRide = movementOverride == null && !_swim.IsSwimming;
            Ride(canRide);
            var wallRun = WallRunMotion.None;
            if (movementOverride != null)
            {
                StopWallRun();
                displacement = StepMovementOverride(movementOverride, move, sprint, crouch, jumpPressed, deltaSeconds);
            }
            else if (_swim.IsSwimming && !IsVaulting && !IsMantling)
            {
                StopWallRun();
                UpdateStance(false, false);
                if (!TryClimbOutOfWater(jumpPressed, deltaSeconds, out displacement))
                {
                    displacement = Paddle(move, crouch, jumpPressed, wasSwimming, deltaSeconds);
                    paddled = true;
                }
            }
            else
            {
                UpdateStance(crouch, IsProneActive);
                if (TryOverrideMove(move, jumpPressed, deltaSeconds, out displacement))
                {
                    StopWallRun();
                }
                else
                {
                    var yawRadians = _look.transform.eulerAngles.y * Mathf.Deg2Rad;
                    var jump = jumpPressed && _jumpRule == Jump.On;

                    // CharacterMotorSimulation と同じく壁走りを先に試し、壁に付いていない tick だけ空中のジャンプとふつうの移動に回す。
                    wallRun = TryRunOnWall(move, jump, yawRadians, deltaSeconds, out displacement);
                    if (wallRun == WallRunMotion.None)
                    {
                        var carry = CarrySpeedMultiplier;
                        var inJumpGrace = StepAirJump(move, jump, yawRadians, jumpGraceLeft, deltaSeconds);

                        // 押しているだけで止まっている・しゃがんでいる tick は走っていないので減らさない。
                        sprinted = sprint && !IsProne && !IsCrouched
                            && move.sqrMagnitude >= SprintInput.MoveThreshold * SprintInput.MoveThreshold;
                        displacement = IsProne
                            ? _motor.StepProne(
                                move.ToNumerics(), yawRadians, _proneSpec.Speed, _controller.isGrounded, deltaSeconds, carry).ToUnity()
                            : _motor.Step(
                                move.ToNumerics(),
                                yawRadians,
                                sprint,
                                IsCrouched,
                                jump,
                                _controller.isGrounded,
                                deltaSeconds,
                                carry,
                                inJumpGrace).ToUnity();
                    }
                }
            }

            var before = transform.position;
            var collisions = _controller.Move(displacement);
            if (paddled)
            {
                StopPaddlingAgainst(collisions);
            }

            SettleWallRun(wallRun, transform.position - before, collisions, deltaSeconds);
            StepStamina(sprinted, deltaSeconds);

            // 泳いでいる間は水底や天井に触れても立っていないので、着地・足音・落下のダメージに数えない。
            var footing = _controller.isGrounded && !_swim.IsSwimming;
            EndRide(footing, canRide);
            ReportLanding(footing);
            ReportGrounded(footing);
            FinishTraversal(footing, move, sprint, crouch, lean, deltaSeconds);
        }

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
                if (!_proneSpecAssigned)
                {
                    _proneSpec = MotorSettings.ResolveProne(_motorSettings);
                }

                Initialize(controller, look, move, sprint, crouch, jump, CreateSpec());
            }
            catch (ArgumentOutOfRangeException e)
            {
                Debug.LogError($"{LogTag} 移動・しゃがみ・伏せの設定値が不正です: {e.Message}", this);
                return false;
            }

            ResolveDodgeAction(actions);
            ResolveProneAction(actions);
            return true;
        }

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
            ResetTraversal();
            ResetPlatform();
            _leanBody = null;
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

        public MotorSpec CreateSpec()
        {
            return MotorSettings.Resolve(_motorSettings);
        }

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
