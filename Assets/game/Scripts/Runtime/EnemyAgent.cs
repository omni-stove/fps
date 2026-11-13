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
    /// 武器は 1 本だけ持ち、持ち替えない。その値は <see cref="WeaponDefinition"/> アセットから作り、このコンポーネントには持たない
    /// (シーン生成スクリプトが、敵の配置ごとに指定した Id の武器を武器の一覧 <see cref="WeaponCatalogAsset"/> から引いて配線する)。
    /// 状態が変わるたびに <c>[AI] id=.. state=..</c> をログに出す。
    /// 撃たれたら (<see cref="Damageable.Damaged"/>) 撃った者の位置を次の知覚 1 回だけ物音として渡し、
    /// 視界の外から撃たれても <see cref="EnemyBrain"/> がその位置を Search するようにする。
    /// 動こうとしているのに進めなければ (<see cref="StuckMonitor"/>)、経路の引き直し → 寄り道 → 目的地をあきらめる →
    /// 瞬間移動の順に立て直し、<c>[AI] id=.. stuck recovery=..</c> をログに出す。NavMesh から外れたら近くの NavMesh に戻し
    /// (近くに無ければ瞬間移動の段階で出現地点へ戻し)、位置が NaN などになったら最後に正常だった位置に戻す。判断の良し悪しはここでは直さない (<see cref="EnemyBrain"/> 側で調整する)。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Damageable))]
    public sealed class EnemyAgent : MonoBehaviour
    {
        public const string LogTag = "[AI]";

        /// <summary>目標を探すタグ。<see cref="GameBootstrap"/> がプレイヤーに付ける。</summary>
        public const string TargetTag = "Player";

        public const float DefaultSightRange = 20f;
        public const float DefaultAttackRange = 12f;
        public const float DefaultLoseSightSeconds = 2f;
        public const float DefaultSearchSeconds = 6f;
        public const float DefaultReactionSeconds = 0.6f;

        /// <summary>足元からの目の高さ (メートル)。</summary>
        public const float DefaultEyeHeight = 1.6f;

        /// <summary>LookAt に向き直る速さ (度/秒)。</summary>
        public const float DefaultTurnSpeed = 360f;

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

        [Header("Body")]
        [SerializeField, Min(0f)]
        private float _eyeHeight = DefaultEyeHeight;

        [SerializeField, Min(0f)]
        private float _turnSpeed = DefaultTurnSpeed;

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

        /// <summary>今の目標。null なら次の Update で <see cref="TargetTag"/> から探す。</summary>
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

        /// <summary>足元の位置。NavMeshAgent の baseOffset を引いた位置。</summary>
        public Vector3 GroundPosition
        {
            get
            {
                var offset = _agent != null ? _agent.baseOffset : 0f;
                return transform.position - (Vector3.up * offset);
            }
        }

        /// <summary>視線と射撃の始点。</summary>
        public Vector3 EyePosition => GroundPosition + (Vector3.up * _eyeHeight);

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
                spec = new EnemySpec(_sightRange, _attackRange, _loseSightSeconds, _searchSeconds, _reactionSeconds);
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
        /// 今の位置・目標・視線と、前の知覚の後に撃たれていれば撃った者の位置 (物音) から、1 tick 分の知覚を組み立てる。
        /// 撃たれた情報はここで使い切るので、次の呼び出しでは物音は無い。未初期化なら <see cref="InvalidOperationException"/>。
        /// </summary>
        public EnemyPerception Perceive()
        {
            if (_brain == null)
            {
                throw new InvalidOperationException($"EnemyAgent '{name}' is not initialized");
            }

            var noise = _pendingNoise;
            _pendingNoise = null;
            return EnemySenses.Perceive(GroundPosition, EyePosition, _target, _sightRange, transform, noise);
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
            ResolveTarget(force: true);

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

            if (_target == null)
            {
                ResolveTarget(force: false);
            }

            var perception = Perceive();
            var decision = _brain.Tick(deltaSeconds, perception);
            if (_brain.IsDead)
            {
                return;
            }

            MoveTo(decision.MoveTo);
            WatchProgress(deltaSeconds);
            Face(decision.LookAt, deltaSeconds);
            if (decision.Fire)
            {
                Shoot();
            }
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
            var recovery = _stuck.Tick(deltaSeconds, ground.ToNumerics(), heading.HasValue ? heading.Value.ToNumerics() : (System.Numerics.Vector3?)null, PathRemainingDistance(heading));
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

        /// <summary>今の経路の角を辿った距離に、最後の角から目的地までの直線を足したもの。寄り道中は寄り道先が終点でも目的地までで測れる。経路が無ければ null。</summary>
        private float? PathRemainingDistance(Vector3? goal)
        {
            if (!goal.HasValue || !_agent.isOnNavMesh || !_agent.hasPath || _agent.pathPending)
            {
                return null;
            }

            var corners = _agent.path.corners;
            if (corners.Length == 0)
            {
                return null;
            }

            var total = Vector3.Distance(GroundPosition, corners[0]);
            for (var i = 1; i < corners.Length; i++)
            {
                total += Vector3.Distance(corners[i - 1], corners[i]);
            }

            return total + Vector3.Distance(corners[corners.Length - 1], goal.Value);
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
                    || (goal.HasValue && Vector3.Distance(hit.position, goal.Value) <= Vector3.Distance(ground, goal.Value) - StuckProgressDistance)
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

            var spec = _weapon.Spec;
            var result = Hitscan.Fire(eye, direction, spec.Range, spec.Damage, spec.Falloff, transform);
            Debug.Log($"{LogTag} id={Id} fire ammo={_weapon.AmmoInMagazine} hit={DescribeHit(result)} dmg={result.Damage}", this);
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
            // 死んだ敵は反応しない (とどめの 1 発では Died の後にここへ来る)。撃った者が分からなければ向かう先も無い。
            if (_brain == null || _brain.IsDead || self.IsDead || !report.SourcePosition.HasValue)
            {
                return;
            }

            _pendingNoise = report.SourcePosition.Value;
            Debug.Log($"{LogTag} id={Id} damaged dmg={report.Amount} from={report.SourcePosition.Value}", this);
        }

        private void OnDied(Damageable self)
        {
            _pendingNoise = null;
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
