using System;
using Fps.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Fps.Runtime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyAgent))]
    // EnemyAgent の Update で判断 (MeleeEnemyBehavior.WantsSwing) が決まった後に、同じフレームで振り始める。
    [DefaultExecutionOrder(10)]
    public sealed class EnemyMelee : MonoBehaviour
    {
        [SerializeField, Min(1), Tooltip("1 回の当たりのダメージ")]
        private int _damage = MeleeSpec.DefaultDamage;

        [SerializeField, Min(0.01f), Tooltip("体の中心から相手の体の表面までの届く距離 (m)。これより近づいた目標を殴る")]
        private float _reach = MeleeSpec.DefaultReach;

        [SerializeField, Range(1f, MeleeSpec.MaxArcDegrees), Tooltip("前方の扇の全体の角度 (度)")]
        private float _arcDegrees = MeleeSpec.DefaultArcDegrees;

        [SerializeField, Min(0.01f), Tooltip("振りの秒数。この間は撃たない")]
        private float _swingSeconds = MeleeSpec.DefaultSwingSeconds;

        [SerializeField, Min(0f), Tooltip("振り終えてから次に振れるまでの秒数")]
        private float _cooldownSeconds = MeleeSpec.DefaultCooldownSeconds;

        [SerializeField, Min(0f), Tooltip("振り始めから当たりを判定するまでの秒数。振りの秒数以下")]
        private float _impactSeconds = MeleeSpec.DefaultImpactSeconds;

        [SerializeField, Min(0f), Tooltip("振るときに前へ踏み込む距離 (m)")]
        private float _lungeDistance = MeleeSpec.DefaultLungeDistance;

        [SerializeField, Tooltip("当たりを探すレイヤー")]
        private LayerMask _hitMask = Physics.DefaultRaycastLayers;

        private EnemyAgent _agent;
        private NavMeshAgent _navAgent;
        private MeleeSwing _swing;
        private MeleeEnemyBehavior _behavior;
        private EnemyBrain _attachedBrain;
        private Melee _rule = Melee.Off;
        private bool _configured;

        public Melee Rule => _rule;

        public EnemyAgent Agent => _agent;

        public MeleeSwing Swing => EnsureSwing();

        public MeleeEnemyBehavior Behavior => EnsureBehavior();

        public MeleeSpec Spec
        {
            get => EnsureSwing().Spec;
            set => EnsureSwing().Spec = value ?? throw new ArgumentNullException(nameof(value));
        }

        public IMeleePresenter Presenter { get; set; }

        public LayerMask HitMask
        {
            get => _hitMask;
            set => _hitMask = value;
        }

        public bool IsSwinging => _swing != null && _swing.IsSwinging;

        public bool IsAttached => _attachedBrain != null;

        private void Awake()
        {
            _agent = GetComponent<EnemyAgent>();
            _navAgent = GetComponent<NavMeshAgent>();
            EnsureBehavior();
        }

        // ルールを持つのは GameBootstrap なので、誰も Configure しなければシーンのルールに従う (プレイヤーと同じ規則で殴る)。
        // 見つからなければ Off のまま (殴らない) にして、今の敵の振る舞いを変えない。
        private void Start()
        {
            if (_configured)
            {
                return;
            }

            var bootstrap = FindAnyObjectByType<GameBootstrap>();
            var world = bootstrap != null ? bootstrap.World : null;
            if (world != null)
            {
                Configure(world.Rules);
            }
        }

        private void OnDisable()
        {
            _swing?.Cancel();
            Detach();
        }

        private void OnDestroy()
        {
            Detach();
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        public void Configure(GameRules rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            _configured = true;
            _rule = rules.Actions.Melee;
            EnsureBehavior().Enabled = _rule == Melee.On;
            if (_rule != Melee.On)
            {
                _swing?.Cancel();
                Detach();
            }
            else
            {
                TryAttach();
            }

            Debug.Log($"{MeleeLog.LogTag} enemy id={AttackerId} rule={_rule}", this);
        }

        // 振り始めたか当たりを判定したかを返す。EnemyAgent の判断 (MeleeEnemyBehavior) を読むので、その Update の後に呼ぶ。
        public bool Tick(float deltaSeconds)
        {
            var swing = EnsureSwing();
            if (_rule != Melee.On || !TryAttach())
            {
                swing.Tick(deltaSeconds);
                return false;
            }

            // 怯みなどで止められている間は判断が進まず WantsSwing が古いままなので、振り始めず、振っている途中なら止める。
            if (_attachedBrain.IsDead || _agent.IsInterrupted)
            {
                swing.Cancel();
                swing.Tick(deltaSeconds);
                return false;
            }

            var started = false;
            if (_behavior.WantsSwing && IsFacingTarget(swing.Spec) && swing.TryStart())
            {
                started = true;
                Debug.Log(MeleeLog.Swing(AttackerId), this);
                ResolvePresenter()?.OnSwingStarted(gameObject, swing.CurrentSpec);
            }

            if (swing.IsLunging)
            {
                Lunge(swing.CurrentSpec, deltaSeconds);
            }

            var spec = swing.CurrentSpec;
            if (swing.Tick(deltaSeconds))
            {
                Strike(spec);
                return true;
            }

            return started;
        }

        private string AttackerId => _agent != null ? _agent.Id : name;

        // 判断は目標の方へ向き直らせるだけなので、向き終える前に振って空振りしないよう、扇に入るまで待つ。高さは問わない (届くかは当たりの判定で測る)。
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

        private void Strike(MeleeSpec spec)
        {
            var presenter = ResolvePresenter();
            var forward = transform.forward;
            if (!MeleeStrike.TryFind(transform, transform.position, forward, spec, _hitMask, out var hit))
            {
                Debug.Log(MeleeLog.Miss(AttackerId), this);
                presenter?.OnMissed(gameObject);
                return;
            }

            Debug.Log(MeleeLog.Hit(AttackerId, hit.Target.Id, spec.Damage, hit.Distance), this);
            var weapon = _agent != null ? _agent.Weapon : null;
            var damage = MeleeStrike.Apply(gameObject, hit, spec.Damage, weapon != null ? weapon.Spec.Id : string.Empty);
            presenter?.OnHit(gameObject, hit.Target, hit.Point, hit.Direction, damage);
        }

        // EnemyAgent は Awake で判断を作るが、同じ GameObject の Awake の順は決まらないので、使うときに付ける。
        private bool TryAttach()
        {
            if (_agent == null)
            {
                _agent = GetComponent<EnemyAgent>();
            }

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

        private MeleeEnemyBehavior EnsureBehavior()
        {
            if (_behavior == null)
            {
                _behavior = new MeleeEnemyBehavior(EnsureSwing()) { Enabled = _rule == Melee.On };
            }

            return _behavior;
        }

        private MeleeSwing EnsureSwing()
        {
            if (_swing != null)
            {
                return _swing;
            }

            MeleeSpec spec;
            try
            {
                spec = new MeleeSpec(_damage, _reach, _arcDegrees, _swingSeconds, _cooldownSeconds, _impactSeconds, _lungeDistance);
            }
            catch (ArgumentException e)
            {
                Debug.LogError($"{MeleeLog.LogTag} '{name}' の近接攻撃の値が不正なので既定値で振ります: {e.Message}", this);
                spec = MeleeSpec.Default;
            }

            _swing = new MeleeSwing(spec);
            return _swing;
        }
    }
}
