using System;
using Fps.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fps.Runtime
{
    /// <summary>
    /// Grenade の入力 (<see cref="InputActionPaths.Grenade"/>) で、視線の向きにグレネード (<see cref="ThrownGrenade"/>) を投げる。
    /// プレイヤーと NetPlayer の本体に付ける (<see cref="CharacterComponents.Attach"/>)。
    /// 何個投げられるかはルール (<see cref="ActionRules.Grenade"/> と <see cref="ActionRules.GrenadeCount"/>) で決まり、
    /// <see cref="Configure"/> (オフラインは <see cref="PlayerActions.Attach"/> が呼ぶ) で受け取るまでは投げず、入力も読まない。
    /// 残りは同じ本体の <see cref="Damageable"/> の体力が作り直されたとき (リスポーン・チェックポイントからの再開) と、
    /// <see cref="Refill"/> (キャンペーンのチェックポイントを通ったとき) に満タンへ戻す。
    /// 信管・初速・爆発の威力と半径は Inspector の値から作る <see cref="Spec"/> (<see cref="GrenadeSpec"/>) に従う。
    /// 投げてから <see cref="GrenadeSpec.ThrowSeconds"/> 秒の間は <see cref="IsThrowing"/> が true で、次を投げられず、
    /// <see cref="GameBootstrap"/> の配線で武器も撃てない。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GrenadeThrower : MonoBehaviour
    {
        public const string LogTag = "[Grenade]";

        /// <summary>Assets/InputSystem_Actions.inputactions の Grenade アクション (キーボードの G、ゲームパッドの左ショルダー)。</summary>
        public const string ActionPath = InputActionPaths.Grenade;

        /// <summary>キルログに出す武器の Id。</summary>
        public const string WeaponId = "grenade";

        /// <summary>キルログに出す武器の名前 (今の言語)。</summary>
        public static string DisplayName => Localizer.Get("weapon.grenade");

        /// <summary>目から視線の向きへ、グレネードを出す位置までの距離 (m)。体のカプセル (半径 0.5m) の外に出す。</summary>
        public const float ReleaseDistance = 0.7f;

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

        // 投げるたびに検証し直さないよう持っておき、OnValidate で捨てる。
        private GrenadeSpec _spec;
        private GrenadePouch _pouch = new GrenadePouch(0);
        private Grenade _rule = Grenade.Off;
        private bool _configured;
        private Damageable _body;
        private InputAction _action;
        private bool _enabledAction;
        private float _throwRemaining;

        /// <summary>残りの数か、投げられるかどうか (ルール) が変わったときに呼ばれる。HUD が読み直す。</summary>
        public event Action<GrenadeThrower> Changed;

        /// <summary>投げたときに、投げたグレネードを渡して呼ばれる。</summary>
        public event Action<ThrownGrenade> Thrown;

        /// <summary>今のルールでグレネードを使うか。<see cref="Configure"/> の前は <see cref="Grenade.Off"/>。</summary>
        public Grenade Rule => _rule;

        /// <summary>今のルールでグレネードを使うなら true (残りが 0 でも true)。</summary>
        public bool UsesGrenades => _rule == Grenade.On;

        /// <summary>持てる数。</summary>
        public int Capacity => _pouch.Capacity;

        /// <summary>残りの数。</summary>
        public int Remaining => _pouch.Remaining;

        /// <summary>投げている最中 (投げてから <see cref="GrenadeSpec.ThrowSeconds"/> 秒の間) か。この間は撃てず、次も投げられない。</summary>
        public bool IsThrowing => _throwRemaining > 0f;

        /// <summary>true を返す間は投げない (回避・乗り越えの最中など)。null なら止めない。オフラインは <see cref="GameBootstrap"/> が配線する。</summary>
        public Func<bool> ThrowBlocker { get; set; }

        /// <summary>投げる入力。<see cref="Configure"/> で On にしたときに探す。見つからなければ null (<see cref="TryThrow"/> でだけ投げる)。</summary>
        public InputAction Action => _action;

        /// <summary>
        /// 今の値で投げるときの <see cref="GrenadeSpec"/>。初めて読んだときと Inspector で値を変えた後に <see cref="CreateSpec"/> で作り直す。
        /// 値が不正なら ArgumentOutOfRangeException。
        /// </summary>
        public GrenadeSpec Spec => _spec ?? (_spec = CreateSpec());

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
        /// spec の値をシリアライズされる値に書き込み、以後はそれで投げる。シーン・プレハブの生成スクリプトとテストが差し込むのに使う。
        /// 飛んでいるグレネードは変えない。
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
            _ = Spec;
            SetPouch(GrenadePouch.For(rules.Actions));
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
            }

            Debug.Log($"{LogTag} rule={_rule} count={_pouch.Capacity}", this);
            Changed?.Invoke(this);
        }

        /// <summary>残りを持てる数に戻す。</summary>
        public void Refill()
        {
            _pouch.Refill();
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
            && _pouch.Remaining > 0
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
            ResolveThrow(out var position, out var direction);
            if (!_pouch.TryTake())
            {
                return false;
            }

            _throwRemaining = spec.ThrowSeconds;
            var velocity = (direction * spec.ThrowSpeed) + (Vector3.up * spec.ThrowLiftSpeed);
            var grenade = ThrownGrenade.Spawn(gameObject, position, velocity, spec.FuseSeconds, spec.Explosion, spec.StructureDamageAtCenter);
            Debug.Log($"{LogTag} thrown remaining={_pouch.Remaining}", this);
            Thrown?.Invoke(grenade);
            return true;
        }

        /// <summary>
        /// グレネードを出す位置と投げる向き。本体の <see cref="ViewRig"/> が初期化されていれば、目から見てカメラの正面の向きに
        /// <see cref="ReleaseDistance"/> 進んだ所 (間に壁があれば壁の手前)。無ければ本体の位置から正面へ。
        /// </summary>
        public void ResolveThrow(out Vector3 position, out Vector3 direction)
        {
            var rig = GetComponent<ViewRig>();
            Vector3 origin;
            if (rig != null && rig.IsInitialized)
            {
                origin = rig.Eye.position;
                direction = rig.Camera.transform.forward;
            }
            else
            {
                origin = transform.position;
                direction = transform.forward;
            }

            direction = direction.sqrMagnitude > 1e-8f ? direction.normalized : Vector3.forward;
            var distance = ReleaseDistance;
            if (TryNearestObstacle(origin, direction, ReleaseDistance + ThrownGrenade.BodyRadius, out var hitDistance))
            {
                distance = Mathf.Max(0f, hitDistance - ThrownGrenade.BodyRadius);
            }

            position = origin + (direction * distance);
        }

        // 自分の体を除いて、目と出す位置の間にある最も近い物までの距離。
        private bool TryNearestObstacle(Vector3 origin, Vector3 direction, float maxDistance, out float distance)
        {
            distance = 0f;
            var found = false;
            var self = transform;
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

        private void SetPouch(GrenadePouch pouch)
        {
            _pouch.Changed -= OnPouchChanged;
            _pouch = pouch;
            _pouch.Changed += OnPouchChanged;
        }

        private void OnPouchChanged(int remaining)
        {
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
        }

        private void DisableAction()
        {
            if (_enabledAction && _action != null)
            {
                _action.Disable();
            }

            _enabledAction = false;
        }
    }
}
