using System;
using Fps.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fps.Runtime
{
    /// <summary>
    /// 遮蔽物に張り付いて身を隠すカバーを受け持つ、プレイヤーのコンポーネント。
    /// <see cref="Configure"/> でルールの <see cref="ActionRules.Cover"/> が <see cref="Cover.On"/> のときだけ有効にする。
    /// Cover を押したとき正面の近くに壁があれば張り付き (<see cref="CoverProbe"/>・<see cref="CoverState"/>)、
    /// 張り付いている間は <see cref="PlayerController.MoveFilter"/> として移動を壁沿いの横移動だけにし、ダッシュとジャンプを止め、
    /// 低い壁ではしゃがませる。Aim を押すと低い壁では立って、高い壁では端から横に出て覗く。
    /// 参照はコードで解決するので Inspector の配線は要らない: <see cref="PlayerController"/> (と、その CharacterController・FirstPersonLook) は同じ
    /// GameObject、<see cref="ViewRig"/>・<see cref="AimDownSights"/>・<see cref="Damageable"/> は同じ GameObject にあれば使い、
    /// 入力はプロジェクト全体のアクション (<see cref="InputSystem.actions"/>) の Cover / Aim / Move。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CoverController : MonoBehaviour, IPlayerMoveFilter
    {
        public const string CoverActionPath = InputActionPaths.Cover;
        public const string AimActionPath = InputActionPaths.Aim;
        public const string MoveActionPath = InputActionPaths.Move;

        /// <summary>無効にされて出た。</summary>
        public const string ReasonDisabled = "disabled";

        /// <summary>死亡して出た。</summary>
        public const string ReasonDeath = "death";

        /// <summary>体力を作り直して (リスポーンして) 出た。</summary>
        public const string ReasonRespawn = "respawn";

        /// <summary>壁を探す Ray の、足元からの高さ (m)。低い壁 (1.1m 未満) にも段差より上で当たる高さ。</summary>
        public const float DefaultProbeHeight = 0.5f;

        /// <summary>壁の上端を探すときに、上から下ろす Ray の始点の足元からの高さ (m)。これより高い壁は上端をこの高さとして扱う。</summary>
        public const float DefaultTopProbeHeight = 3f;

        /// <summary>高い壁の端から横に出る・戻るのにかける時間 (秒)。</summary>
        public const float PeekSlideSeconds = 0.15f;

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

        [SerializeField, Min(0f), Tooltip("壁の上端を探す Ray の始点の、足元からの高さ (m)")]
        private float _topProbeHeight = DefaultTopProbeHeight;

        private readonly CoverState _state = new CoverState();
        private PlayerController _player;
        private ViewRig _rig;
        private AimDownSights _ads;
        private Damageable _damageable;
        private InputAction _coverAction;
        private InputAction _aimAction;
        private InputAction _moveAction;
        private bool _enabledCover;
        private bool _enabledAim;
        private bool _enabledMove;
        private bool _isLive;

        private CoverProbeResult _lastProbe;
        private float _coverTopY;

        // 高い壁の端から覗くために、今どれだけ横に出ているか (m、壁に向いて右が正)。壁の検査はこの分を戻した位置から行う。
        private float _appliedPeek;

        // カバーに入ったときの肩。左端で Left にした後、端を離れるか出たら戻す。
        private ShoulderSide _savedShoulder = ShoulderSide.Right;
        private bool _shoulderOverridden;

        /// <summary>遮蔽物に張り付いている (寄せている途中を含む) か。</summary>
        public bool IsInCover => _state.IsInCover;

        /// <summary>張り付いたまま身を乗り出して狙っているか (低い壁で立つ・高い壁の端から出る)。</summary>
        public bool IsPeeking => _state.IsPeeking;

        /// <summary>張り付いている遮蔽物の上端の高さ (ワールドの y、m)。<see cref="IsInCover"/> が false の間は意味を持たない。</summary>
        public float CoverTopY => _coverTopY;

        /// <summary>張り付いている遮蔽物の面の、こちら向きの法線 (ワールド、水平で長さ 1)。<see cref="IsInCover"/> が false の間は意味を持たない。</summary>
        public Vector3 CoverNormal => _state.Normal.ToUnity();

        /// <summary>張り付いている壁の高さの区分。<see cref="IsInCover"/> が false の間は意味を持たない。</summary>
        public CoverHeight CoverHeight => _state.Height;

        /// <summary>今の状態。</summary>
        public CoverPhase Phase => _state.Phase;

        /// <summary>最後に調べた壁の判定 (カバーの外では Cover を押したときだけ調べる)。</summary>
        public CoverProbeResult LastProbe => _lastProbe;

        /// <summary>高い壁の端から覗くために今横に出ている量 (m、壁に向いて右が正)。</summary>
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

        /// <summary>rules の <see cref="ActionRules.Cover"/> が <see cref="Cover.On"/> なら有効に、それ以外なら無効にする (カバー中なら出る)。</summary>
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

        /// <summary>
        /// 同じ GameObject の <see cref="PlayerController"/> (初期化済み) とプロジェクト全体の Cover / Aim / Move を探して初期化する。
        /// <see cref="ViewRig"/>・<see cref="AimDownSights"/>・<see cref="Damageable"/> は無くてもよい。
        /// 見つからなければ理由を Debug.LogError に出して false を返す。
        /// </summary>
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

        /// <summary>
        /// 参照を直接渡して初期化する。player はこの GameObject の初期化済みのもの。入力は null 可 (<see cref="Tick"/> を直接呼ぶテストなど)。
        /// <see cref="ViewRig"/>・<see cref="AimDownSights"/>・<see cref="Damageable"/> は同じ GameObject にあれば使う。
        /// </summary>
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

        /// <summary>
        /// 1 フレーム分の入力でカバーを進める。外では Cover を押したときだけ正面の壁を調べ、中では張り付いている壁を調べ直す。
        /// 入りかけの間は壁へ寄せて壁に向かせ、覗いている間は横に出る。状態が変わったら <c>[Cover] ..</c> をログに出す。
        /// 無効か未初期化なら何もしない。
        /// </summary>
        /// <param name="coverPressed">このフレームで Cover を押したか。</param>
        /// <param name="aimHeld">Aim を押しているか (覗くか)。</param>
        /// <param name="move">移動入力 (x が右、y が前)。カバー中の後ろの入力で出るのに使う。</param>
        /// <param name="deltaSeconds">経過秒数。0 以上。</param>
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

            var wasInCover = _state.IsInCover;
            if (!wasInCover && !coverPressed)
            {
                return;
            }

            _lastProbe = wasInCover ? ProbeHeldWall() : ProbeFront();
            var progressBefore = _state.EnterProgress;
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
                _appliedPeek = 0f;
            }
            else if (_state.Phase == CoverPhase.Entering || (progressBefore < 1f && _state.EnterProgress >= 1f))
            {
                SlideIn(progressBefore, _state.EnterProgress);
            }

            SlidePeek(deltaSeconds);
            UpdateShoulder();
        }

        /// <summary>
        /// カバー中だけ、移動入力を壁沿いの横移動 (しゃがみの速さ) にし、ダッシュとジャンプを止め、低い壁ではしゃがませる (覗いている間は立たせる)。
        /// 寄せている途中と覗いている間は動かさない。高い壁ではしゃがみの入力をそのまま使う。
        /// </summary>
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

        /// <summary>外の理由でカバーから出る。カバー中だったら true。</summary>
        /// <param name="reason">ログに出す理由 (<see cref="ReasonDeath"/> など)。</param>
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

        // 寄せ終わる時刻までの残りを等分して進むので、最後のフレームでちょうど目標の距離と向きになる。
        private void SlideIn(float progressBefore, float progressAfter)
        {
            if (progressBefore >= 1f)
            {
                return;
            }

            var fraction = Mathf.Clamp01((progressAfter - progressBefore) / (1f - progressBefore));
            if (fraction <= 0f)
            {
                return;
            }

            var normal = _state.Normal.ToUnity();
            var hold = CoverProbe.HoldDistance(BodyRadius());
            var gap = _lastProbe.Distance - hold;
            _player.Controller.Move(-normal * (gap * fraction));
            TurnTowards(-normal, fraction);
        }

        private void SlidePeek(float deltaSeconds)
        {
            var target = _state.PeekOffset;
            if (Mathf.Approximately(target, _appliedPeek))
            {
                return;
            }

            var speed = CoverProbe.PeekSideDistance / PeekSlideSeconds;
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
            _rig.Shoulder = leftEdge ? ShoulderSide.Left : _savedShoulder;
        }

        private void OnExited()
        {
            if (_rig != null && _shoulderOverridden)
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
            if (!TryCast(origin, direction, CoverProbe.MaxEnterDistance, out var hit))
            {
                return CoverProbeResult.None;
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
            var result = CoverProbe.Evaluate(sample, facing.ToNumerics());
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
