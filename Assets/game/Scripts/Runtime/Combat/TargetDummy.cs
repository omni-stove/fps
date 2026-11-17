using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Damageable))]
    public sealed class TargetDummy : MonoBehaviour
    {
        public const float DefaultRespawnDelay = 3f;
        public const int DefaultMaxHealth = Damageable.DefaultMaxHealth;

        public const string BodyName = "Body";
        public const string HeadName = "Head";

        public const string VisualName = "Visual";

        // Capsule は高さ 2m なので、y を 0.85 倍して高さ 1.7m の胴を足元から立たせる。
        public static readonly Vector3 BodyLocalPosition = new Vector3(0f, 0.85f, 0f);
        public static readonly Vector3 BodyScale = new Vector3(1f, 0.85f, 1f);

        // 正面を水平に撃つと胴体、少し上を向くと頭に当たるよう、頭は胴体のすぐ上 (1.7m〜2.2m) に置く。
        public static readonly Vector3 HeadLocalPosition = new Vector3(0f, 1.95f, 0f);
        public static readonly Vector3 HeadScale = new Vector3(0.5f, 0.5f, 0.5f);

        [SerializeField, Min(0f), Tooltip("死んでから復活するまでの秒数")]
        private float _respawnDelay = DefaultRespawnDelay;

        [SerializeField, Tooltip("体力を持つ本体。空なら同じ GameObject から探す")]
        private Damageable _damageable;

        private RespawnCountdown _countdown;
        private Vector3 _homePosition;
        private Quaternion _homeRotation = Quaternion.identity;
        private bool _subscribed;

        public Damageable Damageable => _damageable;

        public float RespawnDelay => _respawnDelay;

        public Vector3 HomePosition => _homePosition;

        public Quaternion HomeRotation => _homeRotation;

        public bool IsWaitingToRespawn => _countdown != null && _countdown.IsRunning;

        public float RemainingRespawnSeconds => _countdown != null ? _countdown.RemainingSeconds : 0f;

        public bool IsInitialized => _countdown != null;

        public static TargetDummy Create(
            string id,
            Vector3 position,
            Quaternion rotation,
            Transform parent = null,
            int maxHealth = DefaultMaxHealth,
            float respawnDelay = DefaultRespawnDelay,
            GameObject visualPrefab = null)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("id must not be empty", nameof(id));
            }

            var root = new GameObject(id);
            try
            {
                root.transform.SetParent(parent, false);
                root.transform.SetPositionAndRotation(position, rotation);

                var damageable = root.AddComponent<Damageable>();
                damageable.Initialize(id, maxHealth);

                var visible = visualPrefab == null;
                CreatePart(root.transform, PrimitiveType.Capsule, BodyName, BodyLocalPosition, BodyScale, HitZone.Body, damageable, visible);
                CreatePart(root.transform, PrimitiveType.Sphere, HeadName, HeadLocalPosition, HeadScale, HitZone.Head, damageable, visible);
                if (visualPrefab != null)
                {
                    // 撃った弾は胴と頭の当たり判定で受けるので、見た目の当たり判定は外す (残すと部位の分からない所で弾が止まる)。
                    VisualPrefab.Instantiate(visualPrefab, root.transform, VisualName, stripColliders: true);
                }

                var dummy = root.AddComponent<TargetDummy>();
                dummy.Initialize(damageable, respawnDelay);
                return dummy;
            }
            catch
            {
                DestroySafely(root);
                throw;
            }
        }

        public void Initialize(Damageable damageable, float respawnDelay)
        {
            if (damageable == null)
            {
                throw new ArgumentNullException(nameof(damageable));
            }

            // 不正な秒数なら、ここで投げて今の設定を残す。
            var countdown = new RespawnCountdown(respawnDelay);

            Unsubscribe();
            _damageable = damageable;
            _respawnDelay = respawnDelay;
            _countdown = countdown;
            _homePosition = transform.position;
            _homeRotation = transform.rotation;
            _damageable.Died += OnDied;
            _subscribed = true;
        }

        private void Awake()
        {
            if (IsInitialized)
            {
                return;
            }

            // シーンに置かれたものは Inspector の値で始める。Create から付けたときは直後の Initialize で上書きされる。
            try
            {
                Initialize(_damageable != null ? _damageable : GetComponent<Damageable>(), _respawnDelay);
            }
            catch (Exception e)
            {
                Debug.LogError($"{Damageable.LogTag} TargetDummy '{name}' の設定が不正です: {e.Message}", this);
                enabled = false;
            }
        }

        // GameBootstrap は Awake で組み立てるので、シーンに置かれた標的は Start まで待ってからそのルールを読む。
        private void Start()
        {
            _damageable.ApplySceneRules();
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        public bool Tick(float deltaSeconds)
        {
            if (_countdown == null || !_countdown.Tick(deltaSeconds))
            {
                return false;
            }

            Respawn();
            return true;
        }

        [ContextMenu("Respawn")]
        public void Respawn()
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException($"TargetDummy '{name}' is not initialized");
            }

            _countdown.Cancel();
            transform.SetPositionAndRotation(_homePosition, _homeRotation);
            _damageable.ResetHealth();
            SetBodyVisible(true);
            Debug.Log(FormatRespawnedLog(_damageable.Id), this);
        }

        public static string FormatRespawnedLog(string id)
        {
            return $"{Damageable.LogTag} target respawned id={id}";
        }

        private void OnDied(Damageable _)
        {
            // 倒れたことが見て分かるよう、復活まで見た目と当たり判定を消す (死体に弾が吸われないようにもなる)。
            SetBodyVisible(false);
            _countdown.Start();
        }

        private void SetBodyVisible(bool visible)
        {
            foreach (var collider in GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = visible;
            }

            // 見せたときに全 Renderer を点け直すと、プレハブや仮の形で消してある物まで点くので、最初に隠す時点で点いている物だけを覚える。
            if (_shownRenderers == null)
            {
                _shownRenderers = new System.Collections.Generic.List<Renderer>();
                foreach (var renderer in GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer.enabled)
                    {
                        _shownRenderers.Add(renderer);
                    }
                }
            }

            foreach (var renderer in _shownRenderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = visible;
                }
            }
        }

        private System.Collections.Generic.List<Renderer> _shownRenderers;

        private void Unsubscribe()
        {
            if (_subscribed && _damageable != null)
            {
                _damageable.Died -= OnDied;
            }

            _subscribed = false;
        }

        private static void CreatePart(
            Transform parent,
            PrimitiveType shape,
            string partName,
            Vector3 localPosition,
            Vector3 localScale,
            HitZone zone,
            Damageable owner,
            bool visible)
        {
            // プリミティブ付属のコライダーをそのまま当たり判定に使う。見せないときは同じ形のコライダーだけを置く。
            var part = visible ? GameObject.CreatePrimitive(shape) : CreateInvisiblePart(shape);
            part.name = partName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = Quaternion.identity;
            part.transform.localScale = localScale;
            part.AddComponent<Hitbox>().Initialize(zone, owner);
        }

        // Unity のプリミティブに付くコライダーと同じ大きさにする (Capsule は高さ 2・半径 0.5、Sphere は半径 0.5)。
        private static GameObject CreateInvisiblePart(PrimitiveType shape)
        {
            var part = new GameObject();
            switch (shape)
            {
                case PrimitiveType.Capsule:
                    var capsule = part.AddComponent<CapsuleCollider>();
                    capsule.radius = 0.5f;
                    capsule.height = 2f;
                    break;
                case PrimitiveType.Sphere:
                    part.AddComponent<SphereCollider>().radius = 0.5f;
                    break;
                default:
                    DestroySafely(part);
                    throw new ArgumentOutOfRangeException(nameof(shape), shape, "only Capsule and Sphere parts are supported");
            }

            return part;
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
