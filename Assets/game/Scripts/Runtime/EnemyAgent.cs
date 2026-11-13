using System;
using System.Collections.Generic;
using System.Globalization;
using Fps.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Fps.Runtime
{
    /// <summary>
    /// キャンペーンの敵をシーン上で動かす。判断は <see cref="EnemyBrain"/> に任せ、ここでは
    /// 毎フレーム知覚 (<see cref="EnemySenses"/>) を組み立てて渡し、返ってきた <see cref="EnemyDecision"/> を
    /// <see cref="NavMeshAgent.SetDestination"/>・向きの変更・射撃 (<see cref="Hitscan.Fire"/>) に変換するだけをする。
    /// 参照はコードで解決する: NavMeshAgent と自分の <see cref="Damageable"/> は同じ GameObject、
    /// 目標は <see cref="TargetTag"/> のタグが付いた GameObject の Damageable。
    /// 同じ GameObject に <see cref="SideMember"/> があれば、タグの代わりに自分と違う陣営で生きている、いちばん近い相手を
    /// <see cref="TargetSearchInterval"/> ごとに (目標が倒れたらすぐに) 選び直す (<see cref="EnemySenses.SelectHostile"/>)。
    /// モードが倒れた敵を戻すときは <see cref="Respawn"/> を呼ぶ。
    /// 武器は 1 本だけ持ち、持ち替えない。その値は <see cref="WeaponDefinition"/> アセットから作り、このコンポーネントには持たない
    /// (シーン生成スクリプトが、敵の配置ごとに指定した Id の武器を武器の一覧 <see cref="WeaponCatalogAsset"/> から引いて配線する)。
    /// 状態が変わるたびに <c>[AI] id=.. state=..</c> をログに出す。
    /// 撃たれたら (<see cref="Damageable.Damaged"/>) 撃った者の位置を次の知覚 1 回だけ物音として渡し、
    /// 視界の外から撃たれても <see cref="EnemyBrain"/> がその位置を Search するようにする。
    /// 動こうとしているのに進めなければ (<see cref="StuckMonitor"/>)、経路の引き直し → 寄り道 → 目的地をあきらめる →
    /// 瞬間移動の順に立て直し、<c>[AI] id=.. stuck recovery=..</c> をログに出す。NavMesh から外れたら近くの NavMesh に戻し
    /// (近くに無ければ瞬間移動の段階で出現地点へ戻し)、位置が NaN などになったら最後に正常だった位置に戻す。判断の良し悪しはここでは直さない (<see cref="EnemyBrain"/> 側で調整する)。
    /// <para>
    /// <see cref="CoverRegistry"/> を配線していれば、その selector を <see cref="EnemyBrain.UseCover"/> に渡し、撃たれたときや体力が減ったときに
    /// 遮蔽点 (<see cref="CoverPoint"/>) へ NavMesh で移る (TakeCover)。低い壁 (<see cref="CoverHeight.Low"/>) の点で隠れている間は
    /// しゃがみ、当たり判定のカプセルと目の高さを <see cref="CrouchHeight"/>・<see cref="CrouchEyeHeight"/> まで下げる。
    /// 覗くときは立ち上がり、点に覗く位置 (<see cref="CoverPoint.PeekPosition"/>) があればそこへ出て撃ち、隠れ直すときに点へ戻る。
    /// 使う点や段階が変わるたびに <c>[AI] id=.. cover phase=.. point=..</c> をログに出す。
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Damageable))]
    public sealed class EnemyAgent : MonoBehaviour, ICharacterMotionSource
    {
        public const string LogTag = "[AI]";

        /// <summary>目標を探すタグ。<see cref="GameBootstrap"/> がプレイヤーに付ける。</summary>
        public const string TargetTag = "Player";

        public const float DefaultSightRange = 20f;
        public const float DefaultAttackRange = 12f;
        public const float DefaultLoseSightSeconds = 2f;
        public const float DefaultSearchSeconds = 6f;
        public const float DefaultReactionSeconds = 1.0f;

        /// <summary>Attack に入って最初の 1 発の散らばりの半角 (度)。12m 先で半径 1m ほどに散り、外れやすい。</summary>
        public const float DefaultOpeningSpreadDegrees = 5f;

        /// <summary>撃ち始めの数発が過ぎた後の散らばりの半角 (度)。</summary>
        public const float DefaultSettledSpreadDegrees = 0f;

        /// <summary>散らばりを絞り切るまでの発数。</summary>
        public const int DefaultOpeningShots = 3;

        /// <summary>足元からの目の高さ (メートル)。</summary>
        public const float DefaultEyeHeight = 1.6f;

        /// <summary>LookAt に向き直る速さ (度/秒)。</summary>
        public const float DefaultTurnSpeed = 360f;

        /// <summary>しゃがんだときの当たり判定の高さ (メートル)。低い壁 (<see cref="CoverProbe.LowWallHeight"/> 未満) に隠れる高さ。</summary>
        public const float DefaultCrouchHeight = 1f;

        /// <summary>しゃがんだときの足元からの目の高さ (メートル)。</summary>
        public const float DefaultCrouchEyeHeight = 0.8f;

        /// <summary>巡回地点を NavMesh 上に寄せるときに探す半径 (メートル)。</summary>
        public const float RouteSnapRadius = 2f;

        /// <summary>目的地がこれ以上動いたら経路を引き直す (メートル)。毎フレームの再計算を避ける。</summary>
        public const float RepathDistance = 0.25f;

        /// <summary>目標が見つからないときに探し直す間隔 (秒)。</summary>
        public const float TargetSearchInterval = 1f;

        /// <summary>進めないまま次の立て直しへ移るまでの秒数。</summary>
        public const float StuckSeconds = 2f;

        /// <summary>これだけ動けば (立て直し中は目的地へこれだけ近づけば) 進めたとみなす距離 (メートル)。</summary>
        public const float StuckProgressDistance = 0.5f;

        /// <summary>立て直しの途中で目的地がこれ以上変わったら、新しい目的地までの距離から測り直す距離 (メートル)。</summary>
        public const float StuckGoalChangeDistance = 2f;

        /// <summary>寄り道の行き先を探す半径 (メートル)。</summary>
        public const float SidestepRadius = 2f;

        /// <summary>寄り道の行き先として、今の位置からこれ以上離れた地点を選ぶ (メートル)。</summary>
        public const float SidestepMinDistance = 0.75f;

        /// <summary>寄り道の行き先を探し直す回数。</summary>
        public const int SidestepAttempts = 6;

        /// <summary>寄り道を切り上げて元の目的地へ向かい直すまでの秒数。</summary>
        public const float SidestepSeconds = 1.5f;

        /// <summary>瞬間移動の行き先にする経路の角は、今の位置からこれ以上先のもの (メートル)。</summary>
        public const float WarpLookAhead = 1.5f;

        /// <summary>NavMesh から外れたときに戻る先を探す半径 (メートル)。</summary>
        public const float OffMeshSnapRadius = 3f;

        /// <summary>NavMesh に戻れなかったときに試し直す間隔 (秒)。</summary>
        public const float OffMeshRetryInterval = 1f;

        [SerializeField, Tooltip("ログに出す識別子。空なら Damageable の Id")]
        private string _id = string.Empty;

        [SerializeField, Tooltip("巡回地点 (ワールド座標の足元)。空なら Idle")]
        private Vector3[] _patrolRoute = Array.Empty<Vector3>();

        [Header("Brain")]
        [SerializeField, Min(0.01f)]
        private float _sightRange = DefaultSightRange;

        [SerializeField, Min(0.01f)]
        private float _attackRange = DefaultAttackRange;

        [SerializeField, Min(0f)]
        private float _loseSightSeconds = DefaultLoseSightSeconds;

        [SerializeField, Min(0f)]
        private float _searchSeconds = DefaultSearchSeconds;

        [SerializeField, Min(0f)]
        private float _reactionSeconds = DefaultReactionSeconds;

        [SerializeField, Range(0f, EnemySpec.MaxSpreadDegrees), Tooltip("Attack に入って最初の 1 発の散らばりの半角 (度)")]
        private float _openingSpreadDegrees = DefaultOpeningSpreadDegrees;

        [SerializeField, Range(0f, EnemySpec.MaxSpreadDegrees), Tooltip("撃ち始めの数発が過ぎた後の散らばりの半角 (度)")]
        private float _settledSpreadDegrees = DefaultSettledSpreadDegrees;

        [SerializeField, Min(0), Tooltip("散らばりを絞り切るまでの発数")]
        private int _openingShots = DefaultOpeningShots;

        [Header("Body")]
        [SerializeField, Min(0f)]
        private float _eyeHeight = DefaultEyeHeight;

        [SerializeField, Min(0f)]
        private float _turnSpeed = DefaultTurnSpeed;

        [SerializeField, Min(0.01f), Tooltip("しゃがんだときの当たり判定の高さ (m)")]
        private float _crouchHeight = DefaultCrouchHeight;

        [SerializeField, Min(0f), Tooltip("しゃがんだときの足元からの目の高さ (m)")]
        private float _crouchEyeHeight = DefaultCrouchEyeHeight;

        [Header("Cover")]
        [SerializeField, Tooltip("隠れに行く遮蔽点をまとめたもの。シーン生成スクリプトが配線する。空なら遮蔽点を使わない")]
        private CoverPointRegistry _coverRegistry;

        [SerializeField, Min(0f), Tooltip("遮蔽点に着いてから、また隠れ直してから、覗くまで隠れている時間 (秒)")]
        private float _coverHideSeconds = EnemySpec.DefaultCoverHideSeconds;

        [SerializeField, Min(0f), Tooltip("遮蔽点から 1 回覗いている時間 (秒)")]
        private float _coverPeekSeconds = EnemySpec.DefaultCoverPeekSeconds;

        [SerializeField, Range(0f, 1f), Tooltip("体力の割合がこれを下回ったら、撃たれていなくても遮蔽点へ移る。0 なら体力では移らない")]
        private float _coverHealthFraction = EnemySpec.DefaultCoverHealthFraction;

        [Header("Weapon")]
        [SerializeField, Tooltip("撃つ武器の定義 (弾数・連射間隔・ダメージ・射程・距離減衰)。シーン生成スクリプトが配置ごとに武器の一覧から引いて配線する")]
        private WeaponDefinition _weaponDefinition;

        private NavMeshAgent _agent;
        private Damageable _self;
        private Damageable _target;
        private EnemyBrain _brain;
        private Weapon _weapon;
        private Vector3? _destination;
        private Vector3? _pendingNoise;
        private bool _pendingHit;
        private Vector3? _pendingHitSource;
        private CapsuleCollider _capsule;
        private float _standingCapsuleHeight;
        private Vector3 _standingCapsuleCenter;
        private bool _crouching;
        private int _loggedCoverIndex = -1;
        private EnemyCoverPhase _loggedCoverPhase = EnemyCoverPhase.None;
        private float _nextTargetSearch;
        private bool _warnedNoTarget;
        private bool _warnedOffMesh;
        private StuckMonitor _stuck;
        private Vector3? _goal;
        private Vector3? _detour;
        private float _detourEndsAt;
        private Vector3 _spawnPosition;
        private Vector3 _lastFinitePosition;
        private float _nextOffMeshRetry;
        private readonly System.Random _random = new System.Random();

        /// <summary>ログに出す識別子。</summary>
        public string Id
        {
            get
            {
                if (!string.IsNullOrEmpty(_id))
                {
                    return _id;
                }

                var self = _self != null ? _self : GetComponent<Damageable>();
                return self != null ? self.Id : name;
            }

            set => _id = value ?? string.Empty;
        }

        /// <summary>判断を行う状態機械。未初期化なら null。</summary>
        public EnemyBrain Brain => _brain;

        public bool IsInitialized => _brain != null;

        /// <summary>今の状態。未初期化なら Idle。</summary>
        public EnemyState State => _brain?.State ?? EnemyState.Idle;

        /// <summary>
        /// 今の目標。null なら次の Update で <see cref="TargetTag"/> から探す。<see cref="SideMember"/> があるときは
        /// <see cref="TargetSearchInterval"/> ごとに選び直すので、設定しても次の選び直しまでしか続かない。
        /// </summary>
        public Damageable Target
        {
            get => _target;
            set => _target = value;
        }

        public Weapon Weapon => _weapon;

        /// <summary>
        /// 撃つ武器の定義。<see cref="TryInitialize"/> がこれから <see cref="Weapon"/> を作るので、それより前に設定する
        /// (初期化後に変えても今の武器は変わらない)。
        /// </summary>
        public WeaponDefinition WeaponDefinition
        {
            get => _weaponDefinition;
            set => _weaponDefinition = value;
        }

        /// <summary>巡回地点 (ワールド座標の足元)。</summary>
        public IReadOnlyList<Vector3> PatrolRoute => _patrolRoute;

        public float SightRange => _sightRange;
        public float AttackRange => _attackRange;
        public float EyeHeight => _eyeHeight;
        public float CrouchHeight => _crouchHeight;
        public float CrouchEyeHeight => _crouchEyeHeight;

        /// <summary>
        /// 隠れに行く遮蔽点をまとめたもの。null なら遮蔽点を使わない。変えたら次の Update から新しい点を使う (使っていた点は放す)。
        /// </summary>
        public CoverPointRegistry CoverRegistry
        {
            get => _coverRegistry;
            set => _coverRegistry = value;
        }

        /// <summary>低い壁の遮蔽点でしゃがんで隠れているか。</summary>
        public bool IsCrouching => _crouching;

        /// <summary>足元の位置。NavMeshAgent の baseOffset を引いた位置。</summary>
        public Vector3 GroundPosition
        {
            get
            {
                var offset = _agent != null ? _agent.baseOffset : 0f;
                return transform.position - (Vector3.up * offset);
            }
        }

        /// <summary>視線と射撃の始点。しゃがんでいれば <see cref="CrouchEyeHeight"/> の高さ。</summary>
        public Vector3 EyePosition => GroundPosition + (Vector3.up * (_crouching ? Mathf.Min(_crouchEyeHeight, _eyeHeight) : _eyeHeight));

        /// <summary>NavMesh の上にいて、Off-Mesh Link (飛び降りなど) を渡っている最中でなければ接地している。未初期化なら false。</summary>
        bool ICharacterMotionSource.IsGrounded => _agent != null && _agent.enabled && _agent.isOnNavMesh && !_agent.isOnOffMeshLink;

        /// <summary>敵はしゃがまない。</summary>
        bool ICharacterMotionSource.IsCrouched => false;

        /// <summary>NavMeshAgent の速さ。未初期化なら 0。</summary>
        float ICharacterMotionSource.WalkSpeed => _agent != null ? _agent.speed : 0f;

        /// <summary>敵はダッシュしない (追うときも NavMeshAgent の速さのまま歩く)。</summary>
        bool ICharacterMotionSource.TryGetSprinting(out bool sprinting)
        {
            sprinting = false;
            return true;
        }

        /// <summary>
        /// 巡回地点を設定する。初期化済みなら NavMesh 上に寄せてすぐ <see cref="EnemyBrain.SetPatrolRoute"/> に渡す。
        /// </summary>
        public void SetPatrolRoute(IReadOnlyList<Vector3> route)
        {
            if (route == null)
            {
                throw new ArgumentNullException(nameof(route));
            }

            var copy = new Vector3[route.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = route[i];
            }

            _patrolRoute = copy;
            if (_brain != null)
            {
                ApplyPatrolRoute();
            }
        }

        /// <summary>
        /// 部品を解決し、Inspector の値から <see cref="EnemyBrain"/> を、<see cref="WeaponDefinition"/> から <see cref="Weapon"/> を作る。
        /// 失敗したら (定義が未設定・不正なときも) 理由を Debug.LogError に出して false を返す。2 回目以降は何もしない。
        /// </summary>
        public bool TryInitialize()
        {
            if (_brain != null)
            {
                return true;
            }

            _agent = GetComponent<NavMeshAgent>();
            _self = GetComponent<Damageable>();
            if (_agent == null || _self == null)
            {
                Debug.LogError($"{LogTag} '{name}' には NavMeshAgent と Damageable が必要です。", this);
                return false;
            }

            if (_weaponDefinition == null)
            {
                Debug.LogError(
                    $"{LogTag} id={Id} に WeaponDefinition が設定されていません。シーン生成スクリプトで配線してください。", this);
                return false;
            }

            EnemySpec spec;
            try
            {
                spec = new EnemySpec(
                    _sightRange,
                    _attackRange,
                    _loseSightSeconds,
                    _searchSeconds,
                    _reactionSeconds,
                    _openingSpreadDegrees,
                    _settledSpreadDegrees,
                    _openingShots,
                    _coverHideSeconds,
                    _coverPeekSeconds,
                    _coverHealthFraction);
            }
            catch (ArgumentException e)
            {
                Debug.LogError($"{LogTag} id={Id} の設定が不正です: {e.Message}", this);
                return false;
            }

            WeaponSpec weaponSpec;
            try
            {
                weaponSpec = _weaponDefinition.ToSpec();
            }
            catch (WeaponDefinitionException e)
            {
                Debug.LogError($"{LogTag} id={Id} の武器定義が不正です: {e.Message}", _weaponDefinition);
                return false;
            }

            // しゃがみで縮めるのは Y 向きのカプセルだけ。ほかの形の当たり判定は形を推し量れないのでそのままにする。
            _capsule = GetComponent<CapsuleCollider>();
            if (_capsule != null && _capsule.direction == 1)
            {
                _standingCapsuleHeight = _capsule.height;
                _standingCapsuleCenter = _capsule.center;
            }
            else
            {
                _capsule = null;
            }

            _stuck = new StuckMonitor(StuckSeconds, StuckProgressDistance, StuckGoalChangeDistance);
            _stuck.Recovered += OnUnstuck;
            _brain = new EnemyBrain(spec);
            _brain.StateChanged += OnStateChanged;
            _weapon = new Weapon(weaponSpec);
            _weapon.Reloaded += OnReloaded;
            _self.Died += OnDied;
            _self.Damaged += OnDamaged;
            return true;
        }

        /// <summary>
        /// 今の位置・目標・視線・体力の割合と、前の知覚の後に撃たれていれば撃たれたこと・撃った者の位置 (物音としても渡す) から、
        /// 1 tick 分の知覚を組み立てる。撃たれた情報はここで使い切るので、次の呼び出しでは撃たれていないことになる。
        /// 未初期化なら <see cref="InvalidOperationException"/>。
        /// </summary>
        public EnemyPerception Perceive()
        {
            if (_brain == null)
            {
                throw new InvalidOperationException($"EnemyAgent '{name}' is not initialized");
            }

            var noise = _pendingNoise;
            var wasHit = _pendingHit;
            var hitSource = _pendingHitSource;
            _pendingNoise = null;
            _pendingHit = false;
            _pendingHitSource = null;
            return EnemySenses.Perceive(
                GroundPosition, EyePosition, _target, _sightRange, transform, noise, wasHit, hitSource, HealthFraction(_self));
        }

        /// <summary>体力の割合 (0 以上 1 以下)。self が無ければ 1。</summary>
        public static float HealthFraction(Damageable self)
        {
            if (self == null)
            {
                return 1f;
            }

            var health = self.Health;
            return health.Max > 0 ? Mathf.Clamp01((float)health.Current / health.Max) : 0f;
        }

        /// <summary>遮蔽点の段階が変わったときのログ。</summary>
        public static string FormatCoverLog(string id, EnemyCoverPhase phase, string point)
        {
            return $"{LogTag} id={id} cover phase={phase} point={(string.IsNullOrEmpty(point) ? "none" : point)}";
        }

        /// <summary>立て直しのログ。</summary>
        public static string FormatStuckLog(
            string id, StuckRecovery recovery, EnemyState state, float stalledSeconds, Vector3 position, Vector3? goal, string path)
        {
            var goalText = goal.HasValue ? goal.Value.ToString("F1") : "none";
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} id={1} stuck recovery={2} state={3} stalled={4:F1}s pos={5} goal={6} path={7}",
                LogTag, id, recovery, state, stalledSeconds, position.ToString("F1"), goalText, path);
        }

        /// <summary>状態変化のログ。</summary>
        public static string FormatStateLog(string id, EnemyState previous, EnemyState next)
        {
            return $"{LogTag} id={id} state={next} prev={previous}";
        }

        private void Awake()
        {
            if (!TryInitialize())
            {
                enabled = false;
            }
        }

        private void Start()
        {
            Debug.Log($"{LogTag} id={Id} state={_brain.State}", this);
            ApplyPatrolRoute();
            var sideMember = GetComponent<SideMember>();
            if (sideMember != null)
            {
                // Player タグの相手を先に目標にすると、間隔が過ぎるまで同陣営のプレイヤーを狙ってしまう。
                ResolveHostile(sideMember, force: true);
            }
            else
            {
                ResolveTarget(force: true);
            }

            _lastFinitePosition = transform.position;
            _spawnPosition = NavMesh.SamplePosition(GroundPosition, out var spawnHit, OffMeshSnapRadius, NavMesh.AllAreas)
                ? spawnHit.position
                : GroundPosition;

            if (!_agent.isOnNavMesh)
            {
                Debug.LogError(
                    $"{LogTag} id={Id} が NavMesh の上にいません。シーンの NavMeshSurface を焼いたか確かめてください。",
                    this);
                _warnedOffMesh = true;
            }
        }

        private void OnDestroy()
        {
            if (_brain != null)
            {
                _brain.StateChanged -= OnStateChanged;
            }

            if (_stuck != null)
            {
                _stuck.Recovered -= OnUnstuck;
            }

            if (_weapon != null)
            {
                _weapon.Reloaded -= OnReloaded;
            }

            if (_self != null)
            {
                _self.Died -= OnDied;
                _self.Damaged -= OnDamaged;
            }
        }

        private void Update()
        {
            if (_brain.IsDead)
            {
                return;
            }

            if (!RecoverNonFinitePosition())
            {
                return;
            }

            var deltaSeconds = Time.deltaTime;
            _weapon.Tick(deltaSeconds);

            var member = GetComponent<SideMember>();
            if (member != null)
            {
                // 陣営で狙う相手は動き回り倒れもするので、見つかっていても間隔ごとに選び直す。倒れたらすぐ次を探す。
                ResolveHostile(member, force: _target != null && _target.IsDead);
            }
            else if (_target == null)
            {
                ResolveTarget(force: false);
            }

            // 同じ selector なら何もしないので毎フレーム渡し、registry の差し替えや作り直しに追いつかせる。
            _brain.UseCover(_coverRegistry != null ? _coverRegistry.Selector : null);

            var perception = Perceive();
            var decision = _brain.Tick(deltaSeconds, perception);
            if (_brain.IsDead)
            {
                return;
            }

            LogCoverChange();
            SetCrouching(decision.Crouch && _brain.CurrentCoverPoint?.Height == CoverHeight.Low);
            MoveTo(CoverMoveTo(decision));
            WatchProgress(deltaSeconds);
            Face(decision.LookAt, deltaSeconds);
            if (decision.Fire)
            {
                Shoot();
            }
        }

        /// <summary>
        /// 遮蔽点に着いた後の TakeCover では <see cref="EnemyBrain"/> は移動先を出さないので、覗くときは覗く位置へ、
        /// 隠れるときは点へ戻る移動先をここで足す。立つ位置に着いていれば null (その場に留まる)。
        /// </summary>
        private System.Numerics.Vector3? CoverMoveTo(EnemyDecision decision)
        {
            if (decision.MoveTo.HasValue || _brain.State != EnemyState.TakeCover)
            {
                return decision.MoveTo;
            }

            var point = _brain.CurrentCoverPoint;
            if (!point.HasValue)
            {
                return null;
            }

            var stand = point.Value.Position.ToUnity();
            if (_brain.CoverPhase == EnemyCoverPhase.Peeking && _coverRegistry != null)
            {
                var peek = _coverRegistry.PeekPositionAt(_brain.CoverPointIndex);
                if (peek.HasValue)
                {
                    stand = peek.Value;
                }
            }

            return HorizontalDistance(GroundPosition, stand) > EnemyBrain.ArrivalDistance ? stand.ToNumerics() : (System.Numerics.Vector3?)null;
        }

        /// <summary>しゃがむ・立つ。Y 向きのカプセルの当たり判定があれば、足元の高さを変えずに縮める・戻す。</summary>
        private void SetCrouching(bool crouching)
        {
            if (_crouching == crouching)
            {
                return;
            }

            _crouching = crouching;
            if (_capsule == null)
            {
                return;
            }

            if (!crouching)
            {
                _capsule.height = _standingCapsuleHeight;
                _capsule.center = _standingCapsuleCenter;
                return;
            }

            var scaleY = Mathf.Abs(transform.lossyScale.y);
            var height = scaleY > 1e-4f ? _crouchHeight / scaleY : _standingCapsuleHeight;
            height = Mathf.Min(height, _standingCapsuleHeight);
            _capsule.height = height;
            _capsule.center = _standingCapsuleCenter - (Vector3.up * ((_standingCapsuleHeight - height) / 2f));
        }

        private void LogCoverChange()
        {
            var index = _brain.CoverPointIndex;
            var phase = _brain.CoverPhase;
            if (index == _loggedCoverIndex && phase == _loggedCoverPhase)
            {
                return;
            }

            _loggedCoverIndex = index;
            _loggedCoverPhase = phase;
            var marker = _coverRegistry != null ? _coverRegistry.MarkerAt(index) : null;
            Debug.Log(FormatCoverLog(Id, phase, marker != null ? marker.name : null), this);
        }

        private void ApplyPatrolRoute()
        {
            var route = new System.Numerics.Vector3[_patrolRoute.Length];
            for (var i = 0; i < route.Length; i++)
            {
                route[i] = SnapToNavMesh(_patrolRoute[i]).ToNumerics();
            }

            _brain.SetPatrolRoute(route);
        }

        private Vector3 SnapToNavMesh(Vector3 point)
        {
            if (NavMesh.SamplePosition(point, out var hit, RouteSnapRadius, NavMesh.AllAreas))
            {
                return hit.position;
            }

            Debug.LogWarning($"{LogTag} id={Id} patrol point {point} is not on the NavMesh; using it as is", this);
            return point;
        }

        private void ResolveTarget(bool force)
        {
            if (!force && Time.time < _nextTargetSearch)
            {
                return;
            }

            _nextTargetSearch = Time.time + TargetSearchInterval;

            var targetObject = GameObject.FindGameObjectWithTag(TargetTag);
            var target = targetObject != null ? targetObject.GetComponentInParent<Damageable>() : null;
            if (target == null)
            {
                if (!_warnedNoTarget)
                {
                    Debug.LogWarning($"{LogTag} id={Id} '{TargetTag}' タグの Damageable が見つかりません。見つかるまで探し続けます。", this);
                    _warnedNoTarget = true;
                }

                return;
            }

            _target = target;
            _warnedNoTarget = false;
            Debug.Log($"{LogTag} id={Id} target={target.Id}", this);
        }

        /// <summary>
        /// member の陣営から見た、いちばん近い生きている敵を目標にする。force でなければ <see cref="TargetSearchInterval"/> ごとにだけ探す。
        /// 敵がいなければ目標を外す (全員倒れている間など)。目標が変わったときだけログに出す。
        /// </summary>
        private void ResolveHostile(SideMember member, bool force)
        {
            if (!force && Time.time < _nextTargetSearch)
            {
                return;
            }

            _nextTargetSearch = Time.time + TargetSearchInterval;

            var hostile = EnemySenses.SelectHostile(member, SideMember.FindAll());
            var target = hostile != null ? hostile.Damageable : null;
            if (target == _target)
            {
                return;
            }

            _target = target;
            Debug.Log($"{LogTag} id={Id} side={member.Side.Id} target={(target != null ? target.Id : "none")}", this);
        }

        /// <summary>
        /// 生きていても倒れていても、groundPosition (足元) に戻して <see cref="Respawn"/> と同じく初めからにする。
        /// ラウンド制のモードが、生き残った敵も含めて全員を出現点に並べ直すのに使う。未初期化なら <see cref="InvalidOperationException"/>。
        /// </summary>
        public void ResetTo(Vector3 groundPosition, Quaternion rotation)
        {
            if (_brain == null)
            {
                throw new InvalidOperationException($"EnemyAgent '{name}' is not initialized");
            }

            // Respawn は倒れた敵だけを戻す約束なので、生きていれば判断だけ倒れたことにしてから同じ手順で戻す。
            _brain.Kill();
            Respawn(groundPosition, rotation);
        }

        /// <summary>
        /// 倒れた敵を groundPosition (足元) に戻して生き返らせる。体力を満たし、武器を弾の満ちた状態で作り直し、NavMeshAgent を有効に戻して
        /// 近くの NavMesh に置き、判断 (<see cref="EnemyBrain.Revive"/>)・経路・立て直し・撃たれた記録を初めからにして、目標を選び直す。
        /// 出現地点 (NavMesh から外れたときの戻り先) も置いた位置にする。
        /// まだ生きていれば何もせず false。未初期化なら <see cref="InvalidOperationException"/>。
        /// </summary>
        public bool Respawn(Vector3 groundPosition, Quaternion rotation)
        {
            if (_brain == null)
            {
                throw new InvalidOperationException($"EnemyAgent '{name}' is not initialized");
            }

            if (!_brain.IsDead)
            {
                return false;
            }

            var ground = NavMesh.SamplePosition(groundPosition, out var hit, OffMeshSnapRadius, NavMesh.AllAreas)
                ? hit.position
                : groundPosition;

            _pendingNoise = null;
            _pendingHit = false;
            _pendingHitSource = null;
            _loggedCoverIndex = -1;
            _loggedCoverPhase = EnemyCoverPhase.None;
            _destination = null;
            _detour = null;
            _goal = null;
            _stuck.Reset();
            SetCrouching(false);

            _weapon.Reloaded -= OnReloaded;
            _weapon = new Weapon(_weapon.Spec);
            _weapon.Reloaded += OnReloaded;

            // 無効のまま Transform を動かしてから有効にすると、NavMeshAgent は新しい位置の近くの NavMesh に載る。
            if (_agent.enabled && _agent.isOnNavMesh)
            {
                _agent.ResetPath();
            }

            _agent.enabled = false;
            transform.SetPositionAndRotation(ground + (Vector3.up * _agent.baseOffset), rotation);
            _agent.enabled = true;
            if (_agent.isOnNavMesh)
            {
                _agent.Warp(ground);
                _agent.isStopped = false;
                _warnedOffMesh = false;
            }
            else
            {
                Debug.LogWarning($"{LogTag} id={Id} respawn point {ground.ToString("F1")} is not on the NavMesh", this);
            }

            _spawnPosition = ground;
            _lastFinitePosition = transform.position;

            _self.ResetHealth();
            _brain.Revive();
            _target = null;
            _nextTargetSearch = 0f;
            Debug.Log($"{LogTag} id={Id} respawned at={ground.ToString("F1")} state={_brain.State}", this);
            return true;
        }

        private void MoveTo(System.Numerics.Vector3? moveTo)
        {
            _goal = moveTo.HasValue ? moveTo.Value.ToUnity() : (Vector3?)null;
            if (!_agent.isOnNavMesh && !TryReturnToNavMesh())
            {
                return;
            }

            if (!_goal.HasValue)
            {
                bool hadDetour = _detour.HasValue;
                _detour = null;
                if (_destination.HasValue || hadDetour)
                {
                    _agent.ResetPath();
                    _destination = null;
                }

                return;
            }

            if (_detour.HasValue)
            {
                if (Time.time < _detourEndsAt && HorizontalDistance(GroundPosition, _detour.Value) > EnemyBrain.ArrivalDistance)
                {
                    return;
                }

                _detour = null;
                _destination = null;
            }

            var destination = _goal.Value;
            if (_destination.HasValue && (destination - _destination.Value).sqrMagnitude < RepathDistance * RepathDistance)
            {
                return;
            }

            _destination = destination;
            _agent.isStopped = false;
            if (!_agent.SetDestination(destination))
            {
                Debug.LogWarning($"{LogTag} id={Id} cannot path to {destination}", this);
            }
        }

        /// <summary>
        /// NavMesh から外れていたら近くの NavMesh に戻す。戻れなければ <see cref="OffMeshRetryInterval"/> ごとに試し直し、
        /// 警告は 1 回だけ出す (戻れないまま進めずにいれば、<see cref="WatchProgress"/> が出現地点へ瞬間移動させる)。
        /// 戻れたら (または外れていなければ) true。
        /// </summary>
        private bool TryReturnToNavMesh()
        {
            if (_agent.isOnNavMesh)
            {
                return true;
            }

            if (Time.time < _nextOffMeshRetry)
            {
                return false;
            }

            _nextOffMeshRetry = Time.time + OffMeshRetryInterval;
            if (NavMesh.SamplePosition(GroundPosition, out var hit, OffMeshSnapRadius, NavMesh.AllAreas) && _agent.Warp(hit.position))
            {
                Debug.LogWarning($"{LogTag} id={Id} returned to NavMesh from={GroundPosition.ToString("F1")} to={hit.position.ToString("F1")}", this);
                _destination = null;
                _warnedOffMesh = false;
                return true;
            }

            if (!_warnedOffMesh)
            {
                Debug.LogWarning($"{LogTag} id={Id} が NavMesh の上にいないので移動できません。近くに NavMesh が無いので、進めないままなら出現地点へ戻します。", this);
                _warnedOffMesh = true;
            }

            return false;
        }

        /// <summary>
        /// 位置が NaN や無限大になっていたら、最後に正常だった位置へ戻す。戻せなかったらこのフレームは何もしない (false)。
        /// </summary>
        private bool RecoverNonFinitePosition()
        {
            var position = transform.position;
            if (StuckMonitor.IsFinite(position.ToNumerics()))
            {
                _lastFinitePosition = position;
                return true;
            }

            Debug.LogWarning($"{LogTag} id={Id} position was not finite ({position}); restoring {_lastFinitePosition.ToString("F1")}", this);
            transform.position = _lastFinitePosition;
            _destination = null;
            _detour = null;
            _stuck.Reset();
            return StuckMonitor.IsFinite(transform.position.ToNumerics());
        }

        /// <summary>進めているかを見張り、進めていなければ <see cref="StuckMonitor"/> の返した立て直しをする。</summary>
        private void WatchProgress(float deltaSeconds)
        {
            // 寄り道中も元の目的地に近づけたかで測る。寄り道先で測ると、寄り道しただけで立ち直ったことになる。
            var ground = GroundPosition;
            var heading = _goal.HasValue && HorizontalDistance(ground, _goal.Value) > EnemyBrain.ArrivalDistance
                ? _goal
                : null;
            // 経路の角の取得は毎回ヒープを確保するので、経路の向きを使う立て直し中だけ計算する。
            var waypoint = _stuck.Stage == StuckRecovery.None ? null : PathWaypoint(heading);
            var recovery = _stuck.Tick(
                deltaSeconds,
                ground.ToNumerics(),
                heading.HasValue ? heading.Value.ToNumerics() : (System.Numerics.Vector3?)null,
                waypoint.HasValue ? waypoint.Value.ToNumerics() : (System.Numerics.Vector3?)null);
            if (recovery == StuckRecovery.None)
            {
                return;
            }

            var path = _agent.isOnNavMesh ? _agent.pathStatus.ToString() : "OffMesh";
            Debug.Log(FormatStuckLog(Id, recovery, _brain.State, _stuck.TotalStalledSeconds, ground, heading, path), this);
            switch (recovery)
            {
                case StuckRecovery.Repath:
                    Repath();
                    break;
                case StuckRecovery.Sidestep:
                    Sidestep(ground, heading);
                    break;
                case StuckRecovery.AbandonGoal:
                    _brain.AbandonGoal();
                    Repath();
                    break;
                case StuckRecovery.Warp:
                    Warp(ground);
                    break;
            }
        }

        /// <summary>
        /// 目的地への今の経路で、<see cref="StuckProgressDistance"/> 以上先にある最初の角 (無ければ最後の角)。
        /// 寄り道中は経路の終点が寄り道先なので null。寄り道先へ歩いただけで立ち直ったことにしないため。
        /// </summary>
        private Vector3? PathWaypoint(Vector3? goal)
        {
            if (!goal.HasValue || _detour.HasValue || !_agent.isOnNavMesh || !_agent.hasPath || _agent.pathPending)
            {
                return null;
            }

            var corners = _agent.path.corners;
            if (corners.Length == 0)
            {
                return null;
            }

            var ground = GroundPosition;
            foreach (var corner in corners)
            {
                if (HorizontalDistance(ground, corner) >= StuckProgressDistance)
                {
                    return corner;
                }
            }

            return corners[corners.Length - 1];
        }

        private void Repath()
        {
            _detour = null;
            _destination = null;
            if (_agent.isOnNavMesh)
            {
                _agent.ResetPath();
            }
        }

        /// <summary>
        /// 近くの、経路が最後までつながる地点へ少しだけ寄り道させる。見つからなければ経路の引き直しだけにする。
        /// 目的地へ <see cref="StuckProgressDistance"/> 以上近づく地点は選ばない。選ぶと、寄り道しただけで
        /// <see cref="StuckMonitor"/> が立ち直ったとみなし、届かない目的地でも段階が先へ進まなくなる。
        /// </summary>
        private void Sidestep(Vector3 ground, Vector3? goal)
        {
            Repath();
            if (!_agent.isOnNavMesh)
            {
                return;
            }

            var path = new NavMeshPath();
            for (var i = 0; i < SidestepAttempts; i++)
            {
                var offset = UnityEngine.Random.insideUnitCircle * SidestepRadius;
                var candidate = ground + new Vector3(offset.x, 0f, offset.y);
                if (!NavMesh.SamplePosition(candidate, out var hit, SidestepRadius, NavMesh.AllAreas)
                    || HorizontalDistance(ground, hit.position) < SidestepMinDistance
                    || (goal.HasValue && MovesTowardGoal(ground, hit.position, goal.Value))
                    || !_agent.CalculatePath(hit.position, path)
                    || path.status != NavMeshPathStatus.PathComplete)
                {
                    continue;
                }

                _detour = hit.position;
                _detourEndsAt = Time.time + SidestepSeconds;
                _agent.isStopped = false;
                _agent.SetPath(path);
                return;
            }
        }

        // StuckMonitor の寄り道中の判定 (基準点から目的地の方向への射影) と同じ基準にそろえる。
        private static bool MovesTowardGoal(Vector3 from, Vector3 to, Vector3 goal)
        {
            var toGoal = goal - from;
            toGoal.y = 0f;
            if (toGoal.sqrMagnitude <= 0f)
            {
                return false;
            }

            var move = to - from;
            move.y = 0f;
            return Vector3.Dot(move, toGoal.normalized) >= StuckProgressDistance;
        }

        /// <summary>
        /// 経路上で <see cref="WarpLookAhead"/> より先の角へ瞬間移動させる。そういう角が無く NavMesh からも外れていれば出現地点へ戻す。
        /// どちらでもなければ (目的地のすぐ手前で詰まっているだけなら) 動かさない。
        /// </summary>
        private void Warp(Vector3 ground)
        {
            Vector3? to = null;
            if (_agent.isOnNavMesh && _agent.hasPath)
            {
                foreach (var corner in _agent.path.corners)
                {
                    if (HorizontalDistance(ground, corner) > WarpLookAhead)
                    {
                        to = corner;
                        break;
                    }
                }
            }
            else if (!_agent.isOnNavMesh)
            {
                to = _spawnPosition;
            }

            Repath();
            if (!to.HasValue)
            {
                Debug.Log($"{LogTag} id={Id} warp skipped: no point ahead on the path", this);
                return;
            }

            if (!_agent.Warp(to.Value))
            {
                Debug.LogWarning($"{LogTag} id={Id} warp to {to.Value.ToString("F1")} failed", this);
                return;
            }

            // 詰まった地点で測った基準のままだと、目的地から遠い出現地点へ戻した後は普通に歩けていても立て直しが走り続ける。
            _stuck.Reset();
            Debug.Log($"{LogTag} id={Id} warped from={ground.ToString("F1")} to={to.Value.ToString("F1")}", this);
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            var delta = a - b;
            delta.y = 0f;
            return delta.magnitude;
        }

        private void Face(System.Numerics.Vector3? lookAt, float deltaSeconds)
        {
            if (!lookAt.HasValue)
            {
                // 向きの指定が無ければ、NavMeshAgent に進む向きを向かせる。
                _agent.updateRotation = true;
                return;
            }

            _agent.updateRotation = false;
            var direction = lookAt.Value.ToUnity() - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-6f)
            {
                return;
            }

            var goal = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, goal, _turnSpeed * deltaSeconds);
        }

        private void Shoot()
        {
            if (_target == null || _target.IsDead)
            {
                return;
            }

            if (!_weapon.TryFire())
            {
                if (_weapon.AmmoInMagazine == 0 && _weapon.TryStartReload())
                {
                    Debug.Log($"{LogTag} id={Id} reload", this);
                }

                return;
            }

            var eye = EyePosition;
            var direction = _target.AimPoint - eye;
            if (direction.sqrMagnitude < 1e-6f)
            {
                direction = transform.forward;
            }

            var spread = _brain.NextShotSpreadDegrees;
            direction = BlindFireShot.Spread(direction.ToNumerics(), spread, _random).ToUnity();
            _brain.RecordShot();

            var spec = _weapon.Spec;
            var result = Hitscan.Fire(eye, direction, spec.Range, spec.Damage, spec.Falloff, transform, weaponId: spec.Id);
            Debug.Log(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} id={1} fire ammo={2} spread={3:F1} hit={4} dmg={5}",
                    LogTag, Id, _weapon.AmmoInMagazine, spread, DescribeHit(result), result.Damage),
                this);
        }

        private static string DescribeHit(HitscanResult result)
        {
            if (!result.Hit)
            {
                return "none";
            }

            if (result.Hitbox != null && result.Hitbox.Owner != null)
            {
                return result.Hitbox.Owner.Id;
            }

            return result.RaycastHit.collider.name;
        }

        private void OnUnstuck(float stalledSeconds)
        {
            Debug.Log(string.Format(CultureInfo.InvariantCulture, "{0} id={1} unstuck after={2:F1}s", LogTag, Id, stalledSeconds), this);
        }

        private void OnStateChanged(EnemyState previous, EnemyState next)
        {
            Debug.Log(FormatStateLog(Id, previous, next), this);
        }

        private void OnReloaded()
        {
            Debug.Log($"{LogTag} id={Id} reloaded ammo={_weapon.AmmoInMagazine}", this);
        }

        private void OnDamaged(Damageable self, DamageReport report)
        {
            // 死んだ敵は反応しない (とどめの 1 発では Died の後にここへ来る)。
            if (_brain == null || _brain.IsDead || self.IsDead)
            {
                return;
            }

            // 撃った者が分からなくても、目標が見えていれば EnemyBrain はその位置から隠れられるので、撃たれたことは伝える。
            _pendingHit = true;
            if (!report.SourcePosition.HasValue)
            {
                Debug.Log($"{LogTag} id={Id} damaged dmg={report.Amount} from=unknown", this);
                return;
            }

            _pendingNoise = report.SourcePosition.Value;
            _pendingHitSource = report.SourcePosition.Value;
            Debug.Log($"{LogTag} id={Id} damaged dmg={report.Amount} from={report.SourcePosition.Value}", this);
        }

        private void OnDied(Damageable self)
        {
            _pendingNoise = null;
            _pendingHit = false;
            _pendingHitSource = null;
            SetCrouching(false);
            _brain.Kill();

            if (_agent.isOnNavMesh)
            {
                _agent.ResetPath();
                _agent.isStopped = true;
            }

            _destination = null;
            _detour = null;
            _goal = null;
            _stuck.Reset();
            _agent.enabled = false;
        }
    }
}
