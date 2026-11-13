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
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class NetworkPlayerMotor : NetworkBehaviour
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

        // 補間した向きの長さがこれより短ければ (真逆の向きの間など) 向きを決められないので、前の向きのままにする
        private const float _minYawDirectionLengthSquared = 1e-6f;

        private CharacterController _controller;
        private FirstPersonLook _look;
        private PlayerMotor _motor;
        private NetworkPlayerRole _role;
        private Vector3 _spawnPosition;

        // オーナー (予測するクライアントとホスト)
        private TickClock _clock;
        private InputAction _moveAction;
        private InputAction _jumpAction;
        private InputAction _sprintAction;
        private readonly List<InputAction> _enabledActions = new List<InputAction>();
        private bool _jumpLatched;

        // ReleaseSprint の後は押し直すまでダッシュしない。tick の進まないフレームの押下も拾うよう、フレームごとに更新する
        private readonly HoldToggleState _sprintState = new HoldToggleState(HoldMode.Hold);
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
        private bool _hasRemoteState;
        private int _latestRemoteTick;
        private double _renderTick;

        /// <summary>このインスタンスの役割。生成前は <see cref="NetworkPlayerRole.None"/>。</summary>
        public NetworkPlayerRole Role => _role;

        /// <summary>予測の履歴。オーナーのクライアント以外では null。</summary>
        public PredictionBuffer<MotorInput, MotorState> Prediction => _prediction;

        /// <summary>次の入力でダッシュを送るか。オーナー以外では常に false。</summary>
        public bool IsSprintActive => _sprintState.IsActive;

        /// <summary>
        /// ダッシュを外から解除する (ADS を始めたときなど)。ボタンを押し続けていても、押し直すまでダッシュを送らない。
        /// サーバーは送られた入力どおりに動かすだけなので、解除はオーナーの入力の側で行う。
        /// </summary>
        public void ReleaseSprint()
        {
            _sprintState.Release();
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _look = GetComponent<FirstPersonLook>();

            try
            {
                _motor = new PlayerMotor(new MotorSpec(
                    _walkSpeed, _sprintSpeed, _jumpHeight, _gravity, _airControl, PlayerController.DefaultCrouchSpeed));
            }
            catch (ArgumentOutOfRangeException e)
            {
                Debug.LogError("[Net] invalid motor settings on " + name + ": " + e.Message, this);
                _motor = null;
            }
        }

        public override void OnNetworkSpawn()
        {
            if (_motor == null || _controller == null)
            {
                Debug.LogError("[Net] " + name + " cannot move: motor settings or CharacterController are missing", this);
                enabled = false;
                return;
            }

            _spawnPosition = transform.position;
            Register();

            if (IsServer)
            {
                _role = IsOwner ? NetworkPlayerRole.HostOwner : NetworkPlayerRole.ServerSimulated;
                _controller.enabled = true;
                _serverState = new MotorState(transform.position, Vector3.zero, false);
                _serverYaw = transform.eulerAngles.y * Mathf.Deg2Rad;
                _serverTick = 0;
                _lastProcessedTick = 0;
                _inputBudget = _maxInputBudget;
            }
            else if (IsOwner)
            {
                _role = NetworkPlayerRole.Predicted;
                _controller.enabled = true;
                _prediction = new PredictionBuffer<MotorInput, MotorState>(Simulate, MotorState.PositionError, ReconcileTolerance);
                _prediction.Reset(0, new MotorState(transform.position, Vector3.zero, false));
            }
            else
            {
                // 位置はサーバーから受け取るだけなので、コライダーとして他を押し出さないよう止める
                _role = NetworkPlayerRole.Interpolated;
                _controller.enabled = false;
                _remoteHistory = new HitboxHistory();
                _remoteYawHistory = new HitboxHistory();
                _hasRemoteState = false;
            }

            if (IsOwner)
            {
                _clock = new TickClock(TickRate);
                _jumpLatched = false;
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
                StateRpc(_serverTick, _serverState.Position, _serverState.Velocity, _serverState.IsGrounded, _serverYaw, default);
            }
        }

        private void UpdateOwner(float deltaSeconds)
        {
            // ジャンプはフレーム単位の押下なので、次に tick が進むまで覚えておく
            if (_jumpAction != null && _jumpAction.WasPressedThisFrame())
            {
                _jumpLatched = true;
            }

            if (_sprintAction != null)
            {
                _sprintState.Update(_sprintAction.IsPressed(), _sprintAction.WasPressedThisFrame());
            }
            else
            {
                _sprintState.Release();
            }

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
            var sprint = _sprintState.IsActive;
            var jump = _jumpLatched;
            _jumpLatched = false;

            var yaw = SampleYaw(_look, transform);
            return new MotorInput(move, yaw, sprint, jump);
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
                return;
            }

            _prediction.Predict(tick, input);
            SubmitInputRpc(tick, input.Move, input.Yaw, input.Sprint, input.Jump);
        }

        private void ApplyAuthoritative(int tick, MotorInput input)
        {
            _serverState = Simulate(_serverState, input);
            _serverYaw = input.Yaw;
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
        /// サーバーで position へ瞬間移動させる (リスポーンなど)。速度は 0 に戻す。
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

            _serverState = new MotorState(position, Vector3.zero, false);
            PlaceAt(position);
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
            _latestRemoteTick = tick;
            _renderTick = tick;
            _hasRemoteState = true;
            transform.position = position;
        }

        [Rpc(SendTo.Server, Delivery = RpcDelivery.Reliable)]
        private void SubmitInputRpc(int tick, Vector2 move, float yaw, bool sprint, bool jump, RpcParams rpcParams = default)
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
            ApplyAuthoritative(tick, new MotorInput(move, yaw, sprint, jump));
        }

        [Rpc(SendTo.NotServer, Delivery = RpcDelivery.Unreliable)]
        private void StateRpc(int tick, Vector3 position, Vector3 velocity, bool isGrounded, float yaw, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != NetworkManager.ServerClientId)
            {
                return;
            }

            var state = new MotorState(position, velocity, isGrounded);
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
                    ReceiveRemoteState(tick, position, yaw);
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
            PlaceAt(_prediction.Current.Position);
            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                "[Net] reconcile tick={0} error={1:F3} resimulated={2}",
                result.Tick,
                result.Error,
                result.ResimulatedTicks), this);
        }

        private void ReceiveRemoteState(int tick, Vector3 position, float yaw)
        {
            // 信頼性なしで送るので順番が入れ替わることがある。古いものは捨てる
            if (_hasRemoteState && tick <= _latestRemoteTick)
            {
                return;
            }

            _remoteHistory.Record(tick, NetMath.ToNumerics(position));
            _remoteYawHistory.Record(tick, YawDirection(yaw));
            _latestRemoteTick = tick;

            if (!_hasRemoteState)
            {
                _hasRemoteState = true;
                _renderTick = tick;
                transform.SetPositionAndRotation(position, YawRotation(yaw));
            }
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
        }

        /// <summary>
        /// 状態から 1 tick 進める。CharacterController を実際に動かすので、終わったとき本体は結果の位置にある。
        /// 予測・再シミュレーション・サーバーで同じ処理を使う。
        /// </summary>
        private MotorState Simulate(MotorState state, MotorInput input)
        {
            PlaceAt(state.Position);
            _motor.SetVelocity(NetMath.ToNumerics(state.Velocity));

            // ネット対戦のしゃがみは入力と状態の同期が要るので、まだしゃがまない。
            var displacement = _motor.Step(
                NetMath.ToNumerics(input.Move), input.Yaw, input.Sprint, false, input.Jump, state.IsGrounded, TickSeconds);
            _controller.Move(NetMath.ToUnity(displacement));

            var position = transform.position;
            var velocity = NetMath.ToUnity(_motor.Velocity);
            var grounded = _controller.isGrounded;

            if (position.y < KillPlaneY)
            {
                position = _spawnPosition;
                velocity = Vector3.zero;
                grounded = false;
                PlaceAt(position);
            }

            return new MotorState(position, velocity, grounded);
        }

        /// <summary>本体を位置へ置き、CharacterController がその位置から動くよう物理側にも反映する。</summary>
        private void PlaceAt(Vector3 position)
        {
            if (transform.position.Equals(position))
            {
                return;
            }

            transform.position = position;
            Physics.SyncTransforms();
        }

        private void ResolveInputActions()
        {
            var actions = InputSystem.actions;
            if (actions == null)
            {
                Debug.LogError("[Net] project-wide Input Actions are not set; " + name + " will send empty input", this);
                return;
            }

            _moveAction = FindAndEnable(actions, MoveActionPath);
            _jumpAction = FindAndEnable(actions, JumpActionPath);
            _sprintAction = FindAndEnable(actions, SprintActionPath);
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

        private void Shutdown()
        {
            _active.Remove(this);

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
            _sprintState.Release();
            _prediction = null;
            _remoteHistory = null;
            _remoteYawHistory = null;
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
