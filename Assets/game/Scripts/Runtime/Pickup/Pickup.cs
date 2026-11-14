using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fps.Runtime
{
    /// <summary>
    /// 拾える物。トリガーの球 (<see cref="Radius"/>) に <see cref="Damageable"/> を持つ人が入ったら (<see cref="PickupCollectMode.Touch"/>)、
    /// または範囲にいるプレイヤーが Interact を押したら (<see cref="PickupCollectMode.Interact"/>) 拾う。
    /// 拾えるかと何が起きるかは <see cref="PickupJudge"/> が決め、ここは拾った人の <see cref="IWeaponHolder"/> (プレイヤーの <see cref="WeaponController"/> か
    /// bot の <see cref="EnemyAgent"/>。持ち武器・予備弾)・
    /// <see cref="Damageable"/> (体力)・<see cref="IArmorHolder"/> (防具) に反映して <c>[Pickup]</c> のログを出し、<see cref="Collected"/> を呼ぶ。
    /// タグは何にも反映せず、<see cref="Collected"/> を受けたモードが得点などを決める。
    /// </summary>
    /// <remarks>
    /// 実行時に置くときは <see cref="Create"/> を使う (当たり判定と見た目もコードで作る)。Scene に置くなら <see cref="PickupSpawnPoint"/> を使う。
    /// 当たり判定は動かない Rigidbody (kinematic) を付けたトリガーなので、Rigidbody を持たない bot のコライダーでも入ったことが分かる。
    /// 射撃や視線の Raycast はトリガーを無視するので、拾える物は弾を遮らない。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class Pickup : MonoBehaviour
    {
        public const string LogTag = PickupLog.Tag;

        /// <summary>Assets/InputSystem_Actions.inputactions の Interact アクション。</summary>
        public const string InteractActionPath = InputActionPaths.Interact;

        /// <summary>拾える範囲の既定の半径 (m)。</summary>
        public const float DefaultRadius = 1f;

        /// <summary>範囲の半径の下限 (m)。</summary>
        public const float MinRadius = 0.1f;

        /// <summary><see cref="Create"/> が作る見た目の子の名前。</summary>
        public const string VisualName = "Visual";

        /// <summary>見た目の立方体の一辺 (m)。</summary>
        public const float VisualSize = 0.4f;

        /// <summary>見た目を浮かせる、足元からの高さ (m)。</summary>
        public const float VisualHeight = 0.6f;

        /// <summary>見た目が回る速さ (度/秒)。拾える物だと遠くから分かるように回す。</summary>
        public const float SpinDegreesPerSecond = 90f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField, Tooltip("中身")]
        private PickupContents _contents = PickupContents.ForHealth(PickupContents.DefaultHealthAmount);

        [SerializeField, Tooltip("拾い方")]
        private PickupCollectMode _collectMode = PickupCollectMode.Touch;

        [SerializeField, Min(MinRadius), Tooltip("拾える範囲の半径 (m)")]
        private float _radius = DefaultRadius;

        [SerializeField, Tooltip("拾われたら破棄する。false なら無効にするだけ (PickupSpawnPoint が再び出す)")]
        private bool _destroyOnCollect = true;

        [SerializeField, Min(0f), Tooltip("置いてから消えるまでの秒数。0 なら消えない")]
        private float _lifetimeSeconds;

        [SerializeField, Tooltip("見た目。原点を見た目の中心にする (足元から浮かせて回す)。大きさと材質はプレハブのまま、当たり判定は外す。空なら中身の種類で色を分けた仮の箱を作る")]
        private GameObject _visualPrefab;

        private readonly HashSet<Collider> _inside = new HashSet<Collider>();
        private readonly List<Collider> _insideBuffer = new List<Collider>();
        private readonly HashSet<Damageable> _triedThisTick = new HashSet<Damageable>();
        private PickupSpec _spec;
        private bool _collected;
        private float _age;
        private Transform _visual;
        private InputAction _interactAction;
        private bool _enabledInteractAction;
        private bool _reportedInvalid;

        /// <summary>拾われたときに呼ばれる。<see cref="DestroyOnCollect"/> なら、呼んだ後に破棄する。</summary>
        public event Action<PickupCollection> Collected;

        /// <summary>中身。変えると次に拾うときから効く。null は <see cref="ArgumentNullException"/>、値が不正なら <see cref="ArgumentException"/>。</summary>
        public PickupContents Contents
        {
            get => _contents;
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                _spec = value.ToSpec();
                _contents = value;
                _reportedInvalid = false;
            }
        }

        /// <summary>中身の Core の表し。<see cref="Contents"/> が不正なら <see cref="ArgumentException"/>。</summary>
        public PickupSpec Spec => _spec ?? (_spec = (_contents ?? throw new InvalidOperationException($"Pickup '{name}' has no contents")).ToSpec());

        public PickupCollectMode CollectMode
        {
            get => _collectMode;
            set
            {
                _collectMode = value;
                if (isActiveAndEnabled)
                {
                    RefreshInteractAction();
                }
            }
        }

        /// <summary>拾える範囲の半径 (m)。<see cref="MinRadius"/> 未満は切り上げる。トリガーの球にもすぐ反映する。</summary>
        public float Radius
        {
            get => _radius;
            set
            {
                _radius = Mathf.Max(MinRadius, value);
                ApplyRadius();
            }
        }

        /// <summary>拾われたら破棄するか。false なら無効にするだけで、<see cref="Restock"/> で戻せる。</summary>
        public bool DestroyOnCollect
        {
            get => _destroyOnCollect;
            set => _destroyOnCollect = value;
        }

        /// <summary>置いてから消えるまでの秒数。0 なら消えない。時間は <see cref="Tick"/> で進む。負の値は 0 にする。</summary>
        public float LifetimeSeconds
        {
            get => _lifetimeSeconds;
            set => _lifetimeSeconds = Mathf.Max(0f, value);
        }

        /// <summary>見た目。null なら中身の種類で色を分けた仮の箱。見た目を作る (Awake か <see cref="Create"/>) 前に設定したものを使う。</summary>
        public GameObject VisualPrefab
        {
            get => _visualPrefab;
            set => _visualPrefab = value;
        }

        /// <summary>まだ拾われていなければ true。</summary>
        public bool IsAvailable => !_collected;

        /// <summary>範囲に入っている人のコライダーの数。</summary>
        public int InsideCount => _inside.Count;

        /// <summary>今使っている Interact アクション。<see cref="PickupCollectMode.Interact"/> で Play 中でなければ null。</summary>
        public InputAction InteractAction => _interactAction;

        /// <summary>
        /// contents の拾える物を groundPosition (足元) に作る。トリガーの球・動かない Rigidbody・見た目 (visualPrefab か、中身の種類で色を分けた仮の箱) を付ける。
        /// contents は写しを持つので、後で書き換えても作った物は変わらない。contents が不正なら何も作らずに <see cref="ArgumentException"/>。
        /// </summary>
        /// <param name="parent">親。null ならシーンの直下。</param>
        /// <param name="visualPrefab">見た目 (<see cref="VisualPrefab"/>)。null なら仮の箱。</param>
        public static Pickup Create(
            PickupContents contents,
            Vector3 groundPosition,
            PickupCollectMode collectMode = PickupCollectMode.Touch,
            Transform parent = null,
            float radius = DefaultRadius,
            GameObject visualPrefab = null)
        {
            if (contents == null)
            {
                throw new ArgumentNullException(nameof(contents));
            }

            var copy = contents.Clone();
            var spec = copy.ToSpec();

            var go = new GameObject(ObjectName(spec.Kind));
            try
            {
                // Awake で既定の中身のまま購読や検査をしないよう、中身を入れ終えるまで無効にしておく。
                go.SetActive(false);
                if (parent != null)
                {
                    go.transform.SetParent(parent, false);
                }

                go.transform.SetPositionAndRotation(groundPosition, Quaternion.identity);
                var pickup = go.AddComponent<Pickup>();
                pickup._contents = copy;
                pickup._spec = spec;
                pickup._collectMode = collectMode;
                pickup._radius = Mathf.Max(MinRadius, radius);
                pickup._visualPrefab = visualPrefab;
                pickup.EnsureParts();
                go.SetActive(true);
                return pickup;
            }
            catch
            {
                DestroySafely(go);
                throw;
            }
        }

        /// <summary>中身の種類ごとの GameObject の名前。</summary>
        public static string ObjectName(PickupKind kind)
        {
            return "Pickup-" + kind;
        }

        /// <summary>シーンにある有効な物のうち、まだ拾われていない物 (順番は決まっていない)。bot が取りに行く物を選ぶのに使う。</summary>
        public static List<Pickup> FindActive()
        {
            var found = FindObjectsByType<Pickup>(FindObjectsInactive.Exclude);
            var active = new List<Pickup>(found.Length);
            foreach (var pickup in found)
            {
                if (pickup != null && pickup.isActiveAndEnabled && pickup.IsAvailable)
                {
                    active.Add(pickup);
                }
            }

            return active;
        }

        private void Awake()
        {
            EnsureParts();
            if (!TryResolveSpec())
            {
                enabled = false;
            }
        }

        private void OnEnable()
        {
            RefreshInteractAction();
        }

        private void OnDisable()
        {
            ReleaseInteractAction();

            // 無効の間はトリガーの出入りが届かないので、戻ったときに入り直しとして数え直す。
            _inside.Clear();
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private void OnTriggerEnter(Collider other)
        {
            HandleEnter(other);
        }

        private void OnTriggerExit(Collider other)
        {
            HandleExit(other);
        }

        /// <summary>
        /// 時間を deltaSeconds 進める。見た目を回し、<see cref="LifetimeSeconds"/> を過ぎたら消す。
        /// <see cref="PickupCollectMode.Touch"/> なら範囲にいる人がまた拾えるようになったかを確かめる (拾えなくてもログは出さない)。
        /// </summary>
        public void Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            if (_collected)
            {
                return;
            }

            if (_visual != null)
            {
                _visual.Rotate(0f, SpinDegreesPerSecond * deltaSeconds, 0f, Space.World);
            }

            if (_lifetimeSeconds > 0f)
            {
                _age += deltaSeconds;
                if (_age >= _lifetimeSeconds)
                {
                    Debug.Log(PickupLog.Expired(name), this);
                    _collected = true;
                    DestroySafely(gameObject);
                    return;
                }
            }

            if (_collectMode == PickupCollectMode.Touch && _inside.Count > 0)
            {
                TryCollectInside();
            }
        }

        /// <summary>
        /// other が範囲に入った。<see cref="Damageable"/> を持つ人のコライダーなら数え、<see cref="PickupCollectMode.Touch"/> なら拾ってみる。
        /// その人の最初のコライダーで拾えなければ、生きていれば理由をログに出す。
        /// </summary>
        public void HandleEnter(Collider other)
        {
            if (other == null || _collected)
            {
                return;
            }

            var collector = CollectorOf(other);
            if (collector == null)
            {
                return;
            }

            var firstCollider = !IsInside(collector);
            if (!_inside.Add(other))
            {
                return;
            }

            if (_collectMode == PickupCollectMode.Touch)
            {
                TryCollect(collector, out _, logRejection: firstCollider);
            }
        }

        /// <summary>other が範囲から出た。</summary>
        public void HandleExit(Collider other)
        {
            if (other != null)
            {
                _inside.Remove(other);
            }
        }

        /// <summary>
        /// 範囲にいるこの画面のプレイヤー (<see cref="Damageable.IsLocalPlayer"/>) が拾う。拾えたら true。
        /// <see cref="PickupCollectMode.Interact"/> の入力から呼ばれる。拾い方に関わらずコードからも呼べる。
        /// </summary>
        public bool Interact()
        {
            if (_collected)
            {
                return false;
            }

            foreach (var collider in SnapshotInside())
            {
                var collector = CollectorOf(collider);
                if (collector != null && collector.IsLocalPlayer)
                {
                    return TryCollect(collector, out _);
                }
            }

            return false;
        }

        /// <summary>
        /// collector に拾わせてみる。拾えたら反映して <c>[Pickup] collected</c> を出し、<see cref="Collected"/> を呼んで、
        /// 破棄するか無効にして true を返す。拾えなければ outcome に理由を入れて false (logRejection で、生きている人なら理由をログに出す)。
        /// 拾えると判定したのに反映できなければ (武器の定義が無いなど) エラーを出し、<see cref="PickupRejection.ApplyFailed"/> で false。
        /// 拾われた後は <see cref="PickupRejection.Gone"/> で false。
        /// </summary>
        public bool TryCollect(Damageable collector, out PickupOutcome outcome, bool logRejection = true)
        {
            if (collector == null)
            {
                throw new ArgumentNullException(nameof(collector));
            }

            if (!TryResolveSpec())
            {
                outcome = PickupOutcome.Rejected(_contents != null ? _contents.Kind : PickupKind.Health, PickupRejection.ApplyFailed);
                return false;
            }

            var spec = _spec;
            if (_collected)
            {
                outcome = PickupOutcome.Rejected(spec.Kind, PickupRejection.Gone);
                return false;
            }

            var weapons = WeaponsOf(collector);
            var armor = collector.GetComponent<IArmorHolder>();
            outcome = PickupJudge.Judge(spec, Describe(collector, weapons, armor));
            if (!outcome.IsCollected)
            {
                if (logRejection && outcome.Rejection != PickupRejection.CollectorDown)
                {
                    Debug.Log(PickupLog.Rejected(name, collector.Id, outcome), this);
                }

                return false;
            }

            if (!TryApply(outcome, collector, weapons, armor, out var failure))
            {
                Debug.LogError(PickupLog.Failed(name, collector.Id, spec.Kind, failure), this);
                outcome = PickupOutcome.Rejected(spec.Kind, PickupRejection.ApplyFailed);
                return false;
            }

            _collected = true;
            _inside.Clear();
            Debug.Log(PickupLog.Collected(name, collector.Id, outcome), this);
            Collected?.Invoke(new PickupCollection(this, collector, spec, outcome));

            if (_destroyOnCollect)
            {
                DestroySafely(gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }

            return true;
        }

        /// <summary>拾われた物をまた拾えるようにする (<see cref="PickupSpawnPoint"/> が再び出すときに呼ぶ)。有効にはしない。</summary>
        public void Restock()
        {
            _collected = false;
            _age = 0f;
            _inside.Clear();
        }

        /// <summary>
        /// collector の今の状態を、拾う判定に要る形にする。陣営は <see cref="SideMember"/>、武器は子の初期化済みの <see cref="WeaponController"/>
        /// (無ければ同じ GameObject の初期化済みの <see cref="EnemyAgent"/>)、防具は同じ GameObject の <see cref="IArmorHolder"/> から読む。
        /// どれも無ければ陣営なし・武器を持てない・防具を持てない人にする。武器を持っていない (<see cref="WeaponController.IsUnarmed"/>) 人は、
        /// 武器を持てるが 1 本も持っていない人にする。
        /// </summary>
        public static PickupCollector Describe(Damageable collector)
        {
            if (collector == null)
            {
                throw new ArgumentNullException(nameof(collector));
            }

            return Describe(collector, WeaponsOf(collector), collector.GetComponent<IArmorHolder>());
        }

        private static PickupCollector Describe(Damageable collector, IWeaponHolder weapons, IArmorHolder armor)
        {
            var side = SideMember.Of(collector.gameObject);
            var health = collector.Health;
            var current = collector.IsDowned ? 0 : health.Current;
            var state = new PickupCollector(side != null ? side.Side : (Side?)null, current, health.Max);

            if (weapons != null)
            {
                var list = weapons.DescribeWeapons(out var currentWeaponId);
                state = state.WithWeapons(list, currentWeaponId);
            }

            // 上限が 0 以下の実装は防具を持てないものとして扱う (Core の検査で例外にしない)。
            if (armor != null && armor.MaxArmor > 0)
            {
                state = state.WithArmor(Mathf.Clamp(armor.Armor, 0, armor.MaxArmor), armor.MaxArmor);
            }

            return state;
        }

        private static IWeaponHolder WeaponsOf(Damageable collector)
        {
            var weapons = collector.GetComponentInChildren<WeaponController>(true);
            if (weapons != null && weapons.IsInitialized)
            {
                return weapons;
            }

            var agent = collector.GetComponent<EnemyAgent>();
            return agent != null && agent.IsInitialized ? agent : null;
        }

        private bool TryApply(PickupOutcome outcome, Damageable collector, IWeaponHolder weapons, IArmorHolder armor, out string failure)
        {
            failure = null;
            switch (outcome.Kind)
            {
                case PickupKind.Weapon when outcome.AddsWeapon:
                    var definition = _contents.Weapon;
                    if (definition == null)
                    {
                        failure = "no WeaponDefinition";
                        return false;
                    }

                    try
                    {
                        if (!weapons.AddWeapon(definition))
                        {
                            failure = $"already holds weapon '{definition.Id}'";
                            return false;
                        }
                    }
                    catch (WeaponDefinitionException e)
                    {
                        failure = e.Message;
                        return false;
                    }

                    return true;
                case PickupKind.Weapon:
                case PickupKind.ReserveAmmo:
                    if (weapons.AddReserveAmmo(outcome.WeaponId, outcome.ReserveAmmo) <= 0)
                    {
                        failure = $"could not add ammo to weapon '{outcome.WeaponId}'";
                        return false;
                    }

                    return true;
                case PickupKind.Health:
                    collector.Health.Heal(outcome.Health);
                    return true;
                case PickupKind.Armor:
                    armor.AddArmor(outcome.Armor);
                    return true;
                default:
                    // タグはモードが Collected を受けて扱うので、拾う人には何も反映しない。
                    return true;
            }
        }

        private void TryCollectInside()
        {
            _triedThisTick.Clear();
            foreach (var collider in SnapshotInside())
            {
                var collector = CollectorOf(collider);
                if (collector == null || !_triedThisTick.Add(collector))
                {
                    continue;
                }

                if (TryCollect(collector, out _, logRejection: false) || _collected)
                {
                    return;
                }
            }
        }

        // 拾った処理の途中 (Collected の購読者など) で集合が変わっても回し続けられるよう写す。破棄されたコライダーはここで外す。
        private List<Collider> SnapshotInside()
        {
            _inside.RemoveWhere(collider => collider == null);
            _insideBuffer.Clear();
            _insideBuffer.AddRange(_inside);
            return _insideBuffer;
        }

        private bool IsInside(Damageable collector)
        {
            foreach (var collider in _inside)
            {
                if (collider != null && CollectorOf(collider) == collector)
                {
                    return true;
                }
            }

            return false;
        }

        private static Damageable CollectorOf(Collider collider)
        {
            return collider != null ? collider.GetComponentInParent<Damageable>() : null;
        }

        private bool TryResolveSpec()
        {
            if (_spec != null)
            {
                return true;
            }

            try
            {
                _ = Spec;
                return true;
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
            {
                if (!_reportedInvalid)
                {
                    _reportedInvalid = true;
                    Debug.LogError($"{LogTag} '{name}' の中身が不正なため拾えません: {e.Message}", this);
                }

                return false;
            }
        }

        private void EnsureParts()
        {
            var trigger = GetComponent<SphereCollider>();
            if (trigger == null)
            {
                trigger = gameObject.AddComponent<SphereCollider>();
            }

            trigger.isTrigger = true;
            ApplyRadius();

            var body = GetComponent<Rigidbody>();
            if (body == null)
            {
                body = gameObject.AddComponent<Rigidbody>();
            }

            body.isKinematic = true;
            body.useGravity = false;

            _visual = transform.Find(VisualName);
            if (_visual == null && _contents != null)
            {
                _visual = CreateVisual(_contents.Kind);
            }
        }

        private void ApplyRadius()
        {
            var trigger = GetComponent<SphereCollider>();
            if (trigger == null)
            {
                return;
            }

            trigger.radius = _radius;

            // 足元から球が立つように、中心を半径だけ上げる。
            trigger.center = new Vector3(0f, _radius, 0f);
        }

        private Transform CreateVisual(PickupKind kind)
        {
            if (_visualPrefab != null)
            {
                // 拾う範囲はトリガーの球で決めるので、見た目の当たり判定は外す (残すと固い物として移動と弾を遮る)。
                var instance = Fps.Runtime.VisualPrefab.Instantiate(_visualPrefab, transform, VisualName, stripColliders: true);
                instance.transform.localPosition = new Vector3(0f, VisualHeight, 0f);
                return instance.transform;
            }

            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = VisualName;

            // Destroy は次のフレームまで残り、その間に物理に固い箱として現れるので、すぐに外す。
            DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(transform, false);
            visual.transform.localPosition = new Vector3(0f, VisualHeight, 0f);
            visual.transform.localScale = Vector3.one * VisualSize;

            var renderer = visual.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // 共有マテリアルを塗り替えると他のプリミティブまで色が変わるので、この物だけの上書きで色を付ける。
            var block = new MaterialPropertyBlock();
            var color = ColorOf(kind);
            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);
            renderer.SetPropertyBlock(block);
            return visual.transform;
        }

        /// <summary>中身の種類ごとの見た目の色。</summary>
        public static Color ColorOf(PickupKind kind)
        {
            switch (kind)
            {
                case PickupKind.Weapon:
                    return new Color(0.75f, 0.75f, 0.8f);
                case PickupKind.ReserveAmmo:
                    return new Color(0.95f, 0.75f, 0.2f);
                case PickupKind.Health:
                    return new Color(0.3f, 0.9f, 0.4f);
                case PickupKind.Armor:
                    return new Color(0.3f, 0.6f, 1f);
                default:
                    return new Color(0.95f, 0.3f, 0.3f);
            }
        }

        private void RefreshInteractAction()
        {
            if (_collectMode != PickupCollectMode.Interact || !Application.isPlaying)
            {
                ReleaseInteractAction();
                return;
            }

            if (_interactAction != null)
            {
                return;
            }

            var actions = InputSystem.actions;
            if (actions == null)
            {
                Debug.LogError($"{LogTag} プロジェクト全体の Input Actions が設定されていないため、'{name}' は Interact で拾えません (Project Settings > Input System Package)。", this);
                return;
            }

            var action = actions.FindAction(InteractActionPath);
            if (action == null)
            {
                Debug.LogError($"{LogTag} Input Actions '{actions.name}' に '{InteractActionPath}' がないため、'{name}' は Interact で拾えません。", this);
                return;
            }

            _interactAction = action;
            _interactAction.performed += OnInteractPerformed;

            // プロジェクト全体のアクションは他の部品と共有なので、自分が有効にしたときだけ後で無効に戻す。
            if (!_interactAction.enabled)
            {
                _interactAction.Enable();
                _enabledInteractAction = true;
            }
        }

        private void ReleaseInteractAction()
        {
            if (_interactAction == null)
            {
                return;
            }

            _interactAction.performed -= OnInteractPerformed;
            if (_enabledInteractAction)
            {
                _interactAction.Disable();
            }

            _enabledInteractAction = false;
            _interactAction = null;
        }

        private void OnInteractPerformed(InputAction.CallbackContext context)
        {
            Interact();
        }

        private static void DestroySafely(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
