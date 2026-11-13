using System;
using System.Collections.Generic;
using System.Globalization;
using Fps.Core;
using Fps.Runtime;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fps.Net
{
    /// <summary>
    /// ネットワーク越しのプレイヤー移動。<see cref="PlayerMotor"/> と CharacterController をサーバー権威で動かす。
    /// <list type="bullet">
    /// <item>オーナーのクライアント: <see cref="TickClock"/> の tick ごとに入力を読み、自分で先に動かして (予測)
    /// 入力をサーバーへ送る。サーバーから確定状態が届いたら <see cref="PredictionBuffer{TInput, TState}"/> で照合し、
    /// ずれが <see cref="ReconcileTolerance"/> を超えていればその tick に巻き戻して以降の入力を再シミュレーションする。</item>
    /// <item>サーバー: 受け取った入力で動かし、確定した tick・位置・速度を全クライアントへ返す。
    /// ホスト自身のプレイヤーはサーバー上で直接動かす。</item>
    /// <item>他人のクライアント: 受け取った位置を <see cref="InterpolationDelayTicks"/> tick 遅らせて補間表示する。</item>
    /// </list>
    /// tick はオーナーのクライアントが数えたものをサーバーがそのまま返す。プレイヤー同士は衝突させない
    /// (予測側には他人の確定位置が無く、衝突させると毎回ずれるため)。
    /// <para>
    /// 向き (yaw) はオーナーの <see cref="FirstPersonLook"/> が本体を回し、その値を入力に乗せて送る。
    /// サーバーは受け取った yaw で本体を回して確定状態と一緒に返し、他人のクライアントは位置と同じ遅れで補間して回す。
    /// オーナー自身の向きは視点操作が決めるので、サーバーからの yaw では上書きしない。
    /// </para>
    /// <para>
    /// しゃがみも入力に乗せて送り、予測とサーバーで同じ <see cref="NetMotorSimulation"/> を通して
    /// <see cref="NetStance"/> で CharacterController の height / center と目を変える。確定状態にしゃがみも乗せて返し、
    /// 予測と食い違えば位置が合っていても巻き戻す (<see cref="MotorState.PositionError"/>)。
    /// 他人のクライアントは受け取ったしゃがみを位置と同じ遅れで当て、カプセル・目・見た目 (<see cref="NetStance.BodyName"/>) を変える。
    /// しゃがみが変わるたび <c>[Net] stance owner=.. tick=.. crouched=..</c> を出す (サーバーと他人のクライアント)。
    /// </para>
    /// <para>
    /// オーナーは Crouch を <see cref="ControlSettings"/> のモード (押している間／押すたびに切り替え) で <see cref="CrouchInput"/> が解釈し、
    /// 送るのは解釈した後の「しゃがみたいか」だけにする (サーバーはモードを知らない)。ジャンプとダッシュの開始で解除し、
    /// 死亡・リスポーンでは <see cref="NetPlayer"/> が <see cref="ReleaseCrouch"/> を呼ぶ。
    /// </para>
    /// <para>
    /// サーバーで確定した着地 (落下ダメージ) は .Landing.cs で扱う。ダウン中に動けなくする処理は .Downed.cs に分けてある。
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed partial class NetworkPlayerMotor : NetworkBehaviour
    {
        /// <summary>予測と確定状態の位置のずれ (m) がこれを超えたら照合する。</summary>
        public const float ReconcileTolerance = 0.05f;

        /// <summary>他人のプレイヤーを何 tick 遅らせて表示するか。</summary>
        public const int InterpolationDelayTicks = 2;

        /// <summary>移動を進める tick レート (Hz)。クライアントとサーバーで同じ値を使う。</summary>
        public const int TickRate = TickClock.DefaultTickRate;

        /// <summary>この高さより下に落ちたら出現位置に戻す。予測とサーバーで同じ規則にするためシミュレーションの中で行う。</summary>
        public const float KillPlaneY = -50f;

        public const string MoveActionPath = InputActionPaths.Move;
        public const string JumpActionPath = InputActionPaths.Jump;
        public const string SprintActionPath = InputActionPaths.Sprint;
        public const string CrouchActionPath = InputActionPaths.Crouch;

        /// <summary>1 tick の長さ (秒)。</summary>
        public static readonly float TickSeconds = 1f / TickRate;

        // サーバーがクライアント 1 人から受け付ける入力の数の上限。毎秒 TickRate ずつ回復し、
        // ラグで溜まって届いた分を 0.5 秒ぶんまで受け入れる。速く送って速く動く不正を防ぐ。
        private const float _maxInputBudget = TickRate * 0.5f;

        // 補間の表示 tick を目標 (最新 - 遅延) へ寄せる強さと、再生速度の変化の上限。
        private const float _catchUpGain = 0.1f;
        private const float _maxCatchUp = 0.2f;

        // 表示 tick が目標からこれ以上離れたら (長い途切れの後など) 寄せずに飛ばす。
        private const float _maxRenderTickDrift = TickRate * 0.5f;

        private const float _warningInterval = 1f;

        // 生成中のプレイヤー。互いの衝突を無効にするために持つ。
        private static readonly List<NetworkPlayerMotor> _active = new List<NetworkPlayerMotor>();

        [SerializeField, Min(0f), Tooltip("歩きの水平速度 (m/s)")]
        private float _walkSpeed = 5f;

        [SerializeField, Min(0f), Tooltip("ダッシュ中の水平速度 (m/s)")]
        private float _sprintSpeed = 8f;

        [SerializeField, Min(0f), Tooltip("ジャンプで到達する高さ (m)")]
        private float _jumpHeight = 1.2f;

        [SerializeField, Min(0.01f), Tooltip("重力加速度の大きさ (m/s²)")]
        private float _gravity = 20f;

        [SerializeField, Range(0f, 1f), Tooltip("空中で水平速度を入力方向へ寄せる 1 tick あたりの割合")]
        private float _airControl = 0.1f;

        [SerializeField, Min(0.01f), Tooltip("しゃがみのカプセルの高さ (m)。半径の 2 倍以上、立ちの高さ以下")]
        private float _crouchHeight = PlayerController.DefaultCrouchHeight;

        // 補間した向きの長さがこれより短ければ (真逆の向きの間など) 向きを決められないので、前の向きのままにする
        private const float _minYawDirectionLengthSquared = 1e-6f;

        private CharacterController _controller;
        private FirstPersonLook _look;
        private NetMotorSimulation _simulation;
        private NetworkPlayerRole _role;

        // オーナー (予測するクライアントとホスト)
        private TickClock _clock;
        private InputAction _moveAction;
        private InputAction _jumpAction;
        private InputAction _sprintAction;
        private InputAction _crouchAction;
        private readonly List<InputAction> _enabledActions = new List<InputAction>();
        private bool _jumpLatched;
        private bool _sprintPressLatched;
        private SprintInput _sprint;
        private ControlSettings _controlSettings = ControlSettings.Default;
        private bool _listeningToSettings;
        private InputDeviceKind _device = InputDeviceKind.KeyboardMouse;
        private bool _crouchPressLatched;
        private CrouchInput _crouch;
        private InputDeviceKind _crouchDevice = InputDeviceKind.KeyboardMouse;
        private PredictionBuffer<MotorInput, MotorState> _prediction;

        // サーバー
        private MotorState _serverState;
        private float _serverYaw;
        private int _serverTick;
        private int _lastProcessedTick;
        private float _inputBudget;
        private bool _hasPendingState;
        private float _nextWarningTime;

        // 他人のプレイヤーの補間
        private HitboxHistory _remoteHistory;

        // 向きは水平の単位ベクトル (sin yaw, 0, cos yaw) として記録し、位置と同じ tick で補間する。
        // 角度のまま補間すると 359° と 1° の間で逆回りしてしまうため
        private HitboxHistory _remoteYawHistory;
        private StanceHistory _remoteStanceHistory;

        // 接地もしゃがみと同じく tick で切り替わる bool なので、同じ履歴で持つ。見た目 (足を動かすか) にだけ使う
        private StanceHistory _remoteGroundedHistory;
        private bool _remoteGrounded = true;
        private bool _hasRemoteState;
        private int _latestRemoteTick;
        private double _renderTick;

        /// <summary>このインスタンスの役割。生成前は <see cref="NetworkPlayerRole.None"/>。</summary>
        public NetworkPlayerRole Role => _role;

        /// <summary>予測の履歴。オーナーのクライアント以外では null。</summary>
        public PredictionBuffer<MotorInput, MotorState> Prediction => _prediction;

        /// <summary>
        /// 姿勢 (立ち・しゃがみの寸法と、今当てている姿勢)。設定が不正か CharacterController が無ければ null。
        /// 射撃の始点 (<see cref="NetPlayer.EyePosition"/>) と巻き戻しの当たり判定 (<see cref="RewindTarget.Stance"/>) が使う。
        /// </summary>
        public NetStance Stance => _simulation != null ? _simulation.Stance : null;

        /// <summary>このインスタンスで今しゃがんでいるか (オーナーは予測、他人は補間表示の姿勢)。</summary>
        public bool IsCrouched => _simulation != null && _simulation.Stance.IsCrouched;

        /// <summary>歩きの水平速度 (m/s)。</summary>
        public float WalkSpeed => _walkSpeed;

        /// <summary>ダッシュ中の水平速度 (m/s)。</summary>
        public float SprintSpeed => _sprintSpeed;

        /// <summary>しゃがみ中の水平速度 (m/s)。移動の計算 (<see cref="MotorSpec"/>) と同じ値。</summary>
        public float CrouchSpeed => PlayerController.DefaultCrouchSpeed;

        /// <summary>
        /// このインスタンスで今接地しているか。サーバー (ホストを含む) は確定状態、予測するオーナーは最新の予測、
        /// 他人のクライアントは届いた確定状態を位置と同じ遅れで当てた値。生成前と、他人で状態がまだ届いていないときは true。
        /// 見た目 (<see cref="Fps.Runtime.CharacterAnimator"/>) に使う。
        /// </summary>
        public bool IsGrounded
        {
            get
            {
                switch (_role)
                {
                    case NetworkPlayerRole.HostOwner:
                    case NetworkPlayerRole.ServerSimulated:
                        return _serverState.IsGrounded;
                    case NetworkPlayerRole.Predicted:
                        return _prediction == null || _prediction.Current.IsGrounded;
                    case NetworkPlayerRole.Interpolated:
                        return _remoteGrounded;
                    default:
                        return true;
                }
            }
        }

        /// <summary>
        /// このインスタンスの最新の水平の速さ (m/s)。サーバー (ホストを含む) は確定状態、予測するオーナーは最新の予測の速度から出す。
        /// 他人のクライアントは速度を受け取らないので 0。射撃のばらつき (<see cref="NetPlayer.SpreadMoveFraction"/>) に使う。
        /// </summary>
        public float HorizontalSpeed
        {
            get
            {
                Vector3 velocity;
                switch (_role)
                {
                    case NetworkPlayerRole.HostOwner:
                    case NetworkPlayerRole.ServerSimulated:
                        velocity = _serverState.Velocity;
                        break;
                    case NetworkPlayerRole.Predicted:
                        velocity = _prediction != null ? _prediction.Current.Velocity : Vector3.zero;
                        break;
                    default:
                        return 0f;
                }

                return new Vector2(velocity.x, velocity.z).magnitude;
            }
        }

        /// <summary>オーナーでダッシュ中か (モードどおりに解釈し、自動解除した後の値)。オーナー以外では false。</summary>
        public bool IsSprinting => _sprint != null && _sprint.IsActive;

        /// <summary>
        /// オーナーのダッシュを解除する。Hold でもボタンを押し直すまでダッシュしない。次の tick の入力から効く。
        /// 射撃・死亡・リスポーン (<see cref="NetPlayer"/>) や ADS から呼ぶ。オーナー以外では何もしない。ダッシュ中だったら true。
        /// </summary>
        /// <param name="reason">ログに出す理由 (<see cref="SprintInput.ReasonFire"/> など)。</param>
        public bool ReleaseSprint(string reason)
        {
            return _sprint != null && _sprint.Release(reason);
        }

        /// <summary>オーナーでしゃがみたいか (モードどおりに解釈し、自動解除した後の値)。オーナー以外では false。</summary>
        public bool IsCrouchActive => _crouch != null && _crouch.IsActive;

        /// <summary>
        /// オーナーのしゃがみを解除する (死亡・リスポーン)。頭上は確かめない。Hold でもボタンを押し直すまでしゃがまない。
        /// 次の tick の入力から効く。オーナー以外では何もしない。しゃがみたい状態だったら true。
        /// </summary>
        /// <param name="reason">ログに出す理由 (<see cref="CrouchInput.ReasonDeath"/> など)。</param>
        public bool ReleaseCrouch(string reason)
        {
            return _crouch != null && _crouch.Release(reason);
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _look = GetComponent<FirstPersonLook>();
            if (_controller == null)
            {
                return;
            }

            try
            {
                var motor = new PlayerMotor(new MotorSpec(
                    _walkSpeed, _sprintSpeed, _jumpHeight, _gravity, _airControl, PlayerController.DefaultCrouchSpeed));
                var stance = NetStance.FromHierarchy(_controller, _crouchHeight);
                _simulation = new NetMotorSimulation(_controller, motor, stance, TickSeconds, KillPlaneY);
            }
            catch (ArgumentOutOfRangeException e)
            {
                Debug.LogError("[Net] invalid motor or crouch settings on " + name + ": " + e.Message, this);
                _simulation = null;
            }
        }

        /// <summary>しゃがみが変わったときのログ。例: <c>[Net] stance owner=1 tick=42 crouched=true</c>。</summary>
        public static string FormatStanceLog(ulong ownerClientId, int tick, bool crouched)
        {
            return "[Net] stance owner=" + ownerClientId.ToString(CultureInfo.InvariantCulture)
                + " tick=" + tick.ToString(CultureInfo.InvariantCulture)
                + " crouched=" + (crouched ? "true" : "false");
        }

        public override void OnNetworkSpawn()
        {
            if (_simulation == null || _controller == null)
            {
                Debug.LogError("[Net] " + name + " cannot move: motor or crouch settings or CharacterController are missing", this);
                enabled = false;
                return;
            }

            _simulation.SpawnPosition = transform.position;
            _simulation.Stance.Apply(false);
            Register();

            if (IsServer)
            {
                _role = IsOwner ? NetworkPlayerRole.HostOwner : NetworkPlayerRole.ServerSimulated;
                _controller.enabled = true;
                _serverState = new MotorState(transform.position, Vector3.zero, false, false);
                _serverYaw = transform.eulerAngles.y * Mathf.Deg2Rad;
                _serverTick = 0;
                _lastProcessedTick = 0;
                _inputBudget = _maxInputBudget;
            }
            else if (IsOwner)
            {
                _role = NetworkPlayerRole.Predicted;
                _controller.enabled = true;
                _prediction = new PredictionBuffer<MotorInput, MotorState>(_simulation.Step, MotorState.PositionError, ReconcileTolerance);
                _prediction.Reset(0, new MotorState(transform.position, Vector3.zero, false, false));
            }
            else
            {
                // 位置はサーバーから受け取るだけなので、コライダーとして他を押し出さないよう止める
                _role = NetworkPlayerRole.Interpolated;
                _controller.enabled = false;
                _remoteHistory = new HitboxHistory();
                _remoteYawHistory = new HitboxHistory();
                _remoteStanceHistory = new StanceHistory();
                _remoteGroundedHistory = new StanceHistory();
                _remoteGrounded = true;
                _hasRemoteState = false;
            }

            if (IsOwner)
            {
                _clock = new TickClock(TickRate);
                _jumpLatched = false;
                _sprintPressLatched = false;
                _crouchPressLatched = false;
                _controlSettings = AppliedControlSettings.Current;
                if (!_listeningToSettings)
                {
                    AppliedControlSettings.Changed += HandleSettingsChanged;
                    _listeningToSettings = true;
                }

                _device = InputDeviceKind.KeyboardMouse;
                _crouchDevice = InputDeviceKind.KeyboardMouse;
                _sprint = new SprintInput();
                _sprint.Changed += OnSprintChanged;
                _crouch = new CrouchInput(_simulation.Stance.HasHeadroomToStand);
                _crouch.Changed += OnCrouchInputChanged;
                ResolveInputActions();
            }

            Debug.Log("[Net] player spawned owner=" + OwnerClientId + " role=" + _role, this);
        }

        public override void OnNetworkDespawn()
        {
            Shutdown();
        }

        public override void OnDestroy()
        {
            Shutdown();
            base.OnDestroy();
        }

        private void Update()
        {
            switch (_role)
            {
                case NetworkPlayerRole.HostOwner:
                case NetworkPlayerRole.Predicted:
                    UpdateOwner(Time.deltaTime);
                    break;
                case NetworkPlayerRole.ServerSimulated:
                    _inputBudget = Mathf.Min(_maxInputBudget, _inputBudget + Time.deltaTime * TickRate);
                    break;
                case NetworkPlayerRole.Interpolated:
                    UpdateInterpolation(Time.deltaTime);
                    break;
            }

            if (IsServer && _hasPendingState)
            {
                // 1 フレームに複数 tick 進んでも送るのは最新だけ。照合も補間も間の tick を必要としない
                _hasPendingState = false;
                StateRpc(
                    _serverTick, _serverState.Position, _serverState.Velocity, _serverState.IsGrounded, _serverState.IsCrouched, _serverYaw, default);
            }
        }

        private void UpdateOwner(float deltaSeconds)
        {
            // ジャンプはフレーム単位の押下なので、次に tick が進むまで覚えておく
            if (_jumpAction != null && _jumpAction.WasPressedThisFrame())
            {
                _jumpLatched = true;
            }

            // 切り替えも押したフレームで起きるので、ジャンプと同じく次の tick まで覚えておく
            if (_sprintAction != null && _sprintAction.WasPressedThisFrame())
            {
                _sprintPressLatched = true;
            }

            if (_crouchAction != null && _crouchAction.WasPressedThisFrame())
            {
                _crouchPressLatched = true;
            }

            // activeControl は動かしている間しか取れないので、tick ではなくフレームごとに見て直前の機器を覚えておく
            _device = InputDeviceKinds.Of(_sprintAction, InputDeviceKinds.Of(_moveAction, _device));
            _crouchDevice = InputDeviceKinds.Of(_crouchAction, InputDeviceKinds.Of(_moveAction, _crouchDevice));

            var ticks = _clock.Advance(deltaSeconds);
            var firstTick = _clock.CurrentTick - ticks + 1;
            for (var i = 0; i < ticks; i++)
            {
                RunOwnerTick(firstTick + i, SampleInput());
            }
        }

        private MotorInput SampleInput()
        {
            var move = _moveAction != null ? _moveAction.ReadValue<Vector2>() : Vector2.zero;
            var sprint = SampleSprint(move);
            var jump = _jumpLatched;
            _jumpLatched = false;
            var crouch = SampleCrouch(jump, sprint);

            var yaw = SampleYaw(_look, transform);
            return new MotorInput(move, yaw, sprint, jump, crouch, _downed);
        }

        // サーバーへはモードを解釈した後の「ダッシュ中か」だけを送る (サーバー側は押し方を知らなくてよい)
        private bool SampleSprint(Vector2 move)
        {
            var wasPressed = _sprintPressLatched;
            _sprintPressLatched = false;
            if (_sprint == null)
            {
                return false;
            }

            var isPressed = _sprintAction != null && _sprintAction.IsPressed();
            var mode = _controlSettings.Get(_device, HoldAction.Sprint);
            return _sprint.Update(mode, NetMath.ToNumerics(move), isPressed, wasPressed);
        }

        private void OnSprintChanged(SprintChange change)
        {
            Debug.Log(SprintInput.FormatLog(change), this);
        }

        // サーバーへはモードを解釈した後の「しゃがみたいか」だけを送る (サーバー側は押し方を知らなくてよい)
        private bool SampleCrouch(bool jumpPressed, bool sprinting)
        {
            var wasPressed = _crouchPressLatched;
            _crouchPressLatched = false;
            if (_crouch == null)
            {
                return false;
            }

            // 空中で押したジャンプは跳ばないので、しゃがみも解かない。本体はいま最新の予測 (ホストは確定) の位置にある
            var grounded = _role == NetworkPlayerRole.HostOwner
                ? _serverState.IsGrounded
                : _prediction != null && _prediction.Current.IsGrounded;
            var isPressed = _crouchAction != null && _crouchAction.IsPressed();
            var mode = _controlSettings.Get(_crouchDevice, HoldAction.Crouch);
            return _crouch.Update(mode, isPressed, wasPressed, jumpPressed && grounded, sprinting);
        }

        private void OnCrouchInputChanged(CrouchInput input, string reason)
        {
            Debug.Log(CrouchInput.FormatLog(input.IsActive, input.Mode, reason), this);
        }

        /// <summary>
        /// 入力に乗せる向き (Y 軸まわり、ラジアン)。視点操作 (<see cref="FirstPersonLook"/>) が動いていればその yaw、
        /// 無いか止まっていれば (他人のプレイヤーや画面の無い batchmode) 本体の yaw。
        /// </summary>
        public static float SampleYaw(FirstPersonLook look, Transform body)
        {
            if (body == null)
            {
                throw new ArgumentNullException(nameof(body));
            }

            var degrees = look != null && look.enabled && look.IsInitialized ? look.Yaw : body.eulerAngles.y;
            return degrees * Mathf.Deg2Rad;
        }

        /// <summary>yaw (ラジアン) だけを持つ回転。</summary>
        public static Quaternion YawRotation(float yaw)
        {
            return Quaternion.Euler(0f, yaw * Mathf.Rad2Deg, 0f);
        }

        // 補間用に、yaw を水平の単位ベクトルにする。Quaternion.Euler(0, yaw, 0) * Vector3.forward と同じ向き
        private static System.Numerics.Vector3 YawDirection(float yaw)
        {
            return new System.Numerics.Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
        }

        private void RunOwnerTick(int tick, MotorInput input)
        {
            if (_role == NetworkPlayerRole.HostOwner)
            {
                ApplyAuthoritative(tick, input);
            }
            else
            {
                _prediction.Predict(tick, input);
                SubmitInputRpc(tick, input.Move, input.Yaw, input.Sprint, input.Jump, input.Crouch);
            }

            OwnerTicked?.Invoke(tick);
        }

        private void ApplyAuthoritative(int tick, MotorInput input)
        {
            var wasCrouched = _serverState.IsCrouched;
            var wasGrounded = _serverState.IsGrounded;
            _serverState = _simulation.Step(_serverState, input);
            _serverYaw = input.Yaw;
            if (_serverState.IsCrouched != wasCrouched)
            {
                Debug.Log(FormatStanceLog(OwnerClientId, tick, _serverState.IsCrouched), this);
            }

            ReportServerLanding(tick, wasGrounded, _serverState);

            if (_role == NetworkPlayerRole.ServerSimulated)
            {
                // ホスト自身のプレイヤーは FirstPersonLook がもう回しているので、遅れた値で上書きしない
                transform.rotation = YawRotation(input.Yaw);
            }

            _serverTick = tick;
            _lastProcessedTick = tick;
            _hasPendingState = true;
        }

        /// <summary>
        /// サーバーで position へ瞬間移動させる (リスポーンなど)。速度は 0 に戻し、立たせる (オーナーがしゃがみを送り続けていれば次の tick でまたしゃがむ)。
        /// 所有クライアントは次に届く確定状態との照合で巻き戻され、他人のクライアントは補間せずに飛ばす。
        /// </summary>
        public void ServerTeleport(Vector3 position)
        {
            if (!IsServer)
            {
                throw new InvalidOperationException("ServerTeleport can only be called on the server");
            }

            if (_role != NetworkPlayerRole.HostOwner && _role != NetworkPlayerRole.ServerSimulated)
            {
                // 生成前・設定不足で動かせないときは位置だけ置く
                transform.position = position;
                return;
            }

            var wasCrouched = _serverState.IsCrouched;
            _serverState = new MotorState(position, Vector3.zero, false, false);
            _simulation.Place(_serverState);
            if (wasCrouched)
            {
                Debug.Log(FormatStanceLog(OwnerClientId, _serverTick, false), this);
            }

            _hasPendingState = true;
            TeleportedRpc(_serverTick, position);
        }

        [Rpc(SendTo.NotServer, Delivery = RpcDelivery.Reliable)]
        private void TeleportedRpc(int tick, Vector3 position, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != NetworkManager.ServerClientId || _role != NetworkPlayerRole.Interpolated)
            {
                // 所有クライアントは StateRpc の照合で置き直す
                return;
            }

            // 補間の履歴を捨て、テレポート先から表示し直す
            _remoteHistory = new HitboxHistory();
            _remoteHistory.Record(tick, NetMath.ToNumerics(position));

            // テレポートでは向きは変わらないので、今の向きから補間し直す
            _remoteYawHistory = new HitboxHistory();
            _remoteYawHistory.Record(tick, YawDirection(transform.eulerAngles.y * Mathf.Deg2Rad));

            // サーバーはテレポートで立たせる
            _remoteStanceHistory = new StanceHistory();
            _remoteStanceHistory.Record(tick, false);
            ShowRemoteStance(tick, false);

            // サーバーはテレポートで宙に置き直す (着地は次の確定状態で届く)
            _remoteGroundedHistory = new StanceHistory();
            _remoteGroundedHistory.Record(tick, false);
            _remoteGrounded = false;

            _latestRemoteTick = tick;
            _renderTick = tick;
            _hasRemoteState = true;
            transform.position = position;
        }

        [Rpc(SendTo.Server, Delivery = RpcDelivery.Reliable)]
        private void SubmitInputRpc(int tick, Vector2 move, float yaw, bool sprint, bool jump, bool crouch, RpcParams rpcParams = default)
        {
            if (_role != NetworkPlayerRole.ServerSimulated)
            {
                return;
            }

            var sender = rpcParams.Receive.SenderClientId;
            if (sender != OwnerClientId)
            {
                WarnThrottled("[Net] dropped input for owner=" + OwnerClientId + " sent by client=" + sender);
                return;
            }

            if (tick <= _lastProcessedTick)
            {
                return;
            }

            if (!MotorInput.IsValid(move, yaw))
            {
                WarnThrottled("[Net] dropped non-finite input from client=" + sender + " tick=" + tick);
                return;
            }

            if (_inputBudget < 1f)
            {
                WarnThrottled("[Net] dropped input from client=" + sender + " tick=" + tick + ": sending faster than the tick rate");
                return;
            }

            _inputBudget -= 1f;

            // ダウンはクライアントの申告ではなく、サーバーの知る状態で決める
            ApplyAuthoritative(tick, new MotorInput(move, yaw, sprint, jump, crouch, _downed));
        }

        [Rpc(SendTo.NotServer, Delivery = RpcDelivery.Unreliable)]
        private void StateRpc(
            int tick, Vector3 position, Vector3 velocity, bool isGrounded, bool isCrouched, float yaw, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != NetworkManager.ServerClientId)
            {
                return;
            }

            var state = new MotorState(position, velocity, isGrounded, isCrouched);
            if (!state.IsFinite || !NetMath.IsFinite(yaw))
            {
                WarnThrottled("[Net] dropped non-finite state for owner=" + OwnerClientId + " tick=" + tick);
                return;
            }

            switch (_role)
            {
                case NetworkPlayerRole.Predicted:
                    Reconcile(tick, state);
                    break;
                case NetworkPlayerRole.Interpolated:
                    ReceiveRemoteState(tick, position, yaw, isCrouched, isGrounded);
                    break;
            }
        }

        private void Reconcile(int tick, MotorState authoritative)
        {
            var result = _prediction.Reconcile(tick, authoritative);
            if (result.Outcome != ReconcileOutcome.Corrected)
            {
                return;
            }

            // 再シミュレーションした tick が無いときは確定状態そのものが最新なので、ここで置き直す
            var current = _prediction.Current;
            _simulation.Place(current);
            Debug.Log(FormatReconcileLog(result, authoritative.IsCrouched, current.IsCrouched), this);
        }

        /// <summary>
        /// 照合で巻き戻したときのログ。しゃがみが食い違って巻き戻したときは error が Infinity になる。
        /// 例: <c>[Net] reconcile tick=42 error=Infinity resimulated=3 crouched=true predictedCrouched=true</c>
        /// (crouched はその tick の確定状態、predictedCrouched は再シミュレーション後の最新の予測)。
        /// </summary>
        public static string FormatReconcileLog(ReconcileResult result, bool authoritativeCrouched, bool predictedCrouched)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "[Net] reconcile tick={0} error={1:F3} resimulated={2} crouched={3} predictedCrouched={4}",
                result.Tick,
                result.Error,
                result.ResimulatedTicks,
                authoritativeCrouched ? "true" : "false",
                predictedCrouched ? "true" : "false");
        }

        private void ReceiveRemoteState(int tick, Vector3 position, float yaw, bool crouched, bool grounded)
        {
            // 信頼性なしで送るので順番が入れ替わることがある。古いものは捨てる
            if (_hasRemoteState && tick <= _latestRemoteTick)
            {
                return;
            }

            _remoteHistory.Record(tick, NetMath.ToNumerics(position));
            _remoteYawHistory.Record(tick, YawDirection(yaw));
            _remoteStanceHistory.Record(tick, crouched);
            _remoteGroundedHistory.Record(tick, grounded);
            _latestRemoteTick = tick;

            if (!_hasRemoteState)
            {
                _hasRemoteState = true;
                _renderTick = tick;
                transform.SetPositionAndRotation(position, YawRotation(yaw));
                ShowRemoteStance(tick, crouched);
                _remoteGrounded = grounded;
            }
        }

        // 他人のプレイヤーの姿勢を表示する。変わったときだけ当ててログを出す
        private void ShowRemoteStance(int tick, bool crouched)
        {
            if (_simulation.Stance.IsCrouched == crouched)
            {
                return;
            }

            _simulation.Stance.Apply(crouched);
            Debug.Log(FormatStanceLog(OwnerClientId, tick, crouched), this);
        }

        private void UpdateInterpolation(float deltaSeconds)
        {
            if (!_hasRemoteState)
            {
                return;
            }

            var target = (double)_latestRemoteTick - InterpolationDelayTicks;
            var lag = target - _renderTick;
            if (Math.Abs(lag) > _maxRenderTickDrift)
            {
                _renderTick = target;
            }
            else
            {
                // 到着の揺れで遅延が増減しても止まったり跳んだりしないよう、再生速度を少しだけ変えて目標へ寄せる
                var rate = 1.0 + Math.Max(-_maxCatchUp, Math.Min(_maxCatchUp, lag * _catchUpGain));
                _renderTick += deltaSeconds * TickRate * rate;
            }

            // 次の状態が届いていない間は最新の位置で止めておく
            _renderTick = Math.Min(_renderTick, _latestRemoteTick);

            if (_remoteHistory.TrySample((float)_renderTick, out var sampled))
            {
                transform.position = NetMath.ToUnity(sampled);
            }

            if (_remoteYawHistory.TrySample((float)_renderTick, out var facing)
                && facing.LengthSquared() > _minYawDirectionLengthSquared)
            {
                transform.rotation = YawRotation(Mathf.Atan2(facing.X, facing.Z));
            }

            if (_remoteStanceHistory.TrySample((float)_renderTick, out var crouched))
            {
                ShowRemoteStance((int)Math.Floor(_renderTick), crouched);
            }

            if (_remoteGroundedHistory.TrySample((float)_renderTick, out var grounded))
            {
                _remoteGrounded = grounded;
            }
        }

        private void ResolveInputActions()
        {
            AppliedInputBindings.EnsureLoaded();
            var actions = InputSystem.actions;
            if (actions == null)
            {
                Debug.LogError("[Net] project-wide Input Actions are not set; " + name + " will send empty input", this);
                return;
            }

            _moveAction = FindAndEnable(actions, MoveActionPath);
            _jumpAction = FindAndEnable(actions, JumpActionPath);
            _sprintAction = FindAndEnable(actions, SprintActionPath);
            _crouchAction = FindAndEnable(actions, CrouchActionPath);
        }

        private InputAction FindAndEnable(InputActionAsset actions, string path)
        {
            var action = actions.FindAction(path);
            if (action == null)
            {
                Debug.LogError("[Net] Input Actions '" + actions.name + "' has no '" + path + "'; that input stays empty", this);
                return null;
            }

            // プロジェクト全体のアクションは他の部品と共有なので、自分が有効にしたものだけ後で無効に戻す
            if (!action.enabled)
            {
                action.Enable();
                _enabledActions.Add(action);
            }

            return action;
        }

        private void Register()
        {
            if (_active.Contains(this))
            {
                return;
            }

            foreach (var other in _active)
            {
                if (other != null && other._controller != null)
                {
                    Physics.IgnoreCollision(_controller, other._controller, true);
                }
            }

            _active.Add(this);
        }

        private void HandleSettingsChanged(ControlSettings settings)
        {
            _controlSettings = settings;
        }

        private void Shutdown()
        {
            _active.Remove(this);
            if (_listeningToSettings)
            {
                AppliedControlSettings.Changed -= HandleSettingsChanged;
                _listeningToSettings = false;
            }

            foreach (var action in _enabledActions)
            {
                if (action != null)
                {
                    action.Disable();
                }
            }

            _enabledActions.Clear();
            _moveAction = null;
            _jumpAction = null;
            _sprintAction = null;
            _crouchAction = null;
            if (_sprint != null)
            {
                _sprint.Changed -= OnSprintChanged;
                _sprint = null;
            }

            if (_crouch != null)
            {
                _crouch.Changed -= OnCrouchInputChanged;
                _crouch = null;
            }

            _sprintPressLatched = false;
            _crouchPressLatched = false;
            _downed = false;
            _prediction = null;
            _remoteHistory = null;
            _remoteYawHistory = null;
            _remoteStanceHistory = null;
            _remoteGroundedHistory = null;
            _remoteGrounded = true;
            _hasPendingState = false;
            _role = NetworkPlayerRole.None;
        }

        private void WarnThrottled(string message)
        {
            if (Time.unscaledTime < _nextWarningTime)
            {
                return;
            }

            _nextWarningTime = Time.unscaledTime + _warningInterval;
            Debug.LogWarning(message, this);
        }
    }
}
