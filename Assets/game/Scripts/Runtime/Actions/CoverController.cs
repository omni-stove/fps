using System;
using Shooter.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class CoverController : MonoBehaviour, IPlayerMoveFilter
    {
        public const string CoverActionPath = InputActionPaths.Cover;
        public const string AimActionPath = InputActionPaths.Aim;
        public const string MoveActionPath = InputActionPaths.Move;

        public const string ReasonDisabled = "disabled";

        public const string ReasonDeath = "death";

        public const string ReasonRespawn = "respawn";

        // 低い壁 (1.1m 未満) にも段差より上で当たる高さにする。
        public const float DefaultProbeHeight = 0.5f;

        // 足元の Ray が車体の下の隙間を抜けたとき、車のように浮いた物にも当たる高さで探し直す。低い壁 (1.1m) より下にする。
        public const float DefaultUpperProbeHeight = CharacterCoverSensor.UpperProbeHeight;

        public const float DefaultTopProbeHeight = 3f;

        private const string LogTag = CoverState.LogTag;

        // 壁の上端を下ろす Ray で探すとき、当たった面から壁の内側へ入れる距離 (m)。面ちょうどだと上端の角を外すことがある。
        private const float TopProbeInset = 0.02f;

        // 端の検査の Ray を、壁までの距離よりこれだけ長く飛ばす (m)。壁がわずかに凹凸していても続いていると見なす。
        private const float EdgeProbeSlack = 0.3f;

        // 自分の体を除いて最も近い命中を選ぶための一時バッファ。毎フレームの確保を避ける。
        private const int MaxCastHits = 16;

        private static readonly RaycastHit[] _castBuffer = new RaycastHit[MaxCastHits];

        [SerializeField, Tooltip("カバーにする壁として当たるレイヤー")]
        private LayerMask _wallMask = Physics.DefaultRaycastLayers;

        [SerializeField, Min(0f), Tooltip("壁を探す Ray の、足元からの高さ (m)")]
        private float _probeHeight = DefaultProbeHeight;

        [SerializeField, Min(0f), Tooltip("足元の Ray が下を抜けたときに壁を探し直す Ray の、足元からの高さ (m)")]
        private float _upperProbeHeight = DefaultUpperProbeHeight;

        [SerializeField, Min(0f), Tooltip("壁の上端を探す Ray の始点の、足元からの高さ (m)")]
        private float _topProbeHeight = DefaultTopProbeHeight;

        // 初期化前にも IsInCover などを読まれるので空の状態を先に作る。値は Tick ごとに PlayerController の移動の設定に差し替える。
        private readonly CoverState _state = new CoverState(MotorSpec.Default);
        private PlayerController _player;
        private ViewRig _rig;
        private AimDownSights _ads;
        private Damageable _damageable;
        // シーンに置いたまま再コンパイルすると、Unity が private の値も保存し直し、null の InputAction を空のアクションとして戻してしまう (再生でシーンを読み直さないので、そのまま使われる)。
        [NonSerialized]
        private InputAction _coverAction;
        [NonSerialized]
        private InputAction _aimAction;
        [NonSerialized]
        private InputAction _moveAction;
        private bool _enabledCover;
        private bool _enabledAim;
        private bool _enabledMove;
        private bool _isLive;

        private CoverProbeResult _lastProbe;
        private float _coverTopY;

        private float _appliedPeek;

        private ShoulderSide _savedShoulder = ShoulderSide.Right;
        private bool _shoulderOverridden;

        public bool IsInCover => _state.IsInCover;

        public bool IsPeeking => _state.IsPeeking;

        public float CoverTopY => _coverTopY;

        public float CoverWallHeight => _state.IsInCover ? _lastProbe.TopHeight : 0f;

        public Vector3 CoverNormal => _state.Normal.ToUnity();

        public CoverHeight CoverHeight => _state.Height;

        public CoverPhase Phase => _state.Phase;

        public CoverProbeResult LastProbe => _lastProbe;

        public float AppliedPeekOffset => _appliedPeek;

        public PlayerController Player => _player;

        public bool IsInitialized => _player != null && _player.IsInitialized;

        public LayerMask WallMask
        {
            get => _wallMask;
            set => _wallMask = value;
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
            _isLive = true;
            AcquireActions();
            SyncMoveFilter();
        }

        private void OnDisable()
        {
            _isLive = false;
            ReleaseActions();
            ExitCover(ReasonDisabled);
            SyncMoveFilter();
        }

        private void OnDestroy()
        {
            BindDamageable(null);
            if (_player != null && ReferenceEquals(_player.MoveFilter, this))
            {
                _player.MoveFilter = null;
            }
        }

        private void Update()
        {
            if (!IsInitialized || _coverAction == null)
            {
                return;
            }

            var move = _moveAction != null ? _moveAction.ReadValue<Vector2>() : Vector2.zero;
            Tick(_coverAction.WasPressedThisFrame(), ReadAim(), move, Time.deltaTime);
        }

        public void Configure(GameRules rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            enabled = rules.Actions.Cover == Cover.On;

            // Edit Mode では enabled を変えても OnEnable / OnDisable が走らないので、移動の差し込みと状態はここでも揃える。
            if (!enabled)
            {
                ExitCover(ReasonDisabled);
            }

            SyncMoveFilter();
        }

        public bool TryInitializeFromHierarchy()
        {
            var player = GetComponent<PlayerController>();
            if (player == null || !player.IsInitialized)
            {
                Debug.LogError(
                    $"{LogTag} '{name}' に初期化済みの PlayerController がありません。カバーは PlayerController の移動に差し込みます。",
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

            var cover = actions.FindAction(CoverActionPath);
            var aim = actions.FindAction(AimActionPath);
            var move = actions.FindAction(MoveActionPath);
            if (cover == null || aim == null || move == null)
            {
                Debug.LogError(
                    $"{LogTag} Input Actions '{actions.name}' に '{CoverActionPath}' / '{AimActionPath}' / '{MoveActionPath}' のどれかがありません。",
                    this);
                return false;
            }

            Initialize(player, cover, aim, move);
            return true;
        }

        public void Initialize(PlayerController player, InputAction coverAction, InputAction aimAction, InputAction moveAction)
        {
            if (player == null)
            {
                throw new ArgumentNullException(nameof(player));
            }

            if (player.gameObject != gameObject)
            {
                throw new ArgumentException($"player must be on '{name}'", nameof(player));
            }

            if (!player.IsInitialized)
            {
                throw new ArgumentException("player must be initialized", nameof(player));
            }

            ExitCover(ReasonDisabled);
            if (_isLive)
            {
                ReleaseActions();
            }

            if (_player != null && _player != player && ReferenceEquals(_player.MoveFilter, this))
            {
                _player.MoveFilter = null;
            }

            _player = player;
            _coverAction = coverAction;
            _aimAction = aimAction;
            _moveAction = moveAction;
            _rig = GetComponent<ViewRig>();
            _ads = GetComponent<AimDownSights>();
            BindDamageable(GetComponent<Damageable>());

            if (_isLive)
            {
                AcquireActions();
            }

            SyncMoveFilter();
        }

        // 回避・乗り越え・崖のぼりの最中にカバーへ入らないのは、寄せる動きと転がり・乗り越え・よじ登りの軌跡がぶつかるため。
        public void Tick(bool coverPressed, bool aimHeld, Vector2 move, float deltaSeconds)
        {
            if (!enabled || !IsInitialized)
            {
                return;
            }

            if (!_player.isActiveAndEnabled)
            {
                ExitCover(ReasonDisabled);
                return;
            }

            var controller = _player.Controller;
            if (!controller.enabled || !controller.gameObject.activeInHierarchy)
            {
                return;
            }

            _state.Spec = _player.Motor.Spec;
            _state.BodyRadius = BodyRadius();
            var wasInCover = _state.IsInCover;
            if (!wasInCover && (!coverPressed || _player.IsDodging || _player.IsVaulting || _player.IsMantling))
            {
                return;
            }

            _lastProbe = wasInCover ? ProbeHeldWall() : ProbeFront();
            var transition = _state.Step(coverPressed, aimHeld, move.ToNumerics(), _lastProbe, deltaSeconds);
            Report(transition);

            if (!_state.IsInCover)
            {
                if (wasInCover)
                {
                    OnExited();
                }

                return;
            }

            if (transition == CoverTransition.Enter)
            {
                _savedShoulder = _rig != null ? _rig.Shoulder : ShoulderSide.Right;
                _shoulderOverridden = false;
                _appliedPeek = CoverProbe.EnterSideOffset(Flatten(transform.forward).ToNumerics(), _state.Normal, _lastProbe.Distance);
            }
            else if (_state.EnterTurn > 0f)
            {
                SlideIn();
            }
            else
            {
                SlidePeek(deltaSeconds);
            }

            UpdateShoulder();
        }

        public void Filter(ref Vector2 move, ref bool sprint, ref bool crouch, ref bool jumpPressed)
        {
            if (!enabled || !IsInitialized || !_state.IsInCover)
            {
                return;
            }

            sprint = false;
            jumpPressed = false;
            if (_state.Height == CoverHeight.Low)
            {
                crouch = !_state.IsPeeking;
            }

            if (_state.Phase != CoverPhase.In)
            {
                move = Vector2.zero;
                return;
            }

            var side = Mathf.Clamp(move.x, -1f, 1f);
            var along = CoverProbe.RightAlong(_state.Normal).ToUnity() * side;
            var body = transform;
            var right = Flatten(body.right);
            var forward = Flatten(body.forward);
            move = new Vector2(Vector3.Dot(along, right), Vector3.Dot(along, forward)) * SpeedScale(crouch);
        }

        public bool ExitCover(string reason)
        {
            if (!_state.Release(reason))
            {
                return false;
            }

            Report(CoverTransition.Exit);
            OnExited();
            return true;
        }

        // 立ったまま動く (高い壁) ときも、しゃがみの速さで動くよう入力を縮める。
        private float SpeedScale(bool crouch)
        {
            var spec = _player.Motor.Spec;
            if (spec.WalkSpeed <= 0f)
            {
                return 0f;
            }

            var willCrouch = crouch || (_player.IsCrouched && !_player.HasHeadroomToStand());
            return willCrouch ? 1f : Mathf.Min(1f, spec.CrouchSpeed / spec.WalkSpeed);
        }

        // 横のずれは壁へ寄せるのと同じ割合で詰め、着いたときに入った時の当たり点の正面に来るようにする。
        private void SlideIn()
        {
            var normal = _state.Normal.ToUnity();
            var right = CoverProbe.RightAlong(_state.Normal).ToUnity();
            var delta = (-normal * _state.EnterStep) + (right * (-_appliedPeek * _state.EnterTurn));
            if (delta != Vector3.zero)
            {
                var before = transform.position;
                _player.Controller.Move(delta);
                _appliedPeek += Vector3.Dot(transform.position - before, right);
            }

            TurnTowards(-normal, _state.EnterTurn);
        }

        private void SlidePeek(float deltaSeconds)
        {
            var target = _state.PeekOffset;
            if (Mathf.Approximately(target, _appliedPeek))
            {
                return;
            }

            var spec = _player.Motor.Spec;
            var speed = spec.CoverPeekSideDistance / spec.CoverPeekSlideSeconds;
            var step = Mathf.MoveTowards(_appliedPeek, target, speed * deltaSeconds) - _appliedPeek;
            var right = CoverProbe.RightAlong(_state.Normal).ToUnity();
            var before = transform.position;
            _player.Controller.Move(right * step);

            // 横に障害物があれば出られた分だけを覚え、戻るときに出た分だけ戻す。
            _appliedPeek += Vector3.Dot(transform.position - before, right);
        }

        private void UpdateShoulder()
        {
            if (_rig == null)
            {
                return;
            }

            var leftEdge = _state.IsPeeking ? _state.PeekOffset < 0f : _lastProbe.IsAtLeftEdge && !_lastProbe.IsAtRightEdge;
            if (leftEdge == _shoulderOverridden)
            {
                return;
            }

            _shoulderOverridden = leftEdge;
            if (leftEdge)
            {
                _savedShoulder = _rig.Shoulder;
                _rig.Shoulder = ShoulderSide.Left;
            }
            else if (_rig.Shoulder == ShoulderSide.Left)
            {
                // 上書き中にプレイヤーが切り替えた肩は戻さない。
                _rig.Shoulder = _savedShoulder;
            }
        }

        private void OnExited()
        {
            if (_rig != null && _shoulderOverridden && _rig.Shoulder == ShoulderSide.Left)
            {
                _rig.Shoulder = _savedShoulder;
            }

            _shoulderOverridden = false;

            // 横に出たまま出ると壁の端の外に取り残されるが、戻すと撃ち合いの最中に勝手に動くので、その場に残す。
            _appliedPeek = 0f;
        }

        private void TurnTowards(Vector3 direction, float fraction)
        {
            var targetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            var delta = Mathf.DeltaAngle(transform.eulerAngles.y, targetYaw) * fraction;
            if (Mathf.Abs(delta) < 1e-4f)
            {
                return;
            }

            // FirstPersonLook が自分の角度を持っているので、回すならそれ経由にしないと次の視点操作で元に戻る。
            var look = _player.Look;
            if (look != null && look.IsInitialized)
            {
                look.ApplyLookDelta(new Vector2(delta, 0f));
            }
            else
            {
                transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y + delta, 0f);
            }
        }

        private CoverProbeResult ProbeFront()
        {
            var forward = Flatten(transform.forward);
            if (forward == Vector3.zero)
            {
                return CoverProbeResult.None;
            }

            return Probe(transform.position, forward, forward);
        }

        private CoverProbeResult ProbeHeldWall()
        {
            var into = -_state.Normal.ToUnity();
            if (into == Vector3.zero)
            {
                return CoverProbeResult.None;
            }

            // 端から横に出ている間は、出る前の位置で壁を調べる (出た先にはもう壁が無い)。
            var right = CoverProbe.RightAlong(_state.Normal).ToUnity();
            return Probe(transform.position - (right * _appliedPeek), into, into);
        }

        private CoverProbeResult Probe(Vector3 bodyPosition, Vector3 direction, Vector3 facing)
        {
            var feetY = FeetY();
            var origin = new Vector3(bodyPosition.x, feetY + _probeHeight, bodyPosition.z);
            if (!TryCast(origin, direction, _state.Spec.CoverMaxEnterDistance, out var hit))
            {
                origin = new Vector3(bodyPosition.x, feetY + _upperProbeHeight, bodyPosition.z);
                if (!TryCast(origin, direction, _state.Spec.CoverMaxEnterDistance, out hit))
                {
                    return CoverProbeResult.None;
                }
            }

            var normal = hit.normal;
            var topHeight = MeasureTopHeight(hit, feetY);
            var flatNormal = Flatten(normal);
            var wallOnLeft = true;
            var wallOnRight = true;
            if (flatNormal != Vector3.zero)
            {
                var right = CoverProbe.RightAlong(flatNormal.ToNumerics()).ToUnity();
                var into = -flatNormal;
                var reach = hit.distance + EdgeProbeSlack;
                wallOnLeft = TryCast(origin - (right * CoverProbe.EdgeProbeDistance), into, reach, out _);
                wallOnRight = TryCast(origin + (right * CoverProbe.EdgeProbeDistance), into, reach, out _);
            }

            var sample = new CoverWallSample(hit.distance, normal.ToNumerics(), topHeight, wallOnLeft, wallOnRight);
            var result = CoverProbe.Evaluate(sample, facing.ToNumerics(), _state.Spec);
            if (result.IsWall)
            {
                _coverTopY = feetY + topHeight;
            }

            return result;
        }

        // 当たった壁のコライダーだけに、上から下ろした Ray を当てて上端を測る。始点が壁の中 (それより高い壁) なら当たらないので上限の高さとする。
        private float MeasureTopHeight(RaycastHit hit, float feetY)
        {
            var inset = hit.point - (Flatten(hit.normal) * TopProbeInset);
            var start = new Vector3(inset.x, feetY + _topProbeHeight, inset.z);
            var ray = new Ray(start, Vector3.down);
            return hit.collider.Raycast(ray, out var top, _topProbeHeight) ? top.point.y - feetY : _topProbeHeight;
        }

        private bool TryCast(Vector3 origin, Vector3 direction, float distance, out RaycastHit nearest)
        {
            var count = Physics.RaycastNonAlloc(
                origin, direction, _castBuffer, distance, _wallMask, QueryTriggerInteraction.Ignore);
            var found = false;
            nearest = default;
            var body = transform;
            for (var i = 0; i < count; i++)
            {
                var hit = _castBuffer[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(body))
                {
                    continue;
                }

                if (!found || hit.distance < nearest.distance)
                {
                    nearest = hit;
                    found = true;
                }
            }

            Array.Clear(_castBuffer, 0, count);
            return found;
        }

        private float FeetY()
        {
            var controller = _player.Controller;
            return transform.TransformPoint(controller.center).y - (controller.height * 0.5f * Mathf.Abs(transform.lossyScale.y));
        }

        private float BodyRadius()
        {
            var scale = transform.lossyScale;
            return _player.Controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        }

        private bool ReadAim()
        {
            if (_ads != null && _ads.isActiveAndEnabled && _ads.IsInitialized)
            {
                return _ads.IsAiming;
            }

            return _aimAction != null && _aimAction.IsPressed();
        }

        private void Report(CoverTransition transition)
        {
            var log = _state.FormatLog(transition);
            if (log != null)
            {
                Debug.Log(log, this);
            }
        }

        private void SyncMoveFilter()
        {
            if (_player == null)
            {
                return;
            }

            if (enabled && _player.IsInitialized)
            {
                _player.MoveFilter = this;
            }
            else if (ReferenceEquals(_player.MoveFilter, this))
            {
                _player.MoveFilter = null;
            }
        }

        private void BindDamageable(Damageable damageable)
        {
            if (_damageable != null)
            {
                _damageable.Died -= OnDied;
                _damageable.HealthReset -= OnHealthReset;
            }

            _damageable = damageable;
            if (_damageable != null)
            {
                _damageable.Died += OnDied;
                _damageable.HealthReset += OnHealthReset;
            }
        }

        private void OnDied(Damageable _)
        {
            ExitCover(ReasonDeath);
        }

        private void OnHealthReset(Health _)
        {
            ExitCover(ReasonRespawn);
        }

        // プロジェクト全体のアクションは他の部品と共有なので、自分が有効にしたものだけ後で無効に戻す。
        private void AcquireActions()
        {
            _enabledCover = EnableIfNeeded(_coverAction);
            _enabledAim = EnableIfNeeded(_aimAction);
            _enabledMove = EnableIfNeeded(_moveAction);
        }

        private void ReleaseActions()
        {
            DisableIfOwned(_coverAction, ref _enabledCover);
            DisableIfOwned(_aimAction, ref _enabledAim);
            DisableIfOwned(_moveAction, ref _enabledMove);
        }

        private static bool EnableIfNeeded(InputAction action)
        {
            if (action == null || action.enabled)
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

        private static Vector3 Flatten(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude < 1e-12f ? Vector3.zero : v.normalized;
        }
    }
}
