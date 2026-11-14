using Fps.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Fps.Runtime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyAgent))]
    // EnemyAgent の Update で判断 (ShieldEnemyBehavior.WantsBash) が決まった後に、同じフレームで殴り始める。
    [DefaultExecutionOrder(10)]
    public sealed class EnemyShield : MonoBehaviour
    {
        public const string InterruptCause = "interrupt";

        [SerializeField, Tooltip("弾を止める盾。シーン生成スクリプトが配線する。空なら実行時に仮の板を作る")]
        private BulletShield _plate;

        [SerializeField, Tooltip("殴りの当たりを探すレイヤー")]
        private LayerMask _hitMask = Physics.DefaultRaycastLayers;

        private readonly ShieldGuard _guard = new ShieldGuard();
        private EnemyAgent _agent;
        private NavMeshAgent _navAgent;
        private Damageable _self;
        private MeleeSwing _bash;
        private ShieldEnemyBehavior _behavior;
        private EnemyBrain _attachedBrain;
        private bool _active;

        public BulletShield Plate
        {
            get => _plate;
            set => _plate = value;
        }

        public ShieldGuard Guard => _guard;

        public MeleeSwing Bash => EnsureBash();

        public ShieldEnemyBehavior Behavior => EnsureBehavior();

        public IMeleePresenter Presenter { get; set; }

        public LayerMask HitMask
        {
            get => _hitMask;
            set => _hitMask = value;
        }

        public bool IsActive => _active;

        public bool IsAttached => _attachedBrain != null;

        private string Id => _agent != null ? _agent.Id : name;

        private void Awake()
        {
            EnsureInitialized();
        }

        // Edit Mode のテストでは Awake が走らないので、Tick からも呼ぶ。
        private void EnsureInitialized()
        {
            if (_agent != null)
            {
                return;
            }

            _agent = GetComponent<EnemyAgent>();
            _navAgent = GetComponent<NavMeshAgent>();
            _self = GetComponent<Damageable>();
            if (_self != null)
            {
                _self.Damaged += OnDamaged;
            }

            EnsureBehavior();
            if (_plate == null)
            {
                _plate = BulletShield.CreatePlaceholder(transform);
            }
        }

        private void OnDisable()
        {
            _bash?.Cancel();
            Detach();
        }

        private void OnDestroy()
        {
            if (_self != null)
            {
                _self.Damaged -= OnDamaged;
            }

            Detach();
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        // 殴り始めたか当たりを判定したかを返す。EnemyAgent の判断 (ShieldEnemyBehavior) を読むので、その Update の後に呼ぶ。
        public bool Tick(float deltaSeconds)
        {
            EnsureInitialized();
            var bash = EnsureBash();
            if (!TryAttach())
            {
                bash.Tick(deltaSeconds);
                return false;
            }

            var spec = _attachedBrain.Spec;
            bash.Spec = spec.Shield.Bash;
            if (spec.Archetype != EnemyArchetype.Shield || _attachedBrain.IsDead)
            {
                Deactivate();
                bash.Tick(deltaSeconds);
                return false;
            }

            _active = true;

            // 怯みの間は判断が進まないので、ここで盾を下ろす。解けたら残りの時間で構え直す。
            if (_agent.IsInterrupted)
            {
                Lower(_agent.InterruptSecondsRemaining, InterruptCause);
            }

            if (_guard.Tick(deltaSeconds))
            {
                Debug.Log(ShieldLog.Raised(Id), this);
            }

            ApplyGuard(spec.Shield);
            if (!_guard.IsRaised || _agent.IsInterrupted)
            {
                bash.Cancel();
                bash.Tick(deltaSeconds);
                return false;
            }

            var started = false;
            if (_behavior.WantsBash && IsFacingTarget(bash.Spec) && bash.TryStart())
            {
                started = true;
                Debug.Log(ShieldLog.Bash(Id), this);
                ResolvePresenter()?.OnSwingStarted(gameObject, bash.CurrentSpec);
            }

            if (bash.IsLunging)
            {
                Lunge(bash.CurrentSpec, deltaSeconds);
            }

            var current = bash.CurrentSpec;
            if (bash.Tick(deltaSeconds))
            {
                Strike(current);
                return true;
            }

            return started;
        }

        // 構えていたのを下ろしたら true。
        public bool Lower(float seconds, string cause)
        {
            if (!_active || !_guard.Lower(seconds, cause))
            {
                return false;
            }

            Debug.Log(ShieldLog.Lowered(Id, cause, seconds), this);

            // 同じフレームの後の弾が盾に止められないよう、次の Update を待たずに当たり判定を切る。
            if (_attachedBrain != null)
            {
                ApplyGuard(_attachedBrain.Spec.Shield);
            }

            return true;
        }

        private void OnDamaged(Damageable self, DamageReport report)
        {
            if (!_active || _attachedBrain == null || self.IsDead)
            {
                return;
            }

            var seconds = _attachedBrain.Spec.Shield.LowerSecondsFor(report.Kind);
            if (seconds > 0f)
            {
                Lower(seconds, report.Kind.ToString().ToLowerInvariant());
            }
        }

        private void ApplyGuard(ShieldSpec spec)
        {
            if (_plate != null)
            {
                _plate.SetRaised(_guard.IsRaised);
            }

            _agent.SprintAllowed = false;
            _agent.GaitSpeedMultiplier = _guard.IsRaised ? spec.ApproachSpeedMultiplier : 1f;
        }

        // 倒れたら盾の当たり判定を切る。切らないと、倒れた敵の盾に撃つたび止めたログが出る。生き返れば次の Tick で構え直す。
        // 種類が Shield でない敵に付いていても弾を止めないよう、一度も構えていなくても下ろす。
        private void Deactivate()
        {
            if (_plate != null)
            {
                _plate.SetRaised(false);
            }

            if (!_active)
            {
                return;
            }

            _active = false;
            _guard.Reset();
            _bash?.Cancel();
            _agent.SprintAllowed = true;
            _agent.GaitSpeedMultiplier = 1f;
        }

        // 判断は目標の方へ向き直らせるだけなので、向き終える前に振って空振りしないよう、扇に入るまで待つ (EnemyMelee と同じ)。
        private bool IsFacingTarget(MeleeSpec spec)
        {
            var target = _agent.Target;
            if (target == null)
            {
                return false;
            }

            var self = transform.position;
            var point = target.GroundPoint;
            point.y = self.y;
            return MeleeJudge.IsWithinArc(self.ToNumerics(), transform.forward.ToNumerics(), spec.ArcDegrees, point.ToNumerics());
        }

        private void Lunge(MeleeSpec spec, float deltaSeconds)
        {
            var forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-6f)
            {
                return;
            }

            var step = forward.normalized * (spec.LungeSpeed * deltaSeconds);
            if (_navAgent != null && _navAgent.enabled && _navAgent.isOnNavMesh)
            {
                _navAgent.Move(step);
            }
        }

        // 武器で殴るのではなく盾で殴るので、帰属の武器は空にする (キルフィードに持っている銃を出さない)。
        private void Strike(MeleeSpec spec)
        {
            var presenter = ResolvePresenter();
            if (!MeleeStrike.TryFind(transform, transform.position, transform.forward, spec, _hitMask, out var hit))
            {
                Debug.Log(MeleeLog.Miss(Id), this);
                presenter?.OnMissed(gameObject);
                return;
            }

            Debug.Log(MeleeLog.Hit(Id, hit.Target.Id, spec.Damage, hit.Distance), this);
            var damage = MeleeStrike.Apply(gameObject, hit, spec.Damage, string.Empty);
            presenter?.OnHit(gameObject, hit.Target, hit.Point, hit.Direction, damage);
        }

        // EnemyAgent は Awake で判断を作るが、同じ GameObject の Awake の順は決まらないので、使うときに付ける。
        private bool TryAttach()
        {
            var brain = _agent != null ? _agent.Brain : null;
            if (brain == null)
            {
                return false;
            }

            if (ReferenceEquals(brain, _attachedBrain))
            {
                return true;
            }

            Detach();
            brain.AddBehavior(EnsureBehavior());
            _attachedBrain = brain;
            return true;
        }

        private void Detach()
        {
            if (_attachedBrain == null)
            {
                return;
            }

            _attachedBrain.RemoveBehavior(_behavior);
            _attachedBrain = null;
        }

        private IMeleePresenter ResolvePresenter()
        {
            return Presenter ?? GetComponent<IMeleePresenter>();
        }

        private ShieldEnemyBehavior EnsureBehavior()
        {
            if (_behavior == null)
            {
                _behavior = new ShieldEnemyBehavior(_guard, EnsureBash());
            }

            return _behavior;
        }

        private MeleeSwing EnsureBash()
        {
            if (_bash == null)
            {
                var brain = _agent != null ? _agent.Brain : null;
                _bash = new MeleeSwing(brain != null ? brain.Spec.Shield.Bash : ShieldSpec.DefaultBash);
            }

            return _bash;
        }
    }
}
