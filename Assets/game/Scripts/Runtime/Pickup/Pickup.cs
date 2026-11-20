using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 当たり判定を動かない Rigidbody (kinematic) を付けたトリガーにするのは、Rigidbody を持たない bot のコライダーでも入ったことが分かるようにするため。
    // 射撃や視線の Raycast はトリガーを無視するので、拾える物は弾を遮らない。
    // シリアライズする欄をこのファイルにまとめるのは、Inspector に出る順をファイルの並びに左右されないようにするため。
    [DisallowMultipleComponent]
    public sealed partial class Pickup : MonoBehaviour
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
        private PickupSpec _spec;
        private bool _collected;
        private float _age;
        private bool _reportedInvalid;

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

        public void Restock()
        {
            _collected = false;
            _age = 0f;
            _inside.Clear();
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
