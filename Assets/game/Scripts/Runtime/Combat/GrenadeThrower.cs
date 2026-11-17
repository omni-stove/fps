using System;
using Shooter.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.Runtime
{
    /// <summary>
    /// Grenade の入力 (<see cref="InputActionPaths.Grenade"/>) で、視線の向きにグレネード (<see cref="ThrownGrenade"/>) を投げる。
    /// プレイヤーと NetPlayer の本体に付ける (<see cref="CharacterComponents.Attach"/>)。
    /// 何個投げられるかはルール (<see cref="ActionRules.Grenade"/> と <see cref="ActionRules.GrenadeCount"/>) で決まり、
    /// <see cref="Configure"/> (オフラインは <see cref="PlayerActions.Attach"/> が呼ぶ) で受け取るまでは投げず、入力も読まない。
    /// 残りは同じ本体の <see cref="Damageable"/> の体力が作り直されたとき (リスポーン・チェックポイントからの再開) と、
    /// <see cref="Refill"/> (キャンペーンのチェックポイントを通ったとき) に満タンへ戻す。
    /// 持つ種類は <see cref="GrenadeCatalog.Carried"/> で、種類ごとに数を持ち、投げる種類を <see cref="InputActionPaths.SwitchGrenade"/> (<see cref="SelectNext"/>) で切り替える。
    /// Play 中に生まれたときは、先に <see cref="ApplySpec"/> か <see cref="ApplyCatalog"/> で渡されていなければ、調整値の JSON (<see cref="KitTuning.Grenades"/>) の種類を持つ。
    /// 種類を渡されていない間は、Inspector の値から作る 1 種類 (<see cref="CreateSpec"/>) だけを持つ。
    /// 投げてから <see cref="GrenadeSpec.ThrowSeconds"/> 秒の間は <see cref="IsThrowing"/> が true で、次を投げられず、
    /// <see cref="GameBootstrap"/> の配線で武器も撃てない。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GrenadeThrower : MonoBehaviour
    {
        public const string LogTag = "[Grenade]";

        /// <summary>Assets/InputSystem_Actions.inputactions の Grenade アクション (キーボードの G、ゲームパッドの左ショルダー)。</summary>
        public const string ActionPath = InputActionPaths.Grenade;

        /// <summary>Assets/InputSystem_Actions.inputactions の SwitchGrenade アクション (キーボードの K)。</summary>
        public const string SwitchActionPath = InputActionPaths.SwitchGrenade;

        /// <summary>キルログに出す武器の Id。</summary>
        public const string WeaponId = "grenade";

        /// <summary>キルログに出す武器の名前を引く、game 側の言語の表のキー。</summary>
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

        /// <summary>残りの数か、投げる種類か、投げられるかどうか (ルール) が変わったときに呼ばれる。HUD が読み直す。</summary>
        public event Action<GrenadeThrower> Changed;

        /// <summary>投げたときに、投げたグレネードを渡して呼ばれる。</summary>
        public event Action<ThrownGrenade> Thrown;

        /// <summary>今のルールでグレネードを使うか。<see cref="Configure"/> の前は <see cref="Grenade.Off"/>。</summary>
        public Grenade Rule => _rule;

        /// <summary>今のルールでグレネードを使うなら true (残りが 0 でも true)。</summary>
        public bool UsesGrenades => _rule == Grenade.On;

        /// <summary>投げる種類の持てる数。</summary>
        public int Capacity => _inventory.SelectedCapacity;

        /// <summary>投げる種類の残りの数。</summary>
        public int Remaining => _inventory.SelectedRemaining;

        /// <summary>持っている種類と、種類ごとの残り。<see cref="Configure"/> の前は空。</summary>
        public GrenadeInventory Inventory => _inventory;

        /// <summary>持つ種類の並び。渡されていなければ Inspector の値の 1 種類。</summary>
        public GrenadeCatalog Catalog => _catalog ?? GrenadeCatalog.Of(InspectorSpec);

        /// <summary>投げている最中 (投げてから <see cref="GrenadeSpec.ThrowSeconds"/> 秒の間) か。この間は撃てず、次も投げられない。</summary>
        public bool IsThrowing => _throwRemaining > 0f;

        /// <summary>true を返す間は投げない (回避・乗り越えの最中など)。null なら止めない。オフラインは <see cref="GameBootstrap"/> が配線する。</summary>
        public Func<bool> ThrowBlocker { get; set; }

        /// <summary>投げる入力。<see cref="Configure"/> で On にしたときに探す。見つからなければ null (<see cref="TryThrow"/> でだけ投げる)。</summary>
        public InputAction Action => _action;

        /// <summary>
        /// 次に投げる種類の <see cref="GrenadeSpec"/>。種類を渡されていなければ Inspector の値から作る 1 種類 (初めて読んだときと Inspector で値を変えた後に
        /// <see cref="CreateSpec"/> で作り直す)。値が不正なら ArgumentOutOfRangeException。
        /// </summary>
        public GrenadeSpec Spec => _catalog != null && _inventory.Selected != null ? _inventory.Selected : InspectorSpec;

        private GrenadeSpec InspectorSpec => _spec ?? (_spec = CreateSpec());

        /// <summary>投げたグレネードの見た目 (<see cref="ThrownGrenade.Spawn(GameObject, Vector3, Vector3, float, ExplosionDamage, int, GameObject)"/>)。null なら仮の球。</summary>
        public GameObject GrenadePrefab
        {
            get => _grenadePrefab;
            set => _grenadePrefab = value;
        }

        /// <summary>シリアライズされた値から <see cref="GrenadeSpec"/> を作る。値が不正なら ArgumentOutOfRangeException。</summary>
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

        /// <summary>
        /// spec の値をシリアライズされる値に書き込み、以後はそれで投げる。Play 中のコードとテストが差し込むのに使う。
        /// Play の外で書き込んだ値は、Play 中に生まれたときに調整値の JSON で上書きされる。飛んでいるグレネードは変えない。
        /// </summary>
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

            // 同じ種類の値を変えただけなら、残りの数はそのままにする
            if (_inventory.Count != 1 || _inventory.IndexOf(spec.Id) < 0)
            {
                Rebuild();
            }
        }

        /// <summary>
        /// catalog の種類を持ち、以後は種類ごとの値で投げる。Inspector の値は使わなくなる。
        /// <see cref="Configure"/> の後なら、持つ数を満タンにして持ち直す。飛んでいるグレネードは変えない。
        /// </summary>
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

        /// <summary>
        /// rules の <see cref="GameRules.Actions"/> で投げられるかと数を決め、残りを満タンにする。
        /// On なら入力を探して (Play 中なら) 有効にし、Off なら入力を手放す。同じ本体の <see cref="Damageable"/> の体力の作り直しを購読する。
        /// 何度呼んでも、最後に渡した rules に従う。Inspector の値が不正 (<see cref="CreateSpec"/>) なら何も変えずに ArgumentOutOfRangeException。
        /// </summary>
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

        /// <summary>残りを持てる数に戻す。</summary>
        public void Refill()
        {
            _inventory.Refill();
        }

        /// <summary>投げる種類を、持っている次の種類にする。持つ種類が 1 つ以下なら何もせず false。</summary>
        public bool SelectNext()
        {
            if (!_inventory.SelectNext())
            {
                return false;
            }

            Debug.Log($"{LogTag} selected type={_inventory.Selected.LogName} remaining={_inventory.SelectedRemaining}", this);
            return true;
        }

        /// <summary>投げている最中の残り時間を deltaSeconds 進める。Play 中は Update が呼ぶ。</summary>
        public void Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            _throwRemaining = Mathf.Max(0f, _throwRemaining - deltaSeconds);
        }

        /// <summary>
        /// 今投げられるか。ルールが On・残りがある・投げている最中でない・<see cref="ThrowBlocker"/> が止めていない・
        /// 本体の <see cref="Damageable"/> が倒れていない、のすべてを満たせば true。
        /// </summary>
        public bool CanThrow =>
            UsesGrenades
            && _inventory.SelectedRemaining > 0
            && !IsThrowing
            && (ThrowBlocker == null || !ThrowBlocker())
            && (_body == null || !_body.IsDead);

        /// <summary>
        /// 投げられれば (<see cref="CanThrow"/>) 残りを 1 つ減らし、視線の向き (<see cref="ResolveThrow"/>) へ <see cref="Spec"/> のグレネードを投げて true。
        /// 投げられなければ何もせず false。Inspector の値が不正なら何も減らさずに ArgumentOutOfRangeException。
        /// </summary>
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

        /// <summary>
        /// 次に投げる種類 (<see cref="Spec"/>) のグレネードを出す位置と投げる向き。本体の <see cref="ViewRig"/> が初期化されていれば、目から見てカメラの正面の向きに
        /// <see cref="GrenadeSpec.ReleaseDistance"/> 進んだ所 (間に壁があれば壁の手前)。無ければ本体の位置から正面へ。
        /// </summary>
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

        // 自分の体を除いて、目と出す位置の間にある最も近い物までの距離。
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
