using System;
using System.Globalization;
using Fps.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using ZiplineRule = Fps.Core.Zipline;

namespace Fps.Runtime.Traversal
{
    // PlayerController (既定の実行順 0) より先に入力を読む。Jump で掴まったフレームの Tick で、その Jump が普通のジャンプにならないようにするため。
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-20)]
    public sealed class ZiplineMovement : MonoBehaviour, IMovementOverride
    {
        public const string LogTag = Zipline.LogTag;
        public const string InteractActionPath = InputActionPaths.Interact;

        // はしごや乗り物の座席など、後から足す横取りと並べられるよう間を空けておく。
        public const int OverridePriority = 100;

        public const float DefaultGrabRadius = 1.5f;
        public const float DefaultHangOffset = 1.3f;
        public const float DefaultRegrabSeconds = 0.5f;
        public const float DefaultSpreadMoveFraction = 1f;

        public const string ReasonEnd = "end";
        public const string ReasonJump = "jump";
        public const string ReasonDisabled = "disabled";
        public const string ReasonDeath = "death";
        public const string ReasonInterrupted = "interrupted";

        [SerializeField, Min(0f), Tooltip("掴む手 (体の中心から上に Hang Offset) から線までがこの距離 (m) 以内なら掴まれる")]
        private float _grabRadius = DefaultGrabRadius;

        [SerializeField, Min(0f), Tooltip("掴まっている間の、線から体の中心までの下向きの距離 (m)")]
        private float _hangOffset = DefaultHangOffset;

        [SerializeField, Min(0f), Tooltip("離れてから次に掴まれるまでの秒数")]
        private float _regrabSeconds = DefaultRegrabSeconds;

        [SerializeField, Range(0f, 1f), Tooltip("掴まっている間、射撃のばらつきに使う移動の割合の下限 (1 で走っているときと同じだけ広がる)")]
        private float _spreadMoveFraction = DefaultSpreadMoveFraction;

        private ZiplineRule _rule = ZiplineRule.Off;
        private PlayerController _mover;
        private AimDownSights _ads;
        private WeaponController _weapon;
        private Damageable _damageable;
        private InputAction _interactAction;
        private bool _enabledInteract;
        private bool _wrappedSpread;
        private Zipline _line;
        private ZiplineMotion _motion;
        private bool _isRiding;
        private bool _ignoreJump;
        private Vector3 _hangPoint;
        private float _regrabRemaining;

        public event Action<Zipline> Grabbed;

        public event Action<Zipline, string> Released;

        public int Priority => OverridePriority;

        public bool IsActive => _isRiding;

        public PlayerActionConditions Conditions => PlayerActionConditions.Ziplining;

        public ZiplineRule Rule => _rule;

        public bool IsRiding => _isRiding;

        public Zipline Line => _isRiding ? _line : null;

        public ZiplineMotion Motion => _motion;

        public PlayerController Mover => _mover;

        public bool IsInitialized => _mover != null && _mover.IsInitialized;

        public float GrabRadius
        {
            get => _grabRadius;
            set => _grabRadius = RequireNonNegative(value, nameof(value));
        }

        public float HangOffset
        {
            get => _hangOffset;
            set => _hangOffset = RequireNonNegative(value, nameof(value));
        }

        public float RegrabSeconds
        {
            get => _regrabSeconds;
            set => _regrabSeconds = RequireNonNegative(value, nameof(value));
        }

        public float SpreadMoveFraction
        {
            get => _spreadMoveFraction;
            set => _spreadMoveFraction = Mathf.Clamp01(RequireNonNegative(value, nameof(value)));
        }

        public float RegrabRemaining => _regrabRemaining;

        public Vector3 HandPosition => transform.position + (Vector3.up * _hangOffset);

        public static string FormatGrabLog(Zipline line, ZiplineMotion motion)
        {
            if (line == null)
            {
                throw new ArgumentNullException(nameof(line));
            }

            if (motion == null)
            {
                throw new ArgumentNullException(nameof(motion));
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} grab line={1} at={2:0.00} remaining={3:0.00} speed={4:0.00}",
                LogTag,
                line.name,
                motion.Distance,
                motion.RemainingDistance,
                motion.Speed);
        }

        public static string FormatReleaseLog(string reason, ZiplineMotion motion)
        {
            if (string.IsNullOrEmpty(reason))
            {
                throw new ArgumentException("reason must not be empty", nameof(reason));
            }

            var speed = motion != null ? motion.Speed : 0f;
            return string.Format(CultureInfo.InvariantCulture, "{0} release reason={1} speed={2:0.00}", LogTag, reason, speed);
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
            AcquireInteract();
        }

        private void OnDisable()
        {
            ReleaseInteract();
            ReleaseNow(ReasonDisabled);
        }

        private void OnDestroy()
        {
            BindDamageable(null);
            if (_ads != null)
            {
                _ads.AimingChanged -= HandleAimingChanged;
            }

            if (_mover != null)
            {
                _mover.UnregisterMovementOverride(this);
            }
        }

        private void Update()
        {
            if (!IsInitialized)
            {
                return;
            }

            TickCooldown(Time.deltaTime);
            if (_isRiding || _mover.IsControlHandedOff)
            {
                return;
            }

            var jump = _mover.JumpAction;
            var pressed = (_interactAction != null && _interactAction.WasPressedThisFrame())
                || (jump != null && jump.WasPressedThisFrame());
            if (pressed)
            {
                TryGrab();
            }
        }

        public void Configure(GameRules rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            _rule = rules.Actions.Zipline;
            enabled = _rule == ZiplineRule.On;

            // Edit Mode では enabled を変えても OnDisable が走らないので、掴まっていればここでも離す。
            if (!enabled)
            {
                ReleaseNow(ReasonDisabled);
            }
        }

        public bool TryInitializeFromHierarchy()
        {
            var mover = GetComponent<PlayerController>();
            if (mover == null || !mover.IsInitialized)
            {
                Debug.LogError(
                    $"{LogTag} '{name}' に初期化済みの PlayerController がありません。ジップラインは PlayerController の移動を横取りします。",
                    this);
                return false;
            }

            Initialize(mover, FindInteractAction());
            return true;
        }

        // interactAction は null 可。Jump だけで掴まる (と TryGrab を直接呼ぶテスト)。
        public void Initialize(PlayerController mover, InputAction interactAction)
        {
            if (mover == null)
            {
                throw new ArgumentNullException(nameof(mover));
            }

            if (mover.gameObject != gameObject)
            {
                throw new ArgumentException($"mover must be on '{name}'", nameof(mover));
            }

            if (!mover.IsInitialized)
            {
                throw new ArgumentException("mover must be initialized", nameof(mover));
            }

            ReleaseNow(ReasonDisabled);
            if (_mover != null && !ReferenceEquals(_mover, mover))
            {
                _mover.UnregisterMovementOverride(this);
            }

            _mover = mover;
            _mover.RegisterMovementOverride(this);

            ReleaseInteract();
            _interactAction = interactAction;
            if (isActiveAndEnabled && Application.isPlaying)
            {
                AcquireInteract();
            }

            if (_ads != null)
            {
                _ads.AimingChanged -= HandleAimingChanged;
            }

            _ads = GetComponent<AimDownSights>();
            if (_ads != null)
            {
                _ads.AimingChanged += HandleAimingChanged;
            }

            BindDamageable(GetComponent<Damageable>());
            BindWeapon(GetComponentInChildren<WeaponController>(true));
        }

        public bool TryGrab()
        {
            if (!CanGrab())
            {
                return false;
            }

            var line = Zipline.FindNearest(HandPosition, _grabRadius);
            return line != null && TryGrab(line);
        }

        public bool TryGrab(Zipline line)
        {
            if (line == null)
            {
                throw new ArgumentNullException(nameof(line));
            }

            if (!CanGrab() || !line.IsUsable || line.DistanceTo(HandPosition) > _grabRadius)
            {
                return false;
            }

            if (!line.TryCreateMotion(out var motion))
            {
                return false;
            }

            var velocity = _mover.Controller != null ? _mover.Controller.velocity : Vector3.zero;
            if (!motion.TryGrab(HandPosition.ToNumerics(), velocity.ToNumerics(), transform.forward.ToNumerics()))
            {
                return false;
            }

            _line = line;
            _motion = motion;
            _isRiding = true;
            _ignoreJump = true;
            _hangPoint = HangPoint();
            Debug.Log(FormatGrabLog(line, motion), this);
            Grabbed?.Invoke(line);
            return true;
        }

        // 端に着いて離れるときと同じく、次の Tick で横取りが外れて PlayerController に戻る。
        public bool Release(string reason)
        {
            if (string.IsNullOrEmpty(reason))
            {
                throw new ArgumentException("reason must not be empty", nameof(reason));
            }

            return ReleaseNow(reason);
        }

        public void TickCooldown(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be finite and not negative");
            }

            _regrabRemaining = Mathf.Max(0f, _regrabRemaining - deltaSeconds);
        }

        public void Begin(PlayerController mover)
        {
            // 片手で線を掴んでいるので覗き込めない。
            if (_ads != null)
            {
                _ads.Release();
            }
        }

        public MovementOverrideStep Step(PlayerController mover, MovementOverrideInput input, float deltaSeconds)
        {
            var ignoreJump = _ignoreJump;
            _ignoreJump = false;
            if (!_isRiding || _motion == null)
            {
                return MovementOverrideStep.Move(Vector3.zero, Vector3.zero);
            }

            if (input.JumpPressed && !ignoreJump)
            {
                var velocity = _motion.Velocity.ToUnity();
                ReleaseNow(ReasonJump);
                return MovementOverrideStep.Move(velocity * deltaSeconds, velocity);
            }

            // 毎 Tick 線上の所へ置き直してから動かす。壁に引っかかっても体が線から離れたままにならない。
            var from = _hangPoint;
            _motion.Step(deltaSeconds);
            _hangPoint = HangPoint();
            var stepVelocity = _motion.Velocity.ToUnity();
            if (_motion.HasArrived)
            {
                ReleaseNow(ReasonEnd);
            }

            return new MovementOverrideStep(_hangPoint - from, stepVelocity, position: from, crouched: false);
        }

        public void End(PlayerController mover)
        {
            // ほかの横取りに替わった・登録を外された・PlayerController が止まったとき。自分で離したならもう掴まっていない。
            ReleaseNow(ReasonInterrupted);
        }

        private bool CanGrab()
        {
            if (!IsInitialized || _rule != ZiplineRule.On || !enabled || _isRiding || _regrabRemaining > 0f)
            {
                return false;
            }

            if (_mover.IsControlHandedOff || (_damageable != null && (_damageable.IsDead || _damageable.IsDowned)))
            {
                return false;
            }

            if (_mover.IsMovementOverridden && !ReferenceEquals(_mover.ActiveMovementOverride, this))
            {
                return false;
            }

            var cover = GetComponent<CoverController>();
            return cover == null || !cover.IsInCover;
        }

        private bool ReleaseNow(string reason)
        {
            if (!_isRiding)
            {
                return false;
            }

            var line = _line;
            _isRiding = false;
            _ignoreJump = false;
            _motion?.Release();
            _regrabRemaining = _regrabSeconds;
            Debug.Log(FormatReleaseLog(reason, _motion), this);
            Released?.Invoke(line, reason);
            return true;
        }

        private Vector3 HangPoint()
        {
            return _motion.Position.ToUnity() - (Vector3.up * _hangOffset);
        }

        private void HandleAimingChanged(bool aiming)
        {
            if (aiming && _isRiding && _ads != null)
            {
                _ads.Release();
            }
        }

        private void BindDamageable(Damageable damageable)
        {
            if (_damageable != null)
            {
                _damageable.Died -= HandleDied;
            }

            _damageable = damageable;
            if (_damageable != null)
            {
                _damageable.Died += HandleDied;
            }
        }

        private void HandleDied(Damageable damageable)
        {
            ReleaseNow(ReasonDeath);
        }

        // 移動のばらつきの元を一度だけ包む。Configure は何度も呼ばれるので、そのたびに包むと入れ子が増える。
        private void BindWeapon(WeaponController weapon)
        {
            _weapon = weapon;
            if (_weapon == null || _wrappedSpread)
            {
                return;
            }

            var previous = _weapon.MoveFractionSource;
            _weapon.MoveFractionSource = () =>
            {
                var moving = previous != null ? previous() : 0f;
                return this != null && _isRiding ? Mathf.Max(moving, _spreadMoveFraction) : moving;
            };
            _wrappedSpread = true;
        }

        private static InputAction FindInteractAction()
        {
            var actions = InputSystem.actions;
            return actions != null ? actions.FindAction(InteractActionPath) : null;
        }

        // プロジェクト全体のアクションは他の部品と共有なので、自分が有効にしたときだけ後で無効に戻す。
        private void AcquireInteract()
        {
            if (_interactAction != null && !_interactAction.enabled)
            {
                _interactAction.Enable();
                _enabledInteract = true;
            }
        }

        private void ReleaseInteract()
        {
            if (_interactAction != null && _enabledInteract)
            {
                _interactAction.Disable();
            }

            _enabledInteract = false;
        }

        private static float RequireNonNegative(float value, string name)
        {
            if (!(value >= 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, $"{name} must be finite and not negative");
            }

            return value;
        }
    }
}
