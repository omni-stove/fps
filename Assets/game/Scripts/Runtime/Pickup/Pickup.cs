using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.Runtime
{
    // 当たり判定を動かない Rigidbody (kinematic) を付けたトリガーにするのは、Rigidbody を持たない bot のコライダーでも入ったことが分かるようにするため。
    // 射撃や視線の Raycast はトリガーを無視するので、拾える物は弾を遮らない。
    [DisallowMultipleComponent]
    public sealed class Pickup : MonoBehaviour
    {
        public const string LogTag = PickupLog.Tag;

        public const string InteractActionPath = InputActionPaths.Interact;

        public const float DefaultRadius = 1f;

        public const float MinRadius = 0.1f;

        public const string VisualName = "Visual";

        public const float VisualSize = 0.4f;

        public const float VisualHeight = 0.6f;

        // 拾える物だと遠くから分かるように回す。Look では照準を合わせやすいよう回さない。
        public const float SpinDegreesPerSecond = 90f;

        public const float LyingRollDegrees = 90f;

        public const float LyingGap = 0.02f;

        // 三人称のカメラは体から離れているので、拾える距離は線の長さでなく範囲の球で決め、線は長めに伸ばす。
        public const float LookRayLength = 30f;

        // 足元に置いた銃は、正面を見たままでも目からこのくらい下に見える。
        public const float DefaultLookAngleDegrees = 90f;

        public const float MaxLookAngleDegrees = 180f;

        public const float DroppedWeaponOffset = 0.6f;

        public const string LookTargetName = "LookTarget";

        public static readonly Vector3 GunBodySize = new Vector3(0.07f, 0.12f, 0.45f);

        public static readonly Vector3 GunBarrelSize = new Vector3(0.035f, 0.035f, 0.3f);

        public static readonly Vector3 GunGripSize = new Vector3(0.05f, 0.14f, 0.07f);

        private const int LookHitCapacity = 16;

        private static readonly RaycastHit[] _lookHits = new RaycastHit[LookHitCapacity];
        private static int _lookFrame = -1;
        private static Damageable _lookViewer;
        private static Pickup _lookResult;

        // 照準の線が見た目に当たらないときの候補。誰かが範囲にいる物だけを持ち、Edit Mode でも HandleEnter から入る。
        private static readonly List<Pickup> _occupied = new List<Pickup>();

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField, Tooltip("中身")]
        private PickupContents _contents = PickupContents.ForHealth(PickupContents.DefaultHealthAmount);

        [SerializeField, Tooltip("拾い方")]
        private PickupCollectMode _collectMode = PickupCollectMode.Touch;

        [SerializeField, Min(MinRadius), Tooltip("拾える範囲の半径 (m)")]
        private float _radius = DefaultRadius;

        [SerializeField, Range(0f, MaxLookAngleDegrees), Tooltip("Look で、照準の向きからこの角度 (度) 以内に見た目が見えていれば照準が合っているとする。0 なら見た目に照準の線が当たったときだけ")]
        private float _lookAngleDegrees = DefaultLookAngleDegrees;

        [SerializeField, Tooltip("拾われたら破棄する。false なら無効にするだけ (PickupSpawnPoint が再び出す)")]
        private bool _destroyOnCollect = true;

        [SerializeField, Min(0f), Tooltip("置いてから消えるまでの秒数。0 なら消えない")]
        private float _lifetimeSeconds;

        [SerializeField, Tooltip("見た目。原点を見た目の中心にする (足元から浮かせて回す。Look なら寝かせて置く)。大きさと材質はプレハブのまま、当たり判定は外す。空なら武器はカタログの見た目か仮の銃、他は中身の種類で色を分けた仮の箱を作る")]
        private GameObject _visualPrefab;

        private readonly HashSet<Collider> _inside = new HashSet<Collider>();
        private readonly List<Collider> _insideBuffer = new List<Collider>();
        private readonly HashSet<Damageable> _triedThisTick = new HashSet<Damageable>();
        private InteractHintSlot _hint;
        private PickupSpec _spec;
        private bool _collected;
        private float _age;
        private Transform _visual;
        private BoxCollider _lookTarget;
        // シーンに置いたまま再コンパイルすると、Unity が private の値も保存し直し、null の InputAction を空のアクションとして戻してしまう (再生でシーンを読み直さないので、そのまま使われる)。
        [NonSerialized]
        private InputAction _interactAction;
        private bool _enabledInteractAction;
        private bool _reportedInvalid;
        private int _armedFrame = -1;

        public event Action<PickupCollection> Collected;

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

        public PickupSpec Spec => _spec ?? (_spec = (_contents ?? throw new InvalidOperationException($"Pickup '{name}' has no contents")).ToSpec());

        public PickupCollectMode CollectMode
        {
            get => _collectMode;
            set
            {
                _collectMode = value;
                ApplyVisualPose();
                if (isActiveAndEnabled)
                {
                    RefreshInteractAction();
                }
            }
        }

        public float Radius
        {
            get => _radius;
            set
            {
                _radius = Mathf.Max(MinRadius, value);
                ApplyRadius();
            }
        }

        public float LookAngleDegrees
        {
            get => _lookAngleDegrees;
            set => _lookAngleDegrees = ClampLookAngle(value);
        }

        public bool DestroyOnCollect
        {
            get => _destroyOnCollect;
            set => _destroyOnCollect = value;
        }

        public float LifetimeSeconds
        {
            get => _lifetimeSeconds;
            set => _lifetimeSeconds = Mathf.Max(0f, value);
        }

        public GameObject VisualPrefab
        {
            get => _visualPrefab;
            set => _visualPrefab = value;
        }

        public bool IsAvailable => !_collected;

        public int InsideCount => _inside.Count;

        public InputAction InteractAction => _interactAction;

        public static Pickup Create(
            PickupContents contents,
            Vector3 groundPosition,
            PickupCollectMode collectMode = PickupCollectMode.Touch,
            Transform parent = null,
            float radius = DefaultRadius,
            GameObject visualPrefab = null,
            float lookAngleDegrees = DefaultLookAngleDegrees)
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
                pickup._lookAngleDegrees = ClampLookAngle(lookAngleDegrees);
                pickup._visualPrefab = visualPrefab;
                pickup.EnsureParts();
                pickup._armedFrame = Time.frameCount;
                go.SetActive(true);
                return pickup;
            }
            catch
            {
                DestroySafely(go);
                throw;
            }
        }

        public static string ObjectName(PickupKind kind)
        {
            return "Pickup-" + kind;
        }

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
            HidePrompt();

            // 無効の間はトリガーの出入りが届かないので、戻ったときに入り直しとして数え直す。
            _inside.Clear();
            _occupied.Remove(this);
        }

        private void OnDestroy()
        {
            _occupied.Remove(this);
        }

        private void Update()
        {
            Tick(Time.deltaTime);
            UpdatePrompt();
        }

        private void OnTriggerEnter(Collider other)
        {
            HandleEnter(other);
        }

        private void OnTriggerExit(Collider other)
        {
            HandleExit(other);
        }

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

            if (_visual != null && _collectMode != PickupCollectMode.Look)
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

            if (!_occupied.Contains(this))
            {
                _occupied.Add(this);
            }

            if (_collectMode == PickupCollectMode.Touch)
            {
                TryCollect(collector, out _, logRejection: firstCollider);
            }
        }

        public void HandleExit(Collider other)
        {
            if (other != null)
            {
                _inside.Remove(other);
            }

            if (_inside.Count == 0)
            {
                _occupied.Remove(this);
            }
        }

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
                    if (_collectMode == PickupCollectMode.Look && !IsLookedAtBy(collector))
                    {
                        return false;
                    }

                    return TryCollect(collector, out _);
                }
            }

            return false;
        }

        public bool IsLookedAtBy(Damageable viewer)
        {
            return viewer != null && !_collected && _collectMode == PickupCollectMode.Look && LookedAt(viewer) == this;
        }

        public static Pickup LookedAt(Damageable viewer)
        {
            if (viewer == null)
            {
                throw new ArgumentNullException(nameof(viewer));
            }

            // 範囲にいる物がそれぞれ同じ線を引くので、Play 中は 1 フレームに 1 本だけ引いて使い回す。
            if (Application.isPlaying && _lookFrame == Time.frameCount && _lookViewer == viewer)
            {
                return _lookResult;
            }

            var view = ViewOf(viewer);
            Pickup result = null;
            if (view != null)
            {
                var hit = CastLook(view.position, view.forward, viewer);
                result = hit != null && hit.IsInside(viewer) ? hit : NearestInView(view.position, view.forward, viewer);
            }

            _lookFrame = Time.frameCount;
            _lookViewer = viewer;
            _lookResult = result;
            return result;
        }

        public static Transform ViewOf(Damageable viewer)
        {
            if (viewer == null)
            {
                throw new ArgumentNullException(nameof(viewer));
            }

            var weapons = viewer.GetComponentInChildren<WeaponController>(true);
            if (weapons != null)
            {
                return weapons.transform;
            }

            var main = Camera.main;
            return main != null ? main.transform : null;
        }

        private static Pickup CastLook(Vector3 origin, Vector3 direction, Damageable viewer)
        {
            var count = Physics.RaycastNonAlloc(
                origin, direction, _lookHits, LookRayLength, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            Array.Sort(_lookHits, 0, count, HitDistanceComparer.Instance);
            for (var i = 0; i < count; i++)
            {
                var collider = _lookHits[i].collider;
                if (collider == null || collider.GetComponentInParent<Damageable>() == viewer)
                {
                    continue;
                }

                var pickup = collider.GetComponentInParent<Pickup>();
                if (pickup != null && pickup._lookTarget == collider)
                {
                    return pickup.IsAvailable && pickup._collectMode == PickupCollectMode.Look ? pickup : null;
                }

                if (!collider.isTrigger)
                {
                    return null;
                }
            }

            return null;
        }

        private static Pickup NearestInView(Vector3 origin, Vector3 forward, Damageable viewer)
        {
            Pickup best = null;
            var bestAngle = float.PositiveInfinity;
            for (var i = _occupied.Count - 1; i >= 0; i--)
            {
                var pickup = _occupied[i];
                if (pickup == null)
                {
                    _occupied.RemoveAt(i);
                    continue;
                }

                if (!pickup.IsAvailable || pickup._collectMode != PickupCollectMode.Look || !pickup.IsInside(viewer))
                {
                    continue;
                }

                var point = pickup.LookPoint;
                var angle = Vector3.Angle(forward, point - origin);
                if (angle > pickup._lookAngleDegrees || angle >= bestAngle || !CanSee(origin, point, viewer))
                {
                    continue;
                }

                best = pickup;
                bestAngle = angle;
            }

            return best;
        }

        private Vector3 LookPoint
        {
            get
            {
                if (_lookTarget != null)
                {
                    return _lookTarget.transform.TransformPoint(_lookTarget.center);
                }

                return _visual != null ? _visual.position : transform.position;
            }
        }

        private static bool CanSee(Vector3 origin, Vector3 point, Damageable viewer)
        {
            var toPoint = point - origin;
            var distance = toPoint.magnitude;
            if (distance < 1e-4f)
            {
                return true;
            }

            var count = Physics.RaycastNonAlloc(
                origin, toPoint / distance, _lookHits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++)
            {
                var collider = _lookHits[i].collider;
                if (collider != null && collider.GetComponentInParent<Damageable>() != viewer)
                {
                    return false;
                }
            }

            return true;
        }

        private static float ClampLookAngle(float degrees)
        {
            return float.IsNaN(degrees) ? 0f : Mathf.Clamp(degrees, 0f, MaxLookAngleDegrees);
        }

        public Damageable LocalPlayerInside()
        {
            foreach (var collider in _inside)
            {
                var collector = CollectorOf(collider);
                if (collector != null && collector.IsLocalPlayer)
                {
                    return collector;
                }
            }

            return null;
        }

        public bool TryDescribe(Damageable player, out PickupHint hint)
        {
            hint = default;
            if (player == null || _collected || _collectMode == PickupCollectMode.Touch || _contents == null || !TryResolveSpec())
            {
                return false;
            }

            var collector = Describe(player);
            var outcome = PickupJudge.Judge(_spec, collector);
            var nameKey = _contents.Weapon != null ? _contents.Weapon.NameKey : null;
            if (_spec.Kind == PickupKind.Item && collector.Inventory != null && collector.Inventory.Catalog.TryFind(_spec.ItemId, out var item))
            {
                nameKey = item.NameKey;
            }

            return PickupHint.TryFrom(outcome, nameKey, InteractActionPath, out hint);
        }

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

            // 入り切らなかったアイテムは、残りの数にしてこの場に置いておく
            if (spec.Kind == PickupKind.Item && outcome.Items < spec.Amount)
            {
                Contents = _contents.WithAmount(spec.Amount - outcome.Items);
                Debug.Log(PickupLog.Collected(name, collector.Id, outcome) + " left=" + (spec.Amount - outcome.Items), this);
                return true;
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

        public void Restock()
        {
            _collected = false;
            _age = 0f;
            _inside.Clear();
        }

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
                state = state.WithWeapons(list, currentWeaponId, Mathf.Max(0, weapons.MaxWeapons));
            }

            // 上限が 0 以下の実装は防具を持てないものとして扱う (Core の検査で例外にしない)。
            if (armor != null && armor.MaxArmor > 0)
            {
                state = state.WithArmor(Mathf.Clamp(armor.Armor, 0, armor.MaxArmor), armor.MaxArmor);
            }

            var inventory = collector.GetComponent<PlayerInventory>();
            if (inventory != null)
            {
                state = state.WithInventory(inventory.Inventory);
            }

            return state;
        }

        internal static IWeaponHolder WeaponsOf(Damageable collector)
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
                        if (outcome.DroppedWeaponId != null)
                        {
                            if (!weapons.SwapWeapon(outcome.DroppedWeaponId, definition, _contents.CarriedAmmo, _contents.Attachments, out var dropped))
                            {
                                failure = $"could not swap weapon '{outcome.DroppedWeaponId}' for '{definition.Id}'";
                                return false;
                            }

                            DropWeapon(dropped, collector);
                        }
                        else if (!weapons.AddWeapon(definition, _contents.CarriedAmmo, _contents.Attachments))
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

                    if (outcome.Kind == PickupKind.Weapon)
                    {
                        HandOverAttachments(collector);
                    }

                    return true;
                case PickupKind.Health:
                    collector.Health.Heal(outcome.Health);
                    return true;
                case PickupKind.Armor:
                    armor.AddArmor(outcome.Armor);
                    return true;
                case PickupKind.Item:
                    var inventory = collector.GetComponent<PlayerInventory>();
                    if (inventory == null || inventory.Inventory.Add(outcome.ItemId, outcome.Items) != outcome.Items)
                    {
                        failure = $"could not put {outcome.Items} of item '{outcome.ItemId}' into the inventory";
                        return false;
                    }

                    return true;
                default:
                    // タグはモードが Collected を受けて扱うので、拾う人には何も反映しない。
                    return true;
            }
        }

        private Pickup DropWeapon(HeldWeapon dropped, Damageable collector)
        {
            var position = transform.position;
            var toward = collector.transform.position - position;
            toward.y = 0f;
            if (toward.sqrMagnitude > 1e-6f)
            {
                position += toward.normalized * Mathf.Min(DroppedWeaponOffset, toward.magnitude);
            }

            var contents = PickupContents.ForDroppedWeapon(dropped.Definition, dropped.Ammo, _contents.Eligibility, dropped.Attachments);
            var pickup = Create(contents, position, PickupCollectMode.Look, radius: _radius, lookAngleDegrees: _lookAngleDegrees);
            Debug.Log(PickupLog.Dropped(collector.Id, pickup.name, pickup.Spec, position.ToNumerics()), pickup);
            return pickup;
        }

        // 同じ武器を持つ人は弾だけ取り込むので、付けた物を持ち物へ入れる。入らない物 (持ち物の無い bot も) は足元に置き、消さない
        private void HandOverAttachments(Damageable collector)
        {
            var inventory = collector.GetComponent<PlayerInventory>();
            foreach (var itemId in _contents.Attachments)
            {
                if (inventory != null && inventory.Inventory.Add(itemId, 1) == 1)
                {
                    Debug.Log($"{LogTag} '{name}' attachment item={itemId} -> inventory of {collector.Id}", this);
                    continue;
                }

                var pickup = Create(PickupContents.ForItem(itemId, 1, _contents.Eligibility), transform.position, PickupCollectMode.Look);
                Debug.Log(PickupLog.Dropped(collector.Id, pickup.name, pickup.Spec, transform.position.ToNumerics()), pickup);
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

            var lookTarget = _visual != null ? _visual.Find(LookTargetName) : null;
            _lookTarget = lookTarget != null ? lookTarget.GetComponent<BoxCollider>() : null;

            ApplyVisualPose();
        }

        // Look は寝かせて照準の判定を付け、それ以外は浮かせて回す。拾い方は作った後にも変えられるので、変えるたびに置き直す。
        private void ApplyVisualPose()
        {
            if (_visual == null)
            {
                return;
            }

            if (_collectMode != PickupCollectMode.Look)
            {
                _visual.localRotation = Quaternion.identity;
                _visual.localPosition = new Vector3(0f, VisualHeight, 0f);
                RemoveLookTarget();
                return;
            }

            _visual.localRotation = Quaternion.Euler(0f, 0f, LyingRollDegrees);
            _visual.localPosition = Vector3.zero;
            var hasBounds = TryLocalBounds(_visual, out var bounds);
            if (hasBounds)
            {
                var lowest = float.PositiveInfinity;
                foreach (var corner in Corners(bounds))
                {
                    lowest = Mathf.Min(lowest, _visual.TransformPoint(corner).y);
                }

                _visual.position += Vector3.up * (transform.position.y + LyingGap - lowest);
            }
            else
            {
                bounds = new Bounds(Vector3.zero, Vector3.one * VisualSize);
                _visual.position += Vector3.up * (VisualSize / 2f + LyingGap);
            }

            EnsureLookTarget(bounds);
        }

        private void EnsureLookTarget(Bounds bounds)
        {
            if (_lookTarget == null)
            {
                var target = new GameObject(LookTargetName);
                target.transform.SetParent(_visual, false);

                // 親の Rigidbody にまとまると、この判定への出入りが範囲の球の出入りとして Pickup に届くので、別の体にする。
                var body = target.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;

                // 照準の線にだけ当てる。トリガーなので移動と射撃は遮らない。
                _lookTarget = target.AddComponent<BoxCollider>();
                _lookTarget.isTrigger = true;
            }

            _lookTarget.center = bounds.center;
            _lookTarget.size = bounds.size;
        }

        private void RemoveLookTarget()
        {
            if (_lookTarget == null)
            {
                return;
            }

            DestroySafely(_lookTarget.gameObject);
            _lookTarget = null;
        }

        private static bool TryLocalBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            var found = false;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var corner in Corners(renderer.localBounds))
                {
                    var point = root.InverseTransformPoint(renderer.transform.TransformPoint(corner));
                    if (!found)
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        found = true;
                    }
                    else
                    {
                        bounds.Encapsulate(point);
                    }
                }
            }

            return found;
        }

        private static IEnumerable<Vector3> Corners(Bounds bounds)
        {
            var min = bounds.min;
            var max = bounds.max;
            for (var i = 0; i < 8; i++)
            {
                yield return new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z);
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
            var prefab = _visualPrefab != null ? _visualPrefab : WeaponModelOf(_contents);
            if (prefab != null)
            {
                // 拾う範囲はトリガーの球で決めるので、見た目の当たり判定は外す (残すと固い物として移動と弾を遮る)。
                var instance = Shooter.Runtime.VisualPrefab.Instantiate(prefab, transform, VisualName, stripColliders: true);
                instance.transform.localPosition = new Vector3(0f, VisualHeight, 0f);
                AttachVisuals(instance.transform);
                return instance.transform;
            }

            if (kind == PickupKind.Weapon)
            {
                var gun = CreateGunShape();
                AttachVisuals(gun);
                return gun;
            }

            var visual = CreatePart(PrimitiveType.Cube, VisualName, transform, Vector3.one * VisualSize, ColorOf(kind));
            visual.localPosition = new Vector3(0f, VisualHeight, 0f);
            return visual;
        }

        private void AttachVisuals(Transform visual)
        {
            if (_contents != null && _contents.Attachments.Count > 0)
            {
                WeaponAttachmentModels.Apply(visual, _contents.Attachments, KitTuning.Items, AttachmentModelCatalog.Load());
            }
        }

        public static GameObject WeaponModelOf(PickupContents contents)
        {
            if (contents == null || contents.Kind != PickupKind.Weapon || contents.Weapon == null)
            {
                return null;
            }

            var catalog = WeaponExtrasCatalog.Load();
            return catalog != null && catalog.TryGetWeapon(contents.Weapon.Id, out var assets) ? assets.PickupModel : null;
        }

        private Transform CreateGunShape()
        {
            var root = new GameObject(VisualName).transform;
            root.SetParent(transform, false);
            root.localPosition = new Vector3(0f, VisualHeight, 0f);
            var color = PlayerMockBody.GunColor;
            CreatePart(PrimitiveType.Cube, "Body", root, GunBodySize, color);
            var barrel = CreatePart(PrimitiveType.Cube, "Barrel", root, GunBarrelSize, color);
            barrel.localPosition = new Vector3(0f, GunBodySize.y / 4f, (GunBodySize.z + GunBarrelSize.z) / 2f);
            var grip = CreatePart(PrimitiveType.Cube, "Grip", root, GunGripSize, color);
            grip.localPosition = new Vector3(0f, -(GunBodySize.y + GunGripSize.y) / 2f, -GunBodySize.z / 6f);
            return root;
        }

        private static Transform CreatePart(PrimitiveType shape, string partName, Transform parent, Vector3 size, Color color)
        {
            var part = GameObject.CreatePrimitive(shape);
            part.name = partName;

            // Destroy は次のフレームまで残り、その間に物理に固い箱として現れるので、すぐに外す。
            DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localScale = size;

            var renderer = part.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // 共有マテリアルを塗り替えると他のプリミティブまで色が変わるので、この物だけの上書きで色を付ける。
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);
            renderer.SetPropertyBlock(block);
            return part.transform;
        }

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
                case PickupKind.Item:
                    return new Color(0.7f, 0.45f, 0.95f);
                default:
                    return new Color(0.95f, 0.3f, 0.3f);
            }
        }

        private void RefreshInteractAction()
        {
            if (_collectMode == PickupCollectMode.Touch || !Application.isPlaying)
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
            // 持ち替えで置いた物は、置いた押下の続きで拾い直さないよう、次のフレームから受け付ける。
            if (Time.frameCount == _armedFrame)
            {
                return;
            }

            Interact();
        }

        internal void UpdatePrompt()
        {
            var player = _collectMode != PickupCollectMode.Touch && !_collected && _inside.Count > 0 ? LocalPlayerInside() : null;
            if (player != null && _collectMode == PickupCollectMode.Look && !IsLookedAtBy(player))
            {
                player = null;
            }

            if (player == null || !TryDescribe(player, out var hint))
            {
                HidePrompt();
                return;
            }

            (_hint ?? (_hint = new InteractHintSlot(this))).Show(hint);
        }

        private void HidePrompt()
        {
            _hint?.Clear();
        }

        private sealed class HitDistanceComparer : IComparer<RaycastHit>
        {
            public static readonly HitDistanceComparer Instance = new HitDistanceComparer();

            public int Compare(RaycastHit x, RaycastHit y) => x.distance.CompareTo(y.distance);
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
