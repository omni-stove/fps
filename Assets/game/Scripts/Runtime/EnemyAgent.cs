using System;
using System.Collections.Generic;
using System.Globalization;
using Shooter.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Shooter.Runtime
{
    // 撃たれたら撃った者の位置を次の知覚 1 回だけ物音として渡すのは、視界の外から撃たれても EnemyBrain がその位置を Search するようにするため。
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Damageable))]
    public sealed partial class EnemyAgent : MonoBehaviour, ICharacterMotionSource, IWeaponHolder
    {
        public const string LogTag = "[AI]";

        public const string TargetTag = "Player";

        // キットを使うゲームがこの名前で読んでいても壊れないよう、EnemySpec に移した後も別名として残す。
        [Obsolete("Use EnemySpec.DefaultSightRange")]
        public const float DefaultSightRange = EnemySpec.DefaultSightRange;

        [Obsolete("Use EnemySpec.DefaultAttackRange")]
        public const float DefaultAttackRange = EnemySpec.DefaultAttackRange;

        [Obsolete("Use EnemySpec.DefaultLoseSightSeconds")]
        public const float DefaultLoseSightSeconds = EnemySpec.DefaultLoseSightSeconds;

        [Obsolete("Use EnemySpec.DefaultSearchSeconds")]
        public const float DefaultSearchSeconds = EnemySpec.DefaultSearchSeconds;

        [Obsolete("Use EnemySpec.DefaultReactionSeconds")]
        public const float DefaultReactionSeconds = EnemySpec.DefaultReactionSeconds;

        [Obsolete("Use EnemySpec.DefaultOpeningSpreadDegrees")]
        public const float DefaultOpeningSpreadDegrees = EnemySpec.DefaultOpeningSpreadDegrees;

        [Obsolete("Use EnemySpec.DefaultSettledSpreadDegrees")]
        public const float DefaultSettledSpreadDegrees = EnemySpec.DefaultSettledSpreadDegrees;

        [Obsolete("Use EnemySpec.DefaultOpeningShots")]
        public const int DefaultOpeningShots = EnemySpec.DefaultOpeningShots;

        public const float DefaultEyeHeight = 1.6f;

        public const float DefaultTurnSpeed = 360f;

        // 低い壁 (MotorSpec.DefaultCoverLowWallHeight 未満) に隠れる高さにする。
        public const float DefaultCrouchHeight = 1f;

        public const float DefaultCrouchEyeHeight = 0.8f;

        public const float RouteSnapRadius = 2f;

        // 毎フレームの経路の再計算を避けるため、目的地がこれ以上動いたときだけ引き直す。
        public const float RepathDistance = 0.25f;

        public const float TargetSearchInterval = 1f;

        public const float StuckSeconds = 2f;

        public const float StuckProgressDistance = 0.5f;

        public const float StuckGoalChangeDistance = 2f;

        public const float SidestepRadius = 2f;

        public const float SidestepMinDistance = 0.75f;

        public const int SidestepAttempts = 6;

        public const float SidestepSeconds = 1.5f;

        public const float WarpLookAhead = 1.5f;

        public const float OffMeshSnapRadius = 3f;

        public const float RecoilControl = EnemySpec.DefaultRecoilControl;

        public const float OffMeshRetryInterval = 1f;

        [SerializeField, Tooltip("ログに出す識別子。空なら Damageable の Id")]
        private string _id = string.Empty;

        [SerializeField, Tooltip("巡回地点 (ワールド座標の足元)。空なら Idle")]
        private Vector3[] _patrolRoute = Array.Empty<Vector3>();

        [Header("Brain")]
        [SerializeField, Tooltip("敵のデータ (視界・反応・散らばり・遮蔽・種類ごとの値)。設定すると、この下の Brain・Body・Cover の値と種類ごとの値の代わりにこれを使う。空なら入口 (GameContent) の同じ種類のデータ、それも無ければこの下の値を使う")]
        private EnemyDefinition _definition;

        [SerializeField, Tooltip("敵の種類")]
        private EnemyArchetype _archetype = EnemyArchetype.Rifleman;

        [SerializeField, Min(0.01f)]
        private float _sightRange = EnemySpec.DefaultSightRange;

        [SerializeField, Min(0.01f)]
        private float _attackRange = EnemySpec.DefaultAttackRange;

        [SerializeField, Min(0f), Tooltip("この距離 (m) 以内に目標が見えていれば、Attack の外でも近づきながら撃つ")]
        private float _fireRange = EnemySpec.DefaultFireRange;

        [SerializeField, Range(0f, EnemySpec.MaxFireToleranceDegrees), Tooltip("体の向きと目標の向きの差 (度) がこれ以下になるまで撃たない")]
        private float _fireToleranceDegrees = EnemySpec.DefaultFireToleranceDegrees;

        [SerializeField, Min(0f)]
        private float _loseSightSeconds = EnemySpec.DefaultLoseSightSeconds;

        [SerializeField, Min(0f)]
        private float _searchSeconds = EnemySpec.DefaultSearchSeconds;

        [SerializeField, Min(0f)]
        private float _reactionSeconds = EnemySpec.DefaultReactionSeconds;

        [SerializeField, Range(0f, EnemySpec.MaxSpreadDegrees), Tooltip("Attack に入って最初の 1 発の散らばりの半角 (度)")]
        private float _openingSpreadDegrees = EnemySpec.DefaultOpeningSpreadDegrees;

        [SerializeField, Range(0f, EnemySpec.MaxSpreadDegrees), Tooltip("撃ち始めの数発が過ぎた後の散らばりの半角 (度)")]
        private float _settledSpreadDegrees = EnemySpec.DefaultSettledSpreadDegrees;

        [SerializeField, Range(0f, EnemySpec.MaxSpreadDegrees), Tooltip("撃ち始める距離にいる目標を撃つときに散らばりへ足す半角 (度)。Attack の距離では 0")]
        private float _rangeSpreadDegrees = EnemySpec.DefaultRangeSpreadDegrees;

        [SerializeField, Min(0), Tooltip("散らばりを絞り切るまでの発数")]
        private int _openingShots = EnemySpec.DefaultOpeningShots;

        [SerializeField, Min(EnemyDefinition.ArchetypeStaggerResistance), Tooltip("怯みにくさ。0 で既定、1 で怯むのに 2 倍のダメージが要り、怯む長さは半分になる。負なら種類ごとの既定値")]
        private float _staggerResistance = EnemyDefinition.ArchetypeStaggerResistance;

        // ApplySpec・ChangeSpec で渡された値は、波の強さやボスの段階のように敵のデータを元に作り直したものなので、データより優先する。
        private bool _specApplied;

        // Inspector に並べず敵のデータ (EnemyDefinition) にだけ置く値。ApplySpec・ChangeSpec の後の CreateSpec で落とさないよう、渡された値を覚えておく。
        private float _arrivalDistance = EnemySpec.DefaultArrivalDistance;
        private float _sprintRatio = EnemySpec.DefaultSprintRatio;
        private float _crouchRatio = EnemySpec.DefaultCrouchRatio;
        private float _recoilControl = EnemySpec.DefaultRecoilControl;

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
        [SerializeField, RequiredReference, Tooltip("撃つ武器の定義 (弾数・連射間隔・ダメージ・射程・距離減衰)。シーン生成スクリプトが配置ごとに武器の一覧から引いて配線する")]
        private WeaponDefinition _weaponDefinition;

        private NavMeshAgent _agent;
        private Damageable _self;
        private EnemyBrain _brain;
        private CapsuleCollider _capsule;
        private float _standingCapsuleHeight;
        private Vector3 _standingCapsuleCenter;
        private bool _crouching;
        private int _loggedCoverIndex = -1;
        private EnemyCoverPhase _loggedCoverPhase = EnemyCoverPhase.None;
        private Vector3? _errand;
        private bool _errandOverridesCombat;
        private float _walkSpeed;
        private float _moveSpeedMultiplier = 1f;
        private bool _sprinting;
        private bool _aiming;

        public float WalkSpeed
        {
            get
            {
                if (_brain != null)
                {
                    return _walkSpeed;
                }

                var agent = _agent != null ? _agent : GetComponent<NavMeshAgent>();
                return agent != null ? agent.speed : 0f;
            }
        }

        public float SprintSpeed => WalkSpeed * _sprintRatio;

        public float CrouchSpeed => WalkSpeed * _crouchRatio;

        public bool IsSprinting => _sprinting;
        public bool IsAiming => _aiming;

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

        public EnemyBrain Brain => _brain;

        public bool IsInitialized => _brain != null;

        public EnemyState State => _brain?.State ?? EnemyState.Idle;

        public EnemyArchetype Archetype => _archetype;

        public float StaggerResistance
        {
            get
            {
                if (_brain != null)
                {
                    return _brain.Spec.StaggerResistance;
                }

                var definition = EffectiveDefinition;
                if (definition != null && !_specApplied)
                {
                    return EnemyDefinition.StaggerResistanceOrArchetype(definition.StaggerResistance)
                        ?? EnemySpec.DefaultStaggerResistanceFor(definition.Archetype);
                }

                return EnemyDefinition.StaggerResistanceOrArchetype(_staggerResistance) ?? EnemySpec.DefaultStaggerResistanceFor(_archetype);
            }
        }

        public Vector3? Errand => _errand;

        public bool ErrandOverridesCombat => _errandOverridesCombat;

        public void SetErrand(Vector3 destination, bool overridesCombat)
        {
            _errand = destination;
            _errandOverridesCombat = overridesCombat;
        }

        public void ClearErrand()
        {
            _errand = null;
            _errandOverridesCombat = false;
        }

        public IReadOnlyList<Vector3> PatrolRoute => _patrolRoute;

        public float SightRange => _sightRange;
        public float AttackRange => _attackRange;
        public float FireRange => _fireRange;
        public float EyeHeight => _eyeHeight;

        public float TurnSpeed => _turnSpeed;
        public float CrouchHeight => _crouchHeight;
        public float CrouchEyeHeight => _crouchEyeHeight;

        public CoverPointRegistry CoverRegistry
        {
            get => _coverRegistry;
            set => _coverRegistry = value;
        }

        public bool IsCrouching => _crouching;

        public Vector3 GroundPosition
        {
            get
            {
                // 初期化の前 (Edit Mode で置いた直後など) でも、付いている NavMeshAgent の baseOffset で足元を返す。
                var navAgent = _agent != null ? _agent : GetComponent<NavMeshAgent>();
                var offset = navAgent != null ? navAgent.baseOffset : 0f;
                return transform.position - (Vector3.up * offset);
            }
        }

        public Vector3 EyePosition => GroundPosition + (Vector3.up * (_crouching ? Mathf.Min(_crouchEyeHeight, _eyeHeight) : _eyeHeight));

        bool ICharacterMotionSource.IsGrounded => _agent != null && _agent.enabled && _agent.isOnNavMesh && !_agent.isOnOffMeshLink;

        bool ICharacterMotionSource.IsCrouched => _crouching;

        float ICharacterMotionSource.WalkSpeed => WalkSpeed;

        bool ICharacterMotionSource.TryGetSprinting(out bool sprinting)
        {
            sprinting = _sprinting;
            return true;
        }

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
                spec = CreateSpec();
            }
            catch (ArgumentException e)
            {
                Debug.LogError($"{LogTag} id={Id} の設定が不正です: {e.Message}", this);
                return false;
            }

            // 知覚 (視界の距離) や SightRange などはフィールドを読むので、敵のデータから作った値もフィールドに揃える。
            CopySpec(spec);
            CopyBody(EffectiveDefinition);

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

            // 歩きの速さはシーン生成スクリプトが NavMeshAgent に書いた値。以後 agent.speed は足の運びで毎フレーム書き換える。
            _walkSpeed = _agent.speed;
            _agent.autoTraverseOffMeshLink = false;
            _spreadSeed = _random.Next();
            _stuck = new StuckMonitor(StuckSeconds, StuckProgressDistance, StuckGoalChangeDistance);
            _stuck.Recovered += OnUnstuck;
            _brain = new EnemyBrain(spec);
            _brain.StateChanged += OnStateChanged;
            _weapon = new Weapon(weaponSpec, _weaponRules);
            _weapon.Reloaded += OnReloaded;
            _self.Died += OnDied;
            _self.Damaged += OnDamaged;
            NotifyInitialized();
            return true;
        }

        // 静的にするのは、波で後から出る敵にも、ルールで決まる部品 (EnemySuppression など) を出てくる前から付けられるようにするため。
        public static event Action<EnemyAgent> Initialized;

        // 受け取り側が投げても、敵の初期化は失敗にしない。
        private void NotifyInitialized()
        {
            var handlers = Initialized;
            if (handlers == null)
            {
                return;
            }

            foreach (var handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<EnemyAgent>)handler)(this);
                }
                catch (Exception e)
                {
                    Debug.LogException(e, this);
                }
            }
        }

        // ドメインの再読み込みを切った Play で、前の Play の購読が残らないようにする。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetInitialized()
        {
            Initialized = null;
        }

        public EnemySpec CreateSpec()
        {
            var definition = EffectiveDefinition;
            if (definition != null && !_specApplied)
            {
                return definition.ToSpec();
            }

            return new EnemySpec(
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
                _coverHealthFraction,
                _archetype,
                CreateShieldSpec(),
                CreateSniperSpec(),
                CreateChargerSpec(),
                CreateBomberSpec(),
                EnemyDefinition.StaggerResistanceOrArchetype(_staggerResistance),
                _fireRange,
                _fireToleranceDegrees,
                _rangeSpreadDegrees,
                _arrivalDistance,
                _sprintRatio,
                _crouchRatio,
                _recoilControl);
        }

        public void ApplySpec(EnemySpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (_brain != null)
            {
                throw new InvalidOperationException($"EnemyAgent '{name}' is already initialized; apply the spec before activating it");
            }

            CopySpec(spec);
            _specApplied = true;
        }

        // 初期化後の差し替えを拒むのは、判断はもう作られていて、差し替えても何も変わらないことに気付けるようにするため。
        public EnemyDefinition Definition
        {
            get => _definition;
            set
            {
                if (_brain != null)
                {
                    throw new InvalidOperationException($"EnemyAgent '{name}' is already initialized; set the definition before activating it");
                }

                _definition = value;
                _specApplied = false;
            }
        }

        public EnemyDefinition EffectiveDefinition => GameContent.Pick(_definition, c => c.EnemyFor(_archetype));

        // ApplySpec は初期化後を拒むので、ボスの段階のように戦いの途中で変える口を別に設ける。
        // 波の強さ (ApplySpec) は出す前に決まるもので、途中で変わったら呼び出し側の誤りとして気付けるよう、そちらの決まりは残す。
        public void ChangeSpec(EnemySpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            CopySpec(spec);
            _specApplied = true;
            _brain?.ChangeSpec(spec);
        }

        // 1 未満にもできる。ボスの段階で遅くする使い方を塞がないように。
        public float MoveSpeedMultiplier
        {
            get => _moveSpeedMultiplier;
            set
            {
                if (!(value > 0f) || float.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "MoveSpeedMultiplier must be positive and finite");
                }

                _moveSpeedMultiplier = value;
            }
        }

        private void CopySpec(EnemySpec spec)
        {
            _sightRange = spec.SightRange;
            _attackRange = spec.AttackRange;
            _fireRange = spec.FireRange;
            _fireToleranceDegrees = spec.FireToleranceDegrees;
            _rangeSpreadDegrees = spec.RangeSpreadDegrees;
            _loseSightSeconds = spec.LoseSightSeconds;
            _searchSeconds = spec.SearchSeconds;
            _reactionSeconds = spec.ReactionSeconds;
            _openingSpreadDegrees = spec.OpeningSpreadDegrees;
            _settledSpreadDegrees = spec.SettledSpreadDegrees;
            _openingShots = spec.OpeningShots;
            _coverHideSeconds = spec.CoverHideSeconds;
            _coverPeekSeconds = spec.CoverPeekSeconds;
            _coverHealthFraction = spec.CoverHealthFraction;
            _archetype = spec.Archetype;
            CopyArchetypeSpecs(spec);
            _staggerResistance = spec.StaggerResistance;
            _arrivalDistance = spec.ArrivalDistance;
            _sprintRatio = spec.SprintRatio;
            _crouchRatio = spec.CrouchRatio;
            _recoilControl = spec.RecoilControl;
        }

        // 体の値は EnemySpec に無く ApplySpec・ChangeSpec でも変わらないので、波の強さやボスの段階を渡した後も敵のデータから読む。
        private void CopyBody(EnemyDefinition definition)
        {
            if (definition == null)
            {
                return;
            }

            _turnSpeed = definition.TurnSpeed;
            _eyeHeight = definition.EyeHeight;
            _crouchHeight = definition.CrouchHeight;
            _crouchEyeHeight = definition.CrouchEyeHeight;
        }

        public static string FormatCoverLog(string id, EnemyCoverPhase phase, string point)
        {
            return $"{LogTag} id={id} cover phase={phase} point={(string.IsNullOrEmpty(point) ? "none" : point)}";
        }

        public static string FormatStuckLog(
            string id, StuckRecovery recovery, EnemyState state, float stalledSeconds, Vector3 position, Vector3? goal, string path)
        {
            var goalText = goal.HasValue ? goal.Value.ToString("F1") : "none";
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} id={1} stuck recovery={2} state={3} stalled={4:F1}s pos={5} goal={6} path={7}",
                LogTag, id, recovery, state, stalledSeconds, position.ToString("F1"), goalText, path);
        }

        public static string FormatStateLog(string id, EnemyState previous, EnemyState next)
        {
            return $"{LogTag} id={id} state={next} prev={previous}";
        }

        private void Awake()
        {
            // 無効なまま置かれた敵は Awake が有効になるまで遅れるので、その間にコードが渡した値を JSON で消さない。
            if (Application.isPlaying)
            {
                ApplyKnockbackTuning(KitTuning.Knockback);
            }

            if (!TryInitialize())
            {
                enabled = false;
            }
        }

        private void Start()
        {
            Debug.Log($"{LogTag} id={Id} state={_brain.State}", this);

            // GameBootstrap は Awake で組み立てるので、Start まで待ってからプレイヤーと同じルールの部位倍率を読む。
            _self.ApplySceneRules();
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

                // 置かれたタレットの近くで有効になった敵が、選び直しの間隔が過ぎるまで遠いプレイヤーを狙わないように。
                if (Turret.Armed.Count > 0)
                {
                    ResolveTurretTarget(force: true);
                }
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

            GiveUpWheelOnDestroy();
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
            _trigger.Bind(_weapon.Trigger);
            _trigger.Advance(deltaSeconds);
            _recoil.Tick(deltaSeconds);

            var member = GetComponent<SideMember>();
            StandDownIfTargetFell();

            // 狙う指示の敵を目標にしている間は、陣営やタグで選び直すと指示の敵から外れてしまう。
            var pinned = TickOrder();
            if (!pinned && member != null)
            {
                // 陣営で狙う相手は動き回り倒れもするので、見つかっていても間隔ごとに選び直す。倒れたらすぐ次を探す。
                ResolveHostile(member, force: _target != null && _target.IsDead);
            }
            else if (!pinned && _target == null)
            {
                ResolveTarget(force: false);
            }
            else if (!pinned && (Turret.Armed.Count > 0 || _target.GetComponent<Turret>() != null))
            {
                // タレットが無くなった後も、狙っていたのがタレットならタグの相手へ戻す。
                ResolveTurretTarget(force: _target.IsDead);
            }

            // 乗っている間は NavMeshAgent を止めているので、止める処理 (MoveTo) を通る一時停止より先に見る。
            if (HoldWhileRiding())
            {
                return;
            }

            if (TickKnockback(deltaSeconds) || TraverseOffMeshLink(deltaSeconds))
            {
                return;
            }

            if (HoldWhileInterrupted(deltaSeconds))
            {
                return;
            }

            // 同じ selector なら何もしないので毎フレーム渡し、registry の差し替えや作り直しに追いつかせる。
            // 守る指示の間は遮蔽点へ移らせない。移る途中は撃たないので、守る場所で撃たれても反撃しなくなる。
            var holding = _order.Kind == BotErrandKind.Hold;
            _brain.UseCover(_coverRegistry != null && !holding ? _coverRegistry.Selector : null);

            var perception = Perceive();
            var decision = _brain.Tick(deltaSeconds, perception);
            if (_brain.IsDead)
            {
                return;
            }

            LogCoverChange();
            System.Numerics.Vector3? moveTo;
            bool steered;
            if (_order.IsOrder)
            {
                // NavMesh で動く敵は bots.json を読まず、着いたかを NoteOrderArrival と同じ敵の設定の到着距離で決める。
                decision = BotDriver.FollowOrder(
                    decision, _brain.State, _order, GroundPosition.ToNumerics(), _orderSpec, OrderInput(), out steered);
                moveTo = steered ? decision.MoveTo : CoverMoveTo(decision);
                NoteOrderArrival();
            }
            else
            {
                var errand = ErrandMoveTo();
                steered = errand.HasValue;
                moveTo = errand ?? CoverMoveTo(decision);
            }

            SetCrouching(!steered && decision.Crouch && _brain.CurrentCoverPoint?.Height == CoverHeight.Low);
            var firing = decision.Fire && !_unarmed;
            UpdateGait(firing);
            MoveTo(moveTo);
            WatchProgress(deltaSeconds);
            Face(decision.LookAt, deltaSeconds);
            LogAimStart();
            if (firing && IsFacing(decision.LookAt))
            {
                Shoot();
            }
            else
            {
                ReleaseTrigger();
                TopUpMagazine();
            }
        }

        private void UpdateGait(bool firing)
        {
            var phase = _brain.CoverPhase;
            _aiming = !_unarmed && _brain.IsAiming;
            // 構えている間はプレイヤーと同じくダッシュしない。撃つ前から歩いて狙ってくるのが見えるように。
            _sprinting = _sprintAllowed && EnemyGait.IsSprinting(_brain.State, phase, _crouching, firing || _aiming);
            _agent.speed = EnemyGait.Speed(GaitWalkSpeed, _sprinting, _crouching, _sprintRatio, _crouchRatio);
        }

        // 着いていれば null にせず今の足元を返すのは、判断の移動先へ戻らずその場に留まらせるため。
        private System.Numerics.Vector3? ErrandMoveTo()
        {
            if (!_errand.HasValue)
            {
                return null;
            }

            var state = _brain.State;
            var engaged = state == EnemyState.Chase || state == EnemyState.Attack || state == EnemyState.TakeCover;
            if (engaged && !_errandOverridesCombat && !_unarmed)
            {
                return null;
            }

            var destination = _errand.Value;
            return HorizontalDistance(GroundPosition, destination) > _arrivalDistance
                ? destination.ToNumerics()
                : GroundPosition.ToNumerics();
        }

        // 遮蔽点に着いた後の TakeCover では EnemyBrain は移動先を出さないので、覗くときは覗く位置へ、
        // 隠れるときは点へ戻る移動先をここで足す。
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

            return HorizontalDistance(GroundPosition, stand) > _arrivalDistance ? stand.ToNumerics() : (System.Numerics.Vector3?)null;
        }

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

            PlaceAndStartOver(groundPosition, rotation, "respawned");
            return true;
        }

        public void ResetTo(Vector3 groundPosition, Quaternion rotation)
        {
            if (_brain == null)
            {
                throw new InvalidOperationException($"EnemyAgent '{name}' is not initialized");
            }

            PlaceAndStartOver(groundPosition, rotation, "reset");
        }

        private void PlaceAndStartOver(Vector3 groundPosition, Quaternion rotation, string logVerb)
        {
            var ground = NavMesh.SamplePosition(groundPosition, out var hit, OffMeshSnapRadius, NavMesh.AllAreas)
                ? hit.position
                : groundPosition;

            LeaveVehicleForReset();
            ClearPendingPerception();
            _interruption.Clear();
            _flight.Clear();
            ClearOrder(logVerb);
            _loggedCoverIndex = -1;
            _loggedCoverPhase = EnemyCoverPhase.None;
            _destination = null;
            _detour = null;
            _goal = null;
            _stuck.Reset();
            ClearLinkTraversal();
            SetCrouching(false);

            _weapon.Reloaded -= OnReloaded;
            _weapon = new Weapon(_weapon.Spec, _weaponRules);
            _weapon.Reloaded += OnReloaded;
            _recoil.Reset();
            _sprinting = false;
            _aiming = false;
            _agent.speed = _walkSpeed * _moveSpeedMultiplier;

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
            _brain.Restart();
            _target = null;
            _perceivedTarget = null;
            _fallenTarget = null;
            _nextTargetSearch = 0f;
            Debug.Log($"{LogTag} id={Id} {logVerb} at={ground.ToString("F1")} state={_brain.State}", this);
        }

        private void OnUnstuck(float stalledSeconds)
        {
            Debug.Log(string.Format(CultureInfo.InvariantCulture, "{0} id={1} unstuck after={2:F1}s", LogTag, Id, stalledSeconds), this);
        }

        private void OnStateChanged(EnemyState previous, EnemyState next)
        {
            Debug.Log(FormatStateLog(Id, previous, next), this);
        }

        private void OnDied(Damageable self)
        {
            ClearOrder("Died");
            ClearPendingPerception();
            _interruption.Clear();
            _flight.Clear();
            SetCrouching(false);
            _sprinting = false;
            _aiming = false;
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
            ClearLinkTraversal();
            _agent.enabled = false;
            LeaveVehicleOnDeath();
        }
    }
}
