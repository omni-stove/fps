using System;
using System.Collections.Generic;
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
    /// 状態が変わるたびに <c>[AI] id=.. state=..</c> をログに出す。
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
        [SerializeField, Min(1)]
        private int _magazineSize = 10;

        [SerializeField, Min(0)]
        private int _reserveAmmo = 990;

        [SerializeField, Min(0f)]
        private float _fireInterval = 0.4f;

        [SerializeField, Min(0f)]
        private float _reloadSeconds = 2f;

        [SerializeField, Min(0)]
        private int _damage = 10;

        [SerializeField, Min(0.01f)]
        private float _range = 40f;

        [SerializeField, Min(0f)]
        private float _falloffStart = 10f;

        [SerializeField, Min(0f)]
        private float _falloffEnd = 30f;

        [SerializeField, Range(0f, 1f)]
        private float _falloffMinMultiplier = 0.5f;

        private NavMeshAgent _agent;
        private Damageable _self;
        private Damageable _target;
        private EnemyBrain _brain;
        private Weapon _weapon;
        private DamageFalloff _falloff;
        private Vector3? _destination;
        private float _nextTargetSearch;
        private bool _warnedNoTarget;
        private bool _warnedOffMesh;

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
        /// 部品を解決し、Inspector の値から <see cref="EnemyBrain"/> と <see cref="Weapon"/> を作る。
        /// 失敗したら理由を Debug.LogError に出して false を返す。2 回目以降は何もしない。
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

            EnemySpec spec;
            WeaponSpec weaponSpec;
            try
            {
                spec = new EnemySpec(_sightRange, _attackRange, _loseSightSeconds, _searchSeconds, _reactionSeconds);
                weaponSpec = new WeaponSpec(_magazineSize, _reserveAmmo, _fireInterval, _reloadSeconds, _damage);
                _falloff = new DamageFalloff(_falloffStart, _falloffEnd, _falloffMinMultiplier);
                if (!(_range > 0f) || float.IsInfinity(_range))
                {
                    throw new ArgumentOutOfRangeException(nameof(_range), _range, "range must be positive and finite");
                }
            }
            catch (ArgumentException e)
            {
                Debug.LogError($"{LogTag} id={Id} の設定が不正です: {e.Message}", this);
                return false;
            }

            _brain = new EnemyBrain(spec);
            _brain.StateChanged += OnStateChanged;
            _weapon = new Weapon(weaponSpec);
            _weapon.Reloaded += OnReloaded;
            _self.Died += OnDied;
            return true;
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

            if (_weapon != null)
            {
                _weapon.Reloaded -= OnReloaded;
            }

            if (_self != null)
            {
                _self.Died -= OnDied;
            }
        }

        private void Update()
        {
            if (_brain.IsDead)
            {
                return;
            }

            var deltaSeconds = Time.deltaTime;
            _weapon.Tick(deltaSeconds);

            if (_target == null)
            {
                ResolveTarget(force: false);
            }

            var perception = EnemySenses.Perceive(GroundPosition, EyePosition, _target, _sightRange, transform);
            var decision = _brain.Tick(deltaSeconds, perception);
            if (_brain.IsDead)
            {
                return;
            }

            MoveTo(decision.MoveTo);
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
            if (!_agent.isOnNavMesh)
            {
                if (!_warnedOffMesh)
                {
                    Debug.LogError($"{LogTag} id={Id} が NavMesh の上にいないので移動できません。", this);
                    _warnedOffMesh = true;
                }

                return;
            }

            if (!moveTo.HasValue)
            {
                if (_destination.HasValue)
                {
                    _agent.ResetPath();
                    _destination = null;
                }

                return;
            }

            var destination = moveTo.Value.ToUnity();
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

            var result = Hitscan.Fire(eye, direction, _range, _weapon.Spec.Damage, _falloff, transform);
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

        private void OnStateChanged(EnemyState previous, EnemyState next)
        {
            Debug.Log(FormatStateLog(Id, previous, next), this);
        }

        private void OnReloaded()
        {
            Debug.Log($"{LogTag} id={Id} reloaded ammo={_weapon.AmmoInMagazine}", this);
        }

        private void OnDied(Damageable self)
        {
            _brain.Kill();

            if (_agent.isOnNavMesh)
            {
                _agent.ResetPath();
                _agent.isStopped = true;
            }

            _destination = null;
            _agent.enabled = false;
        }
    }
}
