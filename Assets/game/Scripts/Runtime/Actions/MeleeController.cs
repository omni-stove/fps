using System;
using Shooter.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    // 撃てない・リロードの取り消しは、WeaponController が同じフレームに Attack と Reload を読むより先に振り始めていないと効かない。
    [DefaultExecutionOrder(-10)]
    public sealed class MeleeController : MonoBehaviour, IWeaponModifier, IMovementOverride
    {
        public const string MeleeActionPath = InputActionPaths.Melee;

        // 近接武器は覗き込まないので、覗き込みのボタンを投げに使う。
        public const string ThrowActionPath = InputActionPaths.Aim;

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

        [SerializeField, Min(0.01f), Tooltip("怯んだ相手への処刑技の演出の秒数")]
        private float _finisherSeconds = MeleeSpec.DefaultFinisherSeconds;

        [SerializeField, Min(0), Tooltip("処刑技で倒したときに得る体力")]
        private int _finisherHealth = MeleeSpec.DefaultFinisherHealth;

        [SerializeField, Min(0), Tooltip("処刑技で倒したときに持っている武器へ足す予備弾")]
        private int _finisherAmmo = MeleeSpec.DefaultFinisherAmmo;

        [SerializeField, Tooltip("処刑技の最中にほかから撃たれたら中断するか")]
        private bool _finisherInterruptible = MeleeSpec.DefaultFinisherInterruptible;

        [SerializeField, Min(0.01f), Tooltip("気づいていない相手を背後から倒す技の演出の秒数")]
        private float _takedownSeconds = MeleeSpec.DefaultTakedownSeconds;

        [SerializeField, Tooltip("背後から倒す技の最中にほかから撃たれたら中断するか")]
        private bool _takedownInterruptible = MeleeSpec.DefaultTakedownInterruptible;

        [SerializeField, Range(0f, 1f), Tooltip("背後から倒す技の最中にこちらを見た敵の発見ゲージを上げる量。1 で交戦、0 で上げない")]
        private float _takedownWitnessAwareness = MeleeSpec.DefaultTakedownWitnessAwareness;

        [SerializeField, Tooltip("当たりを探すレイヤー")]
        private LayerMask _hitMask = Physics.DefaultRaycastLayers;

        [SerializeField, Tooltip("近接で倒したことにする武器 (ナイフなど)。空なら持っている銃で倒したことにする")]
        private WeaponDefinition _meleeWeapon;

        private MeleeSwing _swing;
        private PlayerController _mover;
        private WeaponController _weapon;
        private Damageable _self;
        private AimDownSights _ads;
        // シーンに置いたまま再コンパイルすると、Unity が private の値も保存し直し、null の InputAction を空のアクションとして戻してしまう (再生でシーンを読み直さないので、そのまま使われる)。
        [NonSerialized]
        private InputAction _meleeAction;
        [NonSerialized]
        private InputAction _throwAction;
        private bool _enabledMeleeAction;
        private bool _enabledThrowAction;
        private bool _hooked;
        [NonSerialized]
        private bool _specApplied;
        private Melee _rule = Melee.Off;
        private bool _interruptsReload = ActionRules.Default.InterruptsReload(ReloadInterrupt.Melee);
        private Vector3 _lungeDirection = Vector3.forward;
        private string _swingWeaponId;

        public Melee Rule => _rule;

        public PlayerController Mover => _mover;

        public WeaponController Weapon => _weapon;

        public InputAction MeleeAction => _meleeAction;

        public InputAction ThrowAction => _throwAction;

        public bool IsInitialized => _mover != null && _weapon != null;

        public MeleeSwing Swing => EnsureSwing();

        public MeleeSpec Spec
        {
            get => EnsureSwing().Spec;
            set
            {
                EnsureSwing().Spec = value ?? throw new ArgumentNullException(nameof(value));
                _specApplied = true;
            }
        }

        // Attach は何度も呼ばれるので、コードで Spec を渡された値を調整値の JSON で消さないために印を持つ。
        public bool SpecApplied => _specApplied;

        public void ApplyTuning(MeleeSpec tuning)
        {
            if (tuning == null)
            {
                throw new ArgumentNullException(nameof(tuning));
            }

            if (!_specApplied)
            {
                EnsureSwing().Spec = tuning;
            }
        }

        public IMeleePresenter Presenter { get; set; }

        public LayerMask HitMask
        {
            get => _hitMask;
            set => _hitMask = value;
        }

        public WeaponDefinition MeleeWeapon
        {
            get => _meleeWeapon;
            set => _meleeWeapon = value;
        }

        public WeaponDefinition LastThrownWeapon { get; private set; }

        public string StrikeWeaponId
        {
            get
            {
                if (_meleeWeapon != null)
                {
                    return _meleeWeapon.Id;
                }

                var weapon = _weapon != null ? _weapon.Weapon : null;
                return weapon != null ? weapon.Spec.Id : string.Empty;
            }
        }

        // 盾の武器を持っている間は、近接攻撃のボタンでも攻撃のボタンでも盾の殴りの値で振る。
        public MeleeSpec ShieldBash
        {
            get
            {
                var weapon = _weapon != null ? _weapon.Weapon : null;
                return weapon != null && weapon.Spec.IsShield ? weapon.Spec.Shield.Bash : null;
            }
        }

        public bool IsShieldBashing { get; private set; }

        public MeleeWeaponSpec HeldMeleeWeapon
        {
            get
            {
                var weapon = _weapon != null ? _weapon.Weapon : null;
                return weapon != null ? weapon.Spec.Melee : null;
            }
        }

        public bool IsMeleeWeaponSwinging { get; private set; }

        public bool InterruptsReload => _interruptsReload;

        public bool IsSwinging => _swing != null && _swing.IsSwinging;

        public bool IsLunging => _swing != null && _swing.IsLunging;

        public string AttackerId => _self != null ? _self.Id : name;

        bool IWeaponModifier.BlocksFire => enabled && IsSwinging;

        bool IWeaponModifier.BlocksReload => enabled && IsSwinging && _interruptsReload;

        float IWeaponModifier.SpreadMultiplier => 1f;

        float IWeaponModifier.ModifyConeDegrees(WeaponSpec spec, float coneDegrees) => coneDegrees;

        float IWeaponModifier.ModifySpreadFraction(float spreadFraction) => spreadFraction;

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

            if (_meleeAction != null && !_mover.IsControlHandedOff && KeySharing.WasPressedThisFrame(_meleeAction))
            {
                TryMelee();
            }

            var melee = HeldMeleeWeapon;
            if (melee != null && melee.CanThrow && _throwAction != null && !_mover.IsControlHandedOff && _throwAction.WasPressedThisFrame())
            {
                TryThrowWeapon();
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
            _interruptsReload = rules.Actions.InterruptsReload(ReloadInterrupt.Melee);
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

            var throwAction = actions != null ? actions.FindAction(ThrowActionPath) : null;
            Initialize(mover, weapon, action, throwAction);
            return true;
        }

        public void Initialize(PlayerController mover, WeaponController weapon, InputAction meleeAction, InputAction throwAction = null)
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
            _throwAction = throwAction;
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
                || _mover.IsMantling
                || (_mover.IsMovementOverridden && !ReferenceEquals(_mover.ActiveMovementOverride, this))
                || IsThrowingGrenade()
                || IsPerformingContextMove())
            {
                return MeleeRefusal.Busy;
            }

            return EnsureSwing().CanStart ? MeleeRefusal.None : MeleeRefusal.Cooldown;
        }

        public bool TryMelee()
        {
            var context = GetComponent<ContextMeleeAction>();
            if (context != null && context.enabled && context.TryBegin())
            {
                return true;
            }

            return TrySwing();
        }

        public bool TrySwing()
        {
            return StartSwing(null, 0f);
        }

        public bool TryWeaponSwing(MeleeSpec spec, float chargeFraction)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            return HeldMeleeWeapon != null && StartSwing(spec, chargeFraction);
        }

        private bool StartSwing(MeleeSpec weaponSwing, float chargeFraction)
        {
            var refusal = CheckSwing();
            if (refusal != MeleeRefusal.None)
            {
                Debug.Log(MeleeLog.Refused(AttackerId, refusal), this);
                return false;
            }

            // 振りは始めたときの値で終えるので (MeleeSwing.Spec)、盾・近接武器の値は振り始めにだけ差し替えて戻す。調整値の Spec は変えない。
            var bash = ShieldBash;
            var melee = HeldMeleeWeapon;
            var held = bash ?? weaponSwing ?? melee?.Swing;
            var normal = _swing.Spec;
            if (held != null)
            {
                _swing.Spec = held;
            }

            _swing.TryStart();
            _swing.Spec = normal;
            IsShieldBashing = bash != null;
            IsMeleeWeaponSwinging = bash == null && held != null;
            _swingWeaponId = held != null ? _weapon.Weapon.Spec.Id : StrikeWeaponId;
            _lungeDirection = AimForward();
            if (_ads != null)
            {
                _ads.Release();
            }

            Debug.Log(
                bash != null ? MeleeLog.ShieldBash(AttackerId, _swingWeaponId)
                : IsMeleeWeaponSwinging ? MeleeLog.WeaponSwing(AttackerId, _swingWeaponId, chargeFraction)
                : MeleeLog.Swing(AttackerId),
                this);
            ResolvePresenter()?.OnSwingStarted(gameObject, _swing.CurrentSpec);
            return true;
        }

        public bool TryThrowWeapon()
        {
            return TryThrowWeapon(out _);
        }

        public bool TryThrowWeapon(out ThrownMeleeWeapon thrown)
        {
            thrown = null;
            var melee = HeldMeleeWeapon;
            if (melee == null || !melee.CanThrow || _weapon.IsMeleeWeaponBlocked)
            {
                return false;
            }

            var refusal = CheckSwing();
            if (refusal == MeleeRefusal.Cooldown && !IsSwinging)
            {
                refusal = MeleeRefusal.None;
            }

            if (refusal != MeleeRefusal.None)
            {
                Debug.Log(MeleeLog.Refused(AttackerId, refusal), this);
                return false;
            }

            var weaponId = _weapon.Weapon.Spec.Id;
            var definition = _weapon.Definition;
            var settings = definition != null ? definition.Melee : null;
            var aim = _weapon.Aim;
            if (!_weapon.DropWeapon(weaponId, out var held))
            {
                return false;
            }

            var direction = ThrownMeleeWeapon.LaunchDirection(aim.forward, aim.right, melee.Throw.UpwardDegrees);
            thrown = ThrownMeleeWeapon.Throw(
                gameObject,
                held,
                melee.Throw,
                EyeOrigin(),
                direction,
                aim.right,
                settings != null ? settings.ThrownPrefab : null,
                settings != null ? settings.ThrownCollectMode : PickupCollectMode.Touch);
            // 投げた武器はロードアウトから外れるので、投げで倒した帰属の名前を引けるよう定義を残す。
            LastThrownWeapon = held.Definition;
            Debug.Log(MeleeLog.Thrown(AttackerId, weaponId), this);
            return true;
        }

        public bool Tick(float deltaSeconds)
        {
            var swing = EnsureSwing();
            if (swing.IsSwinging && _self != null && (_self.IsDead || _self.IsDowned))
            {
                CancelSwing();
                return false;
            }

            var spec = swing.CurrentSpec;
            var impact = swing.Tick(deltaSeconds);
            if (impact)
            {
                Strike(spec);
            }

            if (!swing.IsSwinging)
            {
                IsShieldBashing = false;
                IsMeleeWeaponSwinging = false;
            }

            return impact;
        }

        public void CancelSwing()
        {
            _swing?.Cancel();
            IsShieldBashing = false;
            IsMeleeWeaponSwinging = false;
        }

        // 攻撃のボタン (WeaponController.ShieldBashRequested) から振る。盾を持っていなければ振らない。
        public bool TryShieldBash()
        {
            if (ShieldBash == null)
            {
                return false;
            }

            return TrySwing();
        }

        private void OnShieldBashRequested(RiotShieldSpec shield)
        {
            TryShieldBash();
        }

        private void OnMeleeWeaponSwingRequested(MeleeSpec spec, float chargeFraction)
        {
            TryWeaponSwing(spec, chargeFraction);
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
                if (IsMeleeWeaponSwinging)
                {
                    StrikeSurface(spec);
                }

                presenter?.OnMissed(gameObject);
                return;
            }

            Debug.Log(MeleeLog.Hit(AttackerId, hit.Target.Id, spec.Damage, hit.Distance), this);
            // 盾で殴って倒したら、近接の武器 (ナイフなど) ではなく盾の武器で倒したことにする。
            var damage = MeleeStrike.Apply(gameObject, hit, spec.Damage, _swingWeaponId ?? StrikeWeaponId);

            // オフラインで MeleeController を持つのは手元のプレイヤーだけなので、当てたらその画面にヒットマーカーを出す。
            if (_self != null && _self.IsLocalPlayer)
            {
                CombatEvents.RaiseHitConfirmed(new HitConfirm(damage, HitZone.Body, hit.Target.IsDead, downed: false, hit.Point.ToNumerics()));
            }

            presenter?.OnHit(gameObject, hit.Target, hit.Point, hit.Direction, damage);
        }

        // 近接武器で人に当たらなかったときだけ、照準の先の壁や物を叩いた手応え (火花・音・跡) を着弾と同じ出来事で出す。
        private void StrikeSurface(MeleeSpec spec)
        {
            var aim = _weapon != null ? _weapon.Aim : null;
            if (aim == null || !MeleeStrike.TryFindSurface(transform, EyeOrigin(), aim.forward, spec.Reach, _hitMask, out var surface))
            {
                return;
            }

            Debug.Log(MeleeLog.WorldHit(AttackerId, SurfaceMaterial.IdOf(surface.collider)), this);
            CombatEvents.RaiseImpact(surface.point, surface.normal, surface.collider);
        }

        // 三人称ではカメラが体の後ろにあるので、線や投げの起点はカメラでなく目 (ViewRig が無ければ体) にする。GrenadeThrower と同じ。
        private Vector3 EyeOrigin()
        {
            var rig = GetComponent<ViewRig>();
            return rig != null && rig.IsInitialized ? rig.Eye.position : transform.position;
        }

        // 三人称ではカメラと体の向きがずれ得るので、狙っている向き (照準) を水平にして使う。真上・真下を向いていれば体の正面。
        internal Vector3 AimForward()
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

        private bool IsPerformingContextMove()
        {
            var context = GetComponent<ContextMeleeAction>();
            return context != null && context.IsPerforming;
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
                spec = new MeleeSpec(
                    _damage,
                    _reach,
                    _arcDegrees,
                    _swingSeconds,
                    _cooldownSeconds,
                    _impactSeconds,
                    _lungeDistance,
                    _finisherSeconds,
                    _finisherHealth,
                    _finisherAmmo,
                    _finisherInterruptible,
                    _takedownSeconds,
                    _takedownInterruptible,
                    _takedownWitnessAwareness);
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
            _weapon.ShieldBashRequested += OnShieldBashRequested;
            _weapon.MeleeWeaponSwingRequested += OnMeleeWeaponSwingRequested;
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
                _weapon.ShieldBashRequested -= OnShieldBashRequested;
                _weapon.MeleeWeaponSwingRequested -= OnMeleeWeaponSwingRequested;
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

            _enabledThrowAction = _throwAction != null && !_throwAction.enabled;
            if (_enabledThrowAction)
            {
                _throwAction.Enable();
            }
        }

        private void ReleaseAction()
        {
            if (_enabledMeleeAction && _meleeAction != null)
            {
                _meleeAction.Disable();
            }

            _enabledMeleeAction = false;
            if (_enabledThrowAction && _throwAction != null)
            {
                _throwAction.Disable();
            }

            _enabledThrowAction = false;
        }
    }
}
