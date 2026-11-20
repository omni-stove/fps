using System;
using System.Collections.Generic;
using System.Globalization;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed partial class DeathBodyPresenter : MonoBehaviour
    {
        public const string LogTag = "[DeathBody]";

        public const float DefaultLifetime = 5f;

        // 吹き飛ばさず、小さく押して倒すだけにする。
        public const float PushSpeed = 1.5f;

        public const float ToppleSpeed = 2.5f;

        public const float ShellMass = 70f;

        public const string ShellSuffix = " (DeathBody)";

        // 抜け殻は各クライアントの見た目だけなので、Physics.DefaultRaycastLayers に含まれないレイヤーに置き、射撃 (Hitscan・ネットの巻き戻し) や視線の判定に拾わせない。
        public const int ShellLayer = DebrisPool.DebrisLayer;

        // 薄いメッシュ (Plane など) でも床をすり抜けないよう、コライダーの各辺をこれより小さくしない。
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
        private Transform _anchor;

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

        // 乗り物の座席で倒れたときは、抜け殻を乗り物に付けてその場に止める。押して倒すと車体の当たり判定に押し出され、外へ飛び出すため。
        // 抜け殻を作る前にも後にも置けるよう、作った時点と置いた時点の両方で付ける。
        public Transform Anchor
        {
            get => _anchor;
            set
            {
                _anchor = value;
                AnchorShell();
            }
        }

        public bool IsAnchored => HasShell && _anchor != null && _shell.transform.parent == _anchor;

        public bool IsRagdoll => Ragdoll != null;

        public GameObject Shell => _shell != null ? _shell : null;

        public bool HasShell => _shell != null;

        public float RemainingSeconds => HasShell ? Mathf.Max(0f, _remaining) : 0f;

        public Damageable Target => _target;

        public static bool IsPresentationAvailable => !NetStartup.ShouldDisablePresentation(NetStartup.IsDedicatedServerBuild);

        public static DeathBodyPresenter Attach(GameObject body)
        {
            if (body == null)
            {
                throw new ArgumentNullException(nameof(body));
            }

            var presenter = body.GetComponent<DeathBodyPresenter>();
            return presenter != null ? presenter : body.AddComponent<DeathBodyPresenter>();
        }

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

        public GameObject Present(Vector3? hitDirection)
        {
            return Present(hitDirection, 0);
        }

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
            AnchorShell();
            Debug.Log(
                $"{LogTag} collapse id={Id} parts={parts.ToString(CultureInfo.InvariantCulture)} push={FormatDirection(push)} lifetime={_lifetime.ToString("0.##", CultureInfo.InvariantCulture)}s",
                this);
            return shell;
        }

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

            if (IsAnchored)
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

        // 吹き飛ばしでは対応表のダメージごとの速さでなく、生きている体を飛ばすのと同じ速度で押す。倒れた瞬間に飛び方が変わらないように。
        // とどめの被弾の知らせ (OnDamaged) がこの後に来ても、対応表の速さで押し直させない。
        public bool Launch(Vector3 velocity)
        {
            var sqr = velocity.sqrMagnitude;
            if (float.IsNaN(sqr) || float.IsInfinity(sqr))
            {
                throw new ArgumentException($"velocity must be finite: {velocity}", nameof(velocity));
            }

            _awaitingHitDirection = false;
            if (!HasShell || IsAnchored)
            {
                return false;
            }

            var speed = velocity.magnitude;
            var heading = speed > 1e-4f ? velocity / speed : Vector3.zero;
            if (_ragdoll != null)
            {
                _ragdoll.Push(heading, speed);
            }
            else if (_shellBody != null)
            {
                _shellBody.linearVelocity = velocity;
                _shellBody.angularVelocity = Vector3.Cross(Vector3.up, HorizontalDirection(velocity, transform)) * ToppleSpeed;
            }

            Debug.Log(
                $"{LogTag} knockback id={Id} ragdoll={(IsRagdoll ? "true" : "false")} velocity={Knockback.FormatVector(velocity)}",
                this);
            return true;
        }

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

        // 時間を自分で数えるのは、Update が走らない Edit Mode のテストでも直接進められるようにするため。
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
            AnchoredPresenters.Remove(this);
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

            // 近接と突進は、生きている体を飛ばすのと同じ速度で倒す。爆発は強さが知らせに無いので、爆発させた側が後から Launch で押し直す。
            if (KnockbackSpec.ComesFromReport(report.Kind)
                && report.Direction.HasValue
                && Knockback.TryGetVelocity(self, report.Kind, report.Direction.Value, 1f, out var knockback))
            {
                Launch(knockback);
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
            _anchor = null;
            Clear(ReasonRespawn);
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
