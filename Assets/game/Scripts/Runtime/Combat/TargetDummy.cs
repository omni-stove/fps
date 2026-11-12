using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 撃って倒せる標的ダミー。<see cref="Create"/> で、本体の <see cref="Damageable"/> と、
    /// Capsule の胴 (<see cref="BodyName"/>) と上の Sphere の頭 (<see cref="HeadName"/>) の <see cref="Hitbox"/> をコードで組み立てる。
    /// 死ぬと (死亡ログは <see cref="Damageable"/> が出す) 体を隠し、<see cref="RespawnDelay"/> 秒後に元の位置で
    /// Health を作り直して復活する。待ち時間の判断は <see cref="RespawnCountdown"/> に任せる。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Damageable))]
    public sealed class TargetDummy : MonoBehaviour
    {
        public const float DefaultRespawnDelay = 3f;
        public const int DefaultMaxHealth = Damageable.DefaultMaxHealth;

        public const string BodyName = "Body";
        public const string HeadName = "Head";

        /// <summary>胴体のカプセル。高さ 1.7m (Capsule は高さ 2m なので y を 0.85 倍) で足元から立つ。</summary>
        public static readonly Vector3 BodyLocalPosition = new Vector3(0f, 0.85f, 0f);
        public static readonly Vector3 BodyScale = new Vector3(1f, 0.85f, 1f);

        /// <summary>頭の球。直径 0.5m で胴体のすぐ上 (1.7m〜2.2m)。正面を水平に撃つと胴体、少し上を向くと頭に当たる。</summary>
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

        /// <summary>体力を持つ本体。</summary>
        public Damageable Damageable => _damageable;

        /// <summary>死んでから復活するまでの秒数。</summary>
        public float RespawnDelay => _respawnDelay;

        /// <summary>復活する位置 (<see cref="Initialize"/> したときの位置)。</summary>
        public Vector3 HomePosition => _homePosition;

        /// <summary>復活する向き (<see cref="Initialize"/> したときの向き)。</summary>
        public Quaternion HomeRotation => _homeRotation;

        /// <summary>死んで復活を待っている間は true。</summary>
        public bool IsWaitingToRespawn => _countdown != null && _countdown.IsRunning;

        /// <summary>復活までの残り秒数。待っていなければ 0。</summary>
        public float RemainingRespawnSeconds => _countdown != null ? _countdown.RemainingSeconds : 0f;

        /// <summary><see cref="Initialize"/> が済んでいれば true。</summary>
        public bool IsInitialized => _countdown != null;

        /// <summary>
        /// 標的ダミーを 1 体組み立てる。足元が position に来る。途中で失敗したら作りかけを片付けて例外を投げ直す。
        /// </summary>
        /// <param name="id">GameObject の名前とログの識別子。空にはできない。</param>
        /// <param name="position">足元の位置 (復活する位置)。</param>
        /// <param name="rotation">向き (復活する向き)。</param>
        /// <param name="parent">親。null ならシーンの直下。</param>
        /// <param name="maxHealth">最大体力。1 以上。</param>
        /// <param name="respawnDelay">復活までの秒数。0 以上の有限値。</param>
        public static TargetDummy Create(
            string id,
            Vector3 position,
            Quaternion rotation,
            Transform parent = null,
            int maxHealth = DefaultMaxHealth,
            float respawnDelay = DefaultRespawnDelay)
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

                CreatePart(root.transform, PrimitiveType.Capsule, BodyName, BodyLocalPosition, BodyScale, HitZone.Body, damageable);
                CreatePart(root.transform, PrimitiveType.Sphere, HeadName, HeadLocalPosition, HeadScale, HitZone.Head, damageable);

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

        /// <summary>
        /// 本体と復活までの秒数を設定し、今の位置と向きを復活する場所として覚える。
        /// 何度呼んでもよく、<see cref="Damageable.Died"/> の購読は 1 本だけになる。
        /// </summary>
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

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        /// <summary>
        /// 復活待ちの時間を deltaSeconds 進め、待ち時間を過ぎたら復活する。復活したら true。
        /// Play 中は Update から毎フレーム呼ぶ。Edit Mode のテストでは直接呼ぶ。
        /// </summary>
        /// <param name="deltaSeconds">前回からの経過時間 (秒)。0 以上の有限値。</param>
        public bool Tick(float deltaSeconds)
        {
            if (_countdown == null || !_countdown.Tick(deltaSeconds))
            {
                return false;
            }

            Respawn();
            return true;
        }

        /// <summary>
        /// 待ち時間を待たずに、元の位置と向きで満タンの Health を作り直して復活する。
        /// Play 中に Inspector の右クリックメニューからも呼べる。
        /// </summary>
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

        /// <summary>復活したときのログ。</summary>
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

            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = visible;
            }
        }

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
            Damageable owner)
        {
            // プリミティブ付属のコライダーをそのまま当たり判定に使う。
            var part = GameObject.CreatePrimitive(shape);
            part.name = partName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = Quaternion.identity;
            part.transform.localScale = localScale;
            part.AddComponent<Hitbox>().Initialize(zone, owner);
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
