using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fps.Runtime
{
    /// <summary>
    /// 拾える物。トリガーの球 (<see cref="Radius"/>) に <see cref="Damageable"/> を持つ人が入ったら (<see cref="PickupCollectMode.Touch"/>)、
    /// 範囲にいるプレイヤーが Interact を押したら (<see cref="PickupCollectMode.Interact"/>)、
    /// または範囲にいるプレイヤーが見た目に照準を合わせて Interact を押したら (<see cref="PickupCollectMode.Look"/>) 拾う。
    /// Look の照準は見た目に線が当たらなくても、照準の向きから <see cref="LookAngleDegrees"/> 以内に見えていれば合っているとし、範囲にある中で照準に最も近い物を選ぶ。
    /// 持てる武器の数の上限に届いた人が別の武器を拾うと、今の武器と持ち替え、手放した武器をその残弾のまま <see cref="PickupCollectMode.Look"/> で足元に落とす。
    /// 拾えるかと何が起きるかは <see cref="PickupJudge"/> が決め、ここは拾った人の <see cref="IWeaponHolder"/> (プレイヤーの <see cref="WeaponController"/> か
    /// bot の <see cref="EnemyAgent"/>。持ち武器・予備弾)・
    /// <see cref="Damageable"/> (体力)・<see cref="IArmorHolder"/> (防具) に反映して <c>[Pickup]</c> のログを出し、<see cref="Collected"/> を呼ぶ。
    /// タグは何にも反映せず、<see cref="Collected"/> を受けたモードが得点などを決める。
    /// Interact で拾う物は、範囲にこの画面のプレイヤーがいて拾える間 (Look なら照準を合わせている間)、
    /// HUD の <see cref="IInteractPrompt"/> に何を拾うかの案内 (<see cref="PickupPrompt"/>) を出す。
    /// </summary>
    /// <remarks>
    /// 実行時に置くときは <see cref="Create"/> を使う (当たり判定と見た目もコードで作る)。Scene に置くなら <see cref="PickupSpawnPoint"/> を使う。
    /// 武器の見た目は <see cref="VisualPrefab"/>、無ければ <see cref="WeaponExtrasCatalog"/> のその武器の <see cref="WeaponExtrasAssets.PickupModel"/>、
    /// それも無ければ仮の銃の形を組む。
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

        /// <summary>見た目が回る速さ (度/秒)。拾える物だと遠くから分かるように回す。<see cref="PickupCollectMode.Look"/> では照準を合わせやすいよう回さない。</summary>
        public const float SpinDegreesPerSecond = 90f;

        /// <summary><see cref="PickupCollectMode.Look"/> で見た目を横に倒す角度 (度、前後の軸まわり)。銃を地面に寝かせて置く。</summary>
        public const float LyingRollDegrees = 90f;

        /// <summary><see cref="PickupCollectMode.Look"/> で寝かせた見た目の下端と足元の間 (m)。</summary>
        public const float LyingGap = 0.02f;

        /// <summary>
        /// <see cref="PickupCollectMode.Look"/> で照準の線を伸ばす長さ (m)。三人称のカメラは体から離れているので、拾える距離は線の長さでなく範囲の球で決める。
        /// </summary>
        public const float LookRayLength = 30f;

        /// <summary>
        /// <see cref="PickupCollectMode.Look"/> で、照準の向きと見た目の中心への向きの間がこの角度 (度) 以内なら照準が合っているとする既定。
        /// 足元に置いた銃は、正面を見たままでも目からこのくらい下に見える。
        /// </summary>
        public const float DefaultLookAngleDegrees = 90f;

        /// <summary><see cref="LookAngleDegrees"/> の上限 (度)。</summary>
        public const float MaxLookAngleDegrees = 180f;

        /// <summary>持ち替えで手放した武器を、拾った物の位置から拾った人の側へずらして置く距離 (m)。</summary>
        public const float DroppedWeaponOffset = 0.6f;

        /// <summary><see cref="PickupCollectMode.Look"/> の見た目に付ける、照準の線だけが当たる判定の名前。</summary>
        public const string LookTargetName = "LookTarget";

        /// <summary>仮の銃の本体 (機関部と銃床) の大きさ (m)。幅・高さ・長さ。</summary>
        public static readonly Vector3 GunBodySize = new Vector3(0.07f, 0.12f, 0.45f);

        /// <summary>仮の銃の銃身の大きさ (m)。</summary>
        public static readonly Vector3 GunBarrelSize = new Vector3(0.035f, 0.035f, 0.3f);

        /// <summary>仮の銃の握りの大きさ (m)。</summary>
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
        private readonly InteractKeyLabel _keyLabel = new InteractKeyLabel();
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
        private IHud _hud;
        private int _armedFrame = -1;

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
                ApplyVisualPose();
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

        /// <summary>
        /// <see cref="PickupCollectMode.Look"/> で、照準の向きと見た目の中心への向きの間がこの角度 (度) 以内なら、見た目に照準の線が当たらなくても照準が合っているとする。
        /// 0 なら線が当たったときだけ。0〜<see cref="MaxLookAngleDegrees"/> に収め、NaN は 0 にする。
        /// </summary>
        public float LookAngleDegrees
        {
            get => _lookAngleDegrees;
            set => _lookAngleDegrees = ClampLookAngle(value);
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

        /// <summary>見た目。null なら武器はカタログの見た目か仮の銃、他は中身の種類で色を分けた仮の箱。見た目を作る (Awake か <see cref="Create"/>) 前に設定したものを使う。</summary>
        public GameObject VisualPrefab
        {
            get => _visualPrefab;
            set => _visualPrefab = value;
        }

        /// <summary>まだ拾われていなければ true。</summary>
        public bool IsAvailable => !_collected;

        /// <summary>範囲に入っている人のコライダーの数。</summary>
        public int InsideCount => _inside.Count;

        /// <summary>今使っている Interact アクション。<see cref="PickupCollectMode.Interact"/> か <see cref="PickupCollectMode.Look"/> で Play 中でなければ null。</summary>
        public InputAction InteractAction => _interactAction;

        /// <summary>
        /// contents の拾える物を groundPosition (足元) に作る。トリガーの球・動かない Rigidbody・見た目 (<see cref="VisualPrefab"/> を参照) を付ける。
        /// contents は写しを持つので、後で書き換えても作った物は変わらない。contents が不正なら何も作らずに <see cref="ArgumentException"/>。
        /// </summary>
        /// <param name="parent">親。null ならシーンの直下。</param>
        /// <param name="visualPrefab">見た目 (<see cref="VisualPrefab"/>)。null なら武器はカタログの見た目か仮の銃、他は仮の箱。</param>
        /// <param name="lookAngleDegrees">Look で照準が合っているとする角度 (<see cref="LookAngleDegrees"/>)。</param>
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

            if (!_occupied.Contains(this))
            {
                _occupied.Add(this);
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

            if (_inside.Count == 0)
            {
                _occupied.Remove(this);
            }
        }

        /// <summary>
        /// 範囲にいるこの画面のプレイヤー (<see cref="Damageable.IsLocalPlayer"/>) が拾う。拾えたら true。
        /// <see cref="PickupCollectMode.Interact"/> と <see cref="PickupCollectMode.Look"/> の入力から呼ばれる。拾い方に関わらずコードからも呼べるが、
        /// Look ではプレイヤーが見た目に照準を合わせていなければ (<see cref="IsLookedAtBy"/>) 拾わない。
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
                    if (_collectMode == PickupCollectMode.Look && !IsLookedAtBy(collector))
                    {
                        return false;
                    }

                    return TryCollect(collector, out _);
                }
            }

            return false;
        }

        /// <summary>
        /// viewer が照準を合わせている <see cref="PickupCollectMode.Look"/> の物 (<see cref="LookedAt"/>) がこの物なら true。
        /// </summary>
        public bool IsLookedAtBy(Damageable viewer)
        {
            return viewer != null && !_collected && _collectMode == PickupCollectMode.Look && LookedAt(viewer) == this;
        }

        /// <summary>
        /// viewer が照準を合わせている <see cref="PickupCollectMode.Look"/> の拾える物。無ければ null。
        /// 照準の線を viewer の視点 (<see cref="ViewOf"/>) の正面へ <see cref="LookRayLength"/> 伸ばし、壁など固い物より手前で見た目に当たった物を、
        /// viewer が範囲にいれば選ぶ。当たらなければ、viewer が範囲にいる物のうち、照準の向きから見た目の中心までの角度が
        /// その物の <see cref="LookAngleDegrees"/> 以内で、固い物に遮られずに見える中で最も角度の小さい物を選ぶ。
        /// viewer 自身のコライダーと、見た目以外のトリガーは素通りする。
        /// </summary>
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

        /// <summary>viewer の照準の起点。子の <see cref="WeaponController"/> が付いたカメラ、無ければ <see cref="Camera.main"/>。どちらも無ければ null。</summary>
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

        // 見た目の中心。照準の判定があればその箱の中心、無ければ見た目か足元の位置。
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

        /// <summary>範囲にいるこの画面のプレイヤー。いなければ null。</summary>
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

        /// <summary>
        /// player に出す案内。Interact か Look で拾う物を player が今拾えるなら、Interact のキー (keyLabel) と中身を入れた <see cref="PickupPrompt"/> の文で true。
        /// 拾い方が Touch・拾われた後・中身が不正・player が拾えない (満タンや倒れているなど) なら案内は出さない (false)。
        /// Look で照準を合わせているかはここでは見ない (呼ぶ側が <see cref="IsLookedAtBy"/> で確かめる)。
        /// </summary>
        public bool TryDescribe(Damageable player, string keyLabel, out LocalizedText text)
        {
            text = default;
            if (player == null || _collected || _collectMode == PickupCollectMode.Touch || _contents == null || !TryResolveSpec())
            {
                return false;
            }

            var outcome = PickupJudge.Judge(_spec, Describe(player));
            var weaponName = _contents.Weapon != null ? _contents.Weapon.Name : default;
            return PickupPrompt.TryDescribe(outcome, keyLabel, weaponName, out text);
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
                state = state.WithWeapons(list, currentWeaponId, Mathf.Max(0, weapons.MaxWeapons));
            }

            // 上限が 0 以下の実装は防具を持てないものとして扱う (Core の検査で例外にしない)。
            if (armor != null && armor.MaxArmor > 0)
            {
                state = state.WithArmor(Mathf.Clamp(armor.Armor, 0, armor.MaxArmor), armor.MaxArmor);
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
                            if (!weapons.SwapWeapon(outcome.DroppedWeaponId, definition, _contents.CarriedAmmo, out var dropped))
                            {
                                failure = $"could not swap weapon '{outcome.DroppedWeaponId}' for '{definition.Id}'";
                                return false;
                            }

                            DropWeapon(dropped, collector);
                        }
                        else if (!weapons.AddWeapon(definition, _contents.CarriedAmmo))
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

        /// <summary>
        /// 持ち替えで手放した武器を、この物の位置から collector の側へ <see cref="DroppedWeaponOffset"/> ずらした足元に、
        /// その残弾のまま <see cref="PickupCollectMode.Look"/> で置く。範囲の半径・照準の角度・拾える陣営はこの物と同じにする。
        /// </summary>
        private Pickup DropWeapon(HeldWeapon dropped, Damageable collector)
        {
            var position = transform.position;
            var toward = collector.transform.position - position;
            toward.y = 0f;
            if (toward.sqrMagnitude > 1e-6f)
            {
                position += toward.normalized * Mathf.Min(DroppedWeaponOffset, toward.magnitude);
            }

            var contents = PickupContents.ForDroppedWeapon(dropped.Definition, dropped.Ammo, _contents.Eligibility);
            var pickup = Create(contents, position, PickupCollectMode.Look, radius: _radius, lookAngleDegrees: _lookAngleDegrees);
            Debug.Log(PickupLog.Dropped(collector.Id, pickup.name, pickup.Spec, position.ToNumerics()), pickup);
            return pickup;
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

        // root の座標で、子孫のすべての Renderer を囲む箱。Renderer が無ければ false。
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
                var instance = Fps.Runtime.VisualPrefab.Instantiate(prefab, transform, VisualName, stripColliders: true);
                instance.transform.localPosition = new Vector3(0f, VisualHeight, 0f);
                return instance.transform;
            }

            if (kind == PickupKind.Weapon)
            {
                return CreateGunShape();
            }

            var visual = CreatePart(PrimitiveType.Cube, VisualName, transform, Vector3.one * VisualSize, ColorOf(kind));
            visual.localPosition = new Vector3(0f, VisualHeight, 0f);
            return visual;
        }

        /// <summary>contents の武器の、地面に置いたときの見た目 (<see cref="WeaponExtrasAssets.PickupModel"/>)。武器でないか、カタログに無ければ null。</summary>
        public static GameObject WeaponModelOf(PickupContents contents)
        {
            if (contents == null || contents.Kind != PickupKind.Weapon || contents.Weapon == null)
            {
                return null;
            }

            var catalog = WeaponExtrasCatalog.Load();
            return catalog != null && catalog.TryGetWeapon(contents.Weapon.Id, out var assets) ? assets.PickupModel : null;
        }

        // 見た目の素材が無い武器の仮の形。原点を中心に、前 (+Z) を銃口にした本体・銃身・握り。
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

        private void UpdatePrompt()
        {
            var player = _collectMode != PickupCollectMode.Touch && !_collected && _inside.Count > 0 ? LocalPlayerInside() : null;
            if (player != null && _collectMode == PickupCollectMode.Look && !IsLookedAtBy(player))
            {
                player = null;
            }

            if (player == null)
            {
                HidePrompt();
                return;
            }

            // HUD はプレイヤーが範囲に入ったときにだけ探す (シーン全体を見るので毎フレームは探さない)。
            if (!GameUi.IsAlive(_hud))
            {
                _hud = GameUi.FindHud();
            }

            var prompt = GameUi.IsAlive(_hud) ? _hud.InteractPrompt : null;
            if (prompt == null)
            {
                return;
            }

            if (!TryDescribe(player, _keyLabel.For(_interactAction), out var text))
            {
                prompt.Hide(this);
                return;
            }

            prompt.Show(this, text);
        }

        private void HidePrompt()
        {
            if (GameUi.IsAlive(_hud))
            {
                _hud.InteractPrompt?.Hide(this);
            }
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
