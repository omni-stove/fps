using System;
using Fps.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fps.Runtime
{
    [DisallowMultipleComponent]
    // 撃てない・リロードの取り消しは、WeaponController が同じフレームに Attack と Reload を読むより先に振り始めていないと効かない。
    [DefaultExecutionOrder(-10)]
    public sealed class MeleeController : MonoBehaviour, IWeaponModifier, IMovementOverride
    {
        public const string MeleeActionPath = InputActionPaths.Melee;

        public const int LungePriority = 0;

        [SerializeField, Min(1), Tooltip("1 回の当たりのダメージ")]
        private int _damage = MeleeSpec.DefaultDamage;

        [SerializeField, Min(0.01f), Tooltip("体の中心から相手の体の表面までの届く距離 (m)")]
        private float _reach = MeleeSpec.DefaultReach;

        [SerializeField, Range(1f, MeleeSpec.MaxArcDegrees), Tooltip("前方の扇の全体の角度 (度)")]
        private float _arcDegrees = MeleeSpec.DefaultArcDegrees;

        [SerializeField, Min(0.01f), Tooltip("振りの秒数。この間は撃てずリロードもできない")]
        private float _swingSeconds = MeleeSpec.DefaultSwingSeconds;

        [SerializeField, Min(0f), Tooltip("振り終えてから次に振れるまでの秒数")]
        private float _cooldownSeconds = MeleeSpec.DefaultCooldownSeconds;

        [SerializeField, Min(0f), Tooltip("振り始めから当たりを判定するまでの秒数。踏み込みはこの間に行う。振りの秒数以下")]
        private float _impactSeconds = MeleeSpec.DefaultImpactSeconds;

        [SerializeField, Min(0f), Tooltip("振るときに前へ踏み込む距離 (m)")]
        private float _lungeDistance = MeleeSpec.DefaultLungeDistance;

        [SerializeField, Tooltip("当たりを探すレイヤー")]
        private LayerMask _hitMask = Physics.DefaultRaycastLayers;

        private MeleeSwing _swing;
        private PlayerController _mover;
        private WeaponController _weapon;
        private Damageable _self;
        private AimDownSights _ads;
        private InputAction _meleeAction;
        private bool _enabledMeleeAction;
        private bool _hooked;
        private Melee _rule = Melee.Off;
        private Vector3 _lungeDirection = Vector3.forward;

        public Melee Rule => _rule;

        public PlayerController Mover => _mover;

        public WeaponController Weapon => _weapon;

        public InputAction MeleeAction => _meleeAction;

        public bool IsInitialized => _mover != null && _weapon != null;

        public MeleeSwing Swing => EnsureSwing();

        // 振っている最中に替えても、その振りは始めたときの値で終える (MeleeSwing.Spec)。
        public MeleeSpec Spec
        {
            get => EnsureSwing().Spec;
            set => EnsureSwing().Spec = value ?? throw new ArgumentNullException(nameof(value));
        }

        // null なら同じ GameObject で IMeleePresenter を実装したコンポーネントを探す (無ければ演出なし)。
        public IMeleePresenter Presenter { get; set; }

        public LayerMask HitMask
        {
            get => _hitMask;
            set => _hitMask = value;
        }

        public bool IsSwinging => _swing != null && _swing.IsSwinging;

        public bool IsLunging => _swing != null && _swing.IsLunging;

        public string AttackerId => _self != null ? _self.Id : name;

        bool IWeaponModifier.BlocksFire => enabled && IsSwinging;

        bool IWeaponModifier.BlocksReload => enabled && IsSwinging;

        float IWeaponModifier.SpreadMultiplier => 1f;

        int IMovementOverride.Priority => LungePriority;

        bool IMovementOverride.IsActive => enabled && IsLunging;

        PlayerActionConditions IMovementOverride.Conditions => PlayerActionConditions.Meleeing;

        private void Awake()
        {
            EnsureSwing();
            if (!IsInitialized && !TryInitializeFromHierarchy())
            {
                enabled = false;
            }
        }

        private void OnEnable()
        {
            SyncHooks();
        }

        private void OnDisable()
        {
            CancelSwing();
            SyncHooks();
        }

        private void OnDestroy()
        {
            CancelSwing();
            Unhook();
        }

        private void Update()
        {
            if (!IsInitialized)
            {
                return;
            }

            if (_meleeAction != null && !_mover.IsControlHandedOff && _meleeAction.WasPressedThisFrame())
            {
                TrySwing();
            }

            Tick(Time.deltaTime);
        }

        public void Configure(GameRules rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            _rule = rules.Actions.Melee;
            enabled = _rule == Melee.On;

            // Edit Mode では enabled を変えても OnEnable / OnDisable が走らないので、差し込みと振りの取り消しはここでも揃える。
            if (!enabled)
            {
                CancelSwing();
            }

            SyncHooks();
        }

        public bool TryInitializeFromHierarchy()
        {
            var mover = GetComponent<PlayerController>();
            if (mover == null)
            {
                Debug.LogError($"{MeleeLog.LogTag} '{name}' に PlayerController がありません。近接攻撃の踏み込みはその移動に差し込みます。", this);
                return false;
            }

            var weapon = GetComponentInChildren<WeaponController>(true);
            if (weapon == null)
            {
                Debug.LogError($"{MeleeLog.LogTag} '{name}' の子に WeaponController がありません。振っている間はその射撃を止めます。", this);
                return false;
            }

            AppliedInputBindings.EnsureLoaded();
            var actions = InputSystem.actions;
            var action = actions != null ? actions.FindAction(MeleeActionPath) : null;
            if (action == null)
            {
                Debug.LogWarning($"{MeleeLog.LogTag} '{MeleeActionPath}' が見つからないため、入力では振れません (TrySwing からは振れます)。", this);
            }

            Initialize(mover, weapon, action);
            return true;
        }

        public void Initialize(PlayerController mover, WeaponController weapon, InputAction meleeAction)
        {
            if (mover == null)
            {
                throw new ArgumentNullException(nameof(mover));
            }

            if (weapon == null)
            {
                throw new ArgumentNullException(nameof(weapon));
            }

            if (mover.gameObject != gameObject)
            {
                throw new ArgumentException($"mover must be on '{name}'", nameof(mover));
            }

            if (!weapon.transform.IsChildOf(transform))
            {
                throw new ArgumentException($"weapon must be on '{name}' or its children", nameof(weapon));
            }

            CancelSwing();
            Unhook();
            ReleaseAction();
            _mover = mover;
            _weapon = weapon;
            _self = GetComponent<Damageable>();
            _ads = GetComponent<AimDownSights>();
            _meleeAction = meleeAction;
            SyncHooks();
        }

        public MeleeRefusal CheckSwing()
        {
            if (_rule != Melee.On || !enabled)
            {
                return MeleeRefusal.Off;
            }

            if (!IsInitialized
                || (_self != null && (_self.IsDead || _self.IsDowned))
                || _mover.IsDodging
                || _mover.IsVaulting
                || (_mover.IsMovementOverridden && !ReferenceEquals(_mover.ActiveMovementOverride, this))
                || IsThrowingGrenade())
            {
                return MeleeRefusal.Busy;
            }

            return EnsureSwing().CanStart ? MeleeRefusal.None : MeleeRefusal.Cooldown;
        }

        public bool TrySwing()
        {
            var refusal = CheckSwing();
            if (refusal != MeleeRefusal.None)
            {
                Debug.Log(MeleeLog.Refused(AttackerId, refusal), this);
                return false;
            }

            _swing.TryStart();
            _lungeDirection = AimForward();
            if (_ads != null)
            {
                _ads.Release();
            }

            Debug.Log(MeleeLog.Swing(AttackerId), this);
            ResolvePresenter()?.OnSwingStarted(gameObject, _swing.CurrentSpec);
            return true;
        }

        // 当たりを判定した Tick だけ true (当たったかどうかは問わない)。
        public bool Tick(float deltaSeconds)
        {
            var swing = EnsureSwing();
            if (swing.IsSwinging && _self != null && (_self.IsDead || _self.IsDowned))
            {
                swing.Cancel();
                return false;
            }

            var spec = swing.CurrentSpec;
            if (!swing.Tick(deltaSeconds))
            {
                return false;
            }

            Strike(spec);
            return true;
        }

        public void CancelSwing()
        {
            _swing?.Cancel();
        }

        void IMovementOverride.Begin(PlayerController mover)
        {
        }

        MovementOverrideStep IMovementOverride.Step(PlayerController mover, MovementOverrideInput input, float deltaSeconds)
        {
            var current = mover.Motor != null ? mover.Motor.Velocity.ToUnity() : Vector3.zero;
            var vertical = input.IsGrounded ? PlayerMotor.GroundedVerticalSpeed : current.y - (mover.Gravity * deltaSeconds);
            var velocity = (_lungeDirection * EnsureSwing().CurrentSpec.LungeSpeed) + (Vector3.up * vertical);
            return MovementOverrideStep.Move(velocity * deltaSeconds, velocity);
        }

        // 踏み込みの勢いを、終えた後のふつうの移動 (空中の慣性) に持ち越さない。
        void IMovementOverride.End(PlayerController mover)
        {
            if (mover.Motor == null)
            {
                return;
            }

            var velocity = mover.Motor.Velocity;
            mover.Motor.SetVelocity(new System.Numerics.Vector3(0f, velocity.Y, 0f));
        }

        private void Strike(MeleeSpec spec)
        {
            var origin = transform.position;
            var forward = AimForward();
            var presenter = ResolvePresenter();
            if (!MeleeStrike.TryFind(transform, origin, forward, spec, _hitMask, out var hit))
            {
                Debug.Log(MeleeLog.Miss(AttackerId), this);
                presenter?.OnMissed(gameObject);
                return;
            }

            Debug.Log(MeleeLog.Hit(AttackerId, hit.Target.Id, spec.Damage, hit.Distance), this);
            var weapon = _weapon != null ? _weapon.Weapon : null;
            var damage = MeleeStrike.Apply(gameObject, hit, spec.Damage, weapon != null ? weapon.Spec.Id : string.Empty);

            // オフラインで MeleeController を持つのは手元のプレイヤーだけなので、当てたらその画面にヒットマーカーを出す。
            if (_self != null && _self.IsLocalPlayer)
            {
                CombatEvents.RaiseHitConfirmed(new HitConfirm(damage, HitZone.Body, hit.Target.IsDead, downed: false));
            }

            presenter?.OnHit(gameObject, hit.Target, hit.Point, hit.Direction, damage);
        }

        // 三人称ではカメラと体の向きがずれ得るので、狙っている向き (照準) を水平にして使う。真上・真下を向いていれば体の正面。
        private Vector3 AimForward()
        {
            var aim = _weapon != null ? _weapon.Aim : null;
            var forward = aim != null ? aim.forward : transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-6f)
            {
                forward = transform.forward;
                forward.y = 0f;
            }

            return forward.sqrMagnitude < 1e-6f ? Vector3.forward : forward.normalized;
        }

        private bool IsThrowingGrenade()
        {
            var grenades = GetComponent<GrenadeThrower>();
            return grenades != null && grenades.IsThrowing;
        }

        private IMeleePresenter ResolvePresenter()
        {
            return Presenter ?? GetComponent<IMeleePresenter>();
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

        private void SyncHooks()
        {
            if (enabled && IsInitialized)
            {
                Hook();
            }
            else
            {
                Unhook();
            }
        }

        private void Hook()
        {
            if (_hooked)
            {
                return;
            }

            _weapon.AddModifier(this);
            _mover.RegisterMovementOverride(this);
            AcquireAction();
            _hooked = true;
        }

        private void Unhook()
        {
            if (!_hooked)
            {
                return;
            }

            _hooked = false;

            // 破棄済みでも C# のオブジェクトは残っているので、登録は外せる。
            if (!ReferenceEquals(_weapon, null))
            {
                _weapon.RemoveModifier(this);
            }

            if (!ReferenceEquals(_mover, null))
            {
                _mover.UnregisterMovementOverride(this);
            }

            ReleaseAction();
        }

        // プロジェクト全体のアクションは他の部品と共有なので、自分が有効にしたものだけ後で無効に戻す。
        private void AcquireAction()
        {
            _enabledMeleeAction = _meleeAction != null && !_meleeAction.enabled;
            if (_enabledMeleeAction)
            {
                _meleeAction.Enable();
            }
        }

        private void ReleaseAction()
        {
            if (_enabledMeleeAction && _meleeAction != null)
            {
                _meleeAction.Disable();
            }

            _enabledMeleeAction = false;
        }
    }
}
