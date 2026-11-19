using System;
using Shooter.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class GrenadeThrower : MonoBehaviour, IWeaponModifier
    {
        public const string LogTag = "[Grenade]";

        public const string ActionPath = InputActionPaths.Grenade;

        public const string SwitchActionPath = InputActionPaths.SwitchGrenade;

        public const string WeaponId = "grenade";

        public const string NameKey = "weapon.grenade";

        [SerializeField, Min(0f), Tooltip("投げてから爆発するまでの秒数")]
        private float _fuseSeconds = GrenadeSpec.DefaultFuseSeconds;

        [SerializeField, Min(0f), Tooltip("投げてから次を投げられ、武器を撃てるようになるまでの秒数")]
        private float _throwSeconds = GrenadeSpec.DefaultThrowSeconds;

        [SerializeField, Min(0f), Tooltip("視線の向きへの初速 (m/s)")]
        private float _throwSpeed = GrenadeSpec.DefaultThrowSpeed;

        [SerializeField, Min(0f), Tooltip("初速に足す上向きの速さ (m/s)。水平に狙っても弧を描いて遠くへ届くようにする")]
        private float _throwLiftSpeed = GrenadeSpec.DefaultThrowLiftSpeed;

        [SerializeField, Min(0), Tooltip("爆発の中心 (減らさない半径の内側) で人に与えるダメージ")]
        private int _maxDamage = GrenadeSpec.DefaultMaxDamage;

        [SerializeField, Min(0.01f), Tooltip("爆発が届く半径 (m)。建造物への被害もこの半径")]
        private float _damageRadius = GrenadeSpec.DefaultDamageRadius;

        [SerializeField, Min(0f), Tooltip("減らさずに最大ダメージを与える半径 (m)。爆発が届く半径より小さく")]
        private float _fullDamageRadius = GrenadeSpec.DefaultFullDamageRadius;

        [SerializeField, Min(0), Tooltip("爆発の中心で建造物に与える被害")]
        private int _structureDamageAtCenter = GrenadeSpec.DefaultStructureDamageAtCenter;

        [SerializeField, Tooltip("投げたグレネードの見た目。中心が原点。当たり判定は外し、跳ね方は仮の球のまま。空なら仮の球を作る")]
        private GameObject _grenadePrefab;

        // 投げるたびに検証し直さないよう持っておき、OnValidate で捨てる。
        private GrenadeSpec _spec;
        private GrenadeCatalog _catalog;
        private bool _specApplied;
        private GrenadeInventory _inventory = GrenadeInventory.Empty;
        private ActionRules _actions;
        private Grenade _rule = Grenade.Off;
        private bool _configured;
        private Damageable _body;
        // シーンに置いたまま再コンパイルすると、Unity が private の値も保存し直し、null の InputAction を空のアクションとして戻してしまう (再生でシーンを読み直さないので、そのまま使われる)。
        [NonSerialized]
        private InputAction _action;
        [NonSerialized]
        private InputAction _switchAction;
        private bool _enabledAction;
        private bool _enabledSwitchAction;
        private float _throwRemaining;

        public event Action<GrenadeThrower> Changed;

        public event Action<ThrownGrenade> Thrown;

        public Grenade Rule => _rule;

        public bool UsesGrenades => _rule == Grenade.On;

        public int Capacity => _inventory.SelectedCapacity;

        public int Remaining => _inventory.SelectedRemaining;

        public GrenadeInventory Inventory => _inventory;

        public GrenadeCatalog Catalog => _catalog ?? GrenadeCatalog.Of(InspectorSpec);

        public bool IsThrowing => _throwRemaining > 0f;

        public bool InterruptsReload => (_actions ?? ActionRules.Default).InterruptsReload(ReloadInterrupt.Grenade);

        // 射撃は GameBootstrap が FireBlocker で止めているので、ここではリロードだけを止める。
        bool IWeaponModifier.BlocksFire => false;

        bool IWeaponModifier.BlocksReload => IsThrowing && InterruptsReload;

        float IWeaponModifier.SpreadMultiplier => 1f;

        float IWeaponModifier.ModifyConeDegrees(WeaponSpec spec, float coneDegrees) => coneDegrees;

        float IWeaponModifier.ModifySpreadFraction(float spreadFraction) => spreadFraction;

        public Func<bool> ThrowBlocker { get; set; }

        public InputAction Action => _action;

        public GrenadeSpec Spec => _catalog != null && _inventory.Selected != null ? _inventory.Selected : InspectorSpec;

        private GrenadeSpec InspectorSpec => _spec ?? (_spec = CreateSpec());

        public GameObject GrenadePrefab
        {
            get => _grenadePrefab;
            set => _grenadePrefab = value;
        }

        public GrenadeSpec CreateSpec()
        {
            return new GrenadeSpec(
                _fuseSeconds,
                _throwSeconds,
                _throwSpeed,
                _throwLiftSpeed,
                _maxDamage,
                _damageRadius,
                _fullDamageRadius,
                _structureDamageAtCenter);
        }

        public void ApplySpec(GrenadeSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            _fuseSeconds = spec.FuseSeconds;
            _throwSeconds = spec.ThrowSeconds;
            _throwSpeed = spec.ThrowSpeed;
            _throwLiftSpeed = spec.ThrowLiftSpeed;
            _maxDamage = spec.MaxDamage;
            _damageRadius = spec.DamageRadius;
            _fullDamageRadius = spec.FullDamageRadius;
            _structureDamageAtCenter = spec.StructureDamageAtCenter;
            _spec = spec;
            _catalog = null;
            _specApplied = true;

            // 同じ種類の値を変えただけで残りの数を満タンに戻さないよう、種類が変わったときだけ持ち直す
            if (_inventory.Count != 1 || _inventory.IndexOf(spec.Id) < 0)
            {
                Rebuild();
            }
        }

        public void ApplyCatalog(GrenadeCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _specApplied = true;
            Rebuild();
        }

        private void Awake()
        {
            // 無効なまま置かれた本体は Awake が有効になるまで遅れるので、その間に Play 中のコードが ApplySpec で渡した値を JSON で消さない。
            if (Application.isPlaying && !_specApplied)
            {
                ApplyCatalog(KitTuning.Grenades);
            }
        }

        private void OnValidate()
        {
            // Play 中に Inspector で変えた値も次に投げるグレネードから効かせる。不正な値はここでは投げず、次に Spec を読んだときに例外にする。
            _spec = null;
        }

        private void OnEnable()
        {
            if (_configured && UsesGrenades)
            {
                EnableAction();
            }
        }

        private void OnDisable()
        {
            DisableAction();
        }

        private void OnDestroy()
        {
            BindBody(null);
        }

        private void Update()
        {
            Tick(Time.deltaTime);
            if (_switchAction != null && _switchAction.WasPressedThisFrame())
            {
                SelectNext();
            }

            if (_action != null && _action.WasPressedThisFrame())
            {
                TryThrow();
            }
        }

        public void Configure(GameRules rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            // 不正な値に投げるまで気づかないことがないよう、状態を変える前に作って確かめる。
            var inventory = new GrenadeInventory(Catalog, rules.Actions);
            _actions = rules.Actions;
            SetInventory(inventory);
            _rule = rules.Actions.Grenade;
            _configured = true;
            _throwRemaining = 0f;
            BindBody(GetComponent<Damageable>());

            if (UsesGrenades)
            {
                ResolveAction();
                if (Application.isPlaying && isActiveAndEnabled)
                {
                    EnableAction();
                }
            }
            else
            {
                DisableAction();
                _action = null;
                _switchAction = null;
            }

            Debug.Log($"{LogTag} rule={_rule} count={_inventory.SelectedCapacity} types={_inventory.Count}", this);
            Changed?.Invoke(this);
        }

        public void Refill()
        {
            _inventory.Refill();
        }

        public bool SelectNext()
        {
            if (!_inventory.SelectNext())
            {
                return false;
            }

            Debug.Log($"{LogTag} selected type={_inventory.Selected.LogName} remaining={_inventory.SelectedRemaining}", this);
            return true;
        }

        public void Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            _throwRemaining = Mathf.Max(0f, _throwRemaining - deltaSeconds);
        }

        public bool CanThrow =>
            UsesGrenades
            && _inventory.SelectedRemaining > 0
            && !IsThrowing
            && (ThrowBlocker == null || !ThrowBlocker())
            && (_body == null || !_body.IsDead);

        public bool TryThrow()
        {
            if (!CanThrow)
            {
                return false;
            }

            var spec = Spec;
            ResolveRelease(transform, spec, out var position, out var direction);
            if (!_inventory.TryTake(_inventory.SelectedIndex))
            {
                return false;
            }

            _throwRemaining = spec.ThrowSeconds;
            var velocity = LaunchVelocity(direction, spec.ThrowSpeed, spec.ThrowLiftSpeed);
            var grenade = ThrownGrenade.Spawn(gameObject, position, velocity, spec, _grenadePrefab);
            Debug.Log($"{LogTag} thrown remaining={_inventory.SelectedRemaining} type={spec.LogName}", this);
            Thrown?.Invoke(grenade);
            return true;
        }

        public void ResolveThrow(out Vector3 position, out Vector3 direction)
        {
            ResolveRelease(transform, Spec, out position, out direction);
        }

        // 注意をそらす物 (DistractionThrowAbility) もグレネードと同じ所から同じ弧で投げるよう、本体だけから求められる形で公開する。
        public static void ResolveRelease(Transform body, GrenadeSpec spec, out Vector3 position, out Vector3 direction)
        {
            if (body == null)
            {
                throw new ArgumentNullException(nameof(body));
            }

            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            var rig = body.GetComponent<ViewRig>();
            Vector3 origin;
            if (rig != null && rig.IsInitialized)
            {
                origin = rig.Eye.position;
                direction = rig.Camera.transform.forward;
            }
            else
            {
                origin = body.position;
                direction = body.forward;
            }

            direction = direction.sqrMagnitude > 1e-8f ? direction.normalized : Vector3.forward;
            var distance = spec.ReleaseDistance;
            if (TryNearestObstacle(body, origin, direction, spec.ReleaseDistance + spec.BodyRadius, out var hitDistance))
            {
                distance = Mathf.Max(0f, hitDistance - spec.BodyRadius);
            }

            position = origin + (direction * distance);
        }

        public static Vector3 LaunchVelocity(Vector3 direction, float speed, float liftSpeed)
        {
            return (direction * speed) + (Vector3.up * liftSpeed);
        }

        private static bool TryNearestObstacle(Transform self, Vector3 origin, Vector3 direction, float maxDistance, out float distance)
        {
            distance = 0f;
            var found = false;
            foreach (var hit in Physics.RaycastAll(origin, direction, maxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(self))
                {
                    continue;
                }

                if (!found || hit.distance < distance)
                {
                    distance = hit.distance;
                    found = true;
                }
            }

            return found;
        }

        private void SetInventory(GrenadeInventory inventory)
        {
            _inventory.Changed -= OnInventoryChanged;
            _inventory = inventory;
            _inventory.Changed += OnInventoryChanged;
        }

        private void OnInventoryChanged()
        {
            Changed?.Invoke(this);
        }

        private void Rebuild()
        {
            if (!_configured)
            {
                return;
            }

            SetInventory(new GrenadeInventory(Catalog, _actions));
            Changed?.Invoke(this);
        }

        private void BindBody(Damageable body)
        {
            if (_body == body)
            {
                return;
            }

            if (_body != null)
            {
                _body.HealthReset -= OnHealthReset;
            }

            _body = body;
            if (_body != null)
            {
                _body.HealthReset += OnHealthReset;
            }
        }

        private void OnHealthReset(Health health)
        {
            _throwRemaining = 0f;
            Refill();
        }

        private void ResolveAction()
        {
            if (_action != null)
            {
                return;
            }

            var actions = InputSystem.actions;
            _action = actions != null ? actions.FindAction(ActionPath) : null;
            _switchAction = actions != null ? actions.FindAction(SwitchActionPath) : null;
            if (_action == null)
            {
                Debug.LogWarning(
                    $"{LogTag} Input Actions に '{ActionPath}' がないため、グレネードは入力からは投げられません。",
                    this);
            }
        }

        private void EnableAction()
        {
            // プロジェクト全体のアクションは他の部品と共有なので、自分が有効にしたときだけ後で無効に戻す。
            if (_action != null && !_action.enabled)
            {
                _action.Enable();
                _enabledAction = true;
            }

            if (_switchAction != null && !_switchAction.enabled)
            {
                _switchAction.Enable();
                _enabledSwitchAction = true;
            }
        }

        private void DisableAction()
        {
            if (_enabledAction && _action != null)
            {
                _action.Disable();
            }

            if (_enabledSwitchAction && _switchAction != null)
            {
                _switchAction.Disable();
            }

            _enabledAction = false;
            _enabledSwitchAction = false;
        }
    }
}
