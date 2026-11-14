using System;
using System.Collections.Generic;
using System.Globalization;
using Fps.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Fps.Runtime
{
    /// <summary>
    /// やられ演出 (<see cref="DeathBody"/>)。<see cref="Mode"/> が <see cref="DeathBody.Collapse"/> のとき、倒れた瞬間 (<see cref="Present"/>) に
    /// 体の見た目 (子孫の MeshRenderer / SkinnedMeshRenderer) を複製した「抜け殻」を作り、Rigidbody とコライダーを付けて
    /// 最後に受けた弾の向きへ倒す。抜け殻は <see cref="Clear"/> (リスポーン) か <see cref="Lifetime"/> 秒の経過で消える。
    /// <see cref="DeathBody.Vanish"/> では何もしない (体を消すのは今どおり持ち主の側)。
    /// <para>
    /// 抜け殻は各クライアントのローカルな見た目だけで、<see cref="Hitbox"/> も NetworkObject も持たず、
    /// 射撃 (<see cref="Physics.DefaultRaycastLayers"/>) に拾われない <see cref="ShellLayer"/> に置く。
    /// 体の側は Collapse のとき見た目と当たり (CharacterController 以外のコライダー) を隠す。戻すのは持ち主のリスポーン処理。
    /// 専用サーバー (<see cref="NetStartup.ShouldDisablePresentation"/>) では抜け殻を作らない。
    /// </para>
    /// <para>
    /// オフラインでは <see cref="Bind"/> で <see cref="Damageable"/> に繋ぐ (倒れたら作り、体力を作り直したら消す)。
    /// 時間は自分で数える (<see cref="Tick"/>) ので、Update が走らない Edit Mode のテストでも直接進められる。
    /// </para>
    /// <para>
    /// <see cref="RagdollCatalog"/> があり、その全ボーンが体のリグに見つかれば、1 つの塊ではなくボーンごとに Rigidbody と関節を持つ
    /// ラグドールの抜け殻にし、最後の被弾の向きとダメージに応じた速さで倒す。固めるのは抜け殻の <see cref="DeathRagdoll"/>。
    /// 対応表が無い・中身が不正・ボーンが足りないときは 1 つの塊の抜け殻に戻す。
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DeathBodyPresenter : MonoBehaviour
    {
        public const string LogTag = "[DeathBody]";

        /// <summary>抜け殻が消えるまでの秒数の既定値。</summary>
        public const float DefaultLifetime = 5f;

        /// <summary>倒すときに抜け殻に与える水平の速さ (m/s)。小さく押して倒すだけにする。</summary>
        public const float PushSpeed = 1.5f;

        /// <summary>倒すときに抜け殻に与える回転の速さ (rad/s)。頭が押された向きへ傾く。</summary>
        public const float ToppleSpeed = 2.5f;

        /// <summary>抜け殻の質量 (kg)。</summary>
        public const float ShellMass = 70f;

        /// <summary>抜け殻の名前に付ける接尾辞。</summary>
        public const string ShellSuffix = " (DeathBody)";

        /// <summary>
        /// 抜け殻を置くレイヤー。<see cref="Physics.DefaultRaycastLayers"/> に含まれないので、
        /// 射撃 (<see cref="Hitscan"/>・ネットの巻き戻し) や視線の判定に拾われない。<see cref="DebrisPool.DebrisLayer"/> と同じ。
        /// </summary>
        public const int ShellLayer = DebrisPool.DebrisLayer;

        /// <summary>薄いメッシュ (Plane など) でも床をすり抜けないよう、コライダーの各辺をこれより小さくしない (m)。</summary>
        private const float MinColliderSize = 0.05f;

        public const string ReasonRespawn = "respawn";
        public const string ReasonTimeout = "timeout";
        public const string ReasonReplaced = "replaced";
        public const string ReasonDestroyed = "destroyed";

        [SerializeField, Tooltip("倒れた体の見せ方。Collapse のときだけ抜け殻を作る")]
        private DeathBody _mode = DeathBody.Vanish;

        [SerializeField, Min(0.01f), Tooltip("抜け殻が消えるまでの秒数")]
        private float _lifetime = DefaultLifetime;

        [SerializeField, Tooltip("ログに出す識別子。空なら GameObject の名前")]
        private string _id = string.Empty;

        [SerializeField, Tooltip("ラグドールの対応表。空なら Resources の Presentation/RagdollCatalog を読む")]
        private RagdollCatalog _ragdollCatalog;

        [SerializeField, Tooltip("対応表があればラグドールで倒すか。切ると常に 1 つの塊で倒す")]
        private bool _useRagdoll = true;

        private static bool _warnedMissingRagdollCatalog;
        private static readonly HashSet<RagdollCatalog> _warnedInvalidRagdollCatalogs = new HashSet<RagdollCatalog>();

        private readonly List<Mesh> _bakedMeshes = new List<Mesh>();
        private GameObject _shell;
        private Rigidbody _shellBody;
        private DeathRagdoll _ragdoll;
        private RagdollCatalog _shellCatalog;
        private float _remaining;
        private Damageable _target;
        private bool _awaitingHitDirection;

        /// <summary>倒れた体の見せ方。定義済みの値だけ。今ある抜け殻には効かない。</summary>
        public DeathBody Mode
        {
            get => _mode;
            set
            {
                if (!Enum.IsDefined(typeof(DeathBody), value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Mode is not a defined DeathBody");
                }

                _mode = value;
            }
        }

        /// <summary>これから作る抜け殻が消えるまでの秒数。0 より大きい有限値。</summary>
        public float Lifetime
        {
            get => _lifetime;
            set
            {
                if (!(value > 0f) || float.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Lifetime must be positive and finite");
                }

                _lifetime = value;
            }
        }

        /// <summary>ログに出す識別子。未設定なら GameObject の名前。</summary>
        public string Id
        {
            get => string.IsNullOrEmpty(_id) ? name : _id;
            set => _id = value ?? string.Empty;
        }

        public RagdollCatalog RagdollCatalog
        {
            get => _ragdollCatalog;
            set => _ragdollCatalog = value;
        }

        public bool UseRagdoll
        {
            get => _useRagdoll;
            set => _useRagdoll = value;
        }

        public DeathRagdoll Ragdoll => HasShell && _ragdoll != null ? _ragdoll : null;

        public bool IsRagdoll => Ragdoll != null;

        /// <summary>今ある抜け殻。無ければ (外から消されたときも) null。</summary>
        public GameObject Shell => _shell != null ? _shell : null;

        /// <summary>抜け殻があれば true。</summary>
        public bool HasShell => _shell != null;

        /// <summary>抜け殻が消えるまでの残り秒数。無ければ 0。</summary>
        public float RemainingSeconds => HasShell ? Mathf.Max(0f, _remaining) : 0f;

        /// <summary><see cref="Bind"/> で繋いだ相手。無ければ null。</summary>
        public Damageable Target => _target;

        /// <summary>このプロセスで抜け殻を作るか。専用サーバーでは描かないので作らない。</summary>
        public static bool IsPresentationAvailable => !NetStartup.ShouldDisablePresentation(NetStartup.IsDedicatedServerBuild);

        /// <summary>body に付いている DeathBodyPresenter を返す。無ければ付ける。</summary>
        public static DeathBodyPresenter Attach(GameObject body)
        {
            if (body == null)
            {
                throw new ArgumentNullException(nameof(body));
            }

            var presenter = body.GetComponent<DeathBodyPresenter>();
            return presenter != null ? presenter : body.AddComponent<DeathBodyPresenter>();
        }

        /// <summary>
        /// targets のそれぞれに DeathBodyPresenter を付け (<see cref="Attach"/>)、mode で <see cref="Bind"/> する。null は飛ばす。
        /// 繋いだ数を返す。
        /// </summary>
        public static int BindAll(IEnumerable<Damageable> targets, DeathBody mode)
        {
            if (targets == null)
            {
                throw new ArgumentNullException(nameof(targets));
            }

            if (!Enum.IsDefined(typeof(DeathBody), mode))
            {
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "mode is not a defined DeathBody");
            }

            var bound = 0;
            foreach (var target in targets)
            {
                if (target == null)
                {
                    continue;
                }

                Attach(target.gameObject).Bind(target, mode);
                bound++;
            }

            return bound;
        }

        /// <summary>
        /// target が倒れたら mode で見せ、体力を作り直したら (リスポーン) 抜け殻を消すように繋ぐ。前に繋いでいた相手からは外す。
        /// とどめの 1 発に撃った者の位置があれば (<see cref="Damageable.Damaged"/>)、その向きに倒す。無ければ体の後ろへ倒す。
        /// 識別子が未設定なら target の <see cref="Damageable.Id"/> にする。
        /// </summary>
        public void Bind(Damageable target, DeathBody mode)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            Mode = mode;
            Unbind();
            _target = target;
            if (string.IsNullOrEmpty(_id))
            {
                _id = target.Id;
            }

            _target.Died += OnDied;
            _target.Damaged += OnDamaged;
            _target.HealthReset += OnHealthReset;
        }

        /// <summary>繋いだ相手の購読を外す。今ある抜け殻はそのまま残る。</summary>
        public void Unbind()
        {
            if (_target != null)
            {
                _target.Died -= OnDied;
                _target.Damaged -= OnDamaged;
                _target.HealthReset -= OnHealthReset;
            }

            _target = null;
            _awaitingHitDirection = false;
        }

        /// <summary>
        /// 倒れた瞬間に呼ぶ。<see cref="Mode"/> が Collapse なら体の見た目と当たりを隠し、抜け殻を作って hitDirection の向きへ倒し、
        /// <c>[DeathBody] collapse ...</c> を出して抜け殻を返す。前の抜け殻が残っていれば先に消す。
        /// Vanish のとき・専用サーバー・複製できる見た目が無いときは null (最後のときは警告を出す)。
        /// </summary>
        /// <param name="hitDirection">最後に受けた弾の進む向き。null か水平成分が無ければ体の後ろへ倒す。</param>
        public GameObject Present(Vector3? hitDirection)
        {
            return Present(hitDirection, 0);
        }

        /// <param name="damage">最後の被弾のダメージ。ラグドールのときだけ倒す速さに効く。</param>
        public GameObject Present(Vector3? hitDirection, int damage)
        {
            _awaitingHitDirection = false;
            if (_mode != DeathBody.Collapse)
            {
                return null;
            }

            // 抜け殻を作る前に集める。抜け殻は体の子にしないので混ざらないが、隠す前の Renderer を確実に拾うため
            var allRenderers = GetComponentsInChildren<Renderer>(true);
            // CharacterAnimator が置き換えて隠したカプセルだけは抜け殻に写さない。enabled では、同じ Died の購読者
            // (TargetDummy など) が直前に隠したものと区別できない
            var animator = GetComponent<CharacterAnimator>();
            var hiddenHost = animator != null ? animator.HiddenHostRenderer : null;
            var renderers = System.Array.FindAll(allRenderers, r => r != null && r != hiddenHost);
            HideBody(allRenderers);

            if (!IsPresentationAvailable)
            {
                return null;
            }

            Clear(ReasonReplaced);

            var ragdoll = TryPresentRagdoll(renderers, hitDirection, damage);
            if (ragdoll != null)
            {
                return ragdoll;
            }

            var shell = BuildShell(renderers, out var parts);
            if (shell == null)
            {
                Debug.LogWarning($"{LogTag} id={Id} has no mesh to copy; nothing collapses", this);
                return null;
            }

            _shell = shell;
            _shellBody = shell.GetComponent<Rigidbody>();
            _remaining = _lifetime;
            if (Application.isPlaying)
            {
                // 体の GameObject ごと止められると (KillTargetObjective は倒した敵を非アクティブにする) Update が来ないので、
                // Unity の遅延破棄でも寿命で必ず消す。Tick が先に消せばこちらは何もしない
                Destroy(shell, _lifetime);
            }

            var push = ApplyPush(hitDirection);
            Debug.Log(
                $"{LogTag} collapse id={Id} parts={parts.ToString(CultureInfo.InvariantCulture)} push={FormatDirection(push)} lifetime={_lifetime.ToString("0.##", CultureInfo.InvariantCulture)}s",
                this);
            return shell;
        }

        /// <summary>今ある抜け殻を direction の向きへ倒し直す。抜け殻が無ければ何もせず false。</summary>
        public bool Push(Vector3 direction)
        {
            return Push(direction, 0);
        }

        public bool Push(Vector3? direction, int damage)
        {
            if (!HasShell)
            {
                return false;
            }

            if (IsRagdoll)
            {
                var ragdollPush = PushRagdoll(direction, damage, out var speed);
                Debug.Log(
                    $"{LogTag} push id={Id} ragdoll push={FormatDirection(ragdollPush)} speed={FormatSpeed(speed)} damage={damage.ToString(CultureInfo.InvariantCulture)}",
                    this);
                return true;
            }

            var push = ApplyPush(direction);
            Debug.Log($"{LogTag} push id={Id} push={FormatDirection(push)}", this);
            return true;
        }

        /// <summary>今ある抜け殻を消して <c>[DeathBody] removed ...</c> を出す。無ければ何もせず false。</summary>
        /// <param name="reason">ログに出す理由 (<see cref="ReasonRespawn"/> など)。</param>
        public bool Clear(string reason)
        {
            _awaitingHitDirection = false;
            var existed = HasShell;
            if (existed)
            {
                Debug.Log($"{LogTag} removed id={Id} reason={reason}", this);
            }

            DestroySafely(_shell);
            _shell = null;
            _shellBody = null;
            _ragdoll = null;
            _shellCatalog = null;
            _remaining = 0f;
            foreach (var mesh in _bakedMeshes)
            {
                DestroySafely(mesh);
            }

            _bakedMeshes.Clear();
            return existed;
        }

        /// <summary>時間を deltaSeconds 進め、寿命が過ぎたら抜け殻を消す。消したら true。</summary>
        /// <param name="deltaSeconds">前回からの経過時間 (秒)。0 以上の有限値。</param>
        public bool Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            if (!HasShell)
            {
                if (_bakedMeshes.Count > 0)
                {
                    // 抜け殻だけ外から消されたときに、焼いたメッシュを残さない
                    Clear(ReasonDestroyed);
                }

                return false;
            }

            _remaining -= deltaSeconds;
            return _remaining <= 0f && Clear(ReasonTimeout);
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private void OnDestroy()
        {
            Unbind();
            Clear(ReasonDestroyed);
        }

        private void OnDied(Damageable _)
        {
            Present(null);

            // Damageable はとどめの 1 発でも Died の後に Damaged を呼ぶので、撃った者の位置はその後で受け取る
            _awaitingHitDirection = HasShell;
        }

        private void OnDamaged(Damageable self, DamageReport report)
        {
            if (!_awaitingHitDirection)
            {
                return;
            }

            _awaitingHitDirection = false;
            if (!HasShell)
            {
                return;
            }

            Vector3? direction = report.SourcePosition.HasValue ? _shell.transform.position - report.SourcePosition.Value : (Vector3?)null;

            // 塊の抜け殻は撃った者が分からなければ倒れた時の向きのままにする。ラグドールはダメージで速さが変わるので、向きが無くても押し直す
            if (IsRagdoll)
            {
                Push(direction, report.Amount);
            }
            else if (direction.HasValue)
            {
                Push(direction.Value);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            _warnedMissingRagdollCatalog = false;
            _warnedInvalidRagdollCatalogs.Clear();
        }

        private void OnHealthReset(Health _)
        {
            Clear(ReasonRespawn);
        }

        private GameObject TryPresentRagdoll(Renderer[] renderers, Vector3? hitDirection, int damage)
        {
            if (!_useRagdoll)
            {
                return null;
            }

            var catalog = ResolveRagdollCatalog();
            if (catalog == null)
            {
                return null;
            }

            try
            {
                catalog.Validate();
            }
            catch (RagdollCatalogException e)
            {
                if (_warnedInvalidRagdollCatalogs.Add(catalog))
                {
                    Debug.LogWarning($"{LogTag} ragdoll skipped id={Id} reason=invalid catalog: {e.Message}", this);
                }

                return null;
            }

            var rigRoot = RagdollRoot();
            if (!TryFindRagdollBones(rigRoot, catalog, out var bones, out var missing))
            {
                Debug.Log($"{LogTag} ragdoll skipped id={Id} reason=bone '{missing}' not found under '{rigRoot.name}'", this);
                return null;
            }

            var shell = BuildRagdoll(rigRoot, bones, catalog, renderers, out var parts, out var ragdoll);
            _shell = shell;
            _shellBody = null;
            _ragdoll = ragdoll;
            _shellCatalog = catalog;
            _remaining = _lifetime;
            if (Application.isPlaying)
            {
                // 塊の抜け殻と同じく、体の GameObject ごと止められても寿命で必ず消す
                Destroy(shell, _lifetime);
            }

            var push = PushRagdoll(hitDirection, damage, out var speed);
            Debug.Log(
                $"{LogTag} collapse id={Id} ragdoll bones={bones.Count.ToString(CultureInfo.InvariantCulture)} parts={parts.ToString(CultureInfo.InvariantCulture)}"
                + $" push={FormatDirection(push)} speed={FormatSpeed(speed)} damage={damage.ToString(CultureInfo.InvariantCulture)}"
                + $" settle={catalog.SettleSeconds.ToString("0.##", CultureInfo.InvariantCulture)}s lifetime={_lifetime.ToString("0.##", CultureInfo.InvariantCulture)}s",
                this);
            return shell;
        }

        private RagdollCatalog ResolveRagdollCatalog()
        {
            if (_ragdollCatalog != null)
            {
                return _ragdollCatalog;
            }

            var loaded = RagdollCatalog.Load();
            if (loaded == null && !_warnedMissingRagdollCatalog)
            {
                _warnedMissingRagdollCatalog = true;
                Debug.LogWarning(
                    $"{LogTag} no RagdollCatalog at Resources/{RagdollCatalog.ResourcePath}; bodies collapse as one piece. "
                    + "Run Fps/Build All Presentation Assets (Fps.Editor.PresentationAssetSteps.BuildAll)",
                    this);
            }

            return loaded;
        }

        private Transform RagdollRoot()
        {
            var animator = GetComponent<CharacterAnimator>();
            return animator != null && animator.Rig != null ? animator.Rig : transform;
        }

        // 同じ名前が複数あれば根に近いものを使う。見た目の部品が偶然ボーンと同じ名前でも、ボーンの方が浅いので取り違えにくい。
        private static bool TryFindRagdollBones(
            Transform rigRoot, RagdollCatalog catalog, out List<(RagdollBone Entry, Transform Source)> bones, out string missing)
        {
            var byName = new Dictionary<string, Transform>(StringComparer.Ordinal);
            var queue = new Queue<Transform>();
            queue.Enqueue(rigRoot);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!byName.ContainsKey(current.name))
                {
                    byName.Add(current.name, current);
                }

                foreach (Transform child in current)
                {
                    queue.Enqueue(child);
                }
            }

            bones = new List<(RagdollBone Entry, Transform Source)>(catalog.Bones.Count);
            foreach (var entry in catalog.Bones)
            {
                if (!byName.TryGetValue(entry.Bone, out var source))
                {
                    missing = entry.Bone;
                    bones = null;
                    return false;
                }

                bones.Add((entry, source));
            }

            missing = null;
            return true;
        }

        private GameObject BuildRagdoll(
            Transform rigRoot,
            List<(RagdollBone Entry, Transform Source)> bones,
            RagdollCatalog catalog,
            Renderer[] renderers,
            out int parts,
            out DeathRagdoll ragdoll)
        {
            parts = 0;
            var shell = new GameObject(Id + ShellSuffix);
            try
            {
                shell.layer = ShellLayer;
                if (gameObject.scene.IsValid() && shell.scene != gameObject.scene)
                {
                    SceneManager.MoveGameObjectToScene(shell, gameObject.scene);
                }

                shell.transform.SetPositionAndRotation(rigRoot.position, rigRoot.rotation);
                shell.transform.localScale = rigRoot.lossyScale;

                // 非アクティブな枝のボーンも物理には要るので、Transform はすべて写し、見た目だけ見えているものに絞る
                var clones = new Dictionary<Transform, Transform> { [rigRoot] = shell.transform };
                CloneHierarchy(rigRoot, shell.transform, clones);

                var bodies = new Dictionary<Transform, Rigidbody>();
                var orderedBodies = new List<Rigidbody>(bones.Count);
                var shellColliders = new List<Collider>(bones.Count);
                foreach (var (entry, source) in bones)
                {
                    var boneObject = clones[source].gameObject;
                    var body = boneObject.AddComponent<Rigidbody>();
                    body.mass = entry.Mass;
                    body.interpolation = RigidbodyInterpolation.Interpolate;
                    bodies.Add(source, body);
                    orderedBodies.Add(body);
                    shellColliders.Add(AddRagdollCollider(boneObject, entry));
                }

                Transform anchor = null;
                foreach (var (entry, source) in bones)
                {
                    var parentBody = NearestBodyAbove(source, rigRoot, bodies);
                    if (parentBody == null)
                    {
                        anchor ??= clones[source];
                        continue;
                    }

                    AddRagdollJoint(clones[source].gameObject, parentBody, entry);
                }

                anchor ??= shell.transform;
                foreach (var source in renderers)
                {
                    if (source != null && IsShownWithBody(source.transform) && TryCopyRagdollPart(source, clones, anchor))
                    {
                        parts++;
                    }
                }

                IgnoreBodyColliders(shellColliders);

                ragdoll = shell.AddComponent<DeathRagdoll>();
                ragdoll.Initialize(Id, orderedBodies, catalog.SettleSeconds, catalog.FootSpeedRatio);
                return shell;
            }
            catch
            {
                DestroySafely(shell);
                foreach (var mesh in _bakedMeshes)
                {
                    DestroySafely(mesh);
                }

                _bakedMeshes.Clear();
                throw;
            }
        }

        private static void CloneHierarchy(Transform source, Transform clone, Dictionary<Transform, Transform> clones)
        {
            foreach (Transform child in source)
            {
                var copy = new GameObject(child.name);
                copy.layer = ShellLayer;
                var copyTransform = copy.transform;
                copyTransform.SetParent(clone, false);
                copyTransform.localPosition = child.localPosition;
                copyTransform.localRotation = child.localRotation;
                copyTransform.localScale = child.localScale;
                clones.Add(child, copyTransform);
                CloneHierarchy(child, copyTransform, clones);
            }
        }

        private static Rigidbody NearestBodyAbove(Transform source, Transform rigRoot, Dictionary<Transform, Rigidbody> bodies)
        {
            if (source == rigRoot)
            {
                return null;
            }

            for (var current = source.parent; current != null; current = current.parent)
            {
                if (bodies.TryGetValue(current, out var body))
                {
                    return body;
                }

                if (current == rigRoot)
                {
                    break;
                }
            }

            return null;
        }

        private static Collider AddRagdollCollider(GameObject bone, RagdollBone entry)
        {
            var size = new Vector3(Mathf.Abs(entry.Size.x), Mathf.Abs(entry.Size.y), Mathf.Abs(entry.Size.z));
            switch (entry.Shape)
            {
                case RagdollShape.Capsule:
                    var axis = entry.LongestAxis;
                    var capsule = bone.AddComponent<CapsuleCollider>();
                    capsule.center = entry.Center;
                    capsule.direction = axis;
                    capsule.height = size[axis];
                    capsule.radius = Mathf.Max(size[(axis + 1) % 3], size[(axis + 2) % 3]) * 0.5f;
                    return capsule;

                case RagdollShape.Sphere:
                    var sphere = bone.AddComponent<SphereCollider>();
                    sphere.center = entry.Center;
                    sphere.radius = Mathf.Max(size.x, Mathf.Max(size.y, size.z)) * 0.5f;
                    return sphere;

                default:
                    var box = bone.AddComponent<BoxCollider>();
                    box.center = entry.Center;
                    box.size = size;
                    return box;
            }
        }

        private static void AddRagdollJoint(GameObject bone, Rigidbody parentBody, RagdollBone entry)
        {
            var joint = bone.AddComponent<CharacterJoint>();
            joint.connectedBody = parentBody;
            joint.anchor = Vector3.zero;
            joint.axis = entry.TwistAxis.normalized;
            joint.swingAxis = entry.SwingAxis.normalized;
            joint.lowTwistLimit = new SoftJointLimit { limit = entry.LowTwist };
            joint.highTwistLimit = new SoftJointLimit { limit = entry.HighTwist };
            joint.swing1Limit = new SoftJointLimit { limit = entry.Swing1 };
            joint.swing2Limit = new SoftJointLimit { limit = entry.Swing2 };

            // 一瞬で大きく押すので、関節が伸びきって手足が飛ばないよう可動域の外へ出た分を戻す
            joint.enableProjection = true;
        }

        private bool TryCopyRagdollPart(Renderer source, Dictionary<Transform, Transform> clones, Transform anchor)
        {
            if (source is SkinnedMeshRenderer skinned
                && skinned.sharedMesh != null
                && clones.TryGetValue(source.transform, out var skinHost)
                && TryMapBones(skinned, clones, out var mappedBones, out var mappedRoot))
            {
                // 焼かずにボーンを写した側へ付け替えるので、倒れていく間も体の形が関節に付いて曲がる
                var copy = skinHost.gameObject.AddComponent<SkinnedMeshRenderer>();
                copy.sharedMesh = skinned.sharedMesh;
                copy.bones = mappedBones;
                copy.rootBone = mappedRoot;
                copy.localBounds = skinned.localBounds;
                copy.updateWhenOffscreen = skinned.updateWhenOffscreen;
                for (var i = 0; i < skinned.sharedMesh.blendShapeCount; i++)
                {
                    copy.SetBlendShapeWeight(i, skinned.GetBlendShapeWeight(i));
                }

                CopyLook(copy, source);
                return true;
            }

            if (source is MeshRenderer && clones.TryGetValue(source.transform, out var host))
            {
                var filter = source.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                {
                    return false;
                }

                AddMeshCopy(host.gameObject, filter.sharedMesh, source);
                return true;
            }

            // リグの外の見た目と、リグの外のボーンで曲がる皮は、根のボーンに付けて一緒に倒す
            if (!TryGetPartMesh(source, out var mesh, out var scale))
            {
                return false;
            }

            AddMeshCopy(CreateDetachedPart(source, anchor, scale), mesh, source);
            return true;
        }

        private static bool TryMapBones(
            SkinnedMeshRenderer skinned, Dictionary<Transform, Transform> clones, out Transform[] mapped, out Transform root)
        {
            var sourceBones = skinned.bones;
            mapped = new Transform[sourceBones.Length];
            root = null;
            for (var i = 0; i < sourceBones.Length; i++)
            {
                if (sourceBones[i] != null && !clones.TryGetValue(sourceBones[i], out mapped[i]))
                {
                    return false;
                }
            }

            return skinned.rootBone == null || clones.TryGetValue(skinned.rootBone, out root);
        }

        private Vector3 PushRagdoll(Vector3? hitDirection, int damage, out float speed)
        {
            var direction = HorizontalDirection(hitDirection, transform);
            speed = _shellCatalog != null ? _shellCatalog.SpeedFor(damage) : 0f;

            // 倒れた後に対応表が書き換えられて NaN などになっても、押す側で例外にしない
            if (!(speed >= 0f) || float.IsInfinity(speed))
            {
                speed = 0f;
            }

            if (_ragdoll != null)
            {
                _ragdoll.Push(direction, speed);
            }

            return direction;
        }

        private static string FormatSpeed(float speed)
        {
            return speed.ToString("0.##", CultureInfo.InvariantCulture);
        }

        /// <summary>見た目を消し、CharacterController 以外のコライダー (当たり判定) を止める。</summary>
        private void HideBody(Renderer[] renderers)
        {
            foreach (var bodyRenderer in renderers)
            {
                if (bodyRenderer != null)
                {
                    bodyRenderer.enabled = false;
                }
            }

            // CharacterController は移動の持ち主 (PlayerController / NetworkPlayerMotor) が有効・無効を決める
            foreach (var bodyCollider in GetComponentsInChildren<Collider>(true))
            {
                if (bodyCollider != null && !(bodyCollider is CharacterController))
                {
                    bodyCollider.enabled = false;
                }
            }
        }

        private GameObject BuildShell(Renderer[] renderers, out int parts)
        {
            parts = 0;
            var shell = new GameObject(Id + ShellSuffix);
            try
            {
                shell.layer = ShellLayer;
                if (gameObject.scene.IsValid() && shell.scene != gameObject.scene)
                {
                    SceneManager.MoveGameObjectToScene(shell, gameObject.scene);
                }

                shell.transform.SetPositionAndRotation(transform.position, transform.rotation);

                var shellColliders = new List<Collider>();
                foreach (var source in renderers)
                {
                    if (source != null && IsShownWithBody(source.transform) && TryCopyPart(source, shell.transform, shellColliders))
                    {
                        parts++;
                    }
                }

                if (parts == 0)
                {
                    DestroySafely(shell);
                    return null;
                }

                var body = shell.AddComponent<Rigidbody>();
                body.mass = ShellMass;
                body.interpolation = RigidbodyInterpolation.Interpolate;

                IgnoreBodyColliders(shellColliders);
                return shell;
            }
            catch
            {
                DestroySafely(shell);
                foreach (var mesh in _bakedMeshes)
                {
                    DestroySafely(mesh);
                }

                _bakedMeshes.Clear();
                throw;
            }
        }

        /// <summary>
        /// 体が表示されていれば見える部品か。体そのもののアクティブは見ない: 倒れた体を非アクティブにする購読者
        /// (KillTargetObjective) が先に走っても、抜け殻は倒れる前の見た目で作る。
        /// </summary>
        private bool IsShownWithBody(Transform part)
        {
            for (var current = part; current != null && current != transform; current = current.parent)
            {
                if (!current.gameObject.activeSelf)
                {
                    return false;
                }
            }

            return true;
        }

        private bool TryCopyPart(Renderer source, Transform shell, List<Collider> shellColliders)
        {
            if (!TryGetPartMesh(source, out var mesh, out var scale))
            {
                return false;
            }

            var part = CreateDetachedPart(source, shell, scale);
            AddMeshCopy(part, mesh, source);

            // 丸い体 (Capsule など) でも転がり続けないよう、箱で受ける
            var bounds = mesh.bounds;
            var box = part.AddComponent<BoxCollider>();
            box.center = bounds.center;
            box.size = Vector3.Max(bounds.size, MinSizeIn(scale));
            shellColliders.Add(box);
            return true;
        }

        private bool TryGetPartMesh(Renderer source, out Mesh mesh, out Vector3 worldScale)
        {
            mesh = null;
            worldScale = Vector3.one;
            if (source is SkinnedMeshRenderer skinned)
            {
                if (skinned.sharedMesh == null)
                {
                    return false;
                }

                // 倒れた瞬間の姿勢で固める。拡縮も焼き込むので、部品の拡縮は 1 にする
                mesh = new Mesh { name = skinned.sharedMesh.name + ShellSuffix };
                skinned.BakeMesh(mesh, true);
                _bakedMeshes.Add(mesh);
                return true;
            }

            if (source is MeshRenderer)
            {
                var filter = source.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                {
                    return false;
                }

                mesh = filter.sharedMesh;
                worldScale = source.transform.lossyScale;
                return true;
            }

            return false;
        }

        private static GameObject CreateDetachedPart(Renderer source, Transform parent, Vector3 worldScale)
        {
            var part = new GameObject(source.name);
            part.layer = ShellLayer;
            part.transform.SetParent(parent, false);
            part.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            var parentScale = parent.lossyScale;
            part.transform.localScale = new Vector3(
                worldScale.x / NonZero(parentScale.x),
                worldScale.y / NonZero(parentScale.y),
                worldScale.z / NonZero(parentScale.z));
            return part;
        }

        private static float NonZero(float value)
        {
            return Mathf.Abs(value) < 1e-4f ? (value < 0f ? -1e-4f : 1e-4f) : value;
        }

        private static void AddMeshCopy(GameObject host, Mesh mesh, Renderer source)
        {
            host.AddComponent<MeshFilter>().sharedMesh = mesh;
            var copy = host.AddComponent<MeshRenderer>();
            CopyLook(copy, source);
        }

        private static void CopyLook(Renderer copy, Renderer source)
        {
            copy.sharedMaterials = source.sharedMaterials;

            // 一人称の持ち主は自分の体を影だけにしているが、抜け殻は誰からも見えるようにする
            copy.shadowCastingMode = ShadowCastingMode.On;
            copy.receiveShadows = source.receiveShadows;
        }

        private static Vector3 MinSizeIn(Vector3 scale)
        {
            return new Vector3(
                MinColliderSize / Mathf.Max(Mathf.Abs(scale.x), 1e-4f),
                MinColliderSize / Mathf.Max(Mathf.Abs(scale.y), 1e-4f),
                MinColliderSize / Mathf.Max(Mathf.Abs(scale.z), 1e-4f));
        }

        /// <summary>
        /// 体に残っている有効なコライダー (CharacterController) と抜け殻がぶつからないようにする。
        /// 同じ場所に重ねて作るので、ぶつかると抜け殻が弾き飛ばされる。止めたコライダーとは組めない (Unity がエラーを出す) ので外す。
        /// </summary>
        private void IgnoreBodyColliders(List<Collider> shellColliders)
        {
            foreach (var bodyCollider in GetComponentsInChildren<Collider>(false))
            {
                if (bodyCollider == null || !bodyCollider.enabled)
                {
                    continue;
                }

                foreach (var shellCollider in shellColliders)
                {
                    Physics.IgnoreCollision(bodyCollider, shellCollider, true);
                }
            }
        }

        private Vector3 ApplyPush(Vector3? hitDirection)
        {
            var direction = HorizontalDirection(hitDirection, transform);
            if (_shellBody != null)
            {
                _shellBody.linearVelocity = direction * PushSpeed;
                _shellBody.angularVelocity = Vector3.Cross(Vector3.up, direction) * ToppleSpeed;
            }

            return direction;
        }

        /// <summary>
        /// 倒す向き。hitDirection を水平面に落とした単位ベクトルで、null か真上・真下なら body の後ろ (正面から撃たれた向き)。
        /// </summary>
        public static Vector3 HorizontalDirection(Vector3? hitDirection, Transform body)
        {
            if (hitDirection.HasValue)
            {
                var flat = Vector3.ProjectOnPlane(hitDirection.Value, Vector3.up);
                var length = flat.sqrMagnitude;
                if (length > 1e-6f && !float.IsInfinity(length))
                {
                    return flat.normalized;
                }
            }

            var back = body != null ? Vector3.ProjectOnPlane(-body.forward, Vector3.up) : Vector3.back;
            return back.sqrMagnitude > 1e-6f ? back.normalized : Vector3.back;
        }

        private static string FormatDirection(Vector3 direction)
        {
            return "(" + direction.x.ToString("0.##", CultureInfo.InvariantCulture)
                + "," + direction.z.ToString("0.##", CultureInfo.InvariantCulture) + ")";
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
